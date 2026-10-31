using Hotel.Core.Models;

namespace Hotel.Core.Billing;

/// <summary>
/// Окончательный расчет при выезде. Обычный, поздний и досрочный отъезд считаются одной процедурой,
/// отличается только итог: доплата (<see cref="Difference"/> &gt; 0), возврат (&lt; 0) или ноль.
/// </summary>
public sealed record Settlement
{
    public required DateTime CheckOutAt { get; init; }
    public required decimal PricePerPlace { get; init; }

    /// <summary>Оплаченные сутки (по плановую дату отъезда).</summary>
    public required int PaidNights { get; init; }

    /// <summary>Фактически начисленные сутки: от приезда до даты выезда, не меньше одних.</summary>
    public required int ChargedNights { get; init; }

    /// <summary>Доля суток за задержку выезда после расчетного часа: 0, 0,5 или 1.</summary>
    public required decimal LateFraction { get; init; }

    public decimal Paid => Money.Round(PaidNights * PricePerPlace);

    public decimal LateFee => Money.Round(LateFraction * PricePerPlace);

    public decimal Charged => Money.Round(ChargedNights * PricePerPlace) + LateFee;

    /// <summary>Положительная — доплата гостя, отрицательная — возврат гостю.</summary>
    public decimal Difference => Charged - Paid;

    public bool IsEarly => ChargedNights < PaidNights;

    /// <summary>Вид дополнительной квитанции или <c>null</c>, если расчет сошелся.</summary>
    public ReceiptKind? ReceiptKind => Difference switch
    {
        > 0 => Models.ReceiptKind.Surcharge,
        < 0 => Models.ReceiptKind.Refund,
        _ => null
    };

    public string LateDescription => LateFraction switch
    {
        0 => "без задержки",
        < 1 => $"до {StayCalculator.LateHalfDayUntil:HH\\:mm} — 50 % суток",
        _ => $"после {StayCalculator.LateHalfDayUntil:HH\\:mm} — полные сутки"
    };

    /// <summary>Строки дополнительной квитанции. У возврата суммы положительные — это выдается гостю.</summary>
    public IReadOnlyList<ReceiptLine> ReceiptLines()
    {
        var lines = new List<ReceiptLine>();
        var nights = ChargedNights - PaidNights;
        var sign = Difference < 0 ? -1 : 1;

        if (nights > 0)
            lines.Add(new ReceiptLine("Проживание сверх оплаченного срока, суток", nights, PricePerPlace));
        else if (nights < 0)
            lines.Add(new ReceiptLine("Возврат за неиспользованные сутки, суток", -nights, PricePerPlace));

        if (LateFraction > 0)
            lines.Add(new ReceiptLine($"Задержка выезда {LateDescription}", sign * LateFraction, PricePerPlace));

        return lines;
    }
}

/// <summary>Расчет суток и задержки выезда. Расчетный час — 12:00.</summary>
public static class StayCalculator
{
    public static readonly TimeOnly CheckOutHour = new(12, 0);
    public static readonly TimeOnly LateHalfDayUntil = new(18, 0);
    public const int MaxNights = 90;

    /// <summary>Число суток между датами: с 01.11 по 04.11 — 3 суток.</summary>
    public static int Nights(DateOnly arrival, DateOnly departure) => departure.DayNumber - arrival.DayNumber;

    public static decimal Cost(int nights, int places, decimal pricePerPlace) => Money.Round(nights * places * pricePerPlace);

    /// <summary>Доля суток за выезд в <paramref name="time"/>: до 12:00 — 0, до 18:00 — 0,5, позже — 1.</summary>
    public static decimal LateFraction(TimeOnly time) =>
        time <= CheckOutHour ? 0m : time <= LateHalfDayUntil ? 0.5m : 1m;

    public static Settlement Settle(Stay stay, DateTime checkOutAt)
    {
        var date = DateOnly.FromDateTime(checkOutAt);
        var nights = Nights(stay.Arrival, date);

        // Выезд в день приезда — минимум одни сутки, задержка не начисляется: сутки уже покрывают этот день.
        var late = nights >= 1 ? LateFraction(TimeOnly.FromDateTime(checkOutAt)) : 0m;

        return new Settlement
        {
            CheckOutAt = checkOutAt,
            PricePerPlace = stay.PricePerPlace,
            PaidNights = stay.PaidNights,
            ChargedNights = Math.Max(1, nights),
            LateFraction = late
        };
    }
}
