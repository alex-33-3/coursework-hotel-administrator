using Hotel.Core.Models;
using Hotel.Core.Search;
using Hotel.Core.Services;
using Hotel.Views;

namespace Hotel.Presenters;

/// <summary>Вкладка «Гости»: список с фильтром «проживают / выехали / все» и поиск по произвольному признаку.</summary>
public sealed class GuestsTabPresenter : Presenter, IRefreshable
{
    private readonly IGuestsTabView _view;
    private readonly INavigator _navigator;

    public GuestsTabPresenter(IGuestsTabView view, AppServices services, INavigator navigator, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _navigator = navigator;
        _view.FilterChanged += (_, _) => Refresh();
        _view.SearchClicked += (_, _) => Refresh();
        _view.ResetClicked += (_, _) =>
        {
            _view.SearchText = string.Empty;
            Refresh();
        };
        _view.EditClicked += (_, _) => Edit();
        _view.ReceiptsClicked += (_, _) => WithSelected(row => _navigator.ShowReceiptRegistry(row.GuestId));
        _view.ExtendClicked += (_, _) => WithActiveStay(_navigator.ExtendStay);
        _view.CheckOutClicked += (_, _) => WithActiveStay(_navigator.CheckOut);
    }

    public void Refresh()
    {
        var rows = Services.Guests.GetRows(_view.Filter);
        var found = GuestSearch.Search(rows, _view.SearchField, _view.SearchText);
        var status = string.IsNullOrWhiteSpace(_view.SearchText)
            ? $"Записей: {found.Count}"
            : $"Найдено: {found.Count} из {rows.Count} (поиск: {_view.SearchField.GetDescription().ToLowerInvariant()} — «{_view.SearchText.Trim()}»)";
        _view.ShowRows(found.Select(r => GuestGridRow.From(r, Today)).ToList(), status);
    }

    private void Edit() => WithSelected(row =>
    {
        var guest = Services.Guests.Get(row.GuestId);
        _navigator.EditGuest(guest);
    });

    private void WithActiveStay(Func<int, bool> action) => WithSelected(row =>
    {
        if (row.StayId is not { } stayId || !row.IsActive)
        {
            Dialogs.Error("Выберите гостя, который сейчас проживает в гостинице.");
            return;
        }

        action(stayId);
    });

    private void WithSelected(Action<GuestGridRow> action)
    {
        if (_view.Selected is { } row)
            Try(() => action(row));
        else
            Dialogs.Error("Выберите гостя в списке.");
    }
}

/// <summary>Вкладка «Заезд сегодня»: брони с датой приезда сегодня — заселить или отменить.</summary>
public sealed class ArrivalsTabPresenter : Presenter, IRefreshable
{
    private readonly IArrivalsTabView _view;

    public ArrivalsTabPresenter(IArrivalsTabView view, AppServices services, INavigator navigator, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _view.CheckInClicked += (_, _) => WithSelected(r => navigator.CheckIn(new CheckInTarget(r.Number)));
        _view.CancelClicked += (_, _) => WithSelected(r => ReservationActions.Cancel(r, Services, Dialogs));
    }

    public void Refresh() => _view.ShowRows(Services.Reservations.GetArrivalsToday().Select(ReservationRow.From).ToList());

    private void WithSelected(Action<ReservationRow> action)
    {
        if (_view.Selected is { } row)
            Try(() => action(row));
        else
            Dialogs.Error("Выберите бронь в списке.");
    }
}

/// <summary>Вкладка «Отъезд сегодня»: отъезжающие сегодня и не выехавшие в срок — выписать или продлить.</summary>
public sealed class DeparturesTabPresenter : Presenter, IRefreshable
{
    private readonly IDeparturesTabView _view;

    public DeparturesTabPresenter(IDeparturesTabView view, AppServices services, INavigator navigator, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _view.CheckOutClicked += (_, _) => WithSelected(r => navigator.CheckOut(r.StayId));
        _view.ExtendClicked += (_, _) => WithSelected(r => navigator.ExtendStay(r.StayId));
    }

