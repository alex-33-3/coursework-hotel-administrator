using Hotel.Core.Billing;
using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Occupancy;
using Hotel.Core.Storage;
using Hotel.Core.Validation;

namespace Hotel.Core.Services;

/// <summary>
/// Поселение. По брони — номер, даты и цена берутся из брони, <see cref="RoomNumber"/> и <see cref="Departure"/> не нужны.
/// Без брони — в номер <see cref="RoomNumber"/> с сегодняшнего дня по <see cref="Departure"/>.
/// </summary>
public sealed record CheckInRequest
{
    public required IReadOnlyList<int> GuestIds { get; init; }
    public int? ReservationNumber { get; init; }
    public string? RoomNumber { get; init; }
    public DateOnly? Departure { get; init; }
}

public sealed record CheckInResult(IReadOnlyList<Stay> Stays, IReadOnlyList<Receipt> Receipts);

public sealed record CheckOutResult(Settlement Settlement, Receipt? Receipt);

public interface IStayService
{
    Stay Get(int id);

    /// <summary>Отъезд сегодня: проживающие с плановой датой отъезда сегодня и не выехавшие в срок (они первыми).</summary>
    IReadOnlyList<GuestRow> GetDeparturesToday();

    CheckInResult CheckIn(CheckInRequest request);

    /// <summary>Продлевает проживание до <paramref name="newDeparture"/> и выписывает квитанцию «Продление».</summary>
    Receipt Extend(int stayId, DateOnly newDeparture);

    /// <summary>Стоимость продления до <paramref name="newDeparture"/> без проверок и сохранения — для предварительного показа.</summary>
    decimal ExtensionCost(int stayId, DateOnly newDeparture);

    /// <summary>Расчет при выезде сейчас — для предварительного показа, ничего не сохраняет.</summary>
    Settlement PreviewCheckOut(int stayId);

    /// <summary>
    /// Выписка (в том числе досрочная и с задержкой): окончательный расчет, дополнительная квитанция
    /// «Доплата» или «Возврат», если расчет не сошелся, и освобождение места.
    /// </summary>
    CheckOutResult CheckOut(int stayId);
}

public sealed class StayService : IStayService
{
    private readonly HotelContext _context;
    private readonly OccupancyCalculator _occupancy;
    private readonly ITariffService _tariffs;
    private readonly IUserContext _user;
    private readonly IClock _clock;
    private readonly IAuditLog _log;

    public StayService(HotelContext context, OccupancyCalculator occupancy, ITariffService tariffs, IUserContext user,
        IClock clock, IAuditLog log)
    {
        _context = context;
        _occupancy = occupancy;
        _tariffs = tariffs;
        _user = user;
        _clock = clock;
        _log = log;
    }

    public Stay Get(int id) =>
        _context.Stays.FirstOrDefault(s => s.Id == id)
        ?? throw new HotelException($"Регистрационная карточка № {id} не найдена.");

    public IReadOnlyList<GuestRow> GetDeparturesToday()
    {
        var today = _clock.Today;
        return _context.Stays
            .Where(s => s.IsActive && s.Departure <= today)
            .OrderBy(s => s.Departure)
            .ThenBy(s => s.RoomNumber, RoomNumberComparer.Instance)
            .Select(ToRow)
            .ToList();
    }

    public CheckInResult CheckIn(CheckInRequest request)
    {
        var today = _clock.Today;
        var guests = GetGuestsForCheckIn(request.GuestIds);

        Room room;
        DateOnly departure;
        decimal price;
        Reservation? reservation = null;
        if (request.ReservationNumber is { } number)
        {
            reservation = _context.Reservations.FirstOrDefault(r => r.Number == number)
                          ?? throw new HotelException($"Бронь № {number} не найдена.");
            if (reservation.Status != ReservationStatus.Reserved)
                throw new HotelException($"{reservation} в состоянии «{reservation.Status.GetDescription()}», заселить по ней нельзя.");
            if (reservation.Arrival != today)
                throw new HotelException($"Заселение по брони № {number} — только в день приезда, {reservation.Arrival:dd.MM.yyyy}.");
            if (guests.Count > reservation.Places)
                throw new HotelException($"По брони № {number} забронировано мест: {reservation.Places}, а гостей {guests.Count}.");

            room = GetRoom(reservation.RoomNumber);
            departure = reservation.Departure;
            price = reservation.PricePerPlace;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RoomNumber) || request.Departure is null)
                throw new HotelException("Выберите номер и дату отъезда.");

