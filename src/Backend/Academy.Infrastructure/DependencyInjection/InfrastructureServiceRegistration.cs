using Academy.Application.Abstractions;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Repositories;
using Academy.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;

namespace Academy.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<AppDbContext>(
            (serviceProvider, options) =>
            {
                options.UseNpgsql(connectionString);

                options.AddInterceptors(
                    serviceProvider.GetRequiredService<
                        AuditSaveChangesInterceptor>());
            });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IHeartbeatRepository, HeartbeatRepository>();
        services.AddScoped<IRecordingRepository, RecordingRepository>();
        services.AddScoped<IQaRuleRepository, QaRuleRepository>();
        services.AddScoped<IQaAlertRepository, QaAlertRepository>();
        services.AddScoped<IQaCandidateRepository, QaCandidateRepository>();
        services.AddScoped<ITranscriptSegmentRepository, TranscriptSegmentRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ITeacherRepository, TeacherRepository>();
        services.AddScoped<IManagerTeacherAssignmentRepository, ManagerTeacherAssignmentRepository>();
        services.AddScoped<IDeviceTeacherAssignmentRepository, DeviceTeacherAssignmentRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        string minioEndpoint = configuration["Storage:Endpoint"] ?? "localhost:9000";
        string minioAccessKey = configuration["Storage:AccessKey"] ?? "academy_minio";
        string minioSecretKey = configuration["Storage:SecretKey"] ?? "AcademyMinio2026";
        string minioBucket = configuration["Storage:Bucket"] ?? "academy-recordings";

        services.AddSingleton<IMinioClient>(_ =>
            new MinioClient()
                .WithEndpoint(minioEndpoint)
                .WithCredentials(minioAccessKey, minioSecretKey)
                .Build());

        services.AddSingleton(minioBucket);
        services.AddScoped<IStorageService, MinioStorageService>();

        return services;
    }
}
