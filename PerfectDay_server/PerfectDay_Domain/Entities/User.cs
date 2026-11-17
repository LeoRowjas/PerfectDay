using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Exceptions;

namespace PerfectDay_Domain.Entities;

public class User
{
    private readonly List<ExternalLogin> _externalLogins = [];

    private User()
    {
    }

    public User(string email, string? passwordHash)
    {
        Id = Guid.CreateVersion7();
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    /// <summary>Пусто, если пользователь зарегистрировался через Яндекс ID, VK ID или Google.</summary>
    public string? PasswordHash { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public Chronotype? Chronotype { get; private set; }
    public TimeOnly WorkdayStart { get; private set; } = new(9, 0);
    public TimeOnly WorkdayEnd { get; private set; } = new(18, 0);
    public DateTime CreatedAt { get; private set; }

    /// <summary>Когда было построено первое расписание; до этого дашборд показывает быстрый старт.</summary>
    public DateTime? OnboardedAt { get; private set; }

    public bool RemindBeforeTask { get; private set; } = true;
    public bool RestReminders { get; private set; } = true;
    public bool BrowserNotifications { get; private set; }

    public void UpdateProfile(DateOnly? birthDate, Chronotype? chronotype, TimeOnly workdayStart, TimeOnly workdayEnd)
    {
        if (workdayEnd <= workdayStart)
            throw new DomainRuleException("Конец дня должен быть позже начала.");

        BirthDate = birthDate;
        Chronotype = chronotype;
        WorkdayStart = workdayStart;
        WorkdayEnd = workdayEnd;
    }

    public IReadOnlyCollection<ExternalLogin> ExternalLogins => _externalLogins;

    public void LinkExternalLogin(ExternalProvider provider, string subject)
    {
        if (_externalLogins.Any(l => l.Provider == provider)) return;
        _externalLogins.Add(new ExternalLogin(Id, provider, subject));
    }

    public void MarkOnboarded() => OnboardedAt ??= DateTime.UtcNow;

    public void UpdateReminders(bool beforeTask, bool rest, bool browser)
    {
        RemindBeforeTask = beforeTask;
        RestReminders = rest;
        BrowserNotifications = browser;
    }
}
