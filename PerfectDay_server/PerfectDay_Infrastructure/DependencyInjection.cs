using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PerfectDay_Application.Abstractions;
using PerfectDay_Infrastructure.Persistence;
using PerfectDay_Infrastructure.Security;

namespace PerfectDay_Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.Key) >= JwtOptions.MinKeyBytes,
                $"Jwt:Key должен быть не короче {JwtOptions.MinKeyBytes} байт.")
            .ValidateOnStart();

        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        return services;
    }
}
