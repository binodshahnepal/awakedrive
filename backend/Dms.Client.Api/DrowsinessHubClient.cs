using Dms.Shared.Contracts.Alerts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace Dms.Client.Api;

/// <summary>
/// Wraps a HubConnection to Dms.Api's DrowsinessHub (/hubs/drowsiness) for
/// fleet-manager clients (Web Portal, Desktop Console) so they don't each
/// reimplement reconnection and JWT-over-query-string wiring.
/// </summary>
public class DrowsinessHubClient(IOptions<DmsApiOptions> options, AuthSession authSession) : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<DrowsinessAlert>? AlertReceived;
    public event Action<HubConnectionState>? StateChanged;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            await StopAsync();
        }

        var hubUrl = new Uri(new Uri(options.Value.BaseUrl), "/hubs/drowsiness");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, httpOptions =>
            {
                httpOptions.AccessTokenProvider = () => Task.FromResult(authSession.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<DrowsinessAlert>("DrowsinessAlert", alert => AlertReceived?.Invoke(alert));
        _connection.Reconnecting += _ => { StateChanged?.Invoke(HubConnectionState.Reconnecting); return Task.CompletedTask; };
        _connection.Reconnected += _ => { StateChanged?.Invoke(HubConnectionState.Connected); return Task.CompletedTask; };
        _connection.Closed += _ => { StateChanged?.Invoke(HubConnectionState.Disconnected); return Task.CompletedTask; };

        await _connection.StartAsync(ct);
        StateChanged?.Invoke(_connection.State);
    }

    public async Task StopAsync()
    {
        if (_connection is null)
        {
            return;
        }
        await _connection.DisposeAsync();
        _connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}
