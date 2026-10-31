using Hotel.Core;
using Hotel.Core.Models;
using Hotel.Core.Services;
using Hotel.Views;

namespace Hotel.Presenters;

public sealed class LoginPresenter
{
    private readonly ILoginView _view;
    private readonly IAuthService _auth;

    public LoginPresenter(ILoginView view, IAuthService auth)
    {
        _view = view;
        _auth = auth;
        _view.LoginClicked += (_, _) => Login();
    }

    private void Login()
    {
        if (string.IsNullOrWhiteSpace(_view.Login) || string.IsNullOrEmpty(_view.Password))
        {
            _view.ShowError("Введите логин и пароль.");
            return;
        }

        try
        {
            _auth.Login(_view.Login, _view.Password);
            _view.Close(accepted: true);
        }
        catch (HotelException ex)
        {
            _view.ShowError(ex.Message);
        }
    }
}

/// <summary>
/// Подбор номера по датам, классу и числу мест. Из найденного номера — бронь или поселение (если приезд сегодня).
/// </summary>
public sealed class RoomSearchPresenter : Presenter, IDisposable
{
    private readonly IRoomSearchView _view;
    private readonly INavigator _navigator;

    public RoomSearchPresenter(IRoomSearchView view, AppServices services, INavigator navigator, IDialogService dialogs, bool walkIn)
        : base(services, dialogs)
    {
        _view = view;
        _navigator = navigator;
        _view.Arrival = Today;
        _view.Departure = Today.AddDays(1);
        _view.Places = 1;

        _view.SearchClicked += (_, _) => Search();
        _view.ReserveClicked += (_, _) => Reserve();
        _view.CheckInClicked += (_, _) => CheckIn();
        Services.Events.Changed += OnChanged;
        if (walkIn)
            Search();
        else
            _view.ShowRows([], "Укажите даты, класс и число мест и нажмите «Подобрать».");
    }

    public void Dispose() => Services.Events.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => Search();

    private void Search()
    {
        _view.SetCheckInEnabled(_view.Arrival == Today);
        var query = new RoomQuery { Arrival = _view.Arrival, Departure = _view.Departure, Class = _view.RoomClass, Places = _view.Places };
        if (!Try(() =>
            {
                var rows = Services.Rooms.FindAvailable(query).Select(AvailabilityRow.From).ToList();
                var nights = query.Departure.DayNumber - query.Arrival.DayNumber;
                _view.ShowRows(rows, rows.Count == 0
                    ? "Нет номеров со свободными местами на весь срок. Измените даты, класс или число мест."
                    : $"Подходящих номеров: {rows.Count}. Срок: {Format.Period(query.Arrival, query.Departure)}, суток: {nights}, мест: {query.Places}.");
            }, _view))
        {
            _view.ShowRows([], "Исправьте условия подбора.");
        }
    }

    private void Reserve()
    {
        if (_view.Selected is not { } row)
        {
            Dialogs.Error("Выберите номер в списке.");
            return;
        }

        var draft = new ReservationDetails
        {
            ContactName = string.Empty,
            Phone = string.Empty,
            RoomNumber = row.RoomNumber,
            Arrival = _view.Arrival,
            Departure = _view.Departure,
            Places = _view.Places
        };
        if (_navigator.EditReservation(null, draft))
            Dialogs.Info("Бронь оформлена. Она видна на вкладке «Брони».");
    }

    private void CheckIn()
    {
        if (_view.Arrival != Today)
        {
            Dialogs.Error("Поселить можно только с сегодняшнего дня. Для будущих дат оформите бронь.");
            return;
        }

        if (_view.Selected is not { } row)
        {
            Dialogs.Error("Выберите номер в списке.");
            return;
        }

        if (_navigator.CheckIn(new CheckInTarget(null, row.RoomNumber, _view.Departure)))
            _view.Close(accepted: true);
    }
}

/// <summary>Новая бронь или изменение брони; стоимость пересчитывается при изменении полей.</summary>
public sealed class ReservationEditPresenter : Presenter
{
    private readonly IReservationEditView _view;
    private readonly int? _number;

