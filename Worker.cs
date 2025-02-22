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
    private readonly HashSet<ATDid> _subscribers = [];
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var atWebProtocol = new ATJetStreamBuilder().Build();
        atWebProtocol.OnRecordReceived += RecordReceivedHandler;
        await atWebProtocol.ConnectAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
        }

        atWebProtocol.OnRecordReceived -= RecordReceivedHandler;
        await atWebProtocol.CloseAsync();
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
                logger.LogDebug($"{newPost.Did} posted {newPost.Cid}: {newPost.ValidAlt}");
            }
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

    private async Task Backfill(ATDid did)
    {
        var atProtocolBuilder = new ATProtocolBuilder();
        var atProtocol = atProtocolBuilder.Build();

        string? cursor = null;
        var earliestTimeSeen = DateTime.UtcNow;

        while (earliestTimeSeen > DateTime.UtcNow.AddMonths(-1))
        {
            var records = await atProtocol.Repo.ListRecordsAsync(did, "app.bsky.feed.post", 50, cursor);

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
                        logger.LogDebug($"{newPost.Did} posted {newPost.Cid}: {newPost.ValidAlt}");

                        if (post.CreatedAt.GetValueOrDefault(DateTime.UtcNow) < earliestTimeSeen)
                        {
                            earliestTimeSeen = post.CreatedAt!.Value;
                        }
                    }
                }

                cursor = success.Cursor;
            }, error =>
            {
                logger.LogError(error.Detail?.Message, error.Detail?.StackTrace);
                return Task.CompletedTask;
            });
        }

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