namespace Dms.Shared.Contracts.Auth;

public enum UserRole
{
    Driver,
    FleetManager,
    Admin
}

public record LoginRequest(string Email, string Password);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAtUtc, string UserId, string DisplayName, UserRole Role);

/// <summary>Admin-only: onboards a new driver, fleet manager, or admin.</summary>
public record RegisterUserRequest(string Email, string Password, string DisplayName, UserRole Role, string? FleetId);

public record RegisterUserResponse(string UserId, string Email, string DisplayName, UserRole Role);
