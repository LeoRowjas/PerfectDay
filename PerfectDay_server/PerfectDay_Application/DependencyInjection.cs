using Microsoft.Extensions.DependencyInjection;
using PerfectDay_Application.Services;

namespace PerfectDay_Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<TaskService>();
        services.AddScoped<BiorhythmService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<TaskSeriesGenerator>();
        services.AddScoped<CalendarService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<ExternalAuthService>();
        return services;
    }
}
