using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Entities;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Application.Services;

/// <summary>Вход через Яндекс ID, VK ID и Google: находит пользователя по аккаунту провайдера или создаёт нового.</summary>
public sealed class ExternalAuthService(
    IUserRepository users,
    ITokenService tokens,
    IEnumerable<IExternalAuthProvider> providers,
    IUnitOfWork unitOfWork)
{
    public IReadOnlyList<ExternalProviderDto> ListProviders() =>
        Enum.GetValues<ExternalProvider>()
            .Select(p => new ExternalProviderDto(p, providers.Any(x => x.Provider == p && x.IsConfigured)))
            .ToList();

    public IExternalAuthProvider GetProvider(ExternalProvider provider) =>
        providers.FirstOrDefault(p => p.Provider == provider && p.IsConfigured)
        ?? throw new ExternalAuthException($"Вход через {Name(provider)} не настроен на сервере.");

    public async Task<AuthResult> SignInAsync(ExternalIdentity identity, CancellationToken ct)
    {
        var user = await users.GetByExternalLoginAsync(identity.Provider, identity.Subject, ct);
        if (user is null)
        {
            var email = identity.Email?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
                throw new ExternalAuthException($"{Name(identity.Provider)} не передал email. Разрешите доступ к почте или войдите другим способом.");

            user = await users.GetByEmailAsync(email, ct);
            if (user is null)
            {
                user = new User(email, passwordHash: null);
                users.Add(user);
            }
            // Чужой аккаунт можно было бы присвоить, указав его email у провайдера, поэтому без подтверждения не связываем.
            else if (!identity.IsEmailVerified)
            {
                throw new ExternalAuthException(
                    $"Аккаунт с почтой {email} уже есть. Войдите по паролю: {Name(identity.Provider)} не подтвердил, что почта ваша.");
            }

            user.LinkExternalLogin(identity.Provider, identity.Subject);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return new AuthResult(user.ToDto(), tokens.CreateToken(user));
    }

    public static string Name(ExternalProvider provider) => provider switch
    {
        ExternalProvider.Yandex => "Яндекс ID",
        ExternalProvider.Vk => "VK ID",
        _ => "Google"
    };
}
