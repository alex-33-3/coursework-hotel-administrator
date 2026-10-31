using Hotel.Core;
using Hotel.Core.Models;
using Hotel.Core.Services;

namespace Hotel.Tests;

public class OccupancyAndRoomTests
{
    [Fact]
    public void Today_CountsLivingAndReserved()
    {
        var hotel = new TestHotel();
        hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(3));
        hotel.Reserve("201", TestHotel.Today, TestHotel.Day(2));

        var room = hotel.Rooms.GetOccupancyToday().Single(o => o.Room.Number == "201");

        Assert.Equal((1, 1, 1), (room.Living, room.Reserved, room.Free));
    }

    [Fact]
    public void FreePlaces_UsesBusiestNightOfPeriod()
    {
        var hotel = new TestHotel();
        hotel.Reserve("201", TestHotel.Day(8), TestHotel.Day(10), places: 2);
        var room = hotel.Rooms.Get("201");

        Assert.Equal(3, hotel.Occupancy.FreePlaces(room, TestHotel.Day(1), TestHotel.Day(8)));
        Assert.Equal(1, hotel.Occupancy.FreePlaces(room, TestHotel.Day(7), TestHotel.Day(9)));
        Assert.Equal(3, hotel.Occupancy.FreePlaces(room, TestHotel.Day(10), TestHotel.Day(12)));
    }

    [Fact]
    public void FreePlaces_DepartingTodayFreesTonight()
    {
        var hotel = new TestHotel();
        hotel.Clock.Now = TestHotel.Now.AddDays(-2);
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "301", TestHotel.Today);
        hotel.Clock.Now = TestHotel.Now;

        Assert.Equal(1, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("301"), TestHotel.Today, TestHotel.Day(1)));
        Assert.True(stay.IsActive);
    }

    [Fact]
    public void FreePlaces_OverdueGuestStillOccupiesTonight()
    {
        var hotel = new TestHotel();
        hotel.Clock.Now = TestHotel.Now.AddDays(-3);
        hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "301", TestHotel.Day(-1));
        hotel.Clock.Now = TestHotel.Now;

        Assert.Equal(0, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("301"), TestHotel.Today, TestHotel.Day(1)));
        Assert.Equal(1, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("301"), TestHotel.Day(1), TestHotel.Day(2)));
    }

    [Fact]
    public void FindAvailable_FiltersByClassAndPlaces_AndCalculatesTotal()
    {
        var hotel = new TestHotel();
        hotel.Reserve("201", TestHotel.Day(8), TestHotel.Day(10), places: 2);

        var result = hotel.Rooms.FindAvailable(new RoomQuery
        {
            Arrival = TestHotel.Day(8),
            Departure = TestHotel.Day(10),
            Class = RoomClass.Standard,
            Places = 2
        });

        var room = Assert.Single(result);
        Assert.Equal("202", room.Room.Number);
        Assert.Equal(240m, room.Total);
    }

    [Fact]
    public void FindAvailable_PastArrival_Throws()
    {
        var hotel = new TestHotel();

        Assert.Throws<DataValidationException>(() =>
            hotel.Rooms.FindAvailable(new RoomQuery { Arrival = TestHotel.Day(-1), Departure = TestHotel.Day(1) }));
    }

    [Fact]
    public void AddRoom_DuplicateNumber_Rejected()
    {
        var hotel = new TestHotel();
        var room = new Room { Number = "305", Floor = 3, Class = RoomClass.Standard, Places = 2 };
        hotel.Rooms.Add(room);

        var ex = Assert.Throws<DataValidationException>(() => hotel.Rooms.Add(room));
        Assert.Contains("уже есть", ex.Message);
    }

    [Fact]
    public void UpdateRoom_CannotReducePlacesBelowFutureOccupancy()
    {
        var hotel = new TestHotel();
        hotel.Reserve("201", TestHotel.Day(5), TestHotel.Day(7), places: 2);

        Assert.Throws<HotelException>(() => hotel.Rooms.Update(hotel.Rooms.Get("201") with { Places = 1 }));
        hotel.Rooms.Update(hotel.Rooms.Get("201") with { Places = 2 });
        Assert.Equal(2, hotel.Rooms.Get("201").Places);
    }

    [Fact]
    public void DeleteRoom_WithHistoryOrReservation_Rejected()
    {
        var hotel = new TestHotel();
        hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(1));
        hotel.Reserve("202", TestHotel.Day(3), TestHotel.Day(4));

        Assert.Throws<HotelException>(() => hotel.Rooms.Delete("201"));
        Assert.Throws<HotelException>(() => hotel.Rooms.Delete("202"));
        hotel.Rooms.Delete("301");
        Assert.DoesNotContain(hotel.Rooms.GetAll(), r => r.Number == "301");
    }

    [Fact]
    public void Tariff_Update_AffectsNewPricesOnly()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(2));

        hotel.Tariffs.Update(RoomClass.Standard, 70m);

        Assert.Equal(60m, stay.PricePerPlace);
        Assert.Equal(70m, hotel.Tariffs.GetPrice(RoomClass.Standard));
        Assert.Throws<HotelException>(() => hotel.Tariffs.Update(RoomClass.Standard, 0m));
        Assert.Throws<HotelException>(() => hotel.Tariffs.Update(RoomClass.Standard, 10.005m));
    }

    [Fact]
    public void RoomNumberComparer_SortsNumerically()
    {
        string[] numbers = ["101", "99", "204а", "204"];
        Assert.Equal(["99", "101", "204", "204а"], numbers.Order(RoomNumberComparer.Instance));
    }
}
