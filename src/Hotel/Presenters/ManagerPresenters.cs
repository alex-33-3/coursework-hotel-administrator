using Hotel.Core.Models;
using Hotel.Core.Validation;
using Hotel.Views;

namespace Hotel.Presenters;

/// <summary>Реестр квитанций за период (управляющий) или квитанции одного гостя.</summary>
public sealed class ReceiptsPresenter : Presenter
{
    private readonly IReceiptsView _view;
    private readonly INavigator _navigator;
    private readonly int? _guestId;

    public ReceiptsPresenter(IReceiptsView view, AppServices services, INavigator navigator, IDialogService dialogs, int? guestId)
        : base(services, dialogs)
    {
        _view = view;
        _navigator = navigator;
        _guestId = guestId;
        _view.Title = guestId is { } id ? $"Квитанции гостя: {Services.Guests.Get(id).FullName}" : "Квитанции за период";
        _view.SetPeriodVisible(guestId is null);
        _view.From = Today;
        _view.To = Today;
        _view.SearchClicked += (_, _) => Search();
        _view.OpenClicked += (_, _) => Open();
        Search();
    }

    private void Search() => Try(() =>
    {
        var receipts = _guestId is { } id ? Services.Receipts.GetForGuest(id) : Services.Receipts.Find(_view.From, _view.To);
        var stays = new Dictionary<int, (string Guest, string Room)>();
        foreach (var stayId in receipts.Select(r => r.StayId).Distinct())
        {
            var stay = Services.Stays.Get(stayId);
            stays[stayId] = (Services.Guests.Get(stay.GuestId).ShortName, stay.RoomNumber);
        }

        var rows = receipts.Select(r => new ReceiptRow(r.Number, r.Number, Format.DateTime(r.IssuedAt), r.Kind.GetDescription(),
                stays[r.StayId].Guest, stays[r.StayId].Room,
                Format.Money(r.SignedTotal), r.IssuedBy))
            .ToList();
        var s = Services.Receipts.Summarize(receipts);
        _view.ShowRows(rows,
            $"Квитанций: {rows.Count}. Проживание: {Format.Money(s.Accommodation)}; продление: {Format.Money(s.Extension)}; " +
            $"доплаты: {Format.Money(s.Surcharge)}; возвраты: {Format.Money(s.Refund)}; итого: {Format.Money(s.Net)}");
    });

    private void Open()
    {
        if (_view.Selected is { } row)
            _navigator.ShowReceipts([row.Number]);
        else
            Dialogs.Error("Выберите квитанцию в списке.");
    }
}

/// <summary>Номерной фонд (управляющий).</summary>
public sealed class RoomsPresenter : Presenter, IDisposable
{
    private readonly IRoomsView _view;
    private readonly INavigator _navigator;

    public RoomsPresenter(IRoomsView view, AppServices services, INavigator navigator, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _navigator = navigator;
        _view.AddClicked += (_, _) => _navigator.EditRoom(null);
        _view.EditClicked += (_, _) => WithSelected(r => _navigator.EditRoom(Services.Rooms.Get(r.Number)));
        _view.DeleteClicked += (_, _) => WithSelected(Delete);
        Services.Events.Changed += OnChanged;
        Refresh();
    }

    public void Dispose() => Services.Events.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh() => _view.ShowRows(Services.Rooms.GetAll()
        .Select(r => new RoomRow(r.Number, r.Floor, r.Class.GetDescription(), r.Places, r.Description ?? string.Empty))
        .ToList());

    private void Delete(RoomRow row)
    {
        if (Dialogs.Confirm($"Удалить номер {row.Number}?"))
            Services.Rooms.Delete(row.Number);
    }

    private void WithSelected(Action<RoomRow> action)
    {
        if (_view.Selected is { } row)
            Try(() => action(row));
        else
            Dialogs.Error("Выберите номер в списке.");
    }
}

public sealed class RoomEditPresenter : Presenter
{
    private readonly IRoomEditView _view;
    private readonly Room? _room;

    public RoomEditPresenter(IRoomEditView view, AppServices services, IDialogService dialogs, Room? room)
        : base(services, dialogs)
    {
        _view = view;
        _room = room;
        _view.Title = room is null ? "Новый номер" : $"Номер {room.Number}";
        _view.Number = room?.Number ?? string.Empty;
        _view.NumberEditable = room is null;
        _view.Floor = room?.Floor ?? 1;
        _view.RoomClass = room?.Class ?? RoomClass.Standard;
        _view.Places = room?.Places ?? 2;
        _view.Description = room?.Description ?? string.Empty;
        _view.SaveClicked += (_, _) => Save();
    }