    public ReservationEditPresenter(IReservationEditView view, AppServices services, IDialogService dialogs, int? number,
        ReservationDetails? draft)
        : base(services, dialogs)
    {
        _view = view;
        _number = number;
        _view.SetRooms(Services.Rooms.GetAll()
            .Select(r => new Option<string>(r.Number, $"{r.Number} — {r.Class.GetDescription()}, мест: {r.Places}"))
            .ToList());

        if (number is { } n)
        {
            var reservation = Services.Reservations.Get(n);
            _view.Title = $"Бронь № {n}";
            draft = new ReservationDetails
            {
                ContactName = reservation.ContactName,
                Phone = reservation.Phone,
                RoomNumber = reservation.RoomNumber,
                Arrival = reservation.Arrival,
                Departure = reservation.Departure,
                Places = reservation.Places,
                Note = reservation.Note
            };
        }
        else
        {
            _view.Title = "Новая бронь";
        }

        _view.ContactName = draft?.ContactName ?? string.Empty;
        _view.Phone = string.IsNullOrEmpty(draft?.Phone) ? "+375" : draft.Phone;
        _view.RoomNumber = draft?.RoomNumber;
        _view.Arrival = draft?.Arrival ?? Today;
        _view.Departure = draft?.Departure ?? Today.AddDays(1);
        _view.Places = draft?.Places ?? 1;
        _view.Note = draft?.Note ?? string.Empty;

        _view.InputChanged += (_, _) => UpdateCost();
        _view.SaveClicked += (_, _) => Save();
        UpdateCost();
    }

    private ReservationDetails Details => new()
    {
        ContactName = _view.ContactName,
        Phone = _view.Phone,
        RoomNumber = _view.RoomNumber ?? string.Empty,
        Arrival = _view.Arrival,
        Departure = _view.Departure,
        Places = _view.Places,
        Note = _view.Note
    };

    private void UpdateCost()
    {
        var room = Services.Rooms.GetAll().FirstOrDefault(r => r.Number == _view.RoomNumber);
        var nights = _view.Departure.DayNumber - _view.Arrival.DayNumber;
        if (room is null || nights <= 0)
        {
            _view.SetCost("Выберите номер и даты.");
            return;
        }

        var price = _number is { } n && Services.Reservations.Get(n) is { } existing
                                     && Services.Rooms.Get(existing.RoomNumber).Class == room.Class
            ? existing.PricePerPlace
            : Services.Tariffs.GetPrice(room.Class);
        _view.SetCost($"Суток: {nights}, мест: {_view.Places}, цена за место: {Format.Money(price)}. " +
                      $"Сумма к оплате при заезде: {Format.Money(nights * _view.Places * price)}");
    }

    private void Save()
    {
        var details = Details;
        if (Try(() =>
            {
                if (_number is { } n)
                    Services.Reservations.Update(n, details);
                else
                    Services.Reservations.Create(details);
            }, _view))
        {
            _view.Close(accepted: true);
        }
    }
}

/// <summary>
/// Поселение: номер и даты заданы (по брони или из подбора), администратор добавляет гостей —
/// находит по паспорту или регистрирует нового — и оформляет поселение. Затем показываются квитанции.
/// </summary>
public sealed class CheckInPresenter : Presenter, IIssuesReceipts
{
    private readonly ICheckInView _view;
    private readonly INavigator _navigator;
    private readonly CheckInTarget _target;
    private readonly int _maxGuests;
    private readonly List<Guest> _guests = [];

