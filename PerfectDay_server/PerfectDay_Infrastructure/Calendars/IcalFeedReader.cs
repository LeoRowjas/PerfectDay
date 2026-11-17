using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Ical.Net;
using Ical.Net.DataTypes;
using PerfectDay_Application.Abstractions;
using PerfectDay_Application.Exceptions;
using IcalEvent = Ical.Net.CalendarComponents.CalendarEvent;

namespace PerfectDay_Infrastructure.Calendars;

public sealed class CalendarOptions
{
    public const string SectionName = "Calendars";

    /// <summary>Разрешить ссылки на localhost и внутреннюю сеть. Только для разработки: иначе сервер можно заставить ходить во внутренние сервисы.</summary>
    public bool AllowPrivateNetworks { get; init; }
}

/// <summary>Загружает календарь по ссылке iCal (Google, Яндекс, iCloud отдают такие ссылки) и разворачивает повторы.</summary>
internal sealed class IcalFeedReader(IHttpClientFactory httpClients) : ICalendarFeedReader
{
    public const string ClientName = "calendars";
    private const int MaxBytes = 5 * 1024 * 1024;
    private const int MaxEvents = 2000;

    public async Task<CalendarFeed> ReadAsync(string url, string timeZone, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var zone = FindTimeZone(timeZone);
        var text = await DownloadAsync(ParseUrl(url), ct);

        Calendar calendar;
        try
        {
            calendar = Calendar.Load(text) ?? throw new CalendarFeedException("Календарь пустой.");
        }
        catch (Exception e) when (e is not CalendarFeedException)
        {
            throw new CalendarFeedException("Не удалось разобрать календарь. Проверьте, что ссылка ведёт на файл iCal (.ics).");
        }

        var events = new List<FeedEvent>();
        var start = new CalDateTime(from);
        var end = new CalDateTime(to.AddDays(1));
        try
        {
            foreach (var occurrence in calendar.GetOccurrences<IcalEvent>(start).TakeWhileBefore(end))
            {
                if (occurrence.Source is not IcalEvent item || IsSkipped(item)) continue;
                events.AddRange(Split(item, occurrence.Period, zone, from, to));
                if (events.Count >= MaxEvents) break;
            }
        }
        catch (Exception)
        {
            throw new CalendarFeedException("В календаре есть повторы, которые не удалось разобрать.");
        }

        return new CalendarFeed(calendar.Properties["X-WR-CALNAME"]?.Value?.ToString(), events);
    }

    // Отменённые и «свободные» события время не занимают.
    private static bool IsSkipped(IcalEvent item) =>
        string.Equals(item.Status, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(item.Transparency, "TRANSPARENT", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<FeedEvent> Split(IcalEvent item, Period period, TimeZoneInfo zone, DateOnly from, DateOnly to)
    {
        var uid = item.Uid ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{item.Summary}|{period.StartTime}")))[..16];
        var title = string.IsNullOrWhiteSpace(item.Summary) ? "Без названия" : item.Summary.Trim();
        var startTime = period.StartTime;
        var endTime = period.EffectiveEndTime ?? startTime;

        if (!startTime.HasTime)
        {
            var last = endTime.Date > startTime.Date ? endTime.Date.AddDays(-1) : startTime.Date;
            for (var day = startTime.Date; day <= last; day = day.AddDays(1))
                if (day >= from && day <= to) yield return new FeedEvent(uid, title, day, TimeOnly.MinValue, TimeOnly.MaxValue, true);
            yield break;
        }

        var localStart = ToLocal(startTime, zone);
        var localEnd = ToLocal(endTime, zone);
        if (localEnd <= localStart) localEnd = localStart.AddMinutes(1);

        for (var day = DateOnly.FromDateTime(localStart); day.ToDateTime(TimeOnly.MinValue) < localEnd; day = day.AddDays(1))
        {
            var dayStart = day.ToDateTime(TimeOnly.MinValue);
            var pieceStart = localStart > dayStart ? localStart : dayStart;
            var pieceEnd = localEnd < dayStart.AddDays(1) ? localEnd : dayStart.AddDays(1);
            if (day < from || day > to) continue;

            // Событие до полуночи: конец дня в TimeOnly — 23:59.
            var endOfPiece = pieceEnd >= dayStart.AddDays(1) ? new TimeOnly(23, 59) : TimeOnly.FromDateTime(pieceEnd);
            var isWholeDay = pieceStart == dayStart && pieceEnd >= dayStart.AddDays(1);
            yield return new FeedEvent(uid, title, day, TimeOnly.FromDateTime(pieceStart), endOfPiece, isWholeDay);
        }
    }

    private static DateTime ToLocal(CalDateTime time, TimeZoneInfo zone) =>
        time.IsFloating ? time.Value : TimeZoneInfo.ConvertTimeFromUtc(time.AsUtc, zone);

    private static TimeZoneInfo FindTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new CalendarFeedException("Неизвестный часовой пояс. Обновите страницу и попробуйте ещё раз.");
        }
    }

    private static Uri ParseUrl(string url)
    {
        var normalized = url.Trim();
        if (normalized.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) normalized = "https://" + normalized[9..];

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new CalendarFeedException("Нужна ссылка, которая начинается с https:// или webcal://.");
        return uri;
    }

    private async Task<string> DownloadAsync(Uri uri, CancellationToken ct)
    {
        var client = httpClients.CreateClient(ClientName);
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                throw new CalendarFeedException("Календарь по ссылке не найден. Возможно, ссылку сбросили: скопируйте её заново.");
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new CalendarFeedException("Календарь закрыт. Нужна секретная ссылка или публичный доступ к календарю.");
            if (!response.IsSuccessStatusCode)
                throw new CalendarFeedException($"Сервис календаря ответил ошибкой {(int)response.StatusCode}. Попробуйте позже.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxBytes) throw new CalendarFeedException("Календарь слишком большой (больше 5 МБ).");
            }

            var text = Encoding.UTF8.GetString(buffer.ToArray());
            if (!text.Contains("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase))
                throw new CalendarFeedException("По ссылке не календарь. Скопируйте ссылку в формате iCal (.ics).");
            return text;
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new CalendarFeedException("Календарь не ответил за 15 секунд. Попробуйте позже.");
        }
        catch (HttpRequestException)
        {
            throw new CalendarFeedException("Не удалось открыть ссылку. Проверьте её и доступ к календарю.");
        }
    }

    /// <summary>Подключается только к публичным адресам: адрес проверяется после DNS, поэтому подмена имени не помогает.</summary>
    public static SocketsHttpHandler CreateHandler(bool allowPrivateNetworks) => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 3,
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var address = addresses.FirstOrDefault(a => allowPrivateNetworks || IsPublic(a))
                          ?? throw new HttpRequestException("Адрес календаря во внутренней сети.");

            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    private static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 10 || b[0] == 0 || b[0] == 127 ||
                     (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
                     (b[0] == 192 && b[1] == 168) ||
                     (b[0] == 169 && b[1] == 254) ||
                     (b[0] == 100 && b[1] >= 64 && b[1] <= 127) ||
                     b[0] >= 224);
        }

        return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast ||
                 (address.GetAddressBytes()[0] & 0xFE) == 0xFC);
    }
}
