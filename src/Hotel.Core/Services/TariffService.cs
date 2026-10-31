using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Storage;

namespace Hotel.Core.Services;

public interface ITariffService
{
    /// <summary>Тарифы всех классов номеров в порядке классов.</summary>
    IReadOnlyList<Tariff> GetAll();

    decimal GetPrice(RoomClass roomClass);

    void Update(RoomClass roomClass, decimal pricePerPlace);
}

/// <summary>
/// Тарифы — фиксированный набор, по одному на класс номера: их можно изменить, но не добавить и не удалить.
/// Новая цена действует для новых броней и поселений; уже зафиксированные цены не меняются.
/// </summary>
public sealed class TariffService : ITariffService
{
    public const decimal MaxPrice = 10_000m;

    private readonly HotelContext _context;
    private readonly IAuditLog _log;

    public TariffService(HotelContext context, IAuditLog log)
    {
        _context = context;
        _log = log;
    }

    public IReadOnlyList<Tariff> GetAll() =>
        Enum.GetValues<RoomClass>()
            .Select(c => _context.Tariffs.FirstOrDefault(t => t.Class == c) ?? new Tariff { Class = c, PricePerPlace = 0 })
            .ToList();

    public decimal GetPrice(RoomClass roomClass)
    {
        var price = _context.Tariffs.FirstOrDefault(t => t.Class == roomClass)?.PricePerPlace ?? 0;
        return price > 0
            ? price
            : throw new HotelException($"Для класса «{roomClass.GetDescription()}» не задан тариф. Обратитесь к управляющему.");
    }

    public void Update(RoomClass roomClass, decimal pricePerPlace)
    {
        if (!Enum.IsDefined(roomClass))
            throw new HotelException("Неизвестный класс номера.");
        if (pricePerPlace <= 0 || pricePerPlace > MaxPrice)
            throw new HotelException($"Цена за место в сутки — больше 0 и не больше {MaxPrice:N2} {Money.Currency}.");
        if (pricePerPlace != Money.Round(pricePerPlace))
            throw new HotelException("Цена указывается с точностью до копеек.");

        var tariff = new Tariff { Class = roomClass, PricePerPlace = pricePerPlace };
        var index = _context.Tariffs.FindIndex(t => t.Class == roomClass);
        var old = index >= 0 ? _context.Tariffs[index].PricePerPlace : 0;
        if (index >= 0)
            _context.Tariffs[index] = tariff;
        else
            _context.Tariffs.Add(tariff);

        _context.SaveChanges();
        _log.Write($"Тариф «{roomClass.GetDescription()}»: {old:N2} → {pricePerPlace:N2} {Money.Currency} за место в сутки");
    }
}
