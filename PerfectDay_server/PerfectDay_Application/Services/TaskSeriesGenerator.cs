using PerfectDay_Application.Abstractions;

namespace PerfectDay_Application.Services;

/// <summary>Создаёт повторения периодических задач, когда до них доходит очередь. Сохраняет вызывающий сервис.</summary>
public sealed class TaskSeriesGenerator(ITaskRepository tasks)
{
    /// <summary>Повторения создаются на неделю вперёд: столько видно в списке задач.</summary>
    public const int HorizonDays = 6;

    /// <summary>Дальше этого расписание на будущее не тянет за собой повторения.</summary>
    public const int MaxAheadDays = 60;

    public async Task<bool> EnsureAsync(Guid userId, DateOnly upTo, CancellationToken ct)
    {
        var behind = await tasks.ListSeriesBehindAsync(userId, upTo, ct);
        foreach (var series in behind) tasks.AddRange(series.Generate(upTo));
        return behind.Count > 0;
    }

    public static DateOnly Horizon(DateOnly today) => today.AddDays(HorizonDays);
}
