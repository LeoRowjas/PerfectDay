namespace PerfectDay_Domain.Exceptions;

/// <summary>Нарушено бизнес-правило. Сообщение показывается пользователю.</summary>
public sealed class DomainRuleException(string message) : Exception(message);
