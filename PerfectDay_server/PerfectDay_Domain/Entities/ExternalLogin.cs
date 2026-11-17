using PerfectDay_Domain.Enums;

namespace PerfectDay_Domain.Entities;

/// <summary>Аккаунт во внешнем сервисе (Яндекс ID, VK ID, Google), через который пользователь входит.</summary>
public class ExternalLogin
{
    private ExternalLogin()
    {
    }

    public ExternalLogin(Guid userId, ExternalProvider provider, string subject)
    {
        UserId = userId;
        Provider = provider;
        Subject = subject;
        CreatedAt = DateTime.UtcNow;
    }

    public ExternalProvider Provider { get; private set; }

    /// <summary>Постоянный id пользователя у провайдера.</summary>
    public string Subject { get; private set; } = null!;

    public Guid UserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
