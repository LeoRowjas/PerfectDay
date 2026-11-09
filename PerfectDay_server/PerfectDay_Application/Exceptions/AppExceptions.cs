namespace PerfectDay_Application.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

public sealed class AppValidationException(string message) : Exception(message);

public sealed class InvalidCredentialsException() : Exception("Неверный email или пароль.");
