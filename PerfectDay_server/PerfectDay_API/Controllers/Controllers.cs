using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PerfectDay_API.Infrastructure;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Services;
using PerfectDay_Domain.Enums;
using PerfectDay_Infrastructure.Security;

namespace PerfectDay_API.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(AuthCookie.RateLimitPolicy)]
public sealed class AuthController(AuthService auth, IOptions<JwtOptions> jwt) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<ProfileDto>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        AuthCookie.Append(Response, Request, result.Token, jwt.Value.LifetimeDays);
        return Created("/api/profile", result.Profile);
    }

    [HttpPost("login")]
    public async Task<ActionResult<ProfileDto>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        AuthCookie.Append(Response, Request, result.Token, jwt.Value.LifetimeDays);
        return result.Profile;
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        AuthCookie.Delete(Response);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(ProfileService profiles) : ControllerBase
{
    [HttpGet]
    public Task<ProfileDto> Get(CancellationToken ct) => profiles.GetAsync(User.GetUserId(), ct);

    [HttpPut]
    public Task<ProfileDto> Update([FromQuery, BindRequired] DateOnly today, UpdateProfileRequest request, CancellationToken ct) =>
        profiles.UpdateAsync(User.GetUserId(), today, request, ct);

    [HttpPut("reminders")]
    public Task<ProfileDto> UpdateReminders(ReminderSettingsDto request, CancellationToken ct) =>
        profiles.UpdateRemindersAsync(User.GetUserId(), request, ct);
}

[ApiController]
[Authorize]
[Route("api/tasks")]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TaskDto>> List([FromQuery] TaskType? type, [FromQuery] DateOnly? today, CancellationToken ct) =>
        tasks.ListAsync(User.GetUserId(), type, today ?? ServerToday, ct);

    [HttpGet("{id:guid}")]
    public Task<TaskDto> Get(Guid id, CancellationToken ct) => tasks.GetAsync(User.GetUserId(), id, ct);

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(SaveTaskRequest request, [FromQuery] DateOnly? today, CancellationToken ct)
    {
        var task = await tasks.CreateAsync(User.GetUserId(), request, today ?? ServerToday, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    public Task<TaskDto> Update(Guid id, SaveTaskRequest request, [FromQuery] DateOnly? today, CancellationToken ct) =>
        tasks.UpdateAsync(User.GetUserId(), id, request, today ?? ServerToday, ct);

    [HttpPut("{id:guid}/completed")]
    public Task<TaskDto> SetCompleted(Guid id, SetTaskCompletedRequest request, CancellationToken ct) =>
        tasks.SetCompletedAsync(User.GetUserId(), id, request.IsCompleted!.Value, ct);

    [HttpPut("{id:guid}/day")]
    public Task<TaskDto> SetDay(Guid id, SetTaskDayRequest request, CancellationToken ct) =>
        tasks.SetDayAsync(User.GetUserId(), id, request.DueDate!.Value, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool withFollowing, CancellationToken ct)
    {
        await tasks.DeleteAsync(User.GetUserId(), id, withFollowing, ct);
        return NoContent();
    }

    // Старые клиенты не передают today; повторения тогда считаются от даты сервера.
    private static DateOnly ServerToday => DateOnly.FromDateTime(DateTime.Now);
}

[ApiController]
[Authorize]
[Route("api")]
public sealed class BiorhythmsController(BiorhythmService biorhythms) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardDto> Dashboard([FromQuery, BindRequired] DateOnly date, CancellationToken ct) =>
        biorhythms.GetDashboardAsync(User.GetUserId(), date, ct);

    [HttpGet("biorhythms/chart")]
    public Task<ChartDto> Chart([FromQuery, BindRequired] DateOnly date, [FromQuery, BindRequired] ChartScale scale, CancellationToken ct) =>
        biorhythms.GetChartAsync(User.GetUserId(), date, scale, ct);

    [HttpGet("biorhythms/forecast")]
    public Task<ForecastDto> Forecast([FromQuery, BindRequired] DateOnly date, [FromQuery] int days = 30, CancellationToken ct = default) =>
        biorhythms.GetForecastAsync(User.GetUserId(), date, days, ct);
}

[ApiController]
[Authorize]
[Route("api/schedules/{date}")]
public sealed class SchedulesController(ScheduleService schedules) : ControllerBase
{
    [HttpGet]
    public Task<ScheduleDto> Get(DateOnly date, [FromQuery, BindRequired] DateOnly today, CancellationToken ct) =>
        schedules.GetAsync(User.GetUserId(), date, today, ct);

    [HttpPost("build")]
    public Task<ScheduleDto> Build(DateOnly date, [FromQuery, BindRequired] DateOnly today, CancellationToken ct) =>
        schedules.BuildAsync(User.GetUserId(), date, today, ct);

    [HttpPatch("blocks/{blockId:guid}")]
    public Task<ScheduleDto> Move(DateOnly date, Guid blockId, [FromQuery, BindRequired] DateOnly today, MoveBlockRequest request, CancellationToken ct) =>
        schedules.MoveBlockAsync(User.GetUserId(), date, today, blockId, request.Start!.Value, ct);

