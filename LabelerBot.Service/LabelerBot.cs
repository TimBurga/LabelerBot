using System.Reflection;
using FishyFlip;
using FishyFlip.Events;
using FishyFlip.Lexicon.App.Bsky.Embed;
using FishyFlip.Lexicon.App.Bsky.Feed;
using FishyFlip.Models;
using LabelerBot.Data;
using LabelerBot.Data.Entities;

namespace LabelerBot.Service;

public class LabelerBot(IJetstreamSessionManager jetstream, IDataRepository dataRepository, ILabelService labelService, IConfiguration config, ILogger<LabelerBot> logger) : BackgroundService
{
    private readonly ATDid _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;
    private Dictionary<ATDid, string?> _subscribers = [];
    private CancellationToken _cancellationToken = CancellationToken.None;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        logger.LogInformation("Starting LabelerBot");

        try
        {
            _subscribers = (await dataRepository.GetActiveSubscribers()).ToDictionary(k => k.Did, v => v.Rkey);
            logger.LogInformation("Initializing with {count} subscribers", _subscribers.Count);

            await jetstream.OpenAsync(RecordReceivedHandler, cancellationToken);

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
            await jetstream.CloseAsync();
            logger.LogInformation("LabelerBot is kill");
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
                            await HandlePost(message.Record.Did!, post);
                            break;

                        case Like like:
                            if (like.Subject?.Uri.Did?.Handler != _labelerDid.Handler || like.Subject?.Uri.Rkey != "self")
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

    private async Task HandlePost(ATDid did, Post post)
    {
        if (post.Embed is EmbedImages imgPost)
        {
            logger.LogInformation("Handling new post from {did}", did);

            var posts = imgPost.Images
                .Select(image => new ImagePost
                {
                    Cid = image.ImageValue.Ref!.Link!, 
                    Did = did,
                    Timestamp = post.CreatedAt.GetValueOrDefault(DateTime.UtcNow), 
                    ValidAlt = IsValidAlt(image.Alt)
                })
                .ToList();

            await dataRepository.SavePosts(posts);

            await labelService.AdjustLabel(did);
        }
    }

    private async Task HandleLike(ATDid did, string rkey)
    {
        logger.LogInformation("Handling new like from {did} - first removing any existing data before backfilling.", did);

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
        var err = false;

        var backfillPosts = new List<ImagePost>();

        while (earliestTimeSeen > DateTime.UtcNow.AddMonths(-1))
        {
            if (bail || err)
            {
                break;
            }

            var records = await atproto.Repo.ListRecordsAsync(did, Constants.PostsCollectionName, 50, cursor, cancellationToken:_cancellationToken);

            await records.SwitchAsync(success =>
            {
                foreach (var record in success!.Records)
                {
                    var post = record.Value as Post;

                    if (post?.Embed is not EmbedImages imgPost)
                    {
                        continue;
                    }

                    var newPosts = imgPost.Images.Select(image => new ImagePost
                    {
                        Cid = image.ImageValue.Ref!.Link!,
                        Did = did,
                        Timestamp = post.CreatedAt.GetValueOrDefault(DateTime.UtcNow),
                        ValidAlt = IsValidAlt(image.Alt)
                    });

                    backfillPosts.AddRange(newPosts);

                    if (post.CreatedAt.GetValueOrDefault(DateTime.UtcNow) < earliestTimeSeen)
                    {
                        earliestTimeSeen = post.CreatedAt!.Value;
                    }
                }

                lastCursor = cursor;
                cursor = success.Cursor;
                bail = cursor == null && lastCursor != null;
                return Task.CompletedTask;
            }, async error =>
            {
                logger.LogError("Error backfilling: [{status}] {message}|{error}", error.StatusCode, error.Detail?.Message, error.Detail?.Error);
                if (error.StatusCode is 400 or 404)
                {
                    await dataRepository.DeactivateSubscriber(did);
                    _subscribers.Remove(did);
                    logger.LogWarning("Subscriber {did} not found - deactivating", did);
                }
                if (error.StatusCode == 429)
                {
                    logger.LogWarning("Rate limited - waiting 30 seconds");
                    await Task.Delay(30000, _cancellationToken);
                }

                err = true;
            });
        }

        if (err)
        {
            return;
        }

        await dataRepository.SavePosts(backfillPosts);

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
