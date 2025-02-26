﻿using FishyFlip;
using FishyFlip.Models;

namespace LabelerBot;

public interface ILabelerSessionManager
{
    Task<ATProtocol> GetSession();
}

public class LabelerSessionManager : ILabelerSessionManager
{
    private readonly ILogger<LabelerSessionManager> _logger;
    private readonly ATDid _labelerDid;
    private readonly string _labelerPassword;
    private readonly ATProtocol _atproto;

    public LabelerSessionManager(IConfiguration config, ILogger<LabelerSessionManager> logger)
    {
        _logger = logger;
        _labelerDid = ATDid.Create(config.GetValue<string>("Labeler:Did")!)!;
        _labelerPassword = config.GetValue<string>("Labeler:Password")!;
        _atproto = new ATProtocolBuilder().WithOzoneProxy(_labelerDid).Build();
    }

    public async Task<ATProtocol> GetSession()
    {
        if (!_atproto.IsAuthenticated)
        {
            _logger.LogDebug("Authenticating as {did}", _labelerDid.Handler);
            var (session, error) = await _atproto.AuthenticateWithPasswordResultAsync(_labelerDid.Handler, _labelerPassword);

            if (session is null)
            {
                throw new Exception(error!.Detail?.Message);
            }

            _logger.LogDebug("Got new session {jwt}", session.AccessJwt);
        }

        _logger.LogDebug("Session expires at {exp}", _atproto.Session?.ExpiresIn);
        return _atproto;
    }
}