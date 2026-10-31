using Hotel.Core;
using Hotel.Core.Models;
using Hotel.Core.Services;

namespace Hotel.Tests;

public class ReservationServiceTests
{
    [Fact]
    public void Create_FixesPriceAndHoldsPlaces()
    {
        var hotel = new TestHotel();

        var reservation = hotel.Reserve("201", TestHotel.Day(8), TestHotel.Day(10), places: 2);

        Assert.Equal(ReservationStatus.Reserved, reservation.Status);
        Assert.Equal("+375291112233", reservation.Phone);
        Assert.Equal(60m, reservation.PricePerPlace);
        Assert.Equal(240m, reservation.Total);
        Assert.Equal(1, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("201"), TestHotel.Day(8), TestHotel.Day(10)));
    }

    [Fact]
    public void Create_NotEnoughPlaces_Rejected()
    {
        var hotel = new TestHotel();
        hotel.Reserve("202", TestHotel.Day(8), TestHotel.Day(10));

        var ex = Assert.Throws<HotelException>(() => hotel.Reserve("202", TestHotel.Day(9), TestHotel.Day(11), places: 2));
        Assert.Contains("свободно мест: 1", ex.Message);
    }

    [Fact]
    public void Create_InvalidData_Rejected()
    {
        var hotel = new TestHotel();

        Assert.Throws<DataValidationException>(() => hotel.Reserve("201", TestHotel.Day(-1), TestHotel.Day(1)));
        Assert.Throws<DataValidationException>(() => hotel.Reserve("201", TestHotel.Day(3), TestHotel.Day(3)));
        Assert.Throws<DataValidationException>(() => hotel.Reserve("201", TestHotel.Day(1), TestHotel.Day(100)));
        Assert.Throws<DataValidationException>(() => hotel.Reserve("301", TestHotel.Day(1), TestHotel.Day(2), places: 2));
        Assert.Throws<DataValidationException>(() => hotel.Reserve("999", TestHotel.Day(1), TestHotel.Day(2)));
    }

    [Fact]
    public void Update_IgnoresItselfWhenCheckingPlaces()
    {
        var hotel = new TestHotel();
        var reservation = hotel.Reserve("202", TestHotel.Day(8), TestHotel.Day(10), places: 2);

        hotel.Reservations.Update(reservation.Number, new ReservationDetails
        {
            ContactName = reservation.ContactName,
            Phone = reservation.Phone,
            RoomNumber = "202",
            Arrival = TestHotel.Day(9),
            Departure = TestHotel.Day(11),
            Places = 2
        });

        Assert.Equal(TestHotel.Day(9), reservation.Arrival);
        Assert.Equal(2, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("202"), TestHotel.Day(8), TestHotel.Day(9)));
    }

    [Fact]
    public void Cancel_ReleasesPlaces_AndCannotBeRepeated()
    {
        var hotel = new TestHotel();
        var reservation = hotel.Reserve("301", TestHotel.Day(1), TestHotel.Day(3));

        hotel.Reservations.Cancel(reservation.Number);

        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal(1, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("301"), TestHotel.Day(1), TestHotel.Day(3)));
        Assert.Throws<HotelException>(() => hotel.Reservations.Cancel(reservation.Number));
    }

    [Fact]
    public void ArrivalsToday_AndNoShowNextDay()
    {
        var hotel = new TestHotel();
        var today = hotel.Reserve("201", TestHotel.Today, TestHotel.Day(2));
        hotel.Reserve("202", TestHotel.Day(1), TestHotel.Day(2));

        Assert.Equal([today.Number], hotel.Reservations.GetArrivalsToday().Select(r => r.Number));

        hotel.Clock.Now = TestHotel.Now.AddDays(1);
        Assert.Equal(0, hotel.Occupancy.OccupiedPlaces("201", TestHotel.Day(1)));
        Assert.Equal(1, hotel.Reservations.ExpireNoShows());
        Assert.Equal(ReservationStatus.NoShow, today.Status);
        Assert.Contains(hotel.Log.Lines, l => l.StartsWith("Незаезд", StringComparison.Ordinal));
    }

    [Fact]
    public void Find_ByTextAndPeriod()
    {
        var hotel = new TestHotel();
        hotel.Reserve("201", TestHotel.Day(1), TestHotel.Day(3), contact: "Бондарь Виктор");
        hotel.Reserve("202", TestHotel.Day(10), TestHotel.Day(12), contact: "Ткачук Ирина");

        Assert.Single(hotel.Reservations.Find(new ReservationFilter { Text = "бондарь" }));
        Assert.Single(hotel.Reservations.Find(new ReservationFilter { From = TestHotel.Day(5), To = TestHotel.Day(20) }));
        Assert.Equal(2, hotel.Reservations.Find(new ReservationFilter { Text = "29 111" }).Count);
    }
}
