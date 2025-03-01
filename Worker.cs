using System.Net.WebSockets;
using FishyFlip;
using FishyFlip.Events;
using FishyFlip.Lexicon.App.Bsky.Embed;
using FishyFlip.Lexicon.App.Bsky.Feed;
using FishyFlip.Models;
using LabelerBot.Models;

namespace LabelerBot;

public class Worker(IDataRepository dataRepository, ILabelService labelService, IConfiguration config, ILogger<Worker> logger) : BackgroundService
{
    private readonly ATDid _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did"))!;
    private List<ATDid> _subscribers = [];
    private ATJetStream? _atproto;
    private CancellationToken _cancellationToken = CancellationToken.None;


    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        logger.LogInformation("Starting LabelerBot");

        try
        {
            _subscribers = (await dataRepository.GetActiveSubscribers()).Select(x => x.Did).ToList();
            logger.LogInformation("Initializing with {count} subscribers", _subscribers.Count);

            _atproto = new ATJetStreamBuilder().Build();
            _atproto.OnRecordReceived += RecordReceivedHandler;
            _atproto.OnConnectionUpdated += OnConnectionUpdated;
            await _atproto.ConnectAsync(token: cancellationToken);

            // backfill on startup to get anything we missed
            await BackfillAll(cancellationToken);

            logger.LogDebug("Backfill complete - listening for updates");

            while (!cancellationToken.IsCancellationRequested)
            {
            }

            logger.LogInformation("Stopping LabelerBot");

        }
        catch (Exception ex)
        {
            logger.LogCritical(ex.Message, ex);
        }
        finally
        {
            if (_atproto != null)
            {
                _atproto.OnConnectionUpdated -= OnConnectionUpdated;
                _atproto.OnRecordReceived -= RecordReceivedHandler;
                await _atproto.CloseAsync();
            }

            logger.LogInformation("LabelerBot is kill");
        }
    }

    private async void OnConnectionUpdated(object? sender, SubscriptionConnectionStatusEventArgs e)
    {
        try
        {
            logger.LogInformation("Jetstream connection status updated: {status}", e.State);

            if (e.State != WebSocketState.Open && !_cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Attempting to reconnect to Jetstream");
                await _atproto!.ConnectAsync(token: _cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Jetstream connection updated handler");
        }
    }

    private async void RecordReceivedHandler(object? sender, JetStreamATWebSocketRecordEventArgs message)
    {
        try
        {
            if (!IsHandleable(message.Record))
            {
                return;
            }

            switch (message.Record.Commit!.Record)
            {
                case Post post:
                    await HandlePost(message.Record, post);
                    break;

                case Like like:
                    await HandleLike(message.Record, like);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, message.Json);
        }
    }

    private async Task HandlePost(ATWebSocketRecord record, Post post)
    {
        if (!_subscribers.Contains(record.Did!))
        {
            return;
        }

        if (post.Embed is EmbedImages imgPost)
        {
            logger.LogInformation("Processing new post with images from {did}", record.Did);

            foreach (var image in imgPost.Images)
            {
                var newPost = new ImagePost
                {
                    Cid = record.Commit!.Cid?.ToString(),
                    Did = record.Did,
                    Timestamp = post.CreatedAt.GetValueOrDefault(DateTime.UtcNow),
                    ValidAlt = IsValidAlt(image.Alt)
                };

                await dataRepository.SavePost(newPost);
            }

            await labelService.AdjustLabel(record.Did);
        }
    }

    private async Task HandleLike(ATWebSocketRecord record, Like like)
    {
        if (like.Subject?.Uri.Did == null || !like.Subject.Uri.Did.Equals(_labelerDid))
        {
            return;
        }

        await dataRepository.AddSubscriber(record.Did!);

        _subscribers.Add(record.Did!);

        await Backfill(record.Did!);
    }

    private async Task BackfillAll(CancellationToken stoppingToken)
    {
        var atProtocolBuilder = new ATProtocolBuilder();
        var atproto = atProtocolBuilder.Build();

        foreach(var subscriber in _subscribers)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            await Backfill(subscriber, atproto);
        }
    }

    private async Task Backfill(ATDid did, ATProtocol? atproto = null)
    {
        logger.LogInformation("Backfilling posts for {did}", did);

        if (atproto == null)
        {
            var atProtocolBuilder = new ATProtocolBuilder();
            atproto = atProtocolBuilder.Build();
        }

        string? cursor = null;
        string? lastCursor = null;
        var earliestTimeSeen = DateTime.UtcNow;
        var bail = false;

        while (earliestTimeSeen > DateTime.UtcNow.AddMonths(-1) || bail)
        {
            if (cursor == null && lastCursor != null)
            {
                break;
            }

            var records = await atproto.Repo.ListRecordsAsync(did, "app.bsky.feed.post", 50, cursor);

            await records.SwitchAsync(async success =>
            {
                foreach (var record in success!.Records)
                {
                    var post = record.Value as Post;

                    if (post?.Embed is not EmbedImages imgPost)
                    {
                        continue;
                    }

                    foreach (var image in imgPost.Images)
                    {
                        var newPost = new ImagePost
                        {
                            Cid = record.Cid,
                            Did = did,
                            Timestamp = post.CreatedAt.GetValueOrDefault(DateTime.UtcNow),
                            ValidAlt = IsValidAlt(image.Alt)
                        };

                        await dataRepository.SavePost(newPost);

                        if (post.CreatedAt.GetValueOrDefault(DateTime.UtcNow) < earliestTimeSeen)
                        {
                            earliestTimeSeen = post.CreatedAt!.Value;
                        }
                    }
                }

                lastCursor = cursor;
                cursor = success.Cursor;
            }, async error =>
            {
                logger.LogError(error.Detail?.Message, error.Detail.Error);
                await dataRepository.DeactivateSubscriber(did);
                _subscribers.Remove(did);
                bail = true;
            });
        }

        var result = await atproto.Actor.GetProfileAsync(did);
        if (result.IsT1)
        {
            var error = result.AsT1;
            logger.LogError(error.Detail!.Message, error.Detail.Error);
        }

        var profile = result.AsT0;

        await dataRepository.UpdateProfile(profile);
        await labelService.AdjustLabel(did);
    }
    
    private static bool IsHandleable(ATWebSocketRecord record)
    {
        if (record.Kind != ATWebSocketEvent.Commit)
        {
            return false;
        }

        if (record.Did == null)
        {
            return false;
        }

        if (record.Commit?.Operation != ATWebSocketCommitType.Create)
        {
            return false;
        }

        return true;
    }

    private static bool IsValidAlt(string? x)
    {
        return !string.IsNullOrWhiteSpace(x) && x.Length >= 5;
    }
}

public enum LabelLevel
{
    None = 0,
    Bronze = 50,
    Silver = 70,
    Gold = 90
}