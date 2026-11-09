using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Exceptions;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Tests;

public class DayPlannerTests
{
    private static readonly DateOnly Birth = new(2000, 1, 1);
    private static readonly DateOnly Date = Birth.AddDays(9000);
    private static readonly TimeRange Workday = TimeRange.From(new TimeOnly(9, 0), new TimeOnly(18, 0));

    private static EnergyProfile Profile(Chronotype chronotype = Chronotype.Dove) => new(Birth, Date, chronotype);

    private static PlannableTask Task(TaskType type, int minutes, TaskPriority priority = TaskPriority.Medium) =>
        new(Guid.NewGuid(), type.ToString(), type, priority, minutes);

    [Fact]
    public void Plan_PlacesTasksWithoutOverlapInsideWorkday()
    {
        var plan = DayPlanner.Plan(Profile(), Workday,
        [
            Task(TaskType.Intellectual, 120), Task(TaskType.Physical, 60),
            Task(TaskType.Creative, 90), Task(TaskType.Routine, 40)
        ]);

        Assert.Equal(4, plan.Tasks.Count);
        Assert.Empty(plan.Unplaced);

        var ranges = plan.Tasks.Select(p => p.Range).Append(plan.Lunch!.Value).OrderBy(r => r.Start).ToList();
        Assert.All(ranges, r => Assert.True(r.Start >= Workday.Start && r.End <= Workday.End));
        for (var i = 1; i < ranges.Count; i++) Assert.True(ranges[i - 1].End <= ranges[i].Start);
    }

    [Fact]
    public void Plan_PutsLunchAtNoonForNineToSix()
    {
        var plan = DayPlanner.Plan(Profile(), Workday, []);
        Assert.Equal(new TimeRange(12 * 60, 13 * 60), plan.Lunch);
    }

    [Fact]
    public void Plan_ShiftsIntellectualPeakLaterForOwl()
    {
        var task = Task(TaskType.Intellectual, 60);
        var wideDay = TimeRange.From(new TimeOnly(6, 0), new TimeOnly(23, 0));

        var lark = DayPlanner.PlaceAround(Profile(Chronotype.Lark), wideDay, [task], [], null).Tasks.Single();
        var owl = DayPlanner.PlaceAround(Profile(Chronotype.Owl), wideDay, [task], [], null).Tasks.Single();

        Assert.InRange(owl.Range.Start - lark.Range.Start, 180, 240);
    }

    [Fact]
    public void Plan_PutsRoutineAwayFromIntellectualPeak()
    {
        var plan = DayPlanner.Plan(Profile(), Workday, [Task(TaskType.Intellectual, 60), Task(TaskType.Routine, 60)]);

        var intellectual = plan.Tasks.Single(p => p.Task.Type == TaskType.Intellectual);
        var routine = plan.Tasks.Single(p => p.Task.Type == TaskType.Routine);
        Assert.True(
            Profile().Suitability(TaskType.Intellectual, intellectual.Range.StartTime) >
            Profile().Suitability(TaskType.Intellectual, routine.Range.StartTime));
    }

    [Fact]
    public void Plan_HighPriorityRoutineDoesNotTakeHardTaskWindow()
    {
        var plan = DayPlanner.Plan(Profile(), Workday,
        [
            Task(TaskType.Routine, 40, TaskPriority.High), Task(TaskType.Intellectual, 90, TaskPriority.High),
            Task(TaskType.Physical, 60), Task(TaskType.Creative, 120)
        ]);

        var physical = plan.Tasks.Single(p => p.Task.Type == TaskType.Physical);
        Assert.Empty(plan.Unplaced);
        Assert.True(physical.Range.Start >= 13 * 60, $"Физическая задача стоит слишком рано: {physical.Range.StartTime}");
    }

    [Fact]
    public void Explanation_DoesNotClaimPeakForOffPeakWindow()
    {
        var offPeak = new TimeRange(9 * 60, 10 * 60);
        var text = PlacementExplainer.ForPlanned(Profile(), TaskType.Physical, offPeak);
        Assert.Contains("не попадает на дневной подъём", text);
    }

    [Fact]
    public void Plan_SplitsLongTaskAroundLunchAndOtherTasks()
    {
        var workday = TimeRange.From(new TimeOnly(8, 30), new TimeOnly(19, 45));
        var work = Task(TaskType.Routine, 480, TaskPriority.High);
        var sport = Task(TaskType.Physical, 90, TaskPriority.High);

        var plan = DayPlanner.Plan(Profile(Chronotype.Lark), workday, [work, sport]);

        Assert.Empty(plan.Unplaced);
        var parts = plan.Tasks.Where(p => p.Task == work).ToList();
        Assert.True(parts.Count > 1);
        Assert.All(parts, p => Assert.True(p.IsPart && p.Range.Length >= 30));
        Assert.Equal(480, parts.Sum(p => p.Range.Length));

        var all = plan.Tasks.Select(p => p.Range).Append(plan.Lunch!.Value).OrderBy(r => r.Start).ToList();
        for (var i = 1; i < all.Count; i++) Assert.True(all[i - 1].End <= all[i].Start);
    }

    [Fact]
    public void Plan_ReportsTasksThatDoNotFit()
    {
        var tooLong = Task(TaskType.Routine, 600);
        var plan = DayPlanner.Plan(Profile(), Workday, [tooLong]);

        Assert.Empty(plan.Tasks);
        Assert.Equal(tooLong, plan.Unplaced.Single());
    }

    [Fact]
    public void Plan_PrefersHighPriorityWhenDayIsFull()
    {
        var low = Task(TaskType.Routine, 240, TaskPriority.Low);
        var high = Task(TaskType.Routine, 240, TaskPriority.High);
        var shortDay = TimeRange.From(new TimeOnly(9, 0), new TimeOnly(13, 0));

        var plan = DayPlanner.Plan(Profile(), shortDay, [low, high]);

        Assert.Equal(high, plan.Tasks.Single().Task);
        Assert.Equal(low, plan.Unplaced.Single());
    }

    [Fact]
    public void PlaceAround_DoesNotUseFixedRanges()
    {
        var pinned = new TimeRange(9 * 60, 12 * 60);
        var plan = DayPlanner.PlaceAround(Profile(Chronotype.Lark), Workday,
            [Task(TaskType.Intellectual, 60)], [pinned], null);

        Assert.False(plan.Tasks.Single().Range.Overlaps(pinned));
    }

    [Fact]
    public void ValidateMove_RejectsOutsideWorkday() =>
        Assert.Throws<DomainRuleException>(() => DayPlanner.ValidateMove(Workday, new TimeOnly(17, 30), 60, []));

    [Fact]
    public void ValidateMove_RejectsOverlapWithPinned() =>
        Assert.Throws<DomainRuleException>(() =>
            DayPlanner.ValidateMove(Workday, new TimeOnly(10, 0), 60, [new TimeRange(10 * 60 + 30, 11 * 60)]));

    [Fact]
    public void Explanation_ForRoutineAndCycleTaskIsNotEmpty()
    {
        var range = new TimeRange(10 * 60, 11 * 60);
        Assert.Contains("10:00–11:00", PlacementExplainer.ForPlanned(Profile(), TaskType.Intellectual, range));
        Assert.Contains("Рутинная", PlacementExplainer.ForPlanned(Profile(), TaskType.Routine, range));
    }
}