    private void Save()
    {
        var room = new Room
        {
            Number = _room?.Number ?? _view.Number,
            Floor = _view.Floor,
            Class = _view.RoomClass,
            Places = _view.Places,
            Description = _view.Description
        };
        if (Try(() =>
            {
                if (_room is null)
                    Services.Rooms.Add(room);
                else
                    Services.Rooms.Update(room);
            }, _view))
        {
            _view.Close(accepted: true);
        }
    }
}

/// <summary>Тарифы: цена за место в сутки по классам номеров (управляющий).</summary>
public sealed class TariffsPresenter : Presenter
{
    private readonly ITariffsView _view;

    public TariffsPresenter(ITariffsView view, AppServices services, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _view.SelectionChanged += (_, _) =>
        {
            if (_view.Selected is { } row)
                _view.Price = row.PriceValue;
        };
        _view.SaveClicked += (_, _) => Save();
        Refresh();
    }

    private void Refresh() => _view.ShowRows(Services.Tariffs.GetAll()
        .Select(t => new TariffRow(t.Class, t.PricePerPlace, t.Class.GetDescription(), Format.Money(t.PricePerPlace)))
        .ToList());

    private void Save()
    {
        if (_view.Selected is not { } row)
        {
            _view.ShowError("Выберите класс номера.");
            return;
        }

        var price = _view.Price;
        if (Try(() => Services.Tariffs.Update(row.RoomClass, price)))
            Refresh();
    }
}

/// <summary>Учетные записи сотрудников (управляющий).</summary>
public sealed class UsersPresenter : Presenter
{
    private readonly IUsersView _view;
    private readonly INavigator _navigator;

    public UsersPresenter(IUsersView view, AppServices services, INavigator navigator, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _navigator = navigator;
        _view.AddClicked += (_, _) =>
        {
            if (_navigator.AddUser())
                Refresh();
        };
        _view.ChangePasswordClicked += (_, _) => WithSelected(u =>
        {
            if (_navigator.ChangePassword(u.Login))
                Dialogs.Info($"Пароль пользователя {u.Login} изменен.");
        });
        _view.DeleteClicked += (_, _) => WithSelected(u =>
        {
            if (!Dialogs.Confirm($"Удалить учетную запись {u.Login} ({u.FullName})?"))
                return;
            Services.Auth.DeleteUser(u.Login);
            Refresh();
        });
        Refresh();
    }

    private void Refresh() => _view.ShowRows(Services.Auth.Users
        .Select(u => new UserRow(u.Login, u.FullName, u.Role.GetDescription()))
        .ToList());

    private void WithSelected(Action<UserRow> action)
    {
        if (_view.Selected is { } row)
            Try(() => action(row));
        else
            Dialogs.Error("Выберите пользователя в списке.");
    }
}

public sealed class UserEditPresenter : Presenter
{
    private readonly IUserEditView _view;

    public UserEditPresenter(IUserEditView view, AppServices services, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _view.SaveClicked += (_, _) => Save();
    }

    private void Save()
    {
        if (_view.Password != _view.PasswordRepeat)
        {
            _view.ShowErrors([new ValidationError("PasswordRepeat", "Пароли не совпадают.")]);
            return;
        }

        if (Try(() => Services.Auth.AddUser(_view.Login, _view.FullName, _view.Password, _view.Role), _view))
            _view.Close(accepted: true);
    }
}

public sealed class PasswordPresenter : Presenter
{
    private readonly IPasswordView _view;
    private readonly string _login;

    public PasswordPresenter(IPasswordView view, AppServices services, IDialogService dialogs, string login)
        : base(services, dialogs)
    {
        _view = view;
        _login = login;
        _view.Title = $"Новый пароль: {login}";
        _view.SaveClicked += (_, _) => Save();
    }

    private void Save()
    {
        if (_view.Password != _view.PasswordRepeat)
        {
            _view.ShowErrors([new ValidationError("PasswordRepeat", "Пароли не совпадают.")]);
            return;
        }

        if (Try(() => Services.Auth.ChangePassword(_login, _view.Password), _view))
            _view.Close(accepted: true);
    }
}

public sealed class LogPresenter
{
    public LogPresenter(ILogView view, AppServices services) =>
        view.ShowLines(services.Log.ReadAll().Reverse().ToList());
}