            var errors = new List<ValidationError>();
            StayDates.Validate(errors, today, request.Departure.Value, today, today);
            if (errors.Count > 0)
                throw new DataValidationException(errors);

            room = GetRoom(request.RoomNumber);
            departure = request.Departure.Value;
            price = _tariffs.GetPrice(room.Class);
        }

        // Места самой брони при заселении по ней освобождаются, поэтому она в занятости не учитывается.
        var free = _occupancy.FreePlaces(room, today, departure, new OccupancyExclusion(ReservationNumber: reservation?.Number));
        if (free < guests.Count)
            throw new HotelException(
                $"В номере {room.Number} на {today:dd.MM.yyyy}–{departure:dd.MM.yyyy} свободно мест: {free}, а гостей {guests.Count}.");

        var stays = new List<Stay>();
        var receipts = new List<Receipt>();
        foreach (var guest in guests)
        {
            var stay = new Stay(HotelContext.NextId(_context.Stays, s => s.Id), guest.Id, room.Number, reservation?.Number,
                today, departure, price);
            _context.Stays.Add(stay);
            stays.Add(stay);

            var line = new ReceiptLine(
                $"Проживание в номере {room.Number} ({room.Class.GetDescription()}) с {today:dd.MM.yyyy} по {departure:dd.MM.yyyy}, суток",
                stay.PaidNights, price);
            receipts.Add(AddReceipt(stay, ReceiptKind.Accommodation, [line]));
        }

        reservation?.SetStatus(ReservationStatus.CheckedIn);
        _context.SaveChanges();

        foreach (var (stay, receipt) in stays.Zip(receipts))
            _log.Write($"Поселение: {guests.First(g => g.Id == stay.GuestId).FullName} — {stay}" +
                       (reservation is null ? string.Empty : $" (бронь № {reservation.Number})") +
                       $", квитанция № {receipt.Number} на {receipt.Total:N2} {Money.Currency}");
        return new CheckInResult(stays, receipts);
    }

    public decimal ExtensionCost(int stayId, DateOnly newDeparture)
    {
        var stay = Get(stayId);
        var nights = StayCalculator.Nights(stay.Departure, newDeparture);
        return nights > 0 ? StayCalculator.Cost(nights, 1, stay.PricePerPlace) : 0;
    }

    public Receipt Extend(int stayId, DateOnly newDeparture)
    {
        var stay = Get(stayId);
        if (!stay.IsActive)
            throw new HotelException("Гость уже выехал, продлить проживание нельзя.");
        if (newDeparture <= stay.Departure)
            throw new HotelException($"Новая дата отъезда должна быть позже {stay.Departure:dd.MM.yyyy}.");

        var today = _clock.Today;
        if (newDeparture <= today)
            throw new HotelException("Новая дата отъезда должна быть позже сегодняшней.");
        if (StayCalculator.Nights(stay.Arrival, newDeparture) > StayCalculator.MaxNights)
            throw new HotelException($"Срок проживания — не больше {StayCalculator.MaxNights} суток.");

        // Прошедшие ночи у гостя, не выехавшего в срок, уже заняты им самим: проверяются ночи с сегодняшней.
        var room = GetRoom(stay.RoomNumber);
        var from = stay.Departure > today ? stay.Departure : today;
        if (_occupancy.FreePlaces(room, from, newDeparture, new OccupancyExclusion(StayId: stay.Id)) < 1)
            throw new HotelException(
                $"В номере {room.Number} нет свободного места на {from:dd.MM.yyyy}–{newDeparture:dd.MM.yyyy} (места заняты или забронированы). " +
                "Продлить проживание в этом номере нельзя.");

        var oldDeparture = stay.Departure;
        var nights = StayCalculator.Nights(oldDeparture, newDeparture);
        stay.Extend(newDeparture);
        var receipt = AddReceipt(stay, ReceiptKind.Extension,
            [new ReceiptLine($"Продление проживания в номере {room.Number} с {oldDeparture:dd.MM.yyyy} по {newDeparture:dd.MM.yyyy}, суток",
                nights, stay.PricePerPlace)]);
        _context.SaveChanges();
        _log.Write($"Продление: {GuestName(stay)} — {stay}, квитанция № {receipt.Number} на {receipt.Total:N2} {Money.Currency}");
        return receipt;
    }

    public Settlement PreviewCheckOut(int stayId)
    {
        var stay = Get(stayId);
        if (!stay.IsActive)
            throw new HotelException("Гость уже выехал.");
        return StayCalculator.Settle(stay, _clock.Now);
    }

    public CheckOutResult CheckOut(int stayId)
    {
        var settlement = PreviewCheckOut(stayId);
        var stay = Get(stayId);

        Receipt? receipt = null;
        if (settlement.ReceiptKind is { } kind)
            receipt = AddReceipt(stay, kind, settlement.ReceiptLines());

        stay.CheckOut(settlement.CheckOutAt);
        _context.SaveChanges();

        var kindText = settlement.IsEarly ? "Досрочный отъезд" : "Выписка";
        var receiptText = receipt is null
            ? "без дополнительной квитанции"
            : $"квитанция № {receipt.Number} «{receipt.Kind.GetDescription()}» на {receipt.Total:N2} {Money.Currency}";
        _log.Write($"{kindText}: {GuestName(stay)} — {stay}, начислено суток: {settlement.ChargedNights}, " +
                   $"задержка: {settlement.LateDescription}, {receiptText}");
        return new CheckOutResult(settlement, receipt);
    }

    private List<Guest> GetGuestsForCheckIn(IReadOnlyList<int> guestIds)
    {
        if (guestIds.Count == 0)
            throw new HotelException("Добавьте хотя бы одного гостя.");
        if (guestIds.Distinct().Count() != guestIds.Count)
            throw new HotelException("Один и тот же гость указан дважды.");

        var guests = new List<Guest>();
        foreach (var id in guestIds)
        {
            var guest = _context.Guests.FirstOrDefault(g => g.Id == id)
                        ?? throw new HotelException($"Гость с кодом {id} не найден.");
            var active = _context.Stays.FirstOrDefault(s => s.GuestId == id && s.IsActive);
            if (active is not null)
                throw new HotelException($"Гость {guest.FullName} уже проживает в номере {active.RoomNumber}.");
            guests.Add(guest);
        }

        return guests;
    }

    private Receipt AddReceipt(Stay stay, ReceiptKind kind, IReadOnlyList<ReceiptLine> lines)
    {
        var receipt = new Receipt
        {
            Number = HotelContext.NextId(_context.Receipts, r => r.Number),
            StayId = stay.Id,
            Kind = kind,
            Lines = lines,
            IssuedAt = _clock.Now,
            IssuedBy = _user.CurrentUserName
        };
        _context.Receipts.Add(receipt);
        return receipt;
    }

    private Room GetRoom(string number) =>
        _context.Rooms.FirstOrDefault(r => r.Number == number) ?? throw new HotelException($"Номер {number} не найден.");

    private string GuestName(Stay stay) => _context.Guests.FirstOrDefault(g => g.Id == stay.GuestId)?.FullName ?? $"гость {stay.GuestId}";

    private GuestRow ToRow(Stay stay) =>
        new(_context.Guests.First(g => g.Id == stay.GuestId), stay, _context.Rooms.FirstOrDefault(r => r.Number == stay.RoomNumber));
}
