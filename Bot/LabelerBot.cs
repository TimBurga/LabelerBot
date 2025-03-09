using System.Net.WebSockets;
using FishyFlip;
using FishyFlip.Events;
using FishyFlip.Lexicon.App.Bsky.Embed;
using FishyFlip.Lexicon.App.Bsky.Feed;
using FishyFlip.Models;
using LabelerBot.Bot.DataAccess;
using LabelerBot.Bot.Models;

namespace LabelerBot.Bot;

public class LabelerBot(IDataRepository dataRepository, ILabelService labelService, IConfiguration config, ILogger<LabelerBot> logger) : BackgroundService
{
    private readonly ATDid _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;
    private Dictionary<ATDid, string> _subscribers = [];
    private ATJetStream? _atproto;
    private CancellationToken _cancellationToken = CancellationToken.None;


    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        logger.LogInformation("Starting LabelerBot");

        try
        {
            _subscribers = (await dataRepository.GetActiveSubscribers()).ToDictionary(k => k.Did, v => v.Rkey);
            logger.LogInformation("Initializing with {count} subscribers", _subscribers.Count);

            _atproto = new ATJetStreamBuilder().Build();
            _atproto.OnRecordReceived += RecordReceivedHandler;
            _atproto.OnConnectionUpdated += OnConnectionUpdated;
            await _atproto.ConnectAsync(token: cancellationToken);

            //backfill on startup to get anything we missed
            await BackfillAll(cancellationToken);

            logger.LogInformation("Listening for updates");

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
                logger.LogDebug("Attempting to reconnect to Jetstream");
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

            switch (message.Record.Commit!.Operation)
            {
                case ATWebSocketCommitType.Create:

                    switch (message.Record.Commit!.Record)
                    {
                        case Post post:
                            if (!_subscribers.ContainsKey(message.Record.Did!))
                            {
                                return;
                            }
                            await HandlePost(message.Record.Did!, message.Record.Commit.Cid!, post);
                            break;

                        case Like like:
                            if (like.Subject?.Uri.Did == null || !like.Subject.Uri.Did.Equals(_labelerDid))
                            {
                                return;
                            }

                            await HandleLike(message.Record.Did!, message.Record.Commit.RKey!);
                            break;
                    }
                    break;

                case ATWebSocketCommitType.Delete:

                    var commit = message.Record.Commit;

                    if (commit.Collection == Constants.LikesCollectionName)
                    {
                        if (_subscribers.ContainsKey(message.Record.Did!))
                        {
                            if (_subscribers[message.Record.Did!] == commit.RKey)
                            {
                                await HandleUnlike(message.Record.Did!);

                            }
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, message.Json);
        }
    }

    private async Task HandlePost(ATDid did, string cid, Post post)
    {
        if (post.Embed is EmbedImages imgPost)
        {
            logger.LogInformation("Processing new post with images from {did}", did);

            foreach (var image in imgPost.Images)
            {
                var newPost = new ImagePost
                {
                    Cid = cid,
                    Did =did,
                    Timestamp = post.CreatedAt.GetValueOrDefault(DateTime.UtcNow),
                    ValidAlt = IsValidAlt(image.Alt)
                };

                await dataRepository.SavePost(newPost);
            }

            await labelService.AdjustLabel(did);
        }
    }

    private async Task HandleLike(ATDid did, string rkey)
    {
        await HandleUnlike(did);
        await dataRepository.AddSubscriber(did, rkey);

        await Backfill(did);

        _subscribers.Add(did, rkey);
    }

    private async Task HandleUnlike(ATDid did)
    {
        logger.LogInformation("Removing subscriber {did}", did);
        _subscribers.Remove(did);
        var currentLabel = await dataRepository.DeleteSubscriber(did);
        if (currentLabel == LabelLevel.None)
        {
            return;
        }
        if (!await labelService.RemoveLabel(did, currentLabel))
        {
            logger.LogError("Failed to remove label for {did} - manual removal required", did);
        }
    }

    private async Task BackfillAll(CancellationToken stoppingToken)
    {
        var atProtocolBuilder = new ATProtocolBuilder();
        var atproto = atProtocolBuilder.Build();

        logger.LogInformation("Beginning backfill for all subscribers");

        foreach(var subscriber in _subscribers.Keys.OrderBy(x => x.Handler))
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            await Backfill(subscriber, atproto);
        }

        logger.LogInformation("Backfill complete");
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
        string? lastCursor;
        var earliestTimeSeen = DateTime.UtcNow;
        var bail = false;

        while (earliestTimeSeen > DateTime.UtcNow.AddMonths(-1))
        {
            if (bail)
            {
                break;
            }

            var records = await atproto.Repo.ListRecordsAsync(did, Constants.PostsCollectionName, 50, cursor, cancellationToken:_cancellationToken);

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
                bail = cursor == null && lastCursor != null;
            }, async error =>
            {
                logger.LogError(error.Detail?.Message, error.Detail?.Error);
                await dataRepository.DeactivateSubscriber(did);
                _subscribers.Remove(did);
                bail = true;
            });
        }

        var result = await atproto.Actor.GetProfileAsync(did, _cancellationToken);
        if (result.IsT1)
        {
            var error = result.AsT1;
            logger.LogError(error.Detail!.Message, error.Detail.Error);
            return;
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

        if (record.Commit == null)
        {
            return false;
        }

        if (record.Commit.Operation is not ATWebSocketCommitType.Create and not ATWebSocketCommitType.Delete)
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
    Bronze = 70,
    Silver = 85,
    Gold = 95,
    Hero = 100
}