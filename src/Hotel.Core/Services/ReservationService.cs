using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Occupancy;
using Hotel.Core.Storage;
using Hotel.Core.Validation;

namespace Hotel.Core.Services;

/// <summary>Фильтр списка броней: брони, период которых пересекается с [From; To], состояние и текст (контакт, телефон, номер).</summary>
public sealed record ReservationFilter
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public ReservationStatus? Status { get; init; }
    public string? Text { get; init; }
}

public interface IReservationService
{
    Reservation Get(int number);

    IReadOnlyList<Reservation> Find(ReservationFilter filter);

    /// <summary>Заезд сегодня: брони «Забронировано» с датой приезда сегодня.</summary>
    IReadOnlyList<Reservation> GetArrivalsToday();

    Reservation Create(ReservationDetails details);

    void Update(int number, ReservationDetails details);

    void Cancel(int number);

    /// <summary>
    /// Брони, по которым не заселились до конца дня приезда, получают состояние «Незаезд» и перестают держать места.
    /// Возвращает число таких броней.
    /// </summary>
    int ExpireNoShows();
}

public sealed class ReservationService : IReservationService
{
    private readonly HotelContext _context;
    private readonly OccupancyCalculator _occupancy;
    private readonly ITariffService _tariffs;
    private readonly IUserContext _user;
    private readonly IClock _clock;
    private readonly IAuditLog _log;
    private readonly ReservationValidator _validator = new();

    public ReservationService(HotelContext context, OccupancyCalculator occupancy, ITariffService tariffs,
        IUserContext user, IClock clock, IAuditLog log)
    {
        _context = context;
        _occupancy = occupancy;
        _tariffs = tariffs;
        _user = user;
        _clock = clock;
        _log = log;
    }

    public Reservation Get(int number) =>
        _context.Reservations.FirstOrDefault(r => r.Number == number)
        ?? throw new HotelException($"Бронь № {number} не найдена.");

    public IReadOnlyList<Reservation> Find(ReservationFilter filter)
    {
        var query = _context.Reservations.AsEnumerable();
        if (filter.From is { } from)
            query = query.Where(r => r.Departure > from);
        if (filter.To is { } to)
            query = query.Where(r => r.Arrival <= to);
        if (filter.Status is { } status)
            query = query.Where(r => r.Status == status);

        var text = filter.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var digits = PhoneFormat.Normalize(text);
            query = query.Where(r => r.ContactName.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                                     || (digits.Length > 0 && r.Phone.Contains(digits, StringComparison.Ordinal))
                                     || r.RoomNumber.Equals(text, StringComparison.OrdinalIgnoreCase)
                                     || r.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) == text);
        }

        return query.OrderBy(r => r.Arrival).ThenBy(r => r.Number).ToList();
    }

    public IReadOnlyList<Reservation> GetArrivalsToday()
    {
        var today = _clock.Today;
        return _context.Reservations
            .Where(r => r.Status == ReservationStatus.Reserved && r.Arrival == today)
            .OrderBy(r => r.RoomNumber, RoomNumberComparer.Instance)
            .ToList();
    }

    public Reservation Create(ReservationDetails details)
    {
        details = Normalize(details);
        var room = Check(details, exceptNumber: null);

        var number = HotelContext.NextId(_context.Reservations, r => r.Number);
        var reservation = new Reservation(number, details, _tariffs.GetPrice(room.Class), _clock.Now, _user.CurrentUserName);
        _context.Reservations.Add(reservation);
        _context.SaveChanges();
        _log.Write($"Создана {reservation}, мест: {reservation.Places}, сумма {reservation.Total:N2} {Money.Currency}");
        return reservation;
    }

    public void Update(int number, ReservationDetails details)
    {
        var reservation = Get(number);
        EnsureReserved(reservation, "изменить");
        details = Normalize(details);
        var room = Check(details, exceptNumber: number);

        // Цена брони сохраняется, пока класс номера тот же; при переносе в номер другого класса — по текущему тарифу.
        var oldRoom = _context.Rooms.FirstOrDefault(r => r.Number == reservation.RoomNumber);
        var price = oldRoom?.Class == room.Class ? reservation.PricePerPlace : _tariffs.GetPrice(room.Class);

        var before = reservation.ToString();
        reservation.Update(details, price);
        _context.SaveChanges();
        _log.Write($"Изменена {before} → номер {reservation.RoomNumber}, " +
                   $"{reservation.Arrival:dd.MM.yyyy}–{reservation.Departure:dd.MM.yyyy}, мест: {reservation.Places}");
    }

    public void Cancel(int number)
    {
        var reservation = Get(number);
        EnsureReserved(reservation, "отменить");
        reservation.SetStatus(ReservationStatus.Cancelled);
        _context.SaveChanges();
        _log.Write($"Отменена {reservation}");
    }

    public int ExpireNoShows()
    {
        var today = _clock.Today;
        var expired = _context.Reservations
            .Where(r => r.Status == ReservationStatus.Reserved && r.Arrival < today)
            .ToList();
        if (expired.Count == 0)
            return 0;

        foreach (var reservation in expired)
        {
            reservation.SetStatus(ReservationStatus.NoShow);
            _log.Write($"Незаезд: {reservation}");
        }

        _context.SaveChanges();
        return expired.Count;
    }

    private Room Check(ReservationDetails details, int? exceptNumber)
    {
        var today = _clock.Today;
        var errors = _validator.Validate(details, today).ToList();
        var room = _context.Rooms.FirstOrDefault(r => r.Number == details.RoomNumber);
        if (room is null && !string.IsNullOrWhiteSpace(details.RoomNumber))
            errors.Add(new(nameof(details.RoomNumber), $"Номер {details.RoomNumber} не найден."));
        else if (room is not null && details.Places > room.Places)
            errors.Add(new(nameof(details.Places), $"В номере {room.Number} всего {room.Places} мест."));
        if (errors.Count > 0 || room is null)
            throw new DataValidationException(errors);

        var free = _occupancy.FreePlaces(room, details.Arrival, details.Departure, new OccupancyExclusion(ReservationNumber: exceptNumber));
        if (free < details.Places)
            throw new HotelException(
                $"В номере {room.Number} на {details.Arrival:dd.MM.yyyy}–{details.Departure:dd.MM.yyyy} свободно мест: {free}, " +
                $"а нужно {details.Places}. Подберите другой номер или даты.");

        return room;
    }

    private static void EnsureReserved(Reservation reservation, string action)
    {
        if (reservation.Status != ReservationStatus.Reserved)
            throw new HotelException($"{reservation} в состоянии «{reservation.Status.GetDescription()}», {action} ее нельзя.");
    }

    private static ReservationDetails Normalize(ReservationDetails details) => details with
    {
        ContactName = details.ContactName.Trim(),
        Phone = PhoneFormat.Normalize(details.Phone),
        RoomNumber = details.RoomNumber.Trim().ToLowerInvariant()
    };
}
