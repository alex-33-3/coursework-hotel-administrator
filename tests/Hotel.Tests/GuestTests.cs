using Hotel.Core;
using Hotel.Core.Models;
using Hotel.Core.Search;
using Hotel.Core.Services;

namespace Hotel.Tests;

public class GuestTests
{
    [Fact]
    public void Register_NormalizesPassportAndPhone_AndAssignsId()
    {
        var hotel = new TestHotel();

        var guest = hotel.Guests.Register(hotel.Register("Петров", "MP1234567") with { PassportNumber = "kh 3456789", Phone = "+375 (29) 111-22-33" });

        Assert.Equal(2, guest.Id);
        Assert.Equal("KH3456789", guest.PassportNumber);
        Assert.Equal("+375291112233", guest.Phone);
        Assert.Same(guest, hotel.Guests.FindByPassport("kh3456789"));
    }

    [Fact]
    public void Register_DuplicatePassport_Rejected()
    {
        var hotel = new TestHotel();
        hotel.Register("Петров", "MP1234567");

        var ex = Assert.Throws<DataValidationException>(() => hotel.Register("Иванов", "mp 1234567"));
        Assert.Contains(ex.Errors, e => e.Field == nameof(Guest.PassportNumber));
    }

    [Theory]
    [InlineData("Республика Беларусь", "M1234567")]
    [InlineData("Республика Беларусь", "MP12345678")]
    [InlineData("Германия", "C01")]
    public void Register_InvalidPassport_Rejected(string citizenship, string passport)
    {
        var hotel = new TestHotel();

        Assert.Throws<DataValidationException>(() => hotel.Register("Петров", passport, citizenship));
    }

    [Fact]
    public void Register_ForeignPassport_Accepted()
    {
        var hotel = new TestHotel();

        var guest = hotel.Register("Шмидт", "C01X00T47", "Германия");

        Assert.False(guest.IsBelarusCitizen);
    }

    [Fact]
    public void Register_InvalidDates_Rejected()
    {
        var hotel = new TestHotel();
        var guest = hotel.Register("Петров", "MP1234567");

        var ex = Assert.Throws<DataValidationException>(() =>
            hotel.Guests.Update(guest with { BirthDate = TestHotel.Day(1), PassportIssueDate = TestHotel.Day(2) }));
        Assert.Contains(ex.Errors, e => e.Field == nameof(Guest.BirthDate));
        Assert.Contains(ex.Errors, e => e.Field == nameof(Guest.PassportIssueDate));
    }

    [Fact]
    public void Delete_GuestWithStays_Rejected()
    {
        var hotel = new TestHotel();
        var guest = hotel.Register("Петров", "MP1234567");
        hotel.CheckIn(guest, "201", TestHotel.Day(1));
        var lonely = hotel.Register("Иванов", "MP7654321");

        Assert.Throws<HotelException>(() => hotel.Guests.Delete(guest.Id));
        hotel.Guests.Delete(lonely.Id);
        Assert.Null(hotel.Guests.FindByPassport("MP7654321"));
    }

    [Fact]
    public void GetRows_FiltersByStayStatus()
    {
        var hotel = new TestHotel();
        var living = hotel.Register("Петров", "MP1234567");
        hotel.CheckIn(living, "201", TestHotel.Day(2));
        var gone = hotel.Register("Сидорова", "HB2345678");
        var stay = hotel.CheckIn(gone, "202", TestHotel.Day(2));
        hotel.Stays.CheckOut(stay.Id);
        hotel.Register("Новик", "MC4567890");

        Assert.Equal(["Петров"], hotel.Guests.GetRows(GuestListFilter.Living).Select(r => r.Guest.LastName));
        Assert.Equal(["Сидорова"], hotel.Guests.GetRows(GuestListFilter.CheckedOut).Select(r => r.Guest.LastName));
        Assert.Equal(3, hotel.Guests.GetRows(GuestListFilter.All).Count);
    }

    [Theory]
    [InlineData(GuestField.Passport, "mp12", "Петров")]
    [InlineData(GuestField.Room, "202", "Сидорова")]
    [InlineData(GuestField.Any, "ПЕТРОВ", "Петров")]
    [InlineData(GuestField.Arrival, "02.11", "Петров,Сидорова")]
    [InlineData(GuestField.Status, "выехал", "Сидорова")]
    [InlineData(GuestField.Citizenship, "герм", "Шмидт")]
    [InlineData(GuestField.Phone, "+375 29 111", "Петров,Сидорова")]
    public void Search_ByArbitraryField(GuestField field, string text, string expected)
    {
        var hotel = new TestHotel();
        hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(2));
        var stay = hotel.CheckIn(hotel.Register("Сидорова", "HB2345678"), "202", TestHotel.Day(2));
        hotel.Stays.CheckOut(stay.Id);
        hotel.Guests.Register(hotel.Guests.Get(1) with { LastName = "Шмидт", Citizenship = "Германия", PassportNumber = "C01X00T47", Phone = null });

        var found = GuestSearch.Search(hotel.Guests.GetRows(GuestListFilter.All), field, text);

        Assert.Equal(expected.Split(','), found.Select(r => r.Guest.LastName).Order());
    }
}
