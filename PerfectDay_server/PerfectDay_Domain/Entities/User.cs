using PerfectDay_Domain.Enums;
using PerfectDay_Domain.Exceptions;

namespace PerfectDay_Domain.Entities;

public class User
{
    private User()
    {
    }

    public User(string email, string passwordHash)
    {
        Id = Guid.CreateVersion7();
        Email = email;
        PasswordHash = passwordHash;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public DateOnly? BirthDate { get; private set; }
    public Chronotype? Chronotype { get; private set; }
    public TimeOnly WorkdayStart { get; private set; } = new(9, 0);
    public TimeOnly WorkdayEnd { get; private set; } = new(18, 0);
    public DateTime CreatedAt { get; private set; }

    public void UpdateProfile(DateOnly? birthDate, Chronotype? chronotype, TimeOnly workdayStart, TimeOnly workdayEnd)
    {
        if (workdayEnd <= workdayStart)
            throw new DomainRuleException("Конец дня должен быть позже начала.");

        BirthDate = birthDate;
        Chronotype = chronotype;
        WorkdayStart = workdayStart;
        WorkdayEnd = workdayEnd;
    }
}
