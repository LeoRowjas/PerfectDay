using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PerfectDay_Application.Abstractions;
using PerfectDay_Domain.Entities;

namespace PerfectDay_Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinKeyBytes = 32;

    public string Issuer { get; init; } = "PerfectDay";
    public string Audience { get; init; } = "PerfectDay";
    public string Key { get; init; } = "";
    public int LifetimeDays { get; init; } = 7;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public string CreateToken(User user)
    {
        var jwt = options.Value;
        var token = new JwtSecurityToken(
            jwt.Issuer,
            jwt.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())],
            expires: DateTime.UtcNow.AddDays(jwt.LifetimeDays),
            signingCredentials: new SigningCredentials(jwt.SigningKey(), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object HashContext = new();

    public string Hash(string password) => _hasher.HashPassword(HashContext, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(HashContext, hash, password) != PasswordVerificationResult.Failed;
}
