using Academy.Application.Abstractions;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Academy.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IHeartbeatRepository, HeartbeatRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}