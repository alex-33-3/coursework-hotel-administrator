using System.ComponentModel;
using System.Globalization;
using Hotel.Core.Models;
using Hotel.Core.Services;

namespace Hotel.Presenters;

// Строки таблиц: готовые к показу значения. DataGridView берет заголовки из [DisplayName],
// служебные поля скрыты через [Browsable(false)].

/// <summary>Элемент выпадающего списка: значение и текст.</summary>
public sealed record Option<T>(T Value, string Text)
{
    public override string ToString() => Text;
}

/// <summary>Панель «Сегодня» на главной форме.</summary>
public sealed record TodaySummary(DateOnly Today, int Living, int FreePlaces, int Arrivals, int Departures, int Overdue);

public sealed record GuestGridRow(
    [property: Browsable(false)] int GuestId,
    [property: Browsable(false)] int? StayId,
    [property: Browsable(false)] bool IsActive,
    [property: Browsable(false)] bool IsOverdue,
    [property: DisplayName("ФИО")] string FullName,
    [property: DisplayName("Дата рождения")] string BirthDate,
    [property: DisplayName("Гражданство")] string Citizenship,
    [property: DisplayName("Паспорт")] string Passport,
    [property: DisplayName("Кем выдан")] string IssuedBy,
    [property: DisplayName("Дата выдачи")] string IssueDate,
    [property: DisplayName("Телефон")] string Phone,
    [property: DisplayName("Номер")] string Room,
    [property: DisplayName("Приезд")] string Arrival,
    [property: DisplayName("Отъезд")] string Departure,
    [property: DisplayName("Состояние")] string Status)
{
    public static GuestGridRow From(GuestRow row, DateOnly today)
    {
        var (guest, stay) = (row.Guest, row.Stay);
        var overdue = stay?.IsOverdue(today) ?? false;
        return new GuestGridRow(guest.Id, stay?.Id, stay?.IsActive ?? false, overdue, guest.FullName, Format.Date(guest.BirthDate),
            guest.Citizenship, guest.PassportNumber, guest.PassportIssuedBy, Format.Date(guest.PassportIssueDate), guest.Phone ?? string.Empty,
            stay?.RoomNumber ?? string.Empty, stay is null ? string.Empty : Format.Date(stay.Arrival),
            stay is null ? string.Empty : Format.Date(stay.Departure),
            stay is null ? "нет проживаний" : overdue ? "Не выехал в срок" : stay.Status.GetDescription());
    }
}

public sealed record DepartureRow(
    [property: Browsable(false)] int StayId,
    [property: Browsable(false)] bool IsOverdue,
    [property: DisplayName("Карточка №")] int Card,
    [property: DisplayName("ФИО")] string FullName,
    [property: DisplayName("Паспорт")] string Passport,
    [property: DisplayName("Номер")] string Room,
    [property: DisplayName("Приезд")] string Arrival,
    [property: DisplayName("Отъезд (план)")] string Departure,
    [property: DisplayName("Оплачено")] string Paid,
    [property: DisplayName("Отметка")] string Mark)
{
    public static DepartureRow From(GuestRow row, DateOnly today)
    {
        var stay = row.Stay!;
        var overdue = stay.IsOverdue(today);
        return new DepartureRow(stay.Id, overdue, stay.Id, row.Guest.FullName, row.Guest.PassportNumber, stay.RoomNumber,
            Format.Date(stay.Arrival), Format.Date(stay.Departure), Format.Money(stay.PaidNights * stay.PricePerPlace),
            overdue ? $"не выехал в срок ({today.DayNumber - stay.Departure.DayNumber} сут.)" : "отъезд сегодня");
    }
}