    public void Refresh() => _view.ShowRows(Services.Stays.GetDeparturesToday().Select(r => DepartureRow.From(r, Today)).ToList());

    private void WithSelected(Action<DepartureRow> action)
    {
        if (_view.Selected is { } row)
            Try(() => action(row));
        else
            Dialogs.Error("Выберите гостя в списке.");
    }
}

/// <summary>Вкладка «Брони»: список за период с фильтром по состоянию и тексту; создать, изменить, отменить, заселить.</summary>
public sealed class ReservationsTabPresenter : Presenter, IRefreshable
{
    private readonly IReservationsTabView _view;
    private readonly INavigator _navigator;

    public ReservationsTabPresenter(IReservationsTabView view, AppServices services, INavigator navigator, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _navigator = navigator;
        _view.From = Today;
        _view.To = Today.AddDays(30);
        _view.SearchClicked += (_, _) => Refresh();
        _view.NewClicked += (_, _) => _navigator.EditReservation(null);
        _view.EditClicked += (_, _) => WithChangeable(r => _navigator.EditReservation(r.Number));
        _view.CancelClicked += (_, _) => WithChangeable(r => ReservationActions.Cancel(r, Services, Dialogs));
        _view.CheckInClicked += (_, _) => WithChangeable(r => _navigator.CheckIn(new CheckInTarget(r.Number)));
    }

    public void Refresh()
    {
        if (_view.To < _view.From)
        {
            Dialogs.Error("Дата «по» не может быть раньше даты «с».");
            return;
        }

        var filter = new ReservationFilter { From = _view.From, To = _view.To, Status = _view.Status, Text = _view.SearchText };
        _view.ShowRows(Services.Reservations.Find(filter).Select(ReservationRow.From).ToList());
    }

    private void WithChangeable(Action<ReservationRow> action)
    {
        if (_view.Selected is not { } row)
            Dialogs.Error("Выберите бронь в списке.");
        else if (!row.CanChange)
            Dialogs.Error($"Бронь № {row.Number} в состоянии «{row.Status}»: с ней больше ничего сделать нельзя.");
        else
            Try(() => action(row));
    }
}

/// <summary>Вкладка «Номера»: занятость на сегодня с фильтром по классу.</summary>
public sealed class RoomsTabPresenter : Presenter, IRefreshable
{
    private readonly IRoomsTabView _view;

    public RoomsTabPresenter(IRoomsTabView view, AppServices services, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _view.FilterChanged += (_, _) => Refresh();
    }

    public void Refresh()
    {
        var tariffs = Services.Tariffs.GetAll().ToDictionary(t => t.Class, t => t.PricePerPlace);
        var occupancy = Services.Rooms.GetOccupancyToday(_view.ClassFilter);
        var rows = occupancy
            .Select(o => new RoomOccupancyRow(o.Room.Number, o.Room.Floor, o.Room.Class.GetDescription(), o.Room.Places, o.Living,
                o.Reserved, o.Free, Format.Money(tariffs.GetValueOrDefault(o.Room.Class)), o.Room.Description ?? string.Empty))
            .ToList();
        _view.ShowRows(rows,
            $"Номеров: {rows.Count}, мест: {rows.Sum(r => r.Places)}, проживает: {rows.Sum(r => r.Living)}, " +
            $"забронировано на сегодня: {rows.Sum(r => r.Reserved)}, свободно: {rows.Sum(r => r.Free)}");
    }
}

internal static class ReservationActions
{
    public static void Cancel(ReservationRow row, AppServices services, IDialogService dialogs)
    {
        if (dialogs.Confirm($"Отменить бронь № {row.Number} ({row.Contact}, номер {row.Room}, {row.Arrival}–{row.Departure})?"))
            services.Reservations.Cancel(row.Number);
    }
}
