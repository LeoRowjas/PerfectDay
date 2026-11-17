using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Entities;

namespace PerfectDay_Application.Services;

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ITokenService tokens,
    IUnitOfWork unitOfWork)
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        if (await users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("Пользователь с таким email уже зарегистрирован.");

        var user = new User(email, passwordHasher.Hash(request.Password));
        users.Add(user);
        await unitOfWork.SaveChangesAsync(ct);

        return new AuthResult(user.ToDto(), tokens.CreateToken(user));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(NormalizeEmail(request.Email), ct);
        // У аккаунта, созданного через Яндекс ID, VK ID или Google, пароля нет.
        if (user?.PasswordHash is not { } hash || !passwordHasher.Verify(hash, request.Password))
            throw new InvalidCredentialsException();

        return new AuthResult(user.ToDto(), tokens.CreateToken(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