public sealed record ReservationRow(
    [property: Browsable(false)] int Number,
    [property: Browsable(false)] bool CanChange,
    [property: DisplayName("Бронь №")] int Shown,
    [property: DisplayName("Контактное лицо")] string Contact,
    [property: DisplayName("Телефон")] string Phone,
    [property: DisplayName("Номер")] string Room,
    [property: DisplayName("Приезд")] string Arrival,
    [property: DisplayName("Отъезд")] string Departure,
    [property: DisplayName("Суток")] int Nights,
    [property: DisplayName("Мест")] int Places,
    [property: DisplayName("Сумма")] string Total,
    [property: DisplayName("Состояние")] string Status,
    [property: DisplayName("Примечание")] string Note)
{
    public static ReservationRow From(Reservation r) =>
        new(r.Number, r.Status == ReservationStatus.Reserved, r.Number, r.ContactName, r.Phone, r.RoomNumber,
            Format.Date(r.Arrival), Format.Date(r.Departure), r.Nights, r.Places, Format.Money(r.Total), r.Status.GetDescription(),
            r.Note ?? string.Empty);
}

public sealed record RoomOccupancyRow(
    [property: DisplayName("Номер")] string Number,
    [property: DisplayName("Этаж")] int Floor,
    [property: DisplayName("Класс")] string Class,
    [property: DisplayName("Мест")] int Places,
    [property: DisplayName("Проживает")] int Living,
    [property: DisplayName("Забронировано")] int Reserved,
    [property: DisplayName("Свободно")] int Free,
    [property: DisplayName("Цена за место")] string Price,
    [property: DisplayName("Описание")] string Description);

public sealed record AvailabilityRow(
    [property: Browsable(false)] string RoomNumber,
    [property: DisplayName("Номер")] string Number,
    [property: DisplayName("Этаж")] int Floor,
    [property: DisplayName("Класс")] string Class,
    [property: DisplayName("Мест в номере")] int Places,
    [property: DisplayName("Свободно")] int Free,
    [property: DisplayName("Цена за место в сутки")] string Price,
    [property: DisplayName("Сумма за срок")] string Total,
    [property: DisplayName("Описание")] string Description)
{
    public static AvailabilityRow From(RoomAvailability a) =>
        new(a.Room.Number, a.Room.Number, a.Room.Floor, a.Room.Class.GetDescription(), a.Room.Places, a.FreePlaces,
            Format.Money(a.PricePerPlace), Format.Money(a.Total), a.Room.Description ?? string.Empty);
}

public sealed record CheckInGuestRow(
    [property: Browsable(false)] int GuestId,
    [property: DisplayName("ФИО")] string FullName,
    [property: DisplayName("Дата рождения")] string BirthDate,
    [property: DisplayName("Гражданство")] string Citizenship,
    [property: DisplayName("Паспорт")] string Passport);

public sealed record ReceiptRow(
    [property: Browsable(false)] int Number,
    [property: DisplayName("Квитанция №")] int Shown,
    [property: DisplayName("Дата")] string IssuedAt,
    [property: DisplayName("Вид")] string Kind,
    [property: DisplayName("Гость")] string Guest,
    [property: DisplayName("Номер")] string Room,
    [property: DisplayName("Сумма")] string Total,
    [property: DisplayName("Оформил")] string IssuedBy);

public sealed record RoomRow(
    [property: DisplayName("Номер")] string Number,
    [property: DisplayName("Этаж")] int Floor,
    [property: DisplayName("Класс")] string Class,
    [property: DisplayName("Мест")] int Places,
    [property: DisplayName("Описание")] string Description);

public sealed record TariffRow(
    [property: Browsable(false)] RoomClass RoomClass,
    [property: Browsable(false)] decimal PriceValue,
    [property: DisplayName("Класс номера")] string Class,
    [property: DisplayName("Цена за место в сутки, BYN")] string Price);

public sealed record UserRow(
    [property: DisplayName("Логин")] string Login,
    [property: DisplayName("ФИО")] string FullName,
    [property: DisplayName("Роль")] string Role);

/// <summary>Форматирование дат и сумм по ru-RU для показа.</summary>
public static class Format
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    public static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", Russian);

    public static string DateTime(DateTime value) => value.ToString("dd.MM.yyyy HH:mm", Russian);

    public static string Money(decimal value) => value.ToString("N2", Russian) + " " + Core.Models.Money.Currency;

    public static string Period(DateOnly from, DateOnly to) => $"{Date(from)} – {Date(to)}";
}