    public CheckInPresenter(ICheckInView view, AppServices services, INavigator navigator, IDialogService dialogs, CheckInTarget target)
        : base(services, dialogs)
    {
        _view = view;
        _navigator = navigator;
        _target = target;

        if (target.ReservationNumber is { } number)
        {
            var reservation = Services.Reservations.Get(number);
            var room = Services.Rooms.Get(reservation.RoomNumber);
            _maxGuests = reservation.Places;
            _view.SetHeader($"Заселение по брони № {number} ({reservation.ContactName}, {reservation.Phone}){Environment.NewLine}" +
                            $"Номер {room.Number}, {room.Class.GetDescription()}; {Format.Period(reservation.Arrival, reservation.Departure)}, " +
                            $"суток: {reservation.Nights}; цена за место: {Format.Money(reservation.PricePerPlace)}; мест по брони: {reservation.Places}");
        }
        else
        {
            var room = Services.Rooms.Get(target.RoomNumber!);
            var departure = target.Departure!.Value;
            var nights = departure.DayNumber - Today.DayNumber;
            var price = Services.Tariffs.GetPrice(room.Class);
            var occupancy = Services.Rooms.FindAvailable(new RoomQuery { Arrival = Today, Departure = departure, Places = 1 })
                .FirstOrDefault(a => a.Room.Number == room.Number);
            _maxGuests = occupancy?.FreePlaces ?? 0;
            _view.SetHeader($"Поселение без брони{Environment.NewLine}" +
                            $"Номер {room.Number}, {room.Class.GetDescription()}; {Format.Period(Today, departure)}, суток: {nights}; " +
                            $"цена за место: {Format.Money(price)}; свободно мест: {_maxGuests}");
        }

        _view.FindClicked += (_, _) => Find();
        _view.RegisterClicked += (_, _) => Register();
        _view.RemoveClicked += (_, _) => Remove();
        _view.ConfirmClicked += (_, _) => Confirm();
        ShowGuests();
    }

    public IReadOnlyList<int> IssuedReceipts { get; private set; } = [];

    public string? Notice => null;

    private void Find()
    {
        var passport = _view.PassportText.Trim();
        if (passport.Length == 0)
        {
            _view.ShowError("Введите серию и номер паспорта гостя.");
            return;
        }

        var guest = Services.Guests.FindByPassport(passport);
        if (guest is null)
        {
            if (Dialogs.Confirm($"Гость с паспортом {passport} не найден. Зарегистрировать нового гостя?"))
                Register();
            return;
        }

        Add(guest);
    }

    private void Register()
    {
        var guest = _navigator.EditGuest(null, _view.PassportText.Trim());
        if (guest is not null)
            Add(guest);
    }

    private void Add(Guest guest)
    {
        if (_guests.Any(g => g.Id == guest.Id))
        {
            _view.ShowError($"{guest.FullName} уже в списке.");
            return;
        }

        if (_guests.Count >= _maxGuests)
        {
            _view.ShowError($"Больше гостей не поместится: мест {_maxGuests}.");
            return;
        }

        var living = Services.Guests.GetRows(GuestListFilter.Living).FirstOrDefault(r => r.Guest.Id == guest.Id);
        if (living is not null)
        {
            _view.ShowError($"{guest.FullName} уже проживает в номере {living.Stay!.RoomNumber}.");
            return;
        }

        _guests.Add(guest);
        _view.PassportText = string.Empty;
        ShowGuests();
    }

    private void Remove()
    {
        var index = _view.SelectedGuestIndex;
        if (index < 0 || index >= _guests.Count)
            return;

        _guests.RemoveAt(index);
        ShowGuests();
    }

    private void Confirm()
    {
        if (_guests.Count == 0)
        {
            _view.ShowError("Добавьте хотя бы одного гостя.");
            return;
        }

        CheckInResult? result = null;
        var request = new CheckInRequest
        {
            GuestIds = _guests.Select(g => g.Id).ToList(),
            ReservationNumber = _target.ReservationNumber,
            RoomNumber = _target.RoomNumber,
            Departure = _target.Departure
        };
        if (!Try(() => result = Services.Stays.CheckIn(request)))
            return;

        IssuedReceipts = result!.Receipts.Select(r => r.Number).ToList();
        _view.Close(accepted: true);
    }

    private void ShowGuests()
    {
        var rows = _guests.Select(g => new CheckInGuestRow(g.Id, g.FullName, Format.Date(g.BirthDate), g.Citizenship, g.PassportNumber))
            .ToList();
        _view.ShowGuests(rows, $"Гостей: {_guests.Count} из {_maxGuests}", _guests.Count < _maxGuests);
    }
}

/// <summary>Регистрация нового гостя или изменение паспортных данных.</summary>
public sealed class GuestEditPresenter : Presenter
{
    private readonly IGuestEditView _view;
    private readonly Guest? _guest;

