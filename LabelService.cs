using FishyFlip.Models;

namespace LabelerBot;

public interface ILabelService
{
    Task AdjustLabel(ATDid did);
}

public class LabelService(IDataRepository dataRepository, ILabeler labeler) : ILabelService
{
    public async Task AdjustLabel(ATDid did)
    {
        var posts = await dataRepository.GetValidPosts(did); 
        var percentage = posts.Count(y => y.ValidAlt) / posts.Count;
        var newLevel = GetLabelLevel(percentage * 100);

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

    private static LabelLevel GetLabelLevel(int percentage)
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
