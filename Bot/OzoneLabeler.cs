using FishyFlip.Lexicon.Com.Atproto.Admin;
using FishyFlip.Lexicon.Tools.Ozone.Moderation;
using FishyFlip.Models;

namespace LabelerBot.Bot;


public interface ILabeler
{
    Task<bool> Apply(ATDid did, LabelLevel newLevel);
    Task<bool> Negate(ATDid did, LabelLevel oldLevel);
}

public class OzoneLabeler(IAtProtoSessionManager sessionManager, IConfiguration config, ILogger<OzoneLabeler> logger)
    : ILabeler
{
    private readonly ATDid _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;

    public async Task<bool> Apply(ATDid did, LabelLevel newLevel)
    {
        var atproto = await sessionManager.GetSession();

        var label = new ModEventLabel
        {
            CreateLabelVals = [newLevel.ToString().ToLower()],
            NegateLabelVals = []
        };

        var result = await atproto.ToolsOzoneModeration.EmitEventAsync(label, new RepoRef(did), _labelerDid);

        if (result.IsT1)
        {
            var error = result.AsT1;
            logger.LogError(error.Detail!.Message, error.Detail.Error);
        }

        return result.IsT0;
    }

    public async Task<bool> Negate(ATDid did, LabelLevel oldLevel)
    {
        var atproto = await sessionManager.GetSession();

        var label = new ModEventLabel
        {
            CreateLabelVals = [],
            NegateLabelVals = [oldLevel.ToString().ToLower()]
        };


        var result = await atproto.ToolsOzoneModeration.EmitEventAsync(label, new RepoRef(did), _labelerDid);

        if (result.IsT1)
        {
            var error = result.AsT1;
            logger.LogError(error.Detail!.Message, error.Detail.Error);
        }
        
        return result.IsT0;
    }

}