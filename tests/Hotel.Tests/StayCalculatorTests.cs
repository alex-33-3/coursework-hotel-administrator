using Hotel.Core.Billing;
using Hotel.Core.Models;

namespace Hotel.Tests;

public class StayCalculatorTests
{
    [Theory]
    [InlineData(11, 59, 0)]
    [InlineData(12, 0, 0)]
    [InlineData(12, 1, 0.5)]
    [InlineData(18, 0, 0.5)]
    [InlineData(18, 1, 1)]
    [InlineData(23, 30, 1)]
    public void LateFraction_DependsOnCheckOutHour(int hour, int minute, decimal expected) =>
        Assert.Equal(expected, StayCalculator.LateFraction(new TimeOnly(hour, minute)));

    [Fact]
    public void Settle_OnTimeBeforeNoon_NoReceipt()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(3));

        var settlement = StayCalculator.Settle(stay, TestHotel.Day(3).ToDateTime(new TimeOnly(11, 0)));

        Assert.Equal(3, settlement.ChargedNights);
        Assert.Equal(180m, settlement.Paid);
        Assert.Equal(0m, settlement.Difference);
        Assert.Null(settlement.ReceiptKind);
        Assert.Empty(settlement.ReceiptLines());
    }

    [Fact]
    public void Settle_LateUntilSix_HalfDaySurcharge()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(3));

        var settlement = StayCalculator.Settle(stay, TestHotel.Day(3).ToDateTime(new TimeOnly(15, 0)));

        Assert.Equal(ReceiptKind.Surcharge, settlement.ReceiptKind);
        Assert.Equal(30m, settlement.Difference);
        var line = Assert.Single(settlement.ReceiptLines());
        Assert.Equal(30m, line.Amount);
    }

    [Fact]
    public void Settle_Overdue_ChargesUnpaidNightsAndLateFee()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(1));

        // Плановый отъезд 03.11, фактически 05.11 в 19:00: 2 неоплаченных суток + полные сутки задержки.
        var settlement = StayCalculator.Settle(stay, TestHotel.Day(3).ToDateTime(new TimeOnly(19, 0)));

        Assert.Equal(3, settlement.ChargedNights);
        Assert.Equal(180m, settlement.Difference);
        Assert.Equal(settlement.Difference, settlement.ReceiptLines().Sum(l => l.Amount));
    }

    [Fact]
    public void Settle_EarlyDeparture_RefundsUnusedNights()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(3));

        var settlement = StayCalculator.Settle(stay, TestHotel.Day(1).ToDateTime(new TimeOnly(11, 0)));

        Assert.True(settlement.IsEarly);
        Assert.Equal(60m, settlement.Charged);
        Assert.Equal(ReceiptKind.Refund, settlement.ReceiptKind);
        Assert.Equal(-120m, settlement.Difference);
        Assert.Equal(120m, settlement.ReceiptLines().Sum(l => l.Amount));
    }

    [Fact]
    public void Settle_EarlyDepartureAfterNoon_LateFeeReducesRefund()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(3));

        var settlement = StayCalculator.Settle(stay, TestHotel.Day(1).ToDateTime(new TimeOnly(15, 0)));

        Assert.Equal(-90m, settlement.Difference);
        Assert.Equal(90m, settlement.ReceiptLines().Sum(l => l.Amount));
    }

    [Fact]
    public void Settle_SameDayDeparture_ChargesOneNightWithoutLateFee()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(2));

        var settlement = StayCalculator.Settle(stay, TestHotel.Today.ToDateTime(new TimeOnly(20, 0)));

        Assert.Equal(1, settlement.ChargedNights);
        Assert.Equal(0m, settlement.LateFraction);
        Assert.Equal(-60m, settlement.Difference);
    }
}
