using Hotel.Core;
using Hotel.Core.Models;
using Hotel.Core.Storage;

namespace Hotel.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hotel-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void DefaultData_SurvivesSaveAndLoad()
    {
        var context = CreateContext();
        context.Load();
        Assert.True(context.IsNewStorage);
        DefaultData.Seed(context, TestHotel.Now);
        context.SaveChanges();

        var reloaded = CreateContext();
        reloaded.Load();

        Assert.False(reloaded.IsNewStorage);
        Assert.Equal(12, reloaded.Rooms.Count);
        Assert.Equal(context.Stays.Count, reloaded.Stays.Count);
        Assert.Equal(context.Reservations.Select(r => (r.Number, r.Status, r.Arrival)),
            reloaded.Reservations.Select(r => (r.Number, r.Status, r.Arrival)));
        Assert.Equal(context.Receipts.Sum(r => r.Total), reloaded.Receipts.Sum(r => r.Total));
        Assert.Contains(reloaded.Stays, s => s.Status == StayStatus.CheckedOut && s.CheckedOutAt is not null);
    }

    [Fact]
    public void Load_CorruptFile_ThrowsStorageException_AndResetMovesFileAside()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "stays.json");
        File.WriteAllText(path, "[{\"Id\": 1, \"Unknown\": true}]");
        var context = CreateContext();

        var ex = Assert.Throws<StorageException>(context.Load);
        Assert.Equal(path, ex.Location);

        context.ResetStorage(ex.Location);
        context.Load();
        Assert.Empty(context.Stays);
        Assert.Single(Directory.GetFiles(_directory, "stays.json.corrupt-*"));
    }

    [Fact]
    public void Load_UnknownEnumValue_ThrowsStorageException()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "rooms.json"),
            "[{\"Number\": \"101\", \"Floor\": 1, \"Class\": \"Palace\", \"Places\": 2}]");

        Assert.Throws<StorageException>(CreateContext().Load);
    }

    private HotelContext CreateContext() => new(new HotelRepositories(
        new JsonRepository<Room>(Path.Combine(_directory, "rooms.json")),
        new JsonRepository<Tariff>(Path.Combine(_directory, "tariffs.json")),
        new JsonRepository<Guest>(Path.Combine(_directory, "guests.json")),
        new JsonRepository<Reservation>(Path.Combine(_directory, "reservations.json")),
        new JsonRepository<Stay>(Path.Combine(_directory, "stays.json")),
        new JsonRepository<Receipt>(Path.Combine(_directory, "receipts.json")),
        new JsonRepository<UserAccount>(Path.Combine(_directory, "users.json"))));
}
