using FishyFlip.Lexicon.App.Bsky.Actor;
using FishyFlip.Models;
using LabelerBot.Data;
using LabelerBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LabelerBot.Service;

public interface IDataRepository
{
    Task SavePosts(IEnumerable<ImagePost> imagePosts);
    Task AddSubscriber(ATDid subscriber, string rkey);
    Task<List<ImagePost>> GetValidPosts(ATDid did);
    Task<LabelLevel> GetCurrentLabel(ATDid did);
    Task AddLabel(ATDid did, LabelLevel label);
    Task ClearLabels(ATDid did);
    Task<List<Subscriber>> GetActiveSubscribers();
    Task DeactivateSubscriber(ATDid did);
    Task UpdateProfile(ProfileViewDetailed? profile);
    Task<bool> SubscriberExists(ATDid did, string? rkey);
    Task<LabelLevel> DeleteSubscriber(ATDid did);
    Task<Subscriber?> GetSubscriber(ATDid did);
    Task RemovePosts(ATDid did, string rkey);
}

public class DataRepository(IDbContextFactory<DataContext> dbContextFactory, ILogger<DataRepository> logger) : IDataRepository
{
    public async Task SavePosts(IEnumerable<ImagePost> newPosts)
    {
        try
        {
            var context = await dbContextFactory.CreateDbContextAsync();
            var did = newPosts.First().Did;

            var allExistingPosts = await context.Posts.Where(x => x.Did == did).ToListAsync();

            foreach (var newPost in newPosts.DistinctBy(x => new { x.Cid, x.Did }))
            {
                if (!allExistingPosts.Any(exist => exist.Did.Equals(newPost.Did) && exist.Cid.Equals(newPost.Cid)))
                {
                    context.Posts.Add(newPost);
                }
            }

            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex.Message, ex);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, ex);
        }
    }

    public async Task RemovePosts(ATDid did, string rkey)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var existing = dbContext.Posts.Where(x => x.Did.Equals(did) && x.Rkey == rkey);
        dbContext.Posts.RemoveRange(existing);
        await dbContext.SaveChangesAsync();
    }

    public async Task AddSubscriber(ATDid subscriber, string rkey)
    {
        logger.LogInformation("Adding subscriber {did}", subscriber.Handler);

        var dbContext = await dbContextFactory.CreateDbContextAsync();

        var newSubscriber = new Subscriber
        {
            Did = subscriber,
            Rkey = rkey,
            Timestamp = DateTime.UtcNow,
            Active = true
        };
        dbContext.Subscribers.Add(newSubscriber);

        await dbContext.SaveChangesAsync();
    }

    public async Task<List<ImagePost>> GetValidPosts(ATDid did)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Posts
            .Where(x => x.Did.Equals(did) &&
                        x.Timestamp >= DateTime.UtcNow.AddMonths(-1))
            .ToListAsync();
    }

    public async Task<LabelLevel> GetCurrentLabel(ATDid did)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var label = await dbContext.Labels.SingleOrDefaultAsync(x => x.Did.Equals(did));

        return label?.Level ?? LabelLevel.None;
    }

    public async Task AddLabel(ATDid did, LabelLevel level)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var newLabel = new Label
        {
            Did = did,
            Level = level,
            Timestamp = DateTime.UtcNow
        };
        dbContext.Labels.Add(newLabel);
        await dbContext.SaveChangesAsync();
    }

    public async Task ClearLabels(ATDid did)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var existing = await dbContext.Labels.Where(x => x.Did.Equals(did)).ToListAsync();
        dbContext.Labels.RemoveRange(existing);
        await dbContext.SaveChangesAsync();
    }

    public async Task<List<Subscriber>> GetActiveSubscribers()
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Subscribers.Where(x => x.Active).ToListAsync();
    }

    public async Task DeactivateSubscriber(ATDid did)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var sub = await dbContext.Subscribers.SingleAsync(x => x.Did.Equals(did));
        sub.Active = false;
        await dbContext.SaveChangesAsync();
    }

    public async Task UpdateProfile(ProfileViewDetailed? profile)
    {
        if (string.IsNullOrWhiteSpace(profile?.Handle.Handle))
        {
            return;
        }

        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var sub = await dbContext.Subscribers.SingleOrDefaultAsync(x => x.Did.Equals(profile.Did));
        if (sub == null)
        {
            logger.LogWarning("Subscriber {did} not found", profile.Did.Handler);
            return;
        }

        sub.Handle = profile.Handle.Handle;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Saved handle for {did}: {handle}", profile.Did.Handler, profile.Handle.Handle);
    }

    public async Task<bool> SubscriberExists(ATDid did, string? rkey)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Subscribers.Where(x => x.Did.Equals(did) && x.Rkey.Equals(rkey)).SingleOrDefaultAsync() != null;
    }

    public async Task<LabelLevel> DeleteSubscriber(ATDid did)
    {
        var existingLevel = LabelLevel.None;

        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var existingRecords = await dbContext.Posts.Where(x => x.Did.Equals(did)).ToListAsync();
        dbContext.Posts.RemoveRange(existingRecords);

        var existingLabel = await dbContext.Labels.Where(x => x.Did.Equals(did)).SingleOrDefaultAsync();
        if (existingLabel != null)
        {
            existingLevel = existingLabel.Level;
            dbContext.Labels.Remove(existingLabel);
        }

        var existingSubscriber = await dbContext.Subscribers.Where(x => x.Did.Equals(did)).ToListAsync();
        dbContext.Subscribers.RemoveRange(existingSubscriber);

        await dbContext.SaveChangesAsync();

        return existingLevel;
    }

    public async Task<Subscriber?> GetSubscriber(ATDid did)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var sub = await dbContext.Subscribers.SingleOrDefaultAsync(x => x.Did.Equals(did));
        if (sub == null || string.IsNullOrWhiteSpace(sub.Handle))
        {
            return null;
        }

        return sub;
    }
}
