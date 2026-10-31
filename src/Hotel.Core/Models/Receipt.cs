using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Hotel.Core.Models;

public enum ReceiptKind
{
    [Description("Проживание")]
    Accommodation,

    [Description("Продление")]
    Extension,

    [Description("Доплата")]
    Surcharge,

    [Description("Возврат")]
    Refund
}

/// <summary>Строка квитанции: за что, сколько, по какой цене. Сумма может быть отрицательной (зачет).</summary>
public sealed record ReceiptLine(string Description, decimal Quantity, decimal Price)
{
    [JsonIgnore]
    public decimal Amount => Money.Round(Quantity * Price);
}

/// <summary>
/// Квитанция. Не изменяется и не удаляется: ошибка исправляется новой квитанцией.
/// У возврата <see cref="Total"/> — сумма к выдаче гостю.
/// </summary>
public sealed record Receipt
{
    public required int Number { get; init; }
    public required int StayId { get; init; }
    public required ReceiptKind Kind { get; init; }
    public required IReadOnlyList<ReceiptLine> Lines { get; init; }
    public required DateTime IssuedAt { get; init; }
    public required string IssuedBy { get; init; }

    [JsonIgnore]
    public decimal Total => Lines.Sum(l => l.Amount);

    /// <summary>Сумма со знаком: поступление в кассу положительно, возврат — отрицателен.</summary>
    [JsonIgnore]
    public decimal SignedTotal => Kind == ReceiptKind.Refund ? -Total : Total;

    public override string ToString() => $"Квитанция № {Number} ({Kind.GetDescription()}) на {Total:N2} BYN";
}

public static class Money
{
    public const string Currency = "BYN";

    /// <summary>Округление до копеек по арифметическому правилу.</summary>
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
