namespace Dms.Shared.Contracts.Auth;

public enum UserRole
{
    Driver,
    FleetManager,
    Admin
}

public record LoginRequest(string Email, string Password);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string UserId, string DisplayName, UserRole Role);
