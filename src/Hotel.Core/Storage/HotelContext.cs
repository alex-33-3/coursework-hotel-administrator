using Hotel.Core.Models;

namespace Hotel.Core.Storage;

/// <summary>
/// Уведомление об изменении данных: формы подписываются, чтобы обновить списки.
/// </summary>
public interface IHotelEvents
{
    event EventHandler? Changed;
}

/// <summary>Хранилища всех коллекций гостиницы — для конструктора <see cref="HotelContext"/>.</summary>
public sealed record HotelRepositories(
    IRepository<Room> Rooms,
    IRepository<Tariff> Tariffs,
    IRepository<Guest> Guests,
    IRepository<Reservation> Reservations,
    IRepository<Stay> Stays,
    IRepository<Receipt> Receipts,
    IRepository<UserAccount> Users)
{
    public IEnumerable<IRepositoryBase> All => [Rooms, Tariffs, Guests, Reservations, Stays, Receipts, Users];
}

/// <summary>
/// Единица работы: все коллекции хранятся в памяти и сохраняются одним вызовом <see cref="SaveChanges"/>.
/// </summary>
public sealed class HotelContext : IHotelEvents
{
    private readonly HotelRepositories _repositories;

    public HotelContext(HotelRepositories repositories)
    {
        _repositories = repositories;
    }

    /// <summary>Данные изменены и сохранены: формы обновляют списки.</summary>
    public event EventHandler? Changed;

    public List<Room> Rooms { get; private set; } = [];
    public List<Tariff> Tariffs { get; private set; } = [];
    public List<Guest> Guests { get; private set; } = [];
    public List<Reservation> Reservations { get; private set; } = [];
    public List<Stay> Stays { get; private set; } = [];
    public List<Receipt> Receipts { get; private set; } = [];
    public List<UserAccount> Users { get; private set; } = [];

    /// <summary>Файлов данных еще нет — первый запуск, можно создать демонстрационные данные.</summary>
    public bool IsNewStorage { get; private set; }

    public void Load()
    {
        // Сначала читаются все файлы: если один поврежден, данные в памяти не меняются частично.
        var rooms = _repositories.Rooms.Load();
        var tariffs = _repositories.Tariffs.Load();
        var guests = _repositories.Guests.Load();
        var reservations = _repositories.Reservations.Load();
        var stays = _repositories.Stays.Load();
        var receipts = _repositories.Receipts.Load();
        var users = _repositories.Users.Load();

        IsNewStorage = !_repositories.All.Any(r => r.Exists);
        Rooms = rooms;
        Tariffs = tariffs;
        Guests = guests;
        Reservations = reservations;
        Stays = stays;
        Receipts = receipts;
        Users = users;
    }

    public void SaveChanges()
    {
        _repositories.Rooms.Save(Rooms);
        _repositories.Tariffs.Save(Tariffs);
        _repositories.Guests.Save(Guests);
        _repositories.Reservations.Save(Reservations);
        _repositories.Stays.Save(Stays);
        _repositories.Receipts.Save(Receipts);
        _repositories.Users.Save(Users);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Откладывает поврежденный файл, чтобы начать с пустого.</summary>
    public void ResetStorage(string location)
    {
        foreach (var repository in _repositories.All.Where(r => r.Location == location))
            repository.Reset();
    }

    public static int NextId<T>(IEnumerable<T> items, Func<T, int> id) => items.Select(id).DefaultIfEmpty().Max() + 1;
}
