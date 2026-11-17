using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PerfectDay_API.Infrastructure;
using PerfectDay_Application.Contracts;
using PerfectDay_Application.Exceptions;
using PerfectDay_Application.Services;
using PerfectDay_Domain.Enums;
using PerfectDay_Infrastructure.ExternalAuth;
using PerfectDay_Infrastructure.Security;

namespace PerfectDay_API.Controllers;

/// <summary>
/// Вход через Яндекс ID, VK ID и Google по OAuth 2.0 с PKCE. Браузер уходит на страницу провайдера и возвращается
/// на callback; state и code_verifier на это время лежат в зашифрованной cookie.
/// </summary>
[ApiController]
[Route("api/auth/external")]
[EnableRateLimiting(AuthCookie.RateLimitPolicy)]
public sealed class ExternalAuthController(
    ExternalAuthService externalAuth,
    IDataProtectionProvider dataProtection,
    IOptions<ExternalAuthOptions> options,
    IOptions<JwtOptions> jwt,
    ILogger<ExternalAuthController> logger) : ControllerBase
{
    private const string StateCookie = "perfectday_oauth";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private sealed record PendingLogin(ExternalProvider Provider, string State, string Verifier);

    [HttpGet("providers")]
    public IReadOnlyList<ExternalProviderDto> Providers() => externalAuth.ListProviders();

    [HttpGet("{provider}")]
    public IActionResult Start(string provider)
    {
        try
        {
            var kind = Parse(provider);
            var client = externalAuth.GetProvider(kind);
            var pending = new PendingLogin(kind, RandomToken(), RandomToken());

            // Lax, а не Strict: cookie должна прийти, когда провайдер вернёт пользователя со своего сайта.
            Response.Cookies.Append(StateCookie, Protector().Protect(JsonSerializer.Serialize(pending), StateLifetime), new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = "/api/auth/external",
                MaxAge = StateLifetime
            });

            var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(pending.Verifier)));
            return Redirect(client.BuildAuthorizeUrl(RedirectUri(kind), pending.State, challenge));
        }
        catch (ExternalAuthException e)
        {
            return RedirectToLogin(e.Message);
        }
    }

    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(string provider, CancellationToken ct)
    {
        var cookie = Request.Cookies[StateCookie];
        Response.Cookies.Delete(StateCookie, new CookieOptions { Path = "/api/auth/external" });

        try
        {
            var kind = Parse(provider);
            var query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());
            if (query.ContainsKey("error")) throw new ExternalAuthException("Вход отменён.");

            var pending = Unprotect(cookie);
            if (pending is null || pending.Provider != kind || !query.TryGetValue("state", out var state) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state), Encoding.UTF8.GetBytes(pending.State)))
                throw new ExternalAuthException("Время на вход истекло. Попробуйте ещё раз.");

            if (!query.TryGetValue("code", out var code) || code.Length == 0)
                throw new ExternalAuthException("Вход отменён.");

            var identity = await externalAuth.GetProvider(kind).ExchangeAsync(RedirectUri(kind), code, pending.Verifier, query, ct);
            var result = await externalAuth.SignInAsync(identity, ct);
            AuthCookie.Append(Response, Request, result.Token, jwt.Value.LifetimeDays);
            return Redirect(options.Value.PublicOrigin.TrimEnd('/') + "/");
        }
        catch (ExternalAuthException e)
        {
            logger.LogInformation("Вход через {Provider} не удался: {Message}", provider, e.Message);
            return RedirectToLogin(e.Message);
        }
        catch (ConflictException e)
        {
            return RedirectToLogin(e.Message);
        }
    }

    private PendingLogin? Unprotect(string? cookie)
    {
        if (string.IsNullOrEmpty(cookie)) return null;
        try
        {
            return JsonSerializer.Deserialize<PendingLogin>(Protector().Unprotect(cookie));
        }
        catch (Exception e) when (e is CryptographicException or JsonException)
        {
            return null;
        }
    }

    private ITimeLimitedDataProtector Protector() =>
        dataProtection.CreateProtector("PerfectDay.ExternalAuth").ToTimeLimitedDataProtector();

    private string RedirectUri(ExternalProvider provider) =>
        $"{options.Value.PublicOrigin.TrimEnd('/')}/api/auth/external/{provider.ToString().ToLowerInvariant()}/callback";

    private RedirectResult RedirectToLogin(string message) =>
        Redirect($"{options.Value.PublicOrigin.TrimEnd('/')}/login?error={Uri.EscapeDataString(message)}");

    private static ExternalProvider Parse(string provider) =>
        Enum.TryParse<ExternalProvider>(provider, ignoreCase: true, out var kind) && Enum.IsDefined(kind)
            ? kind
            : throw new ExternalAuthException("Такого способа входа нет.");

    private static string RandomToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
