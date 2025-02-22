using FishyFlip.Models;
using LabelerBot.Models;
using Microsoft.EntityFrameworkCore;

namespace LabelerBot;

public interface IDataRepository
{
    Task SavePost(ImagePost imagePost);
    Task AddSubscriber(ATDid subscriber);
    Task<List<ImagePost>> GetValidPosts(ATDid did);
    Task<LabelLevel?> GetCurrentLabel(ATDid did);
    Task AddLabel(ATDid did, LabelLevel label);
    Task ClearLabels(ATDid did);

}

public class DataRepository(IDbContextFactory<DataContext> dbContextFactory, ILogger<DataRepository> logger) : IDataRepository
{
    public async Task SavePost(ImagePost imagePost)
    {
        try
        {
            var context = await dbContextFactory.CreateDbContextAsync();
            context.Posts.Add(imagePost);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            if (ex.GetBaseException().HResult == -2146232060)
            {
                // ignore PK violations, we have the record already
                return;
            }

            logger.LogError(ex.Message, ex);
        }
    }

    public async Task AddSubscriber(ATDid subscriber)
    {
        await ClearExisting(subscriber);

        var dbContext = await dbContextFactory.CreateDbContextAsync();

        var newSubscriber = new Subscriber
        {
            Did = subscriber,
            Timestamp = DateTime.UtcNow
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

    public async Task<LabelLevel?> GetCurrentLabel(ATDid did)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var label = await dbContext.Labels.SingleOrDefaultAsync(x => x.Did.Equals(did));

        return label?.Level;
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

    private async Task ClearExisting(ATDid did)
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var existingRecords = await dbContext.Posts.Where(x => x.Did.Equals(did)).ToListAsync();
        dbContext.Posts.RemoveRange(existingRecords);

        var existingLabels = await dbContext.Labels.Where(x => x.Did.Equals(did)).ToListAsync();
        dbContext.Labels.RemoveRange(existingLabels);

        var existingSubscriber = await dbContext.Subscribers.Where(x => x.Did.Equals(did)).ToListAsync();
        dbContext.Subscribers.RemoveRange(existingSubscriber);

        await dbContext.SaveChangesAsync();
    }
}
