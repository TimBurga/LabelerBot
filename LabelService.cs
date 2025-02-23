using FishyFlip.Models;

namespace LabelerBot;

public interface ILabelService
{
    Task AdjustLabel(ATDid did);
    Task Reprocess();
}

public class LabelService(IDataRepository dataRepository, ILabeler labeler, ILogger<LabelService> logger) : ILabelService
{
    public async Task AdjustLabel(ATDid did)
    {
        logger.LogInformation("Adjusting labels for {Did}", did);

        var posts = await dataRepository.GetValidPosts(did); 
        if (posts.Count == 0)
        {
            return;
        }

        var totalPosts = posts.Count;
        var postsWithValidAlt = posts.Count(y => y.ValidAlt);
        var percentage = (decimal)postsWithValidAlt / (decimal)totalPosts;
        var newLevel = GetLabelLevel(percentage * 100);

        logger.LogInformation("{did}: {postsWithValidAlt} / {totalPosts} = {percentage} [{newLevel}]", 
            did.Handler, postsWithValidAlt, totalPosts, percentage, newLevel);

        var currentLabel = await dataRepository.GetCurrentLabel(did);

        if (currentLabel.HasValue)
        {
            if (currentLabel.Value != newLevel)
            {
                if (await labeler.Negate(did, currentLabel.Value))
                {
                    await dataRepository.ClearLabels(did);
                }

                if (await labeler.Apply(did, newLevel))
                {
                    await dataRepository.AddLabel(did, newLevel);
                }
            }
        }
        else
        {
            if (newLevel > LabelLevel.None)
            {
                if (await labeler.Apply(did, newLevel))
                {
                    await dataRepository.AddLabel(did, newLevel);
                }
            }
        }
    }

    public async Task Reprocess()
    {
        logger.LogInformation("Reprocessing all subscribers");

        var subs = await dataRepository.GetSubscribers();
        foreach (var sub in subs)
        {
            await AdjustLabel(sub.Did);
        }
    }

    private static LabelLevel GetLabelLevel(decimal percentage)
    {
        return percentage switch
        {
            >= 90 => LabelLevel.Gold,
            >= 70 => LabelLevel.Silver,
            >= 50 => LabelLevel.Bronze,
            _ => LabelLevel.None
        };
    }
}
