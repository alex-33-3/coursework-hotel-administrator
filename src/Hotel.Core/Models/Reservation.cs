using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hotel.Core.Models;

public enum ReservationStatus
{
    [Description("Забронировано")]
    Reserved,

    [Description("Заселено")]
    CheckedIn,

    [Description("Отменено")]
    Cancelled,

    [Description("Незаезд")]
    NoShow
}

/// <summary>
/// Бронь мест в номере на даты — корень агрегата. Держит места, пока в состоянии «Забронировано».
/// Методы изменения <c>internal</c>: их вызывают только сервисы, которые проверяют свободные места.
/// </summary>
public sealed class Reservation
{
    [JsonConstructor]
    private Reservation()
    {
    }

    internal Reservation(int number, ReservationDetails details, decimal pricePerPlace, DateTime createdAt, string createdBy)
    {
        Number = number;
        Apply(details);
        PricePerPlace = pricePerPlace;
        Status = ReservationStatus.Reserved;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
    }

    [JsonInclude, JsonRequired]
    public int Number { get; private set; }

    [JsonInclude, JsonRequired]
    public string ContactName { get; private set; } = string.Empty;

    [JsonInclude, JsonRequired]
    public string Phone { get; private set; } = string.Empty;

    [JsonInclude, JsonRequired]
    public string RoomNumber { get; private set; } = string.Empty;

    [JsonInclude, JsonRequired]
    public DateOnly Arrival { get; private set; }

    [JsonInclude, JsonRequired]
    public DateOnly Departure { get; private set; }

    [JsonInclude, JsonRequired]
    public int Places { get; private set; }

    /// <summary>Цена за место в сутки на момент бронирования: гость платит по ней при заселении.</summary>
    [JsonInclude, JsonRequired]
    public decimal PricePerPlace { get; private set; }

    [JsonInclude, JsonRequired]
    public ReservationStatus Status { get; private set; }

    [JsonInclude]
    public string? Note { get; private set; }

    [JsonInclude, JsonRequired]
    public DateTime CreatedAt { get; private set; }

    [JsonInclude, JsonRequired]
    public string CreatedBy { get; private set; } = string.Empty;

    [JsonIgnore]
    public int Nights => Departure.DayNumber - Arrival.DayNumber;

    [JsonIgnore]
    public decimal Total => Money.Round(Nights * Places * PricePerPlace);

    /// <summary>Держит ли бронь места: только «Забронировано» и дата приезда еще не прошла.</summary>
    public bool HoldsPlaces(DateOnly today) => Status == ReservationStatus.Reserved && Arrival >= today;

    /// <summary>Занимает ли бронь места в ночь <paramref name="night"/> (ночь с этой даты на следующую).</summary>
    public bool CoversNight(DateOnly night) => Arrival <= night && night < Departure;

    internal void Update(ReservationDetails details, decimal pricePerPlace)
    {
        Apply(details);
        PricePerPlace = pricePerPlace;
    }

    internal void SetStatus(ReservationStatus status) => Status = status;

    private void Apply(ReservationDetails details)
    {
        ContactName = details.ContactName;
        Phone = details.Phone;
        RoomNumber = details.RoomNumber;
        Arrival = details.Arrival;
        Departure = details.Departure;
        Places = details.Places;
        Note = string.IsNullOrWhiteSpace(details.Note) ? null : details.Note.Trim();
    }

    public override string ToString() =>
        $"Бронь № {Number}: {ContactName}, номер {RoomNumber}, {Arrival:dd.MM.yyyy}–{Departure:dd.MM.yyyy}";
}

/// <summary>Данные брони, которые вводит администратор.</summary>
public sealed record ReservationDetails
{
    public required string ContactName { get; init; }
    public required string Phone { get; init; }
    public required string RoomNumber { get; init; }
    public required DateOnly Arrival { get; init; }
    public required DateOnly Departure { get; init; }
    public required int Places { get; init; }
    public string? Note { get; init; }
}
