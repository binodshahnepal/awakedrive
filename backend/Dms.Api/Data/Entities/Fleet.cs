namespace Dms.Api.Data.Entities;

public class Fleet
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public List<User> Users { get; set; } = [];
    public List<Device> Devices { get; set; } = [];
}
