using System.Text.Json.Serialization;

namespace Hotel.Core.Models;

/// <summary>
/// Гость гостиницы и его паспортные данные. Повторный гость находится по номеру паспорта.
/// </summary>
public sealed record Guest
{
    public const string DefaultCitizenship = "Республика Беларусь";

    public required int Id { get; init; }
    public required string LastName { get; init; }
    public required string FirstName { get; init; }
    public string? MiddleName { get; init; }
    public required DateOnly BirthDate { get; init; }
    public required string Citizenship { get; init; }

    /// <summary>Серия и номер паспорта без пробелов, заглавными буквами (для РБ — MP1234567).</summary>
    public required string PassportNumber { get; init; }

    public required string PassportIssuedBy { get; init; }
    public required DateOnly PassportIssueDate { get; init; }
    public string? Phone { get; init; }

    [JsonIgnore]
    public string FullName => PersonName.Full(LastName, FirstName, MiddleName);

    [JsonIgnore]
    public string ShortName => PersonName.WithInitials(LastName, FirstName, MiddleName);

    [JsonIgnore]
    public bool IsBelarusCitizen => string.Equals(Citizenship.Trim(), DefaultCitizenship, StringComparison.OrdinalIgnoreCase);

    public override string ToString() => $"{FullName}, паспорт {PassportNumber}";
}

public static class PersonName
{
    public static string Full(string lastName, string firstName, string? middleName) =>
        string.IsNullOrWhiteSpace(middleName)
            ? $"{lastName} {firstName}"
            : $"{lastName} {firstName} {middleName}";

    public static string WithInitials(string lastName, string firstName, string? middleName) =>
        string.IsNullOrWhiteSpace(middleName)
            ? $"{lastName} {firstName[..1]}."
            : $"{lastName} {firstName[..1]}. {middleName[..1]}.";
}
