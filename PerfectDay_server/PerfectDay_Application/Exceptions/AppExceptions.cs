namespace PerfectDay_Application.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

public sealed class AppValidationException(string message) : Exception(message);

public sealed class InvalidCredentialsException() : Exception("Неверный email или пароль.");

/// <summary>Вход через внешний сервис не удался; текст показывается на странице входа.</summary>
public sealed class ExternalAuthException(string message) : Exception(message);

/// <summary>Календарь по ссылке не загрузился или не разобрался.</summary>
public sealed class CalendarFeedException(string message) : Exception(message);
