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
}

[ApiController]
[Authorize]
[Route("api/tasks")]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TaskDto>> List([FromQuery] TaskType? type, CancellationToken ct) =>
        tasks.ListAsync(User.GetUserId(), type, ct);

    [HttpGet("{id:guid}")]
    public Task<TaskDto> Get(Guid id, CancellationToken ct) => tasks.GetAsync(User.GetUserId(), id, ct);

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(SaveTaskRequest request, CancellationToken ct)
    {
        var task = await tasks.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpPut("{id:guid}")]
    public Task<TaskDto> Update(Guid id, SaveTaskRequest request, CancellationToken ct) =>
        tasks.UpdateAsync(User.GetUserId(), id, request, ct);

    [HttpPut("{id:guid}/completed")]
    public Task<TaskDto> SetCompleted(Guid id, SetTaskCompletedRequest request, CancellationToken ct) =>
        tasks.SetCompletedAsync(User.GetUserId(), id, request.IsCompleted!.Value, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tasks.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
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

    [HttpDelete("blocks/{blockId:guid}/pin")]
    public Task<ScheduleDto> Unpin(DateOnly date, Guid blockId, [FromQuery, BindRequired] DateOnly today, CancellationToken ct) =>
        schedules.UnpinBlockAsync(User.GetUserId(), date, today, blockId, ct);
}
