using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Exceptions;
using PerfectDay_Application.Services;
using PerfectDay_Domain.Analytics;
using PerfectDay_Domain.Biorhythms;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Exceptions;
using PerfectDay_Domain.Planning;

namespace PerfectDay_Tests;

public class TaskSeriesTests
{
    // 9 ноября 2026 — понедельник.
    private static readonly DateOnly Monday = new(2026, 11, 9);

    private static TaskSeries Weekly(DateOnly? until = null, params DayOfWeek[] days) =>
        new(Guid.NewGuid(), "Тренировка", TaskType.Physical, 60, TaskPriority.Medium, RecurrenceKind.Weekly, days, Monday, until);

    [Fact]
    public void Generate_CreatesOccurrencesOnSelectedWeekdays()
    {
        var series = Weekly(null, DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday);

        var created = series.Generate(Monday.AddDays(13));

        Assert.Equal([9, 11, 13, 16, 18, 20], created.Select(t => t.DueDate.Day));
        Assert.All(created, t => Assert.Equal(series.Id, t.SeriesId));
    }

    [Fact]
    public void Generate_DoesNotRepeatDatesAlreadyCreated()
    {
        var series = new TaskSeries(Guid.NewGuid(), "Зарядка", TaskType.Physical, 15, TaskPriority.Low, RecurrenceKind.Daily, [], Monday, null);

        series.Generate(Monday.AddDays(2));
        var next = series.Generate(Monday.AddDays(4));

        Assert.Equal([Monday.AddDays(3), Monday.AddDays(4)], next.Select(t => t.DueDate));
        Assert.Empty(series.Generate(Monday.AddDays(4)));
    }

    [Fact]
    public void Generate_StopsAtEndDate()
    {
        var series = Weekly(Monday.AddDays(7), DayOfWeek.Monday);
        Assert.Equal([Monday, Monday.AddDays(7)], series.Generate(Monday.AddDays(30)).Select(t => t.DueDate));
    }

    [Fact]
    public void Weekly_WithoutDays_IsRejected() =>
        Assert.Throws<DomainRuleException>(() => Weekly());
}

public class EfficiencyAnalyzerTests
{
    private static readonly DateOnly Birth = new(1995, 3, 14);
    private static readonly DateOnly Today = new(2026, 11, 9);

    private static PlannedBlock Block(TaskType type, int startHour, bool done, bool pinned = false, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), type, done, new TimeRange(startHour * 60, startHour * 60 + 60), pinned);

    [Fact]
    public void Analyze_CountsTaskAsDoneOnlyOnItsLastDay()
    {
        var carried = Guid.NewGuid();
        var days = new[]
        {
            new PlannedDay(Today.AddDays(-2), [Block(TaskType.Routine, 10, true, id: carried), Block(TaskType.Routine, 11, true)]),
            new PlannedDay(Today.AddDays(-1), [Block(TaskType.Routine, 10, true, id: carried)])
        };

        var report = EfficiencyAnalyzer.Analyze(Birth, Chronotype.Lark, days, Today.AddDays(-7), Today.AddDays(-1), Today);

        Assert.Equal(3, report.Planned);
        Assert.Equal(2, report.Done);
        Assert.Equal(1, report.Days.Single(d => d.Date == Today.AddDays(-2)).Done);
    }

    [Fact]
    public void Analyze_NoticesMorningPerformanceAndManualMoves()
    {
        var days = Enumerable.Range(1, 7).Select(offset => new PlannedDay(Today.AddDays(-offset),
        [
            Block(TaskType.Intellectual, 9, true),
            Block(TaskType.Creative, 17, false, pinned: true)
        ])).ToList();

        var report = EfficiencyAnalyzer.Analyze(Birth, Chronotype.Lark, days, Today.AddDays(-7), Today.AddDays(-1), Today);

        Assert.Equal(7, report.MovedManually);
        Assert.Equal(TaskType.Creative, report.MostMovedType);
        Assert.Contains(report.Insights, i => i.Kind == InsightKind.TimeOfDay && i.Title == "Утро — ваше время");
        Assert.Contains(report.Insights, i => i.Kind == InsightKind.Postponed && i.Title.StartsWith("Творческие"));
    }

    [Fact]
    public void Analyze_ComparesWithPreviousPeriod()
    {
        var days = Enumerable.Range(1, 14).Select(offset => new PlannedDay(Today.AddDays(-offset),
            [Block(TaskType.Routine, 10, offset <= 7)])).ToList();

        var report = EfficiencyAnalyzer.Analyze(Birth, Chronotype.Lark, days, Today.AddDays(-7), Today.AddDays(-1), Today);

        Assert.Equal(7, report.PreviousPlanned);
        Assert.Equal(0, report.PreviousDone);
        Assert.Contains(report.Insights, i => i.Title == "Лучше прошлой недели");
    }
}

public class MeetingAdvisorTests
{
    [Fact]
    public void Suggest_MovesMeetingFromEveningToEmotionalPeak()
    {
        var birth = new DateOnly(1995, 3, 14);
        var date = new DateOnly(2026, 11, 9);
        var profile = new EnergyProfile(birth, date, Chronotype.Lark);
        var workday = new TimeRange(9 * 60, 18 * 60);

        var advice = MeetingAdvisor.Suggest(profile, workday, [new Meeting("Созвон", new TimeRange(17 * 60, 18 * 60))]);

        Assert.NotNull(advice);
        Assert.True(advice.Better.Start < 14 * 60);
        Assert.True(advice.BetterPercent > advice.CurrentPercent);
    }
}

public class ExternalAuthServiceTests
{
    private sealed class Users : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id));
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email));

        public Task<User?> GetByExternalLoginAsync(ExternalProvider provider, string subject, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(u => u.ExternalLogins.Any(l => l.Provider == provider && l.Subject == subject)));

        public void Add(User user) => Items.Add(user);
    }

    private sealed class Tokens : ITokenService
    {
        public string CreateToken(User user) => user.Id.ToString();
    }

    private sealed class Saver : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private static (ExternalAuthService Service, Users Users) Create()
    {
        var users = new Users();
        return (new ExternalAuthService(users, new Tokens(), [], new Saver()), users);
    }

    [Fact]
    public async Task SignIn_CreatesUserWithoutPasswordAndFindsHimNextTime()
    {
        var (service, users) = Create();
        var identity = new ExternalIdentity(ExternalProvider.Yandex, "42", "Anna@Yandex.ru", true);

        var first = await service.SignInAsync(identity, CancellationToken.None);
        var second = await service.SignInAsync(identity with { Email = null }, CancellationToken.None);

        Assert.Single(users.Items);
        Assert.Equal("anna@yandex.ru", first.Profile.Email);
        Assert.False(first.Profile.HasPassword);
        Assert.Equal(first.Profile.Id, second.Profile.Id);
    }

    [Fact]
    public async Task SignIn_LinksExistingAccountOnlyWithVerifiedEmail()
    {
        var (service, users) = Create();
        users.Add(new User("anna@example.ru", "hash"));

        await Assert.ThrowsAsync<ExternalAuthException>(() =>
            service.SignInAsync(new ExternalIdentity(ExternalProvider.Vk, "7", "anna@example.ru", false), CancellationToken.None));

        var result = await service.SignInAsync(new ExternalIdentity(ExternalProvider.Google, "g1", "anna@example.ru", true), CancellationToken.None);
        Assert.Equal(users.Items[0].Id, result.Profile.Id);
        Assert.Equal([ExternalProvider.Google], result.Profile.ExternalLogins);
    }
}
