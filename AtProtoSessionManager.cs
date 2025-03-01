using FishyFlip;
using FishyFlip.Models;
using Timer = System.Timers.Timer;

namespace LabelerBot;

public interface IAtProtoSessionManager
{
    Task<ATProtocol> GetSession();
}

public class AtProtoSessionManager : IAtProtoSessionManager
{
    private readonly ILogger<AtProtoSessionManager> _logger;
    private readonly ATDid _labelerDid;
    private readonly string _labelerPassword;
    private readonly ATProtocol _atproto;
    private Timer _refreshTimer;

    public AtProtoSessionManager(IConfiguration config, ILogger<AtProtoSessionManager> logger)
    {
        _logger = logger;
        _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;
        _labelerPassword = config.GetValue<string>("Labeler:Password")!;
        _atproto = new ATProtocolBuilder().WithOzoneProxy(_labelerDid).Build();
        _refreshTimer = new Timer();
        _refreshTimer.Elapsed += async (sender, args) => await RefreshSession();
    }

    private async Task RefreshSession()
    {
        _logger.LogInformation("Timer elapsed - refreshing authenticated session");

        var session = await _atproto.RefreshAuthSessionAsync();
        if (session == null)
        {
            _logger.LogError("Failed to refresh session - trying new session");
            var (newSession, error) = await _atproto.AuthenticateWithPasswordResultAsync(_labelerDid.Handler, _labelerPassword);
            if (error != null)
            {
                _logger.LogCritical("Failed to get new session: {error}", error);
                return;
            }

            _logger.LogInformation("Got new session: {error}", error);
            return;
        }

        _refreshTimer.Interval = session.Session.ExpiresIn.Subtract(DateTime.Now).Add(TimeSpan.FromSeconds(10)).TotalMilliseconds;
        _refreshTimer.Start();
    }

    public async Task<ATProtocol> GetSession()
    {
        try
        {
            if (!_atproto.IsAuthenticated)
            {
                _logger.LogDebug("Authenticating as {did}", _labelerDid.Handler);
                var (session, error) = await _atproto.AuthenticateWithPasswordResultAsync(_labelerDid.Handler, _labelerPassword);

                _refreshTimer.Interval = session.ExpiresIn.Subtract(DateTime.Now).Add(TimeSpan.FromSeconds(10)).TotalMilliseconds;
                _refreshTimer.Start();

                _logger.LogDebug("Got new session {jwt}", session.AccessJwt);
            }

            _logger.LogDebug("Session expires at {exp}", _atproto.Session?.ExpiresIn);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get authenticated session");
        }

        return _atproto;
    }
}