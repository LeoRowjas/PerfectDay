using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Application.Services;

internal static class CalendarRules
{
    /// <summary>Событие занимает время в расписании: импорт включён, у него есть время и из него не делается задача.</summary>
    public static bool IsBusy(ExternalEvent item, CalendarConnection connection) =>
        connection.ImportAsBusy && !item.IsAllDay && !IsTask(item, connection);

    public static bool IsTask(ExternalEvent item, CalendarConnection connection) =>
        connection.CreateTasksFromTagged && item.IsTaggedAsTask;

    public static string ProviderName(CalendarProvider provider) => provider switch
    {
        CalendarProvider.Google => "Google",
        CalendarProvider.Yandex => "Яндекс",
        _ => "Apple"
    };
}
