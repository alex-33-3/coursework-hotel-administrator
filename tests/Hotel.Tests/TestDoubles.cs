using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Occupancy;
using Hotel.Core.Services;
using Hotel.Core.Storage;
using Hotel.Presenters;

namespace Hotel.Tests;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; set; }
}

public sealed class FakeUser : IUserContext
{
    public string CurrentUserName => "Тестовый администратор";
}

public sealed class InMemoryRepository<T> : IRepository<T>
{
    private List<T>? _items;

    public string Location => typeof(T).Name;
    public bool Exists => _items is not null;

    public List<T> Load() => _items is null ? [] : [.. _items];

    public void Save(IEnumerable<T> items) => _items = items.ToList();

    public void Reset() => _items = null;
}

public sealed class MemoryAuditLog : IAuditLog
{
    public List<string> Lines { get; } = [];
    public void Write(string message) => Lines.Add(message);
    public IReadOnlyList<string> ReadAll() => Lines;
}

/// <summary>
/// Общий набор данных: «сегодня» — понедельник 02.11.2026 10:00. Тариф «Стандарт» — 60,00 за место в сутки,
/// «Люкс» — 180,00. Номера: 201 «Стандарт» на 3 места, 202 «Стандарт» на 2 места, 301 «Люкс» на 1 место. Гостей и броней нет.
/// </summary>
public sealed class TestHotel
{
    public static readonly DateTime Now = new(2026, 11, 2, 10, 0, 0);
    public static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    public TestHotel()
    {
        Clock = new FakeClock(Now);
        Context = new HotelContext(new HotelRepositories(
            new InMemoryRepository<Room>(), new InMemoryRepository<Tariff>(), new InMemoryRepository<Guest>(),
            new InMemoryRepository<Reservation>(), new InMemoryRepository<Stay>(), new InMemoryRepository<Receipt>(),
            new InMemoryRepository<UserAccount>()));
        Context.Load();

        Context.Tariffs.AddRange(Enum.GetValues<RoomClass>().Select(c => new Tariff
        {
            Class = c,
            PricePerPlace = c switch { RoomClass.Standard => 60m, RoomClass.Suite => 180m, _ => 50m }
        }));
        Context.Rooms.AddRange(
        [
            new Room { Number = "201", Floor = 2, Class = RoomClass.Standard, Places = 3 },
            new Room { Number = "202", Floor = 2, Class = RoomClass.Standard, Places = 2 },
            new Room { Number = "301", Floor = 3, Class = RoomClass.Suite, Places = 1 }
        ]);
        Context.SaveChanges();

        Occupancy = new OccupancyCalculator(Context, Clock);
        Tariffs = new TariffService(Context, Log);
        Rooms = new RoomService(Context, Occupancy, Tariffs, Clock, Log);
        Guests = new GuestService(Context, Clock, Log);
        Reservations = new ReservationService(Context, Occupancy, Tariffs, User, Clock, Log);
        Stays = new StayService(Context, Occupancy, Tariffs, User, Clock, Log);
        Receipts = new ReceiptService(Context);
        Auth = new AuthService(Context, Log);
        App = new AppServices(Rooms, Tariffs, Guests, Reservations, Stays, Receipts, Auth, Clock, Log, Context);
    }

    public FakeClock Clock { get; }
    public FakeUser User { get; } = new();
    public MemoryAuditLog Log { get; } = new();
    public HotelContext Context { get; }
    public OccupancyCalculator Occupancy { get; }
    public TariffService Tariffs { get; }
    public RoomService Rooms { get; }
    public GuestService Guests { get; }
    public ReservationService Reservations { get; }
    public StayService Stays { get; }
    public ReceiptService Receipts { get; }
    public AuthService Auth { get; }

    /// <summary>Сервисы для презентеров.</summary>
    public AppServices App { get; }

    public static DateOnly Day(int offset) => Today.AddDays(offset);

    public Guest Register(string lastName, string passport, string citizenship = Guest.DefaultCitizenship) =>
        Guests.Register(new Guest
        {
            Id = 0,
            LastName = lastName,
            FirstName = "Иван",
            MiddleName = "Сергеевич",
            BirthDate = new DateOnly(1985, 3, 14),
            Citizenship = citizenship,
            PassportNumber = passport,
            PassportIssuedBy = "Первомайским РУВД г. Минска",
            PassportIssueDate = new DateOnly(2019, 6, 2),
            Phone = "+375291112233"
        });

    /// <summary>Поселяет гостя без брони с сегодняшнего дня.</summary>
    public Stay CheckIn(Guest guest, string room, DateOnly departure) =>
        Stays.CheckIn(new CheckInRequest { GuestIds = [guest.Id], RoomNumber = room, Departure = departure }).Stays[0];

    public Reservation Reserve(string room, DateOnly arrival, DateOnly departure, int places = 1, string contact = "Иванова Мария") =>
        Reservations.Create(new ReservationDetails
        {
            ContactName = contact,
            Phone = "+375 29 111-22-33",
            RoomNumber = room,
            Arrival = arrival,
            Departure = departure,
            Places = places
        });
}
