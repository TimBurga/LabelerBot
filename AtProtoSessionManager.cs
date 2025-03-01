﻿using FishyFlip;
using FishyFlip.Models;

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

    public AtProtoSessionManager(IConfiguration config, ILogger<AtProtoSessionManager> logger)
    {
        _logger = logger;
        _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;
        _labelerPassword = config.GetValue<string>("Labeler:Password")!;
        _atproto = new ATProtocolBuilder().WithOzoneProxy(_labelerDid).Build();
    }

    public async Task<ATProtocol> GetSession()
    {
        try
        {
            if (!_atproto.IsAuthenticated)
            {
                _logger.LogDebug("Authenticating as {did}", _labelerDid.Handler);
                var (session, error) = await _atproto.AuthenticateWithPasswordResultAsync(_labelerDid.Handler, _labelerPassword);

                if (session is null)
                {
                    _logger.LogInformation("Refreshing authenticated session");
                    await _atproto.RefreshAuthSessionAsync();
                }

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