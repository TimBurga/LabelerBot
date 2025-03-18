using FishyFlip.Lexicon.App.Bsky.Feed;
using FishyFlip.Lexicon.App.Bsky.Richtext;
using FishyFlip.Models;
using LabelerBot.Bot.DataAccess;

namespace LabelerBot.Bot;

public interface IPostService
{
    Task PostAchievement(ATDid did, LabelLevel level);
}

public class PostService(IDataRepository data, IAtProtoSessionManager sessionManager, IConfiguration config, ILogger<PostService> logger) : IPostService
{
    private readonly Dictionary<ATDid,DateTime> _lastPost = new();

    public async Task PostAchievement(ATDid did, LabelLevel level)
    {
        var sub = await data.GetSubscriber(did);
        if (sub == null || IsDupe(did))
        {
            logger.LogInformation("Skipping post for {did}", did);
            return;
        }

        var text = $"@{sub.Handle} just leveled up to {level}! Congratulations and keep up the good work!\n\n" +
                   $"~ Like and subscribe to get your own Alt Heroes medal! ~";

        var post = new Post
        {
            Text = text,
            Facets = [Facet.CreateFacetMention(0, sub.Handle.Length + 1, did)],
            CreatedAt = DateTime.Now
        };

        var labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;

        var session = await sessionManager.GetSession();
        var result = await session.Repo.CreateRecordAsync(labelerDid, Constants.PostsCollectionName, post);
        if (result.IsT1)
        {
            var error = result.AsT1;
            logger.LogError("Failed to post achievement record: {error}", error.Detail?.Error);
        }

        _lastPost[did] = DateTime.Now;

        logger.LogInformation("New post: {text}", text);
    }

    private bool IsDupe(ATDid did)
    {
        if (_lastPost.TryGetValue(did, out var dt))
        {
            return DateTime.Now < dt.AddMinutes(5);
        }

        return false;
    }
}