using Hotel.Core.Billing;
using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Occupancy;
using Hotel.Core.Storage;
using Hotel.Core.Validation;

namespace Hotel.Core.Services;

/// <summary>Подбор номера: даты, класс (пусто — любой) и число нужных мест.</summary>
public sealed record RoomQuery
{
    public required DateOnly Arrival { get; init; }
    public required DateOnly Departure { get; init; }
    public RoomClass? Class { get; init; }
    public int Places { get; init; } = 1;
}

/// <summary>Номер, в котором хватает свободных мест на весь срок, и стоимость проживания.</summary>
public sealed record RoomAvailability(Room Room, int FreePlaces, decimal PricePerPlace, int Nights, int Places)
{
    public decimal Total => StayCalculator.Cost(Nights, Places, PricePerPlace);
}

public interface IRoomService
{
    IReadOnlyList<Room> GetAll();

    Room Get(string number);

    /// <summary>Номера с занятостью на сегодня; <paramref name="roomClass"/> — фильтр по классу.</summary>
    IReadOnlyList<RoomOccupancy> GetOccupancyToday(RoomClass? roomClass = null);

    /// <summary>Номера, где свободно не меньше нужного числа мест на весь срок (с учетом проживающих и броней).</summary>
    IReadOnlyList<RoomAvailability> FindAvailable(RoomQuery query);

    void Add(Room room);

    /// <summary>Изменяет этаж, класс, число мест и описание. Номер комнаты — ключ, он не меняется.</summary>
    void Update(Room room);

    void Delete(string number);
}

public sealed class RoomService : IRoomService
{
    private readonly HotelContext _context;
    private readonly OccupancyCalculator _occupancy;
    private readonly ITariffService _tariffs;
    private readonly IClock _clock;
    private readonly IAuditLog _log;
    private readonly RoomValidator _validator = new();

    public RoomService(HotelContext context, OccupancyCalculator occupancy, ITariffService tariffs, IClock clock, IAuditLog log)
    {
        _context = context;
        _occupancy = occupancy;
        _tariffs = tariffs;
        _clock = clock;
        _log = log;
    }

    public IReadOnlyList<Room> GetAll() => _context.Rooms.OrderBy(r => r.Floor).ThenBy(r => r.Number, RoomNumberComparer.Instance).ToList();

    public Room Get(string number) =>
        _context.Rooms.FirstOrDefault(r => r.Number == number)
        ?? throw new HotelException($"Номер {number} не найден.");

    public IReadOnlyList<RoomOccupancy> GetOccupancyToday(RoomClass? roomClass = null) =>
        GetAll().Where(r => roomClass is null || r.Class == roomClass).Select(_occupancy.Today).ToList();

    public IReadOnlyList<RoomAvailability> FindAvailable(RoomQuery query)
    {
        var today = _clock.Today;
        var errors = new List<ValidationError>();
        StayDates.Validate(errors, query.Arrival, query.Departure, today, today.AddDays(ReservationValidator.MaxDaysAhead));
        if (query.Places is < 1 or > RoomValidator.MaxPlaces)
            errors.Add(new(nameof(query.Places), $"Число мест — от 1 до {RoomValidator.MaxPlaces}."));
        if (errors.Count > 0)
            throw new DataValidationException(errors);

        var nights = StayCalculator.Nights(query.Arrival, query.Departure);
        return GetAll()
            .Where(r => query.Class is null || r.Class == query.Class)
            .Where(r => r.Places >= query.Places)
            .Select(r => new RoomAvailability(r, _occupancy.FreePlaces(r, query.Arrival, query.Departure),
                _tariffs.GetPrice(r.Class), nights, query.Places))
            .Where(a => a.FreePlaces >= query.Places)
            .OrderBy(a => a.PricePerPlace).ThenBy(a => a.Room.Number, RoomNumberComparer.Instance)
            .ToList();
    }

    public void Add(Room room)
    {
        room = Normalize(room);
        var errors = _validator.Validate(room).ToList();
        if (_context.Rooms.Any(r => string.Equals(r.Number, room.Number, StringComparison.OrdinalIgnoreCase)))
            errors.Add(new(nameof(Room.Number), $"Номер {room.Number} уже есть."));
        if (errors.Count > 0)
            throw new DataValidationException(errors);

        _context.Rooms.Add(room);
        _context.SaveChanges();
        _log.Write($"Добавлен номер {room}");
    }

    public void Update(Room room)
    {
        room = Normalize(room);
        var existing = Get(room.Number);
        var errors = _validator.Validate(room).ToList();
        if (errors.Count > 0)
            throw new DataValidationException(errors);

        var occupied = _occupancy.MaxFutureOccupancy(room.Number);
        if (room.Places < occupied)
            throw new HotelException(
                $"В номере {room.Number} уже занято или забронировано {occupied} мест. Уменьшить число мест до {room.Places} нельзя.");

        _context.Rooms[_context.Rooms.IndexOf(existing)] = room;
        _context.SaveChanges();
        _log.Write($"Изменен номер {existing} → {room}");
    }

    public void Delete(string number)
    {
        var room = Get(number);
        if (_context.Stays.Any(s => s.RoomNumber == number))
            throw new HotelException($"В номере {number} уже жили гости: он нужен для истории проживаний. Удалить его нельзя.");
        if (_context.Reservations.Any(r => r.RoomNumber == number && r.Status == ReservationStatus.Reserved))
            throw new HotelException($"На номер {number} есть бронь. Сначала отмените или перенесите ее.");

        _context.Rooms.Remove(room);
        _context.SaveChanges();
        _log.Write($"Удален номер {room}");
    }

    private static Room Normalize(Room room) => room with
    {
        Number = room.Number.Trim().ToLowerInvariant(),
        Description = string.IsNullOrWhiteSpace(room.Description) ? null : room.Description.Trim()
    };
}

/// <summary>Сортирует номера комнат по числу, а не как строки: 99 раньше 101.</summary>
public sealed class RoomNumberComparer : IComparer<string>
{
    public static RoomNumberComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        var byNumber = Digits(x).CompareTo(Digits(y));
        return byNumber != 0 ? byNumber : string.Compare(x, y, StringComparison.Ordinal);
    }

    private static int Digits(string? value) =>
        int.TryParse(new string((value ?? string.Empty).TakeWhile(char.IsDigit).ToArray()), out var n) ? n : 0;
}
