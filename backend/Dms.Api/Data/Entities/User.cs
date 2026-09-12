using Dms.Shared.Contracts.Auth;

namespace Dms.Api.Data.Entities;

/// <summary>
/// A single table covers drivers, fleet managers, and admins — distinguished
/// by <see cref="Role"/> — since all three authenticate through the same
/// AuthenticationController and the set is small enough that split tables
/// would just add join overhead without a real benefit.
/// </summary>
public class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string DisplayName { get; set; }
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Drivers and fleet managers belong to a fleet; Admins may not.</summary>
    public Guid? FleetId { get; set; }
    public Fleet? Fleet { get; set; }

    public List<Device> Devices { get; set; } = [];
    public List<Incident> Incidents { get; set; } = [];
}
