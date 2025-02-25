using FishyFlip;
using FishyFlip.Models;

namespace LabelerBot;

public interface ILabelerSessionManager
{
    Task<ATProtocol> GetSession();
}

public class LabelerSessionManager : ILabelerSessionManager
{
    private readonly ATDid _labelerDid;
    private readonly string _labelerPassword;
    private readonly ATProtocol _atproto;

    public LabelerSessionManager(IConfiguration config)
    {
        _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;
        _labelerPassword = config.GetValue<string>("Labeler:Password")!;
        _atproto = new ATProtocolBuilder().WithOzoneProxy(_labelerDid).Build();
    }

    public async Task<ATProtocol> GetSession()
    {
        if (!_atproto.IsAuthenticated)
        {
            var (session, error) = await _atproto.AuthenticateWithPasswordResultAsync(_labelerDid.Handler, _labelerPassword);

            if (session is null)
            {
                throw new Exception(error!.Detail?.Message);
            }
        }

        return _atproto;
    }
}