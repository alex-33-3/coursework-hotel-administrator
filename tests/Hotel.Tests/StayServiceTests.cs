using Hotel.Core;
using Hotel.Core.Models;
using Hotel.Core.Services;

namespace Hotel.Tests;

public class StayServiceTests
{
    [Fact]
    public void CheckIn_WithoutReservation_IssuesAccommodationReceipt()
    {
        var hotel = new TestHotel();
        var guest = hotel.Register("Петров", "MP1234567");

        var result = hotel.Stays.CheckIn(new CheckInRequest { GuestIds = [guest.Id], RoomNumber = "201", Departure = TestHotel.Day(3) });

        var stay = Assert.Single(result.Stays);
        var receipt = Assert.Single(result.Receipts);
        Assert.Equal((TestHotel.Today, TestHotel.Day(3), StayStatus.Active), (stay.Arrival, stay.Departure, stay.Status));
        Assert.Equal(ReceiptKind.Accommodation, receipt.Kind);
        Assert.Equal(180m, receipt.Total);
        Assert.Equal(3, receipt.Lines[0].Quantity);

        var card = hotel.Receipts.GetCard(receipt.Number);
        Assert.Equal("MP1234567", card.Guest.PassportNumber);
        Assert.Equal("201 (Стандарт)", card.RoomText);
    }

