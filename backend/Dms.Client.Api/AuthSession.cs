using Dms.Shared.Contracts.Auth;

namespace Dms.Client.Api;

/// <summary>
/// Holds the current signed-in user's JWT for the lifetime the host app
/// registers it with — Scoped (per-circuit) in Blazor Server, Singleton in
/// a single-user WPF app. Deliberately just a plain mutable holder: no
/// persistence, no refresh-token logic yet (tokens just expire and the user
/// re-logs-in — fine for an internal fleet-manager tool at this stage).
/// </summary>
public class AuthSession
{
    public string? AccessToken { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public string? UserId { get; private set; }
    public string? DisplayName { get; private set; }
    public UserRole? Role { get; private set; }

    public bool IsAuthenticated => AccessToken is not null && ExpiresAtUtc > DateTimeOffset.UtcNow;

    public event Action? Changed;

    public void SetFromLogin(LoginResponse response)
    {
        AccessToken = response.AccessToken;
        ExpiresAtUtc = response.ExpiresAtUtc;
        UserId = response.UserId;
        DisplayName = response.DisplayName;
        Role = response.Role;
        Changed?.Invoke();
    }

    public void Clear()
    {
        AccessToken = null;
        ExpiresAtUtc = null;
        UserId = null;
        DisplayName = null;
        Role = null;
        Changed?.Invoke();
    }
}
