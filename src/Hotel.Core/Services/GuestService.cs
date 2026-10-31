using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Storage;
using Hotel.Core.Validation;

namespace Hotel.Core.Services;

public enum GuestListFilter
{
    Living,
    CheckedOut,
    All
}

/// <summary>
/// Строка списка гостей: гость и одно его проживание. У гостя без проживаний <see cref="Stay"/> — <c>null</c>.
/// </summary>
public sealed record GuestRow(Guest Guest, Stay? Stay, Room? Room);

public interface IGuestService
{
    Guest Get(int id);

    /// <summary>Гость по серии и номеру паспорта или <c>null</c>.</summary>
    Guest? FindByPassport(string passport);

    /// <summary>Список гостей: каждое проживание — строка; свежие сверху.</summary>
    IReadOnlyList<GuestRow> GetRows(GuestListFilter filter);

    /// <summary>Регистрирует гостя; <see cref="Guest.Id"/> из параметра не используется — присваивается новый.</summary>
    Guest Register(Guest guest);

    void Update(Guest guest);

    void Delete(int id);
}

public sealed class GuestService : IGuestService
{
    private readonly HotelContext _context;
    private readonly IClock _clock;
    private readonly IAuditLog _log;
    private readonly GuestValidator _validator = new();

    public GuestService(HotelContext context, IClock clock, IAuditLog log)
    {
        _context = context;
        _clock = clock;
        _log = log;
    }

    public Guest Get(int id) =>
        _context.Guests.FirstOrDefault(g => g.Id == id)
        ?? throw new HotelException($"Гость с кодом {id} не найден.");

    public Guest? FindByPassport(string passport)
    {
        var normalized = GuestValidator.NormalizePassport(passport);
        return normalized.Length == 0 ? null : _context.Guests.FirstOrDefault(g => g.PassportNumber == normalized);
    }

    public IReadOnlyList<GuestRow> GetRows(GuestListFilter filter)
    {
        var rooms = _context.Rooms.ToDictionary(r => r.Number);
        var stays = _context.Stays.ToLookup(s => s.GuestId);
        var rows = new List<GuestRow>();
        foreach (var guest in _context.Guests)
        {
            var guestStays = stays[guest.Id].ToList();
            if (guestStays.Count == 0 && filter == GuestListFilter.All)
                rows.Add(new GuestRow(guest, null, null));

            rows.AddRange(guestStays
                .Where(s => filter switch
                {
                    GuestListFilter.Living => s.IsActive,
                    GuestListFilter.CheckedOut => !s.IsActive,
                    _ => true
                })
                .Select(s => new GuestRow(guest, s, rooms.GetValueOrDefault(s.RoomNumber))));
        }

        return rows
            .OrderByDescending(r => r.Stay?.IsActive ?? false)
            .ThenByDescending(r => r.Stay?.Arrival ?? DateOnly.MinValue)
            .ThenBy(r => r.Guest.FullName, StringComparer.CurrentCulture)
            .ToList();
    }

    public Guest Register(Guest guest)
    {
        guest = Normalize(guest with { Id = HotelContext.NextId(_context.Guests, g => g.Id) });
        Validate(guest);

        _context.Guests.Add(guest);
        _context.SaveChanges();
        _log.Write($"Зарегистрирован гость {guest}");
        return guest;
    }

    public void Update(Guest guest)
    {
        var existing = Get(guest.Id);
        guest = Normalize(guest);
        Validate(guest);

        _context.Guests[_context.Guests.IndexOf(existing)] = guest;
        _context.SaveChanges();
        _log.Write($"Изменены данные гостя {existing} → {guest}");
    }

    public void Delete(int id)
    {
        var guest = Get(id);
        if (_context.Stays.Any(s => s.GuestId == id))
            throw new HotelException($"У гостя {guest.FullName} есть проживания и квитанции, удалить его нельзя.");

        _context.Guests.Remove(guest);
        _context.SaveChanges();
        _log.Write($"Удален гость {guest}");
    }

    private void Validate(Guest guest)
    {
        var errors = _validator.Validate(guest, _clock.Today).ToList();
        var twin = FindByPassport(guest.PassportNumber);
        if (twin is not null && twin.Id != guest.Id)
            errors.Add(new(nameof(Guest.PassportNumber), $"Паспорт {guest.PassportNumber} уже зарегистрирован: {twin.FullName}."));
        if (errors.Count > 0)
            throw new DataValidationException(errors);
    }

    private static Guest Normalize(Guest guest) => guest with
    {
        LastName = guest.LastName.Trim(),
        FirstName = guest.FirstName.Trim(),
        MiddleName = string.IsNullOrWhiteSpace(guest.MiddleName) ? null : guest.MiddleName.Trim(),
        Citizenship = guest.Citizenship.Trim(),
        PassportNumber = GuestValidator.NormalizePassport(guest.PassportNumber),
        PassportIssuedBy = guest.PassportIssuedBy.Trim(),
        Phone = string.IsNullOrWhiteSpace(guest.Phone) ? null : PhoneFormat.Normalize(guest.Phone)
    };
}
