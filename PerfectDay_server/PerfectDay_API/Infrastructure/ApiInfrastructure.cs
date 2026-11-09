using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PerfectDay_Application.Exceptions;
using PerfectDay_Domain.Exceptions;

namespace PerfectDay_API.Infrastructure;

public static class AuthCookie
{
    public const string Name = "perfectday_session";
    public const string RateLimitPolicy = "auth";

    public static void Append(HttpResponse response, HttpRequest request, string token, int lifetimeDays) =>
        response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromDays(lifetimeDays)
        });

    public static void Delete(HttpResponse response) => response.Cookies.Delete(Name, new CookieOptions { Path = "/" });
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? throw new InvalidOperationException("В токене нет идентификатора пользователя."));
}

/// <summary>Время в API всегда передаётся как HH:mm.</summary>
public sealed class TimeOnlyMinutesConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (TimeOnly.TryParseExact(reader.GetString(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            return time;

        throw new JsonException("Время должно быть в формате ЧЧ:ММ.");
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}

public sealed class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        int? status = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException or DomainRuleException => StatusCodes.Status409Conflict,
            AppValidationException => StatusCodes.Status400BadRequest,
            InvalidCredentialsException => StatusCodes.Status401Unauthorized,
            _ => null
        };
        if (status is null) return false;

        context.Response.StatusCode = status.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = exception.Message }
        });
    }
}
