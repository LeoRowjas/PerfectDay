using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;

namespace PerfectDay_Application.Services;

public sealed class ProfileService(IUserRepository users, IScheduleRepository schedules, IUnitOfWork unitOfWork)
{
    private static readonly DateOnly MinBirthDate = new(1900, 1, 1);

    public async Task<ProfileDto> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
        return user.ToDto();
    }

    public async Task<ProfileDto> UpdateAsync(Guid userId, DateOnly today, UpdateProfileRequest request, CancellationToken ct)
    {
        if (request.BirthDate is { } birthDate &&
            (birthDate < MinBirthDate || birthDate > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))))
            throw new AppValidationException("Проверьте дату рождения.");

        var user = await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
        var affectsPlanning = user.BirthDate != request.BirthDate || user.Chronotype != request.Chronotype ||
                              user.WorkdayStart != request.WorkdayStart || user.WorkdayEnd != request.WorkdayEnd;
        user.UpdateProfile(request.BirthDate, request.Chronotype, request.WorkdayStart!.Value, request.WorkdayEnd!.Value);

        // Старые расписания больше не верны: задачи снова станут неразмещёнными и встанут заново.
        if (affectsPlanning) await schedules.RemoveFromDateAsync(userId, today, ct);

        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }

    public async Task<ProfileDto> UpdateRemindersAsync(Guid userId, ReminderSettingsDto request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Пользователь не найден.");
        user.UpdateReminders(request.BeforeTask!.Value, request.Rest!.Value, request.Browser!.Value);
        await unitOfWork.SaveChangesAsync(ct);
        return user.ToDto();
    }
}
