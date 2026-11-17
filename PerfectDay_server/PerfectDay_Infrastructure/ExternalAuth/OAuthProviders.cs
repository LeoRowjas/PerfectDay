using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Exceptions;
using PerfectDay_Application.Services;
using PerfectDay_Domain.Enums;

namespace PerfectDay_Infrastructure.ExternalAuth;

public sealed class ProviderCredentials
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
}

public sealed class ExternalAuthOptions
{
    public const string SectionName = "ExternalAuth";

    /// <summary>Адрес, с которого пользователь открывает приложение: на него провайдер возвращает после входа.</summary>
    public string PublicOrigin { get; init; } = "http://localhost:5173";

    public ProviderCredentials Yandex { get; init; } = new();
    public ProviderCredentials Vk { get; init; } = new();
    public ProviderCredentials Google { get; init; } = new();
}

/// <summary>Общая часть OAuth 2.0 с PKCE: ошибки сети и провайдера превращаются в понятный текст.</summary>
internal abstract class OAuthProvider(IHttpClientFactory httpClients) : IExternalAuthProvider
{
    public const string ClientName = "oauth";

    public abstract ExternalProvider Provider { get; }
    protected abstract ProviderCredentials Credentials { get; }
    public virtual bool IsConfigured => Credentials.ClientId.Length > 0 && Credentials.ClientSecret.Length > 0;

    public abstract string BuildAuthorizeUrl(string redirectUri, string state, string codeChallenge);

    public abstract Task<ExternalIdentity> ExchangeAsync(string redirectUri, string code, string codeVerifier,
        IReadOnlyDictionary<string, string> callback, CancellationToken ct);

    protected static string Query(string baseUrl, IEnumerable<KeyValuePair<string, string>> parameters) =>
        baseUrl + "?" + string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

    protected async Task<JsonElement> PostFormAsync(string url, Dictionary<string, string> form, CancellationToken ct) =>
        await SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) }, ct);

    protected async Task<JsonElement> GetJsonAsync(string url, AuthenticationHeaderValue authorization, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = authorization;
        return await SendAsync(request, ct);
    }

    private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var name = ExternalAuthService.Name(Provider);
        try
        {
            using var response = await httpClients.CreateClient(ClientName).SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new ExternalAuthException($"{name} не подтвердил вход. Попробуйте ещё раз.");

            var root = JsonDocument.Parse(json).RootElement.Clone();
            if (root.TryGetProperty("error", out _))
                throw new ExternalAuthException($"{name} не подтвердил вход. Попробуйте ещё раз.");
            return root;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new ExternalAuthException($"Нет связи с {name}. Попробуйте позже.");
        }
    }

    protected static string? String(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
}

/// <summary>Яндекс ID: oauth.yandex.ru, данные пользователя из login.yandex.ru/info.</summary>
internal sealed class YandexAuthProvider(IHttpClientFactory httpClients, IOptions<ExternalAuthOptions> options) : OAuthProvider(httpClients)
{
    public override ExternalProvider Provider => ExternalProvider.Yandex;
    protected override ProviderCredentials Credentials => options.Value.Yandex;

    public override string BuildAuthorizeUrl(string redirectUri, string state, string codeChallenge) =>
        Query("https://oauth.yandex.ru/authorize", new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = Credentials.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "login:email",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        });

    public override async Task<ExternalIdentity> ExchangeAsync(string redirectUri, string code, string codeVerifier,
        IReadOnlyDictionary<string, string> callback, CancellationToken ct)
    {
        var token = await PostFormAsync("https://oauth.yandex.ru/token", new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = Credentials.ClientId,
            ["client_secret"] = Credentials.ClientSecret,
            ["code_verifier"] = codeVerifier
        }, ct);

        var info = await GetJsonAsync("https://login.yandex.ru/info?format=json",
            new AuthenticationHeaderValue("OAuth", String(token, "access_token")), ct);

        // Яндекс отдаёт только подтверждённые адреса.
        return new ExternalIdentity(Provider, String(info, "id") ?? throw Missing(), String(info, "default_email"), true);
    }

    private static ExternalAuthException Missing() => new("Яндекс ID не передал данные аккаунта.");
}

/// <summary>VK ID (id.vk.com): публичный клиент с PKCE, секрет не нужен; при возврате VK присылает device_id.</summary>
internal sealed class VkAuthProvider(IHttpClientFactory httpClients, IOptions<ExternalAuthOptions> options) : OAuthProvider(httpClients)
{
    public override ExternalProvider Provider => ExternalProvider.Vk;
    protected override ProviderCredentials Credentials => options.Value.Vk;
    public override bool IsConfigured => Credentials.ClientId.Length > 0;

    public override string BuildAuthorizeUrl(string redirectUri, string state, string codeChallenge) =>
        Query("https://id.vk.com/authorize", new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = Credentials.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "email",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        });

    public override async Task<ExternalIdentity> ExchangeAsync(string redirectUri, string code, string codeVerifier,
        IReadOnlyDictionary<string, string> callback, CancellationToken ct)
    {
        var token = await PostFormAsync("https://id.vk.com/oauth2/auth", new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["client_id"] = Credentials.ClientId,
            ["device_id"] = callback.GetValueOrDefault("device_id", ""),
            ["redirect_uri"] = redirectUri,
            ["state"] = callback.GetValueOrDefault("state", "")
        }, ct);

        var info = await PostFormAsync("https://id.vk.com/oauth2/user_info", new Dictionary<string, string>
        {
            ["client_id"] = Credentials.ClientId,
            ["access_token"] = String(token, "access_token") ?? ""
        }, ct);

        var user = info.TryGetProperty("user", out var value) ? value : info;
        var id = String(user, "user_id") ?? String(token, "user_id") ?? throw new ExternalAuthException("VK ID не передал данные аккаунта.");

        // VK не сообщает, подтверждена ли почта, поэтому существующий аккаунт по ней не связывается.
        return new ExternalIdentity(Provider, id, String(user, "email"), false);
    }
}

/// <summary>Google: OpenID Connect, email_verified приходит в userinfo.</summary>
internal sealed class GoogleAuthProvider(IHttpClientFactory httpClients, IOptions<ExternalAuthOptions> options) : OAuthProvider(httpClients)
{
    public override ExternalProvider Provider => ExternalProvider.Google;
    protected override ProviderCredentials Credentials => options.Value.Google;

    public override string BuildAuthorizeUrl(string redirectUri, string state, string codeChallenge) =>
        Query("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = Credentials.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid email",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account"
        });

    public override async Task<ExternalIdentity> ExchangeAsync(string redirectUri, string code, string codeVerifier,
        IReadOnlyDictionary<string, string> callback, CancellationToken ct)
    {
        var token = await PostFormAsync("https://oauth2.googleapis.com/token", new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = Credentials.ClientId,
            ["client_secret"] = Credentials.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        }, ct);

        var info = await GetJsonAsync("https://openidconnect.googleapis.com/v1/userinfo",
            new AuthenticationHeaderValue("Bearer", String(token, "access_token")), ct);

        var verified = info.TryGetProperty("email_verified", out var flag) && flag.ValueKind == JsonValueKind.True;
        return new ExternalIdentity(Provider, String(info, "sub") ?? throw new ExternalAuthException("Google не передал данные аккаунта."),
            String(info, "email"), verified);
    }
}