    public GuestEditPresenter(IGuestEditView view, AppServices services, IDialogService dialogs, Guest? guest, string? passport)
        : base(services, dialogs)
    {
        _view = view;
        _guest = guest;
        _view.Title = guest is null ? "Регистрация гостя" : $"Гость: {guest.FullName}";
        _view.LastName = guest?.LastName ?? string.Empty;
        _view.FirstName = guest?.FirstName ?? string.Empty;
        _view.MiddleName = guest?.MiddleName ?? string.Empty;
        _view.BirthDate = guest?.BirthDate;
        _view.Citizenship = guest?.Citizenship ?? Guest.DefaultCitizenship;
        _view.PassportNumber = guest?.PassportNumber ?? passport ?? string.Empty;
        _view.PassportIssuedBy = guest?.PassportIssuedBy ?? string.Empty;
        _view.PassportIssueDate = guest?.PassportIssueDate;
        _view.Phone = guest?.Phone ?? string.Empty;
        _view.SaveClicked += (_, _) => Save();
    }

    /// <summary>Сохраненный гость (после успешного «Сохранить»).</summary>
    public Guest? Result { get; private set; }

    private void Save()
    {
        var guest = new Guest
        {
            Id = _guest?.Id ?? 0,
            LastName = _view.LastName,
            FirstName = _view.FirstName,
            MiddleName = _view.MiddleName,
            BirthDate = _view.BirthDate ?? default,
            Citizenship = _view.Citizenship,
            PassportNumber = _view.PassportNumber,
            PassportIssuedBy = _view.PassportIssuedBy,
            PassportIssueDate = _view.PassportIssueDate ?? default,
            Phone = _view.Phone is "" or "+" or "+375" ? null : _view.Phone
        };

        if (Try(() =>
            {
                if (_guest is null)
                {
                    Result = Services.Guests.Register(guest);
                }
                else
                {
                    Services.Guests.Update(guest);
                    Result = Services.Guests.Get(guest.Id);
                }
            }, _view))
        {
            _view.Close(accepted: true);
        }
    }
}

/// <summary>Продление проживания: новая дата отъезда и сумма доплаты.</summary>
public sealed class ExtendStayPresenter : Presenter, IIssuesReceipts
{
    private readonly IExtendStayView _view;
    private readonly Stay _stay;

    public ExtendStayPresenter(IExtendStayView view, AppServices services, IDialogService dialogs, int stayId)
        : base(services, dialogs)
    {
        _view = view;
        _stay = Services.Stays.Get(stayId);
        var guest = Services.Guests.Get(_stay.GuestId);
        _view.SetInfo($"{guest.FullName}, номер {_stay.RoomNumber}{Environment.NewLine}" +
                      $"Проживание: {Format.Period(_stay.Arrival, _stay.Departure)} (оплачено суток: {_stay.PaidNights}), " +
                      $"цена за место: {Format.Money(_stay.PricePerPlace)}");
        var from = _stay.Departure > Today ? _stay.Departure : Today;
        _view.NewDeparture = from.AddDays(1);
        _view.DepartureChanged += (_, _) => UpdateCost();
        _view.SaveClicked += (_, _) => Save();
        UpdateCost();
    }

    public IReadOnlyList<int> IssuedReceipts { get; private set; } = [];

    public string? Notice => null;

    private void UpdateCost()
    {
        var nights = _view.NewDeparture.DayNumber - _stay.Departure.DayNumber;
        _view.SetCost(nights <= 0
            ? "Новая дата отъезда должна быть позже текущей."
            : $"Продление на {nights} сут. К оплате: {Format.Money(Services.Stays.ExtensionCost(_stay.Id, _view.NewDeparture))}");
    }

    private void Save()
    {
        Receipt? receipt = null;
        if (!Try(() => receipt = Services.Stays.Extend(_stay.Id, _view.NewDeparture)))
            return;

        IssuedReceipts = [receipt!.Number];
        _view.Close(accepted: true);
    }
}

/// <summary>
/// Выписка: показывает окончательный расчет (оплачено, начислено, задержка, доплата или возврат) и после
/// подтверждения освобождает место. Если нужна дополнительная квитанция — открывает ее.
/// </summary>
public sealed class CheckOutPresenter : Presenter, IIssuesReceipts
{
    private readonly ICheckOutView _view;
    private readonly int _stayId;

