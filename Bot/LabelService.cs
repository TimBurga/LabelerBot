using FishyFlip.Models;
using LabelerBot.Bot.DataAccess;

namespace LabelerBot.Bot;

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
        logger.LogInformation("Adjusting labels for {Did}", did);

        var posts = await dataRepository.GetValidPosts(did); 
        if (posts.Count == 0)
        {
            return;
        }

        var totalPosts = posts.Count;
        var postsWithValidAlt = posts.Count(y => y.ValidAlt);
        var percentage = postsWithValidAlt / (decimal)totalPosts;
        var level = GetLabelLevel(percentage * 100);

        logger.LogInformation("{did}: {postsWithValidAlt} of {totalPosts} = {percentage} [{newLevel}]", 
            did.Handler, postsWithValidAlt, totalPosts, Math.Round(percentage, 3), level);

        var currentLabel = await dataRepository.GetCurrentLabel(did);

        if (currentLabel.HasValue)
        {
            if (currentLabel.Value != level)
            {
                //if (await labeler.Negate(did, currentLabel.Value))
                //{
                //    logger.LogInformation("Removed old label {label} for {did}", currentLabel.Value, did);
                //    await dataRepository.ClearLabels(did);
                //}

                if (await labeler.Apply(did, level))
                {
                    logger.LogInformation("Added new label {label} for {did}", level, did);
                    await dataRepository.AddLabel(did, level);
                    if (level > currentLabel.Value)
                    {
                        await poster.PostAchievement(did, level);
                    }
                }
            }
        }
        else
        {
            if (level > LabelLevel.None)
            {
                if (await labeler.Apply(did, level))
                {
                    logger.LogInformation("Added new label {label} for {did}", level, did);
                    await dataRepository.AddLabel(did, level);
                    await poster.PostAchievement(did, level);
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

    private static LabelLevel GetLabelLevel(decimal percentage)
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
