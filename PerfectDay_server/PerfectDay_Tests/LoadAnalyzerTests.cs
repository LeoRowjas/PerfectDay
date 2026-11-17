using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Tests;

public class LoadAnalyzerTests
{
    private static readonly DateOnly Birth = new(2000, 1, 1);

    private static LoadItem Item(TaskType type, int startHour, int endHour) =>
        new(Guid.NewGuid(), type.ToString(), type, new TimeRange(startHour * 60, endHour * 60));

    [Fact]
    public void TripleCriticalDay_IsCriticalSeverity()
    {
        var warnings = LoadAnalyzer.CriticalDayWarnings(new EnergyProfile(Birth, Birth, Chronotype.Dove));
        var warning = Assert.Single(warnings);
        Assert.Equal(WarningSeverity.Critical, warning.Severity);
        Assert.Contains("Тройной", warning.Message);
    }

    [Fact]
    public void LongHardSeries_IsDetected()
    {
        var profile = new EnergyProfile(Birth, Birth.AddDays(1), Chronotype.Dove);
        var warnings = LoadAnalyzer.Analyze(profile,
            [Item(TaskType.Intellectual, 9, 11), Item(TaskType.Creative, 11, 13)]);

        Assert.Contains(warnings, w => w.Kind == LoadWarningKind.LongHardSeries);
    }

    [Fact]
    public void RoutineBetweenHardTasks_DoesNotCountAsHardSeriesTime()
    {
        var profile = new EnergyProfile(Birth, Birth.AddDays(1), Chronotype.Dove);
        var warnings = LoadAnalyzer.Analyze(profile,
            [Item(TaskType.Intellectual, 9, 11), Item(TaskType.Routine, 11, 13), Item(TaskType.Creative, 13, 15)]);

        Assert.DoesNotContain(warnings, w => w.Kind == LoadWarningKind.LongHardSeries);
    }

    [Fact]
    public void TaskOnCycleDecline_IsReported()
    {
        // 21-й день: эмоциональный цикл в нижней точке.
        var profile = new EnergyProfile(Birth, Birth.AddDays(21), Chronotype.Dove);
        var warnings = LoadAnalyzer.Analyze(profile, [Item(TaskType.Creative, 10, 11)]);

        Assert.Contains(warnings, w => w.Kind == LoadWarningKind.TaskOnCycleDecline);
    }

    [Fact]
    public void LongHardSeries_SuggestsBreakBetweenTasks()
    {
        var profile = new EnergyProfile(Birth, Birth.AddDays(1), Chronotype.Dove);
        var first = new LoadItem(Guid.NewGuid(), "Экзамен", TaskType.Intellectual, new TimeRange(9 * 60, 10 * 60 + 30));
        var second = new LoadItem(Guid.NewGuid(), "Отчёт", TaskType.Intellectual, new TimeRange(10 * 60 + 30, 12 * 60));

        var warning = Assert.Single(LoadAnalyzer.Analyze(profile, [first, second]), w => w.Kind == LoadWarningKind.LongHardSeries);

        Assert.Equal(LoadActionKind.AddBreak, warning.Action!.Kind);
        Assert.Equal(10 * 60 + 30, warning.Action.Start);
    }

    [Fact]
    public void FifteenMinutePause_EndsSeries()
    {
        var profile = new EnergyProfile(Birth, Birth.AddDays(1), Chronotype.Dove);
        var warnings = LoadAnalyzer.Analyze(profile,
        [
            new LoadItem(Guid.NewGuid(), "A", TaskType.Intellectual, new TimeRange(9 * 60, 11 * 60)),
            new LoadItem(Guid.NewGuid(), "B", TaskType.Physical, new TimeRange(11 * 60 + 15, 13 * 60))
        ]);

        Assert.DoesNotContain(warnings, w => w.Kind == LoadWarningKind.LongHardSeries);
    }

    [Fact]
    public void SameTypeBackToBack_SuggestsWalk()
    {
        var profile = new EnergyProfile(Birth, Birth.AddDays(1), Chronotype.Dove);
        var warnings = LoadAnalyzer.Analyze(profile,
        [
            new LoadItem(Guid.NewGuid(), "Курсовая", TaskType.Intellectual, new TimeRange(13 * 60, 14 * 60 + 30)),
            new LoadItem(Guid.NewGuid(), "Лекции", TaskType.Intellectual, new TimeRange(14 * 60 + 30, 15 * 60 + 30))
        ]);

        var walk = Assert.Single(warnings, w => w.Kind == LoadWarningKind.SwitchActivity);
        Assert.Equal(LoadActionKind.InsertWalk, walk.Action!.Kind);
        Assert.Equal(14 * 60 + 30, walk.Action.Start);
    }

    [Fact]
    public void Decline_SuggestsDayWithRisingCycle()
    {
        var profile = new EnergyProfile(Birth, Birth.AddDays(21), Chronotype.Dove);
        var taskId = Guid.NewGuid();
        var warning = Assert.Single(
            LoadAnalyzer.Analyze(profile, [new LoadItem(taskId, "Презентация", TaskType.Creative, new TimeRange(600, 660))]),
            w => w.Kind == LoadWarningKind.TaskOnCycleDecline);

        Assert.Equal(taskId, warning.Action!.TaskId);
        Assert.True(BiorhythmCalculator.Value(Birth, warning.Action.TargetDate!.Value, BiorhythmCycle.Emotional) > 0);
    }

    [Fact]
    public void Summary_CountsLongestSeriesAndRest()
    {
        var items = new[]
        {
            new LoadItem(Guid.NewGuid(), "A", TaskType.Intellectual, new TimeRange(9 * 60, 10 * 60 + 30)),
            new LoadItem(Guid.NewGuid(), "B", TaskType.Intellectual, new TimeRange(10 * 60 + 30, 12 * 60)),
            new LoadItem(Guid.NewGuid(), "C", TaskType.Routine, new TimeRange(16 * 60, 17 * 60))
        };

        var summary = LoadAnalyzer.Summarize(items, [new TimeRange(12 * 60, 13 * 60)], new TimeRange(9 * 60, 18 * 60), []);

        Assert.Equal(180, summary.HardMinutes);
        Assert.Equal(180, summary.LongestSeriesMinutes);
        Assert.Equal(2, summary.LongestSeriesCount);
        Assert.Equal(60, summary.RestMinutes);
    }
}
