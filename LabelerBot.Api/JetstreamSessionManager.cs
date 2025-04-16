using System.Diagnostics;
using System.Net.WebSockets;
using System.Timers;
using FishyFlip;
using FishyFlip.Events;
using Timer = System.Timers.Timer;

namespace LabelerBot.Api;

public interface IJetstreamSessionManager
{
    Task OpenAsync(EventHandler<JetStreamATWebSocketRecordEventArgs> recordReceivedHandler,
        CancellationToken cancellationToken);
    Task CloseAsync();
}

public class JetstreamSessionManager(ILogger<JetstreamSessionManager> logger) : IJetstreamSessionManager
{
    private ATJetStream? _atproto;
    private CancellationToken _cancellationToken = CancellationToken.None;
    private EventHandler<JetStreamATWebSocketRecordEventArgs>? _recordReceivedHandler;
    private readonly Timer _jetstreamRetryTimer = new(5000);
    private int _retryCount;


    public async Task OpenAsync(EventHandler<JetStreamATWebSocketRecordEventArgs> recordReceivedHandler,
        CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _recordReceivedHandler = recordReceivedHandler;

        _atproto = new ATJetStreamBuilder().Build();
        _atproto.OnConnectionUpdated += OnConnectionUpdated;
        _atproto.OnRecordReceived += _recordReceivedHandler;

        await _atproto.ConnectAsync(token: _cancellationToken);

        _jetstreamRetryTimer.Elapsed += JetstreamRetryTimerOnElapsed;
    }

    public async Task CloseAsync()
    {
        if (_atproto != null)
        {
            _atproto.OnConnectionUpdated -= OnConnectionUpdated;
            _atproto.OnRecordReceived -= _recordReceivedHandler;
            await _atproto.CloseAsync();
        }

        _jetstreamRetryTimer.Elapsed -= JetstreamRetryTimerOnElapsed;
    }

    private async void OnConnectionUpdated(object? sender, SubscriptionConnectionStatusEventArgs e)
    {
        try
        {
            logger.LogInformation("Jetstream connection status updated: {status}", e.State);

            if (e.State == WebSocketState.CloseReceived)
            {
                logger.LogError("Jetstream force closed the connection");
            }

            if (e.State != WebSocketState.Open && !_cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug("Attempting to reconnect to Jetstream");
                try
                {
                    await _atproto!.ConnectAsync(token: _cancellationToken);
                    _retryCount = 0;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error attempting to reconnect to Jetstream. Starting the retry timer");
                    _jetstreamRetryTimer.Start();
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Jetstream connection updated handler");
        }
    }

    private async void JetstreamRetryTimerOnElapsed(object? sender, ElapsedEventArgs e)
    {
        _jetstreamRetryTimer.Stop();

        if (_retryCount >= 10)
        {
            _jetstreamRetryTimer.Close();
            logger.LogCritical($"Failed to reconnect to Jetstream after {_retryCount} attempts");
            Process.GetCurrentProcess().Kill();
        }

        logger.LogInformation("Attempting to retry reconnection to Jetstream");
        _retryCount++;

        try
        {
            await _atproto!.ConnectAsync(token: _cancellationToken);
            _retryCount = 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Still failed to reconnect to Jetstream. Waiting to retry...");
            _jetstreamRetryTimer.Start();
        }
    }
}