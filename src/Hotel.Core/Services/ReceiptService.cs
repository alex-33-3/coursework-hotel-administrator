using Hotel.Core.Models;
using Hotel.Core.Storage;

namespace Hotel.Core.Services;

/// <summary>Все сведения для печати квитанции: проживание, гость и номер.</summary>
public sealed record ReceiptCard(Receipt Receipt, Stay Stay, Guest Guest, Room? Room)
{
    public string Title => $"Квитанция № {Receipt.Number}";

    public string KindName => Receipt.Kind.GetDescription();

    public string RoomText => Room is null ? Stay.RoomNumber : $"{Room.Number} ({Room.Class.GetDescription()})";
}

/// <summary>Итог квитанций за период: поступило по видам, возвращено, всего.</summary>
public sealed record ReceiptSummary(decimal Accommodation, decimal Extension, decimal Surcharge, decimal Refund)
{
    public decimal Net => Accommodation + Extension + Surcharge - Refund;
}

public interface IReceiptService
{
    ReceiptCard GetCard(int number);

    IReadOnlyList<Receipt> GetForStay(int stayId);

    /// <summary>Квитанции гостя по всем его проживаниям.</summary>
    IReadOnlyList<Receipt> GetForGuest(int guestId);

    /// <summary>Квитанции, выписанные в даты с <paramref name="from"/> по <paramref name="to"/> включительно.</summary>
    IReadOnlyList<Receipt> Find(DateOnly from, DateOnly to);

    ReceiptSummary Summarize(IEnumerable<Receipt> receipts);
}

/// <summary>Чтение квитанций. Выписывает их <see cref="StayService"/> — при поселении, продлении и выезде.</summary>
public sealed class ReceiptService : IReceiptService
{
    private readonly HotelContext _context;

    public ReceiptService(HotelContext context)
    {
        _context = context;
    }

    public ReceiptCard GetCard(int number)
    {
        var receipt = _context.Receipts.FirstOrDefault(r => r.Number == number)
                      ?? throw new HotelException($"Квитанция № {number} не найдена.");
        var stay = _context.Stays.FirstOrDefault(s => s.Id == receipt.StayId)
                   ?? throw new HotelException($"Для квитанции № {number} не найдено проживание.");
        var guest = _context.Guests.FirstOrDefault(g => g.Id == stay.GuestId)
                    ?? throw new HotelException($"Для квитанции № {number} не найден гость.");
        return new ReceiptCard(receipt, stay, guest, _context.Rooms.FirstOrDefault(r => r.Number == stay.RoomNumber));
    }

    public IReadOnlyList<Receipt> GetForStay(int stayId) =>
        _context.Receipts.Where(r => r.StayId == stayId).OrderBy(r => r.Number).ToList();

    public IReadOnlyList<Receipt> GetForGuest(int guestId)
    {
        var stays = _context.Stays.Where(s => s.GuestId == guestId).Select(s => s.Id).ToHashSet();
        return _context.Receipts.Where(r => stays.Contains(r.StayId)).OrderBy(r => r.Number).ToList();
    }

    public IReadOnlyList<Receipt> Find(DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new HotelException("Дата «по» не может быть раньше даты «с».");

        return _context.Receipts
            .Where(r => DateOnly.FromDateTime(r.IssuedAt) >= from && DateOnly.FromDateTime(r.IssuedAt) <= to)
            .OrderBy(r => r.Number)
            .ToList();
    }

    public ReceiptSummary Summarize(IEnumerable<Receipt> receipts)
    {
        var list = receipts.ToList();
        decimal Sum(ReceiptKind kind) => list.Where(r => r.Kind == kind).Sum(r => r.Total);
        return new ReceiptSummary(Sum(ReceiptKind.Accommodation), Sum(ReceiptKind.Extension), Sum(ReceiptKind.Surcharge),
            Sum(ReceiptKind.Refund));
    }
}
