using System.ComponentModel;

namespace Hotel.Core.Models;

public enum RoomClass
{
    [Description("Эконом")]
    Economy,

    [Description("Стандарт")]
    Standard,

    [Description("Улучшенный")]
    Superior,

    [Description("Полулюкс")]
    JuniorSuite,

    [Description("Люкс")]
    Suite
}

/// <summary>
/// Номер гостиницы. Ключ — номер комнаты. Гости селятся по местам: в номере не больше <see cref="Places"/> гостей.
/// </summary>
public sealed record Room
{
    public required string Number { get; init; }
    public required int Floor { get; init; }
    public required RoomClass Class { get; init; }
    public required int Places { get; init; }
    public string? Description { get; init; }

    public override string ToString() => $"№ {Number} ({Class.GetDescription()}, мест: {Places})";
}

/// <summary>Цена за место в сутки для класса номера (BYN).</summary>
public sealed record Tariff
{
    public required RoomClass Class { get; init; }
    public required decimal PricePerPlace { get; init; }
}
