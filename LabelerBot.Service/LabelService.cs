using FishyFlip.Models;
using LabelerBot.Data;

namespace LabelerBot;

public interface ILabelService
{
    Task AdjustLabel(ATDid did);
    Task Reprocess();
    Task<bool> RemoveLabel(ATDid did, LabelLevel currentLabel);
}

public class LabelService(IDataRepository dataRepository, ILabeler labeler, IPostService poster, ILogger<LabelService> logger) : ILabelService
{
    public async Task AdjustLabel(ATDid did)
    {
        logger.LogDebug("Adjusting labels for {Did}", did);

        var posts = await dataRepository.GetValidPosts(did); 
        if (posts.Count == 0)
        {
            return;
        }

        var totalPosts = posts.Count;
        var postsWithValidAlt = posts.Count(y => y.ValidAlt);
        var percentage = (decimal)postsWithValidAlt / (decimal)totalPosts;
        var newLabel = GetLabel(percentage * 100);

        logger.LogDebug("{did}: {postsWithValidAlt} of {totalPosts} = {percentage} [{newLevel}]", 
            did.Handler, postsWithValidAlt, totalPosts, Math.Round(percentage, 3), newLabel);

        var currentLabel = await dataRepository.GetCurrentLabel(did);

        if (newLabel != currentLabel)
        {
            if (await labeler.Negate(did, currentLabel))
            {
                logger.LogInformation("Removed old label {label} for {did}", currentLabel, did);
                await dataRepository.ClearLabels(did);
            }

            if (newLabel != LabelLevel.None && await labeler.Apply(did, newLabel))
            {
                logger.LogInformation("Added new label {label} for {did}", newLabel, did);
                await dataRepository.AddLabel(did, newLabel);
                if (newLabel > currentLabel)
                {
                    await poster.PostAchievement(did, newLabel);
                }
            }
        }
    }

    public async Task Reprocess()
    {
        logger.LogInformation("Reprocessing labels for all subscribers");

        var subs = await dataRepository.GetActiveSubscribers();
        foreach (var sub in subs)
        {
            await AdjustLabel(sub.Did);
        }
    }

    public async Task<bool> RemoveLabel(ATDid did, LabelLevel currentLabel)
    {
        return await labeler.Negate(did, currentLabel);
    }

    private static LabelLevel GetLabel(decimal percentage)
    {
        return percentage switch
        {
            >= 100 => LabelLevel.Hero,
            >= 95 => LabelLevel.Gold,
            >= 85 => LabelLevel.Silver,
            >= 70 => LabelLevel.Bronze,
            _ => LabelLevel.None
        };
    }
}
