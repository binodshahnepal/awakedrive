using Dms.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dms.Api.Data;

public class DmsDbContext(DbContextOptions<DmsDbContext> options) : DbContext(options)
{
    public DbSet<Fleet> Fleets => Set<Fleet>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Fleet>(fleet =>
        {
            fleet.HasKey(f => f.Id);
            fleet.Property(f => f.Name).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<User>(user =>
        {
            user.HasKey(u => u.Id);
            user.Property(u => u.Email).HasMaxLength(320).IsRequired();
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(30);

            user.HasOne(u => u.Fleet)
                .WithMany(f => f.Users)
                .HasForeignKey(u => u.FleetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Device>(device =>
        {
            device.HasKey(d => d.Id);
            device.Property(d => d.DeviceUuid).HasMaxLength(100).IsRequired();
            device.HasIndex(d => d.DeviceUuid).IsUnique();
            device.Property(d => d.DeviceType).HasConversion<string>().HasMaxLength(30);
            device.Property(d => d.FirmwareVersion).HasMaxLength(50);
            device.Property(d => d.ModelName).HasMaxLength(100);

            device.OwnsOne(d => d.Thresholds, thresholds =>
            {
                thresholds.Property(t => t.EarThreshold).HasColumnName("EarThreshold");
                thresholds.Property(t => t.EarDurationSeconds).HasColumnName("EarDurationSeconds");
                thresholds.Property(t => t.PerclosThreshold).HasColumnName("PerclosThreshold");
                thresholds.Property(t => t.MarThreshold).HasColumnName("MarThreshold");
                thresholds.Property(t => t.MarDurationSeconds).HasColumnName("MarDurationSeconds");
                thresholds.Property(t => t.YawThresholdDegrees).HasColumnName("YawThresholdDegrees");
                thresholds.Property(t => t.YawDurationSeconds).HasColumnName("YawDurationSeconds");
            });

            device.HasOne(d => d.Driver)
                .WithMany(u => u.Devices)
                .HasForeignKey(d => d.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            device.HasOne(d => d.Fleet)
                .WithMany(f => f.Devices)
                .HasForeignKey(d => d.FleetId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Incident>(incident =>
        {
            incident.HasKey(i => i.Id);
            incident.Property(i => i.Type).HasConversion<string>().HasMaxLength(30);
            incident.HasIndex(i => i.TimestampUtc);
            incident.HasIndex(i => new { i.DriverId, i.TimestampUtc });

            incident.HasOne(i => i.Device)
                .WithMany(d => d.Incidents)
                .HasForeignKey(i => i.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            incident.HasOne(i => i.Driver)
                .WithMany(u => u.Incidents)
                .HasForeignKey(i => i.DriverId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
