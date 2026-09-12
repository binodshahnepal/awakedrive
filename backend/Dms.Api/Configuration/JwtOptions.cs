namespace Dms.Api.Configuration;

/// <summary>Bound from the "Jwt" configuration section; used by both the
/// bearer-auth validation setup and JwtTokenService's issuance so the two
/// can never drift apart.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string SigningKey { get; set; }
    public string Issuer { get; set; } = "AwakeDrive";
    public string Audience { get; set; } = "AwakeDriveClients";
    public int ExpiryMinutes { get; set; } = 60;
}
