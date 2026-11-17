using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PerfectDay_Application.Abstractions;
using PerfectDay_Infrastructure.Calendars;
using PerfectDay_Infrastructure.ExternalAuth;
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
        services.AddScoped<INotificationReadRepository, NotificationReadRepository>();
        services.AddScoped<ICalendarRepository, CalendarRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        var calendars = configuration.GetSection(CalendarOptions.SectionName).Get<CalendarOptions>() ?? new CalendarOptions();
        services.AddHttpClient(IcalFeedReader.ClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("PerfectDay/1.0 (+calendar import)");
            })
            .ConfigurePrimaryHttpMessageHandler(() => IcalFeedReader.CreateHandler(calendars.AllowPrivateNetworks));
        services.AddScoped<ICalendarFeedReader, IcalFeedReader>();

        services.Configure<ExternalAuthOptions>(configuration.GetSection(ExternalAuthOptions.SectionName));
        services.AddHttpClient(OAuthProvider.ClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<IExternalAuthProvider, YandexAuthProvider>();
        services.AddScoped<IExternalAuthProvider, VkAuthProvider>();
        services.AddScoped<IExternalAuthProvider, GoogleAuthProvider>();

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
