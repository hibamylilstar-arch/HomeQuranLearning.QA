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
    public DbSet<TranscriptSegment> TranscriptSegments => Set<TranscriptSegment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<ManagerTeacherAssignment> ManagerTeacherAssignments => Set<ManagerTeacherAssignment>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceId).IsRequired().HasMaxLength(128);
            entity.HasIndex(x => x.DeviceId).IsUnique();
            entity.Property(x => x.DeviceName).IsRequired().HasMaxLength(256);
            entity.Property(x => x.AgentVersion).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<DeviceHeartbeat>(entity =>
        {
            entity.ToTable("device_heartbeats");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AgentVersion).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

            entity.HasOne(x => x.Device)
                .WithMany(d => d.Heartbeats)
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Recording>(entity =>
        {
            entity.ToTable("recordings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).IsRequired().HasMaxLength(512);
            entity.Property(x => x.StorageKey).IsRequired().HasMaxLength(1024);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<QaRule>(entity =>
        {
            entity.ToTable("qa_rules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Phrase).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.IsActive).IsRequired();
        });

        modelBuilder.Entity<QaAlert>(entity =>
        {
            entity.ToTable("qa_alerts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MatchedPhrase).IsRequired().HasMaxLength(512);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

            entity.HasOne(x => x.Recording)
                .WithMany(r => r.QaAlerts)
                .HasForeignKey(x => x.RecordingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.QaRule)
                .WithMany()
                .HasForeignKey(x => x.QaRuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TranscriptSegment>(entity =>
        {
            entity.ToTable("transcript_segments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).IsRequired().HasMaxLength(4096);
            entity.Property(x => x.Language).HasMaxLength(32);
            entity.HasIndex(x => new { x.RecordingId, x.SegmentIndex }).IsUnique();

            entity.HasOne(x => x.Recording)
                .WithMany()
                .HasForeignKey(x => x.RecordingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).IsRequired().HasMaxLength(1024);
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.IsActive).IsRequired();
        });

        modelBuilder.Entity<Teacher>(entity =>
        {
            entity.ToTable("teachers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Phone).HasMaxLength(64);
        });

        modelBuilder.Entity<ManagerTeacherAssignment>(entity =>
        {
            entity.ToTable("manager_teacher_assignments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ManagerUserId, x.TeacherId }).IsUnique();

            entity.HasOne(x => x.ManagerUser)
                .WithMany()
                .HasForeignKey(x => x.ManagerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Teacher)
                .WithMany(t => t.ManagerAssignments)
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.ToTable("students");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Email).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Phone).HasMaxLength(64);

            entity.HasOne(x => x.AssignedTeacher)
                .WithMany()
                .HasForeignKey(x => x.AssignedTeacherId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.ToTable("courses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(256);
            entity.Property(x => x.Description).HasMaxLength(1024);
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.ToTable("schedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DayOfWeek).HasConversion<int>();
            entity.Property(x => x.IsActive).IsRequired();

            entity.HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Course)
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

            entity.HasOne(x => x.Schedule)
                .WithMany()
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Course)
                .WithMany()
                .HasForeignKey(x => x.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Device)
                .WithMany()
                .HasForeignKey(x => x.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionEvent>(entity =>
        {
            entity.ToTable("session_events");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.EventType)
                .HasConversion<string>()
                .HasMaxLength(64);

            entity.Property(x => x.Source)
                .HasMaxLength(64);

            entity.Property(x => x.Details)
                .HasMaxLength(2048);

            entity.Property(x => x.IdempotencyKey)
                .HasMaxLength(256);

            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique();

            entity.HasIndex(x => new
            {
                x.SessionId,
                x.OccurredAtUtc
            });

            entity.HasOne(x => x.Session)
                .WithMany(x => x.Events)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
