using Academy.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceHeartbeat> DeviceHeartbeats => Set<DeviceHeartbeat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.DeviceId)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasIndex(x => x.DeviceId)
                .IsUnique();

            entity.Property(x => x.DeviceName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.AgentVersion)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32);
        });

        modelBuilder.Entity<DeviceHeartbeat>(entity =>
        {
            entity.ToTable("device_heartbeats");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.AgentVersion)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasOne(x => x.Device)
                .WithMany(d => d.Heartbeats)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}