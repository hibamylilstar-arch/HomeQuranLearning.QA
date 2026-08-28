using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.DependencyInjection;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.IntegrationTests;

public class IntegrationTestBase : IDisposable
{
    protected readonly ServiceProvider ServiceProvider;
    protected readonly AppDbContext DbContext;
    private readonly string _testDatabaseName;

    public IntegrationTestBase()
    {
        _testDatabaseName = $"academy_test_{Guid.NewGuid():N}";

        string adminConnectionString =
            Environment.GetEnvironmentVariable("TEST_PG_ADMIN_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=postgres;Username=academy;Password=AcademyLocalDev2026";

        string databaseHost =
            Environment.GetEnvironmentVariable("TEST_PG_HOST")
            ?? "localhost";

        int databasePort =
            int.TryParse(Environment.GetEnvironmentVariable("TEST_PG_PORT"), out int port)
                ? port
                : 5433;

        using (var adminConnection = new Npgsql.NpgsqlConnection(adminConnectionString))
        {
            adminConnection.Open();
            using var cmd = adminConnection.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE \"{_testDatabaseName}\"";
            cmd.ExecuteNonQuery();
        }

        var connectionString =
            $"Host={databaseHost};Port={databasePort};Database={_testDatabaseName};Username=academy;Password=AcademyLocalDev2026";

        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString
        });

        var configuration = configBuilder.Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        ServiceProvider = services.BuildServiceProvider();
        DbContext = ServiceProvider.GetRequiredService<AppDbContext>();
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        DbContext.Dispose();

        string adminConnectionString =
            Environment.GetEnvironmentVariable("TEST_PG_ADMIN_CONNECTION")
            ?? "Host=localhost;Port=5433;Database=postgres;Username=academy;Password=AcademyLocalDev2026";

        using var adminConnection = new Npgsql.NpgsqlConnection(adminConnectionString);
        adminConnection.Open();
        using var cmd = adminConnection.CreateCommand();
        cmd.CommandText = $"DROP DATABASE IF EXISTS \"{_testDatabaseName}\" WITH (FORCE)";
        cmd.ExecuteNonQuery();
    }
}

public class RecordingServiceIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task SubmitRecording_AssociatesActiveSession_UsingRealDatabase()
    {
        // Arrange
        var deviceRepo = new DeviceRepository(DbContext);
        var recordingRepo = new RecordingRepository(DbContext);
        var sessionRepo = new SessionRepository(DbContext);
        var teacherRepo = new TeacherRepository(DbContext);
        var unitOfWork = new UnitOfWork(DbContext);

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            FullName = "Ahmed Teacher",
            Email = "ahmed@academy.local",
            Phone = "123",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await teacherRepo.AddAsync(teacher, CancellationToken.None);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            FullName = "Ali Student",
            Email = "ali@academy.local",
            Phone = "123",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        DbContext.Students.Add(student);

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Name = "Nazra Quran",
            Description = "Nazra reading",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        DbContext.Courses.Add(course);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = "device-123",
            DeviceName = "Laptop-01",
            AgentVersion = "0.1.0",
            Status = DeviceStatus.Online,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await deviceRepo.AddAsync(device, CancellationToken.None);

        await DbContext.SaveChangesAsync();

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            StudentId = student.Id,
            CourseId = course.Id,
            DeviceId = device.Id,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            Status = SessionStatus.Live,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        await sessionRepo.AddAsync(session, CancellationToken.None);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        var storageService = new FakeStorageService();
        var service = new RecordingService(
            recordingRepo,
            deviceRepo,
            sessionRepo,
            storageService,
            unitOfWork,
            "test-bucket");

        // Act
        var result = await service.SubmitRecordingAsync(new RecordingSubmittedRequest
        {
            DeviceId = device.DeviceId,
            FileName = "test.mp4",
            StartedAtUtc = DateTimeOffset.UtcNow,
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(10),
            SizeBytes = 100
        });

        // Assert
        var savedRecording = await DbContext.Recordings
            .FirstOrDefaultAsync(r => r.Id == result.RecordingId);

        Assert.NotNull(savedRecording);
        Assert.Equal(session.Id, savedRecording.SessionId);
        Assert.Equal(teacher.Id, savedRecording.TeacherId);
    }

    [Fact]
    public async Task ManagerFiltering_ReturnsOnlyAssignedTeachersData_RealDatabase()
    {
        // Arrange
        var teacher1 = new Teacher
        {
            Id = Guid.NewGuid(),
            FullName = "Teacher A",
            Email = "a@a.local",
            Phone = "1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var teacher2 = new Teacher
        {
            Id = Guid.NewGuid(),
            FullName = "Teacher B",
            Email = "b@b.local",
            Phone = "2",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        var manager = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Manager A",
            Email = "m@m.local",
            PasswordHash = "hash",
            Role = UserRole.Manager,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        DbContext.Teachers.AddRange(teacher1, teacher2);
        DbContext.Users.Add(manager);

        DbContext.ManagerTeacherAssignments.Add(new ManagerTeacherAssignment
        {
            Id = Guid.NewGuid(),
            ManagerUserId = manager.Id,
            TeacherId = teacher1.Id,
            AssignedAtUtc = DateTimeOffset.UtcNow
        });

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = "dev-1",
            DeviceName = "Laptop-01",
            AgentVersion = "0.1.0",
            Status = DeviceStatus.Online,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        DbContext.Devices.Add(device);

        DbContext.Recordings.AddRange(
            new Recording
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                TeacherId = teacher1.Id,
                FileName = "a.mp4",
                StorageKey = "a",
                StartedAtUtc = DateTimeOffset.UtcNow,
                EndedAtUtc = DateTimeOffset.UtcNow,
                Status = RecordingStatus.Uploaded,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            },
            new Recording
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                TeacherId = teacher2.Id,
                FileName = "b.mp4",
                StorageKey = "b",
                StartedAtUtc = DateTimeOffset.UtcNow,
                EndedAtUtc = DateTimeOffset.UtcNow,
                Status = RecordingStatus.Uploaded,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            }
        );

        await DbContext.SaveChangesAsync();

        var dashboard = new DashboardQueryService(
            new RecordingRepository(DbContext),
            new QaAlertRepository(DbContext),
            new DeviceRepository(DbContext),
            new ManagerTeacherAssignmentRepository(DbContext),
            new SessionRepository(DbContext),
            new SessionEventRepository(DbContext));

        // Act
        var visible = await dashboard.GetVisibleRecordingsAsync(manager.Id, UserRole.Manager.ToString());

        // Assert
        Assert.Single(visible);
        Assert.Equal("a.mp4", visible[0].FileName);
    }
}

public sealed class FakeStorageService : IStorageService
{
    public Task UploadAsync(string bucketName, string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
    public Task<string> GetPresignedUrlAsync(string bucketName, string objectKey, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("https://fake.url");
    }
}
