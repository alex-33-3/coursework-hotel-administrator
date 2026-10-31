using System.ComponentModel;
using System.Globalization;
using Hotel.Core.Models;
using Hotel.Core.Services;

namespace Hotel.Core.Search;

/// <summary>Признак, по которому ищется гость.</summary>
public enum GuestField
{
    [Description("Все поля")]
    Any,

    [Description("Фамилия")]
    LastName,

    [Description("Имя")]
    FirstName,

    [Description("Отчество")]
    MiddleName,

    [Description("Дата рождения")]
    BirthDate,

    [Description("Гражданство")]
    Citizenship,

    [Description("Паспорт")]
    Passport,

    [Description("Кем выдан паспорт")]
    PassportIssuedBy,

    [Description("Телефон")]
    Phone,

    [Description("Номер комнаты")]
    Room,

    [Description("Класс номера")]
    RoomClass,

    [Description("Дата приезда")]
    Arrival,

    [Description("Дата отъезда")]
    Departure,

    [Description("Состояние")]
    Status
}

/// <summary>
/// Поиск гостя по произвольному признаку: значение поля содержит введенный текст без учета регистра.
/// Даты сравниваются в виде «дд.мм.гггг», поэтому «02.11» найдет все 2 ноября. Номер комнаты — точное совпадение.
/// </summary>
public static class GuestSearch
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static IReadOnlyList<GuestRow> Search(IEnumerable<GuestRow> rows, GuestField field, string? text)
    {
        var term = text?.Trim();
        if (string.IsNullOrEmpty(term))
            return rows.ToList();

        return rows.Where(r => Matches(r, field, term)).ToList();
    }

    public static bool Matches(GuestRow row, GuestField field, string term)
    {
        if (field == GuestField.Any)
            return Enum.GetValues<GuestField>().Where(f => f != GuestField.Any).Any(f => Matches(row, f, term));

        if (field == GuestField.Room)
            return row.Stay is not null && string.Equals(row.Stay.RoomNumber, term, StringComparison.OrdinalIgnoreCase);

        var value = ValueOf(row, field);
        if (string.IsNullOrEmpty(value))
            return false;

        if (field is GuestField.Passport or GuestField.Phone)
            term = new string(term.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '(' && c != ')').ToArray());

        return Russian.CompareInfo.IndexOf(value, term, CompareOptions.IgnoreCase) >= 0;
    }

    public static string? ValueOf(GuestRow row, GuestField field)
    {
        var guest = row.Guest;
        var stay = row.Stay;
        return field switch
        {
            GuestField.LastName => guest.LastName,
            GuestField.FirstName => guest.FirstName,
            GuestField.MiddleName => guest.MiddleName,
            GuestField.BirthDate => guest.BirthDate.ToString("dd.MM.yyyy", Russian),
            GuestField.Citizenship => guest.Citizenship,
            GuestField.Passport => guest.PassportNumber,
            GuestField.PassportIssuedBy => guest.PassportIssuedBy,
            GuestField.Phone => guest.Phone,
            GuestField.Room => stay?.RoomNumber,
            GuestField.RoomClass => row.Room?.Class.GetDescription(),
            GuestField.Arrival => stay?.Arrival.ToString("dd.MM.yyyy", Russian),
            GuestField.Departure => stay?.Departure.ToString("dd.MM.yyyy", Russian),
            GuestField.Status => stay?.Status.GetDescription(),
            _ => null
        };
    }
}
