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
    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<QaRule> QaRules => Set<QaRule>();
    public DbSet<QaAlert> QaAlerts => Set<QaAlert>();

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

        modelBuilder.Entity<Recording>(entity =>
        {
            entity.ToTable("recordings");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.FileName)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(x => x.StorageKey)
                .IsRequired()
                .HasMaxLength(1024);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QaRule>(entity =>
        {
            entity.ToTable("qa_rules");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Phrase)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(x => x.Severity)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.Property(x => x.IsActive)
                .IsRequired();
        });

        modelBuilder.Entity<QaAlert>(entity =>
        {
            entity.ToTable("qa_alerts");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.MatchedPhrase)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            entity.HasOne(x => x.Recording)
                .WithMany(r => r.QaAlerts)
                .HasForeignKey(x => x.RecordingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.QaRule)
                .WithMany()
                .HasForeignKey(x => x.QaRuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}