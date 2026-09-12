using Microsoft.AspNetCore.SignalR;

namespace Dms.Api.Hubs;

/// <summary>
/// WebSocket endpoint: /hubs/drowsiness
/// Pushes real-time drowsiness/distraction alerts to connected Fleet Manager
/// clients (Web Portal, Desktop Console). Driver clients (mobile, desktop HUD)
/// never connect here directly — they POST incidents to TelemetryController,
/// which broadcasts through this hub server-side.
/// </summary>
public class DrowsinessHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // TODO: join caller to a fleet-scoped group (e.g. Groups.AddToGroupAsync)
        // once fleet/tenant membership is resolved from the authenticated user.
        await base.OnConnectedAsync();
    }
}
