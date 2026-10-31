using Hotel.Core.Models;
using Hotel.Core.Services;
using Hotel.Core.Storage;

namespace Hotel.Core.Occupancy;

/// <summary>
/// Что не учитывать при подсчете занятости: изменяемую бронь или продлеваемое проживание,
/// иначе они мешали бы сами себе.
/// </summary>
public sealed record OccupancyExclusion(int? ReservationNumber = null, int? StayId = null)
{
    public static OccupancyExclusion None { get; } = new();
}

/// <summary>Занятость номера на сегодня: проживают, забронировано на сегодня, свободно.</summary>
public sealed record RoomOccupancy(Room Room, int Living, int Reserved)
{
    public int Free => Math.Max(0, Room.Places - Living - Reserved);
}

/// <summary>
/// Занятость номеров по ночам. Ночь <c>d</c> — с даты <c>d</c> на <c>d + 1</c>. В ночь занято:
/// активные проживания (по одному месту) и брони «Забронировано» (по числу мест брони).
/// Одна функция для подбора номера, бронирования, поселения, продления и изменения номерного фонда.
/// </summary>
public sealed class OccupancyCalculator
{
    private readonly HotelContext _context;
    private readonly IClock _clock;

    public OccupancyCalculator(HotelContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Занято мест в номере в ночь <paramref name="night"/>.</summary>
    public int OccupiedPlaces(string roomNumber, DateOnly night, OccupancyExclusion? except = null)
    {
        var today = _clock.Today;
        except ??= OccupancyExclusion.None;

        var living = _context.Stays.Count(s =>
            s.RoomNumber == roomNumber && s.Id != except.StayId && s.CoversNight(night, today));
        var reserved = _context.Reservations
            .Where(r => r.RoomNumber == roomNumber && r.Number != except.ReservationNumber
                        && r.HoldsPlaces(today) && r.CoversNight(night))
            .Sum(r => r.Places);
        return living + reserved;
    }

    /// <summary>
    /// Свободно мест на весь период [<paramref name="from"/>; <paramref name="to"/>): число мест номера минус
    /// наибольшая занятость среди ночей периода.
    /// </summary>
    public int FreePlaces(Room room, DateOnly from, DateOnly to, OccupancyExclusion? except = null)
    {
        if (to <= from)
            return 0;

        var maxOccupied = 0;
        for (var night = from; night < to; night = night.AddDays(1))
            maxOccupied = Math.Max(maxOccupied, OccupiedPlaces(room.Number, night, except));

        return Math.Max(0, room.Places - maxOccupied);
    }

    /// <summary>Наибольшая занятость номера среди будущих ночей (с сегодняшней) — ниже нее нельзя уменьшить число мест.</summary>
    public int MaxFutureOccupancy(string roomNumber)
    {
        var today = _clock.Today;
        var lastNight = _context.Stays.Where(s => s.RoomNumber == roomNumber && s.IsActive)
            .Select(s => s.Departure < today ? today : s.Departure.AddDays(-1))
            .Concat(_context.Reservations.Where(r => r.RoomNumber == roomNumber && r.HoldsPlaces(today))
                .Select(r => r.Departure.AddDays(-1)))
            .DefaultIfEmpty(today.AddDays(-1))
            .Max();

        var max = 0;
        for (var night = today; night <= lastNight; night = night.AddDays(1))
            max = Math.Max(max, OccupiedPlaces(roomNumber, night));
        return max;
    }

    /// <summary>Занятость номера на сегодняшнюю ночь.</summary>
    public RoomOccupancy Today(Room room)
    {
        var today = _clock.Today;
        var living = _context.Stays.Count(s => s.RoomNumber == room.Number && s.CoversNight(today, today));
        var reserved = _context.Reservations
            .Where(r => r.RoomNumber == room.Number && r.HoldsPlaces(today) && r.CoversNight(today))
            .Sum(r => r.Places);
        return new RoomOccupancy(room, living, reserved);
    }
}
