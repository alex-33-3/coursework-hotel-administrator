using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hotel.Core.Models;

public enum StayStatus
{
    [Description("Проживает")]
    Active,

    [Description("Выехал")]
    CheckedOut
}

/// <summary>
/// Проживание гостя в номере — корень агрегата. Один гость занимает одно место.
/// Методы изменения <c>internal</c>: их вызывает только <c>StayService</c>.
/// </summary>
public sealed class Stay
{
    [JsonConstructor]
    private Stay()
    {
    }

    internal Stay(int id, int guestId, string roomNumber, int? reservationNumber, DateOnly arrival, DateOnly departure,
        decimal pricePerPlace)
    {
        Id = id;
        GuestId = guestId;
        RoomNumber = roomNumber;
        ReservationNumber = reservationNumber;
        Arrival = arrival;
        Departure = departure;
        PricePerPlace = pricePerPlace;
        Status = StayStatus.Active;
    }

    /// <summary>Номер регистрационной карточки.</summary>
    [JsonInclude, JsonRequired]
    public int Id { get; private set; }

    [JsonInclude, JsonRequired]
    public int GuestId { get; private set; }

    [JsonInclude, JsonRequired]
    public string RoomNumber { get; private set; } = string.Empty;

    [JsonInclude]
    public int? ReservationNumber { get; private set; }

    [JsonInclude, JsonRequired]
    public DateOnly Arrival { get; private set; }

    /// <summary>Плановая дата отъезда (оплачено по нее). Меняется при продлении.</summary>
    [JsonInclude, JsonRequired]
    public DateOnly Departure { get; private set; }

    [JsonInclude]
    public DateTime? CheckedOutAt { get; private set; }

    /// <summary>Цена за место в сутки, зафиксированная при бронировании или поселении.</summary>
    [JsonInclude, JsonRequired]
    public decimal PricePerPlace { get; private set; }

    [JsonInclude, JsonRequired]
    public StayStatus Status { get; private set; }

    /// <summary>Оплаченные сутки — от приезда до плановой даты отъезда.</summary>
    [JsonIgnore]
    public int PaidNights => Departure.DayNumber - Arrival.DayNumber;

    [JsonIgnore]
    public bool IsActive => Status == StayStatus.Active;

    /// <summary>Не выехал в срок: проживает, а плановая дата отъезда уже прошла.</summary>
    public bool IsOverdue(DateOnly today) => IsActive && Departure < today;

    /// <summary>
    /// Занимает ли проживание место в ночь <paramref name="night"/>. Гость, не выехавший в срок,
    /// занимает место и сегодня, пока его не выписали.
    /// </summary>
    public bool CoversNight(DateOnly night, DateOnly today)
    {
        if (!IsActive)
            return false;

        var end = Departure < today ? today.AddDays(1) : Departure;
        return Arrival <= night && night < end;
    }

    internal void Extend(DateOnly departure) => Departure = departure;

    internal void CheckOut(DateTime at)
    {
        CheckedOutAt = at;
        Status = StayStatus.CheckedOut;
    }

    public override string ToString() => $"Карточка № {Id}: номер {RoomNumber}, {Arrival:dd.MM.yyyy}–{Departure:dd.MM.yyyy}";
}