    public CheckOutPresenter(ICheckOutView view, AppServices services, IDialogService dialogs, int stayId)
        : base(services, dialogs)
    {
        _view = view;
        _stayId = stayId;

        var stay = Services.Stays.Get(stayId);
        var guest = Services.Guests.Get(stay.GuestId);
        var s = Services.Stays.PreviewCheckOut(stayId);
        var kind = s.IsEarly ? "Досрочный отъезд" : stay.IsOverdue(Today) ? "Выписка (выезд позже срока)" : "Выписка";
        _view.SetTitle($"{kind}: {guest.FullName}, номер {stay.RoomNumber}");

        List<(string, string)> lines =
        [
            ("Проживание по плану", $"{Format.Period(stay.Arrival, stay.Departure)}, суток: {s.PaidNights}"),
            ("Фактический выезд", Format.DateTime(s.CheckOutAt)),
            ("Цена за место в сутки", Format.Money(s.PricePerPlace)),
            ("Оплачено", Format.Money(s.Paid)),
            ("Начислено за проживание", $"{s.ChargedNights} сут. = {Format.Money(s.ChargedNights * s.PricePerPlace)}"),
            ("Задержка выезда (расчетный час 12:00)", s.LateFraction == 0 ? "нет" : $"{s.LateDescription} = {Format.Money(s.LateFee)}"),
            ("Итого начислено", Format.Money(s.Charged))
        ];
        var total = s.Difference switch
        {
            > 0 => $"Доплата гостя: {Format.Money(s.Difference)} — будет выписана квитанция «Доплата»",
            < 0 => $"Возврат гостю: {Format.Money(-s.Difference)} — будет выписана квитанция «Возврат»",
            _ => "Расчет сошелся: дополнительная квитанция не нужна"
        };
        _view.SetSettlement(lines, total);
        _view.ConfirmClicked += (_, _) => Confirm();
    }

    public IReadOnlyList<int> IssuedReceipts { get; private set; } = [];

    public string? Notice { get; private set; }

    private void Confirm()
    {
        CheckOutResult? result = null;
        if (!Try(() => result = Services.Stays.CheckOut(_stayId)))
            return;

        if (result!.Receipt is { } receipt)
            IssuedReceipts = [receipt.Number];
        else
            Notice = "Гость выписан, место освобождено. Расчет сошелся — дополнительная квитанция не нужна.";
        _view.Close(accepted: true);
    }
}

/// <summary>Просмотр квитанции (одной или нескольких, например при заселении семьи) и печать.</summary>
public sealed class ReceiptPresenter : Presenter
{
    private readonly IReceiptView _view;
    private readonly IReceiptPrinter _printer;

    public ReceiptPresenter(IReceiptView view, AppServices services, IDialogService dialogs, IReceiptPrinter printer,
        IReadOnlyList<int> numbers)
        : base(services, dialogs)
    {
        _view = view;
        _printer = printer;
        _view.ShowList(numbers.Select(n =>
        {
            var card = Services.Receipts.GetCard(n);
            return new Option<int>(n, $"№ {n} — {card.Guest.ShortName}, {Format.Money(card.Receipt.Total)}");
        }).ToList());
        _view.SelectionChanged += (_, _) => ShowSelected();
        _view.PrintClicked += (_, _) => Print();
        ShowSelected();
    }

    private void ShowSelected()
    {
        if (_view.SelectedNumber is { } number)
            Try(() => _view.ShowCard(Services.Receipts.GetCard(number)));
    }

    private void Print()
    {
        if (_view.SelectedNumber is not { } number)
            return;

        var card = Services.Receipts.GetCard(number);
        try
        {
            if (_printer.Print(card))
                Services.Log.Write($"Напечатана квитанция № {number}");
        }
        catch (InvalidOperationException ex)
        {
            Dialogs.Error($"Не удалось напечатать квитанцию: {ex.Message}");
        }
    }
}

/// <summary>Печать квитанции; в тестах — подделка.</summary>
public interface IReceiptPrinter
{
    /// <summary>Показывает диалог печати и печатает. <c>false</c> — пользователь отменил.</summary>
    bool Print(ReceiptCard card);
}