    [Fact]
    public void CheckIn_ByReservation_UsesReservedPriceAndReleasesUnusedPlaces()
    {
        var hotel = new TestHotel();
        var reservation = hotel.Reserve("201", TestHotel.Today, TestHotel.Day(2), places: 2);
        hotel.Tariffs.Update(RoomClass.Standard, 75m);
        var guest = hotel.Register("Петров", "MP1234567");

        var result = hotel.Stays.CheckIn(new CheckInRequest { GuestIds = [guest.Id], ReservationNumber = reservation.Number });

        Assert.Equal(ReservationStatus.CheckedIn, reservation.Status);
        Assert.Equal(reservation.Number, result.Stays[0].ReservationNumber);
        Assert.Equal(120m, result.Receipts[0].Total);
        Assert.Equal(2, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("201"), TestHotel.Today, TestHotel.Day(2)));
    }

    [Fact]
    public void CheckIn_SeveralGuests_EachGetsOwnPlaceAndReceipt()
    {
        var hotel = new TestHotel();
        var first = hotel.Register("Петров", "MP1234567");
        var second = hotel.Register("Петрова", "MP7654321");

        var result = hotel.Stays.CheckIn(new CheckInRequest { GuestIds = [first.Id, second.Id], RoomNumber = "202", Departure = TestHotel.Day(1) });

        Assert.Equal(2, result.Stays.Count);
        Assert.Equal(2, result.Receipts.Select(r => r.Number).Distinct().Count());
        Assert.Equal(0, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("202"), TestHotel.Today, TestHotel.Day(1)));
    }

    [Fact]
    public void CheckIn_NoFreePlace_Rejected()
    {
        var hotel = new TestHotel();
        hotel.Reserve("202", TestHotel.Today, TestHotel.Day(2), places: 2);
        var guest = hotel.Register("Сидоров", "MP1234567");

        var ex = Assert.Throws<HotelException>(() => hotel.CheckIn(guest, "202", TestHotel.Day(1)));
        Assert.Contains("свободно мест: 0", ex.Message);
    }

    [Fact]
    public void CheckIn_GuestAlreadyLiving_Rejected()
    {
        var hotel = new TestHotel();
        var guest = hotel.Register("Петров", "MP1234567");
        hotel.CheckIn(guest, "201", TestHotel.Day(2));

        Assert.Throws<HotelException>(() => hotel.CheckIn(guest, "202", TestHotel.Day(2)));
    }

    [Fact]
    public void CheckIn_ByReservation_NotOnArrivalDay_Rejected()
    {
        var hotel = new TestHotel();
        var reservation = hotel.Reserve("201", TestHotel.Day(1), TestHotel.Day(2));
        var guest = hotel.Register("Петров", "MP1234567");

        Assert.Throws<HotelException>(() =>
            hotel.Stays.CheckIn(new CheckInRequest { GuestIds = [guest.Id], ReservationNumber = reservation.Number }));
    }

    [Fact]
    public void Extend_IssuesExtensionReceipt()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(3));

        Assert.Equal(120m, hotel.Stays.ExtensionCost(stay.Id, TestHotel.Day(5)));
        var receipt = hotel.Stays.Extend(stay.Id, TestHotel.Day(5));

        Assert.Equal(ReceiptKind.Extension, receipt.Kind);
        Assert.Equal(120m, receipt.Total);
        Assert.Equal(TestHotel.Day(5), stay.Departure);
        Assert.Equal(300m, hotel.Receipts.GetForStay(stay.Id).Sum(r => r.Total));
    }

    [Fact]
    public void Extend_ConflictsWithFutureReservation_Rejected()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "301", TestHotel.Day(3));
        hotel.Reserve("301", TestHotel.Day(8), TestHotel.Day(10));

        hotel.Stays.Extend(stay.Id, TestHotel.Day(8));
        Assert.Throws<HotelException>(() => hotel.Stays.Extend(stay.Id, TestHotel.Day(9)));
        Assert.Equal(TestHotel.Day(8), stay.Departure);
    }

    [Fact]
    public void DeparturesToday_IncludesOverdueFirst()
    {
        var hotel = new TestHotel();
        hotel.Clock.Now = TestHotel.Now.AddDays(-3);
        var overdue = hotel.CheckIn(hotel.Register("Кузьмич", "KH3456789"), "201", TestHotel.Day(-1));
        var leaving = hotel.CheckIn(hotel.Register("Сидорова", "HB2345678"), "201", TestHotel.Today);
        hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(2));
        hotel.Clock.Now = TestHotel.Now;

        var rows = hotel.Stays.GetDeparturesToday();

        Assert.Equal([overdue.Id, leaving.Id], rows.Select(r => r.Stay!.Id));
        Assert.True(rows[0].Stay!.IsOverdue(TestHotel.Today));
    }

    [Fact]
    public void CheckOut_OnTime_FreesPlaceWithoutReceipt()
    {
        var hotel = new TestHotel();
        hotel.Clock.Now = TestHotel.Now.AddDays(-2);
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "301", TestHotel.Today);
        hotel.Clock.Now = TestHotel.Now;

        var result = hotel.Stays.CheckOut(stay.Id);

        Assert.Null(result.Receipt);
        Assert.Equal(StayStatus.CheckedOut, stay.Status);
        Assert.Equal(TestHotel.Now, stay.CheckedOutAt);
        Assert.Equal(1, hotel.Occupancy.FreePlaces(hotel.Rooms.Get("301"), TestHotel.Today, TestHotel.Day(1)));
        Assert.Throws<HotelException>(() => hotel.Stays.CheckOut(stay.Id));
    }

    [Fact]
    public void CheckOut_Late_IssuesSurcharge()
    {
        var hotel = new TestHotel();
        hotel.Clock.Now = TestHotel.Now.AddDays(-2);
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Today);
        hotel.Clock.Now = TestHotel.Today.ToDateTime(new TimeOnly(15, 0));

        var result = hotel.Stays.CheckOut(stay.Id);

        Assert.Equal(ReceiptKind.Surcharge, result.Receipt!.Kind);
        Assert.Equal(30m, result.Receipt.Total);
    }

    [Fact]
    public void CheckOut_Early_IssuesRefund()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(3));
        hotel.Clock.Now = TestHotel.Day(1).ToDateTime(new TimeOnly(11, 0));

        var result = hotel.Stays.CheckOut(stay.Id);

        Assert.Equal(ReceiptKind.Refund, result.Receipt!.Kind);
        Assert.Equal(120m, result.Receipt.Total);
        Assert.Contains(hotel.Log.Lines, l => l.StartsWith("Досрочный отъезд", StringComparison.Ordinal));

        var summary = hotel.Receipts.Summarize(hotel.Receipts.Find(TestHotel.Today, TestHotel.Day(1)));
        Assert.Equal((180m, 120m, 60m), (summary.Accommodation, summary.Refund, summary.Net));
    }
}