    [HttpPost("blocks/{blockId:guid}/preview")]
    public Task<MovePreviewDto> PreviewMove(DateOnly date, Guid blockId, [FromQuery, BindRequired] DateOnly today, MoveBlockRequest request, CancellationToken ct) =>
        schedules.PreviewMoveAsync(User.GetUserId(), date, today, blockId, request.Start!.Value, ct);

    [HttpDelete("blocks/{blockId:guid}/pin")]
    public Task<ScheduleDto> Unpin(DateOnly date, Guid blockId, [FromQuery, BindRequired] DateOnly today, CancellationToken ct) =>
        schedules.UnpinBlockAsync(User.GetUserId(), date, today, blockId, ct);

    [HttpDelete("blocks/{blockId:guid}")]
    public Task<ScheduleDto> RemoveBreak(DateOnly date, Guid blockId, [FromQuery, BindRequired] DateOnly today, CancellationToken ct) =>
        schedules.RemoveBreakAsync(User.GetUserId(), date, today, blockId, ct);

    [HttpPost("breaks")]
    public Task<ScheduleDto> AddBreak(DateOnly date, [FromQuery, BindRequired] DateOnly today, AddBreakRequest request, CancellationToken ct) =>
        schedules.AddBreakAsync(User.GetUserId(), date, today, request, ct);

    [HttpPut("lightened")]
    public Task<ScheduleDto> SetLightened(DateOnly date, [FromQuery, BindRequired] DateOnly today, SetLightenedRequest request, CancellationToken ct) =>
        schedules.SetLightenedAsync(User.GetUserId(), date, today, request.IsLightened!.Value, ct);

    [HttpPost("warnings/dismiss")]
    public Task<ScheduleDto> DismissWarning(DateOnly date, [FromQuery, BindRequired] DateOnly today, DismissWarningRequest request, CancellationToken ct) =>
        schedules.DismissWarningAsync(User.GetUserId(), date, today, request.WarningId, ct);
}

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(NotificationService notifications) : ControllerBase
{
    [HttpGet]
    public Task<NotificationsDto> List([FromQuery, BindRequired] DateOnly today, [FromQuery, BindRequired] TimeOnly now, CancellationToken ct) =>
        notifications.ListAsync(User.GetUserId(), today, now, ct);

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead(MarkNotificationsReadRequest request, CancellationToken ct)
    {
        await notifications.MarkReadAsync(User.GetUserId(), request.Ids, ct);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/calendars")]
public sealed class CalendarsController(CalendarService calendars) : ControllerBase
{
    [HttpGet]
    public Task<CalendarsDto> List([FromQuery, BindRequired] DateOnly today, CancellationToken ct) =>
        calendars.GetAsync(User.GetUserId(), today, ct);

    [HttpPost]
    public Task<CalendarsDto> Connect([FromQuery, BindRequired] DateOnly today, ConnectCalendarRequest request, CancellationToken ct) =>
        calendars.ConnectAsync(User.GetUserId(), today, request, ct);

    [HttpPut("{id:guid}")]
    public Task<CalendarsDto> UpdateSettings(Guid id, [FromQuery, BindRequired] DateOnly today, CalendarSettingsRequest request, CancellationToken ct) =>
        calendars.UpdateSettingsAsync(User.GetUserId(), id, today, request, ct);

    [HttpPost("sync")]
    public Task<CalendarsDto> Sync([FromQuery, BindRequired] DateOnly today, SyncCalendarsRequest request, CancellationToken ct) =>
        calendars.SyncAsync(User.GetUserId(), today, request, ct);

    [HttpDelete("{id:guid}")]
    public Task<CalendarsDto> Disconnect(Guid id, [FromQuery, BindRequired] DateOnly today, CancellationToken ct) =>
        calendars.DisconnectAsync(User.GetUserId(), id, today, ct);
}

[ApiController]
[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    [HttpGet]
    public Task<AnalyticsDto> Get([FromQuery, BindRequired] DateOnly today, [FromQuery] AnalyticsPeriod period, CancellationToken ct) =>
        analytics.GetAsync(User.GetUserId(), today, period, ct);

    [HttpPost("apply")]
    public Task<ApplyRecommendationsDto> Apply([FromQuery, BindRequired] DateOnly today, CancellationToken ct) =>
        analytics.ApplyAsync(User.GetUserId(), today, ct);

    [HttpGet("report")]
    public async Task<IActionResult> Report([FromQuery, BindRequired] DateOnly today, [FromQuery] AnalyticsPeriod period, CancellationToken ct)
    {
        var csv = await analytics.ReportAsync(User.GetUserId(), today, period, ct);
        return File(csv, "text/csv; charset=utf-8", $"perfectday-{period.ToString().ToLowerInvariant()}-{today:yyyy-MM-dd}.csv");
    }
}
