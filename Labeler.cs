using FishyFlip;
using FishyFlip.Lexicon.Com.Atproto.Admin;
using FishyFlip.Lexicon.Tools.Ozone.Moderation;
using FishyFlip.Models;

namespace LabelerBot;

public interface ILabeler
{
    Task<bool> Apply(ATDid did, LabelLevel newLevel);
    Task<bool> Negate(ATDid did, LabelLevel oldLevel);
}

public class Labeler : ILabeler
{
    private readonly ILogger<Labeler> _logger;
    private readonly ATDid _labelerDid;
    private readonly string _labelerPassword;
    private readonly ATProtocol _atproto;

    public Labeler(ILogger<Labeler> logger, IConfiguration config)
    {
        _logger = logger;
        _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;
        _labelerPassword = config.GetValue<string>("Labeler:Password")!;
        _atproto = new ATProtocolBuilder().WithOzoneProxy(_labelerDid!).Build();
    }

    public async Task<bool> Apply(ATDid did, LabelLevel newLevel)
    {
        await EnsureAuth();
        var label = new ModEventLabel
        {
            CreateLabelVals = [newLevel.ToString().ToLower()],
            NegateLabelVals = []
        };

        var result = await _atproto.ToolsOzoneModeration.EmitEventAsync(label, new RepoRef(did), _labelerDid);

        if (result.IsT1)
        {
            var error = result.AsT1;
            _logger.LogError(error.Detail!.Message, error.Detail.StackTrace);
        }

        return result.IsT0;
    }

    public async Task<bool> Negate(ATDid did, LabelLevel oldLevel)
    {
        await EnsureAuth();
        
        var label = new ModEventLabel
        {
            CreateLabelVals = [],
            NegateLabelVals = [oldLevel.ToString().ToLower()]
        };


        var result = await _atproto.ToolsOzoneModeration.EmitEventAsync(label, new RepoRef(did), _labelerDid);
        return result.IsT0;
    }

    private async Task EnsureAuth()
    {
        var authResult = await _atproto.AuthenticateWithPasswordResultAsync(_labelerDid.Handler, _labelerPassword);
        if (authResult.IsT1)
        {
            var error = authResult.AsT1;
            _logger.LogError(error.Detail?.Message, error.Detail?.StackTrace);
        }
    }
}