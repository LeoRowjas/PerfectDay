using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Application.Services;

/// <summary>
/// Напоминания внутри приложения. Они не хранятся, а вычисляются из расписания на текущий момент:
/// клиент передаёт свои дату и время и раз в минуту запрашивает список. Хранится только отметка о прочтении.
/// </summary>
public sealed class NotificationService(
    IUserRepository users,
    IScheduleRepository schedules,
    INotificationReadRepository reads,
    IUnitOfWork unitOfWork)
{
    private const int RemindBeforeMinutes = 10;
    private const int MorningSummaryBeforeMinutes = 60;
    private const int CriticalNoticeBeforeMinutes = 30;
    private const int LongWorkMinutes = 60;

    public async Task<NotificationsDto> ListAsync(Guid userId, DateOnly today, TimeOnly now, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
        var schedule = await schedules.GetAsync(userId, today, ct);
        var minute = TimeRange.ToMinutes(now);

        var items = new List<NotificationDto>();
        var blocks = schedule?.Blocks.OrderBy(b => b.Start).ToList() ?? [];

        AddPlanReady(user, today, blocks, minute, items);
        if (user.RemindBeforeTask) AddTaskStarts(user, today, blocks, minute, items);
        if (user.RestReminders)
        {
            AddBreaks(blocks, minute, items);
            AddCriticalTomorrow(user, today, minute, items);
        }

        var read = await reads.ListReadKeysAsync(userId, items.Select(i => i.Id).ToList(), ct);
        var result = items
            .Select(i => i with { IsRead = read.Contains(i.Id) })
            .OrderByDescending(i => i.At)
            .ToList();

        return new NotificationsDto(result, result.Count(i => !i.IsRead));
    }

    public async Task MarkReadAsync(Guid userId, IReadOnlyList<string> ids, CancellationToken ct)
    {
        var keys = ids.Where(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 100).Distinct().ToList();
        var known = await reads.ListReadKeysAsync(userId, keys, ct);
        foreach (var key in keys.Where(k => !known.Contains(k))) reads.Add(new NotificationRead(userId, key));
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void AddPlanReady(User user, DateOnly today, List<ScheduleBlock> blocks, int minute, List<NotificationDto> items)
    {
        var taskBlocks = blocks.Where(b => b.Task is not null).ToList();
        var at = Math.Max(0, TimeRange.ToMinutes(user.WorkdayStart) - MorningSummaryBeforeMinutes);
        if (taskBlocks.Count == 0 || minute < at) return;

        var count = taskBlocks.Select(b => b.TaskId).Distinct().Count();
        var total = taskBlocks.Sum(b => Length(b));
        var first = taskBlocks[0];
        items.Add(Item($"plan:{today:yyyy-MM-dd}", NotificationKind.PlanReady, "План на сегодня готов",
            $"{TasksWord(count)}, {Duration(total)}. Первая — «{first.Task!.Title}» в {first.Start:HH\\:mm}.",
            at, minute));
    }

    private static void AddTaskStarts(User user, DateOnly today, List<ScheduleBlock> blocks, int minute, List<NotificationDto> items)
    {
        var profile = user.BirthDate is { } birthDate && user.Chronotype is { } chronotype
            ? new EnergyProfile(birthDate, today, chronotype)
            : null;

        foreach (var block in blocks.Where(b => b.Task is { IsCompleted: false }))
        {
            var range = TimeRange.From(block.Start, block.End);
            var at = range.Start - RemindBeforeMinutes;
            if (minute < at || minute >= range.End) continue;

            var text = $"Начало в {block.Start:HH\\:mm}.";
            if (profile is not null && EnergyProfile.CycleFor(block.Task!.Type) is { } cycle)
                text += $" Энергия цикла в это время — {BiorhythmCalculator.Percent(profile.AverageEnergy(cycle, range))}%.";

            items.Add(Item($"task:{block.Id}", NotificationKind.TaskStart,
                $"Через {RemindBeforeMinutes} минут: {block.Task!.Title}", text, at, minute, range.Start));
        }
    }

    private static void AddBreaks(List<ScheduleBlock> blocks, int minute, List<NotificationDto> items)
    {
        foreach (var pause in blocks.Where(b => b.Kind == BlockKind.Break))
        {
            var range = TimeRange.From(pause.Start, pause.End);
            var at = range.Start - RemindBeforeMinutes;
            if (minute < at || minute >= range.End) continue;

            var worked = HardWorkBefore(blocks, range.Start);
            var text = $"В {pause.Start:HH\\:mm} — {pause.Title.ToLowerInvariant()}, {Duration(range.Length)}.";
            if (worked >= LongWorkMinutes) text += $" Сложная работа идёт уже {Duration(worked)}: встаньте и пройдитесь.";

            items.Add(Item($"break:{pause.Id}", NotificationKind.BreakSoon, $"Через {RemindBeforeMinutes} минут перерыв",
                text, at, minute, range.Start));
        }
    }

    private static void AddCriticalTomorrow(User user, DateOnly today, int minute, List<NotificationDto> items)
    {
        if (user.BirthDate is not { } birthDate) return;

        var tomorrow = today.AddDays(1);
        var cycles = BiorhythmCalculator.CriticalCycles(birthDate, tomorrow);
        var at = Math.Max(0, TimeRange.ToMinutes(user.WorkdayStart) - CriticalNoticeBeforeMinutes);
        if (cycles.Count == 0 || minute < at) return;

        var names = string.Join(" и ", cycles.Select(c => PlacementExplainer.CycleName(c).ToLowerInvariant()));
        var verb = cycles.Count == 1 ? "цикл пересекает" : "циклы пересекают";
        items.Add(Item($"critical:{tomorrow:yyyy-MM-dd}", NotificationKind.CriticalTomorrow, "Завтра критический день",
            $"Завтра {names} {verb} ноль. Расписание на завтра лучше облегчить и оставить больше времени на отдых.",
            at, minute));
    }

    // Сколько минут сложной работы идёт подряд перед указанным моментом.
    private static int HardWorkBefore(List<ScheduleBlock> blocks, int moment)
    {
        var total = 0;
        var cursor = moment;
        foreach (var block in blocks.Where(b => TimeRange.ToMinutes(b.End) <= moment).OrderByDescending(b => b.End))
        {
            if (cursor - TimeRange.ToMinutes(block.End) >= 15 || block.Task is null or { Type: TaskType.Routine }) break;
            total += Length(block);
            cursor = TimeRange.ToMinutes(block.Start);
        }

        return total;
    }

    private static NotificationDto Item(string id, NotificationKind kind, string title, string text, int at, int now, int? startsAt = null) =>
        new(id, kind, title, text, TimeRange.FromMinutes(at), false, startsAt is { } start && now < start);

    private static int Length(ScheduleBlock block) => TimeRange.From(block.Start, block.End).Length;

    private static string TasksWord(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var word = lastTwo is >= 11 and <= 14 ? "задач" : last switch { 1 => "задача", >= 2 and <= 4 => "задачи", _ => "задач" };
        return $"{count} {word}";
    }

    private static string Duration(int minutes)
    {
        var hours = minutes / 60;
        var rest = minutes % 60;
        if (hours == 0) return $"{rest} мин";
        return rest == 0 ? $"{hours} ч" : $"{hours} ч {rest} мин";
    }
}
