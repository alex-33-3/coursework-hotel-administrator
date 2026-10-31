using Hotel.Core.Models;
using Hotel.Views;

namespace Hotel.Presenters;

/// <summary>
/// Главная форма: панель «Сегодня», меню по роли и вкладки (у каждой вкладки свой презентер).
/// Списки обновляются при любом изменении данных (событие <c>IHotelEvents.Changed</c>) и при возврате в окно
/// (чтобы «сегодня» и незаезды пересчитывались, если программа открыта долго).
/// </summary>
public sealed class MainPresenter : Presenter, IDisposable
{
    private readonly IMainView _view;
    private readonly INavigator _navigator;
    private readonly IReadOnlyList<IRefreshable> _tabs;
    private DateOnly _shownDay;

    public MainPresenter(IMainView view, AppServices services, INavigator navigator, IDialogService dialogs)
        : base(services, dialogs)
    {
        _view = view;
        _navigator = navigator;
        _tabs =
        [
            new GuestsTabPresenter(view.Guests, services, navigator, dialogs),
            new ArrivalsTabPresenter(view.Arrivals, services, navigator, dialogs),
            new DeparturesTabPresenter(view.Departures, services, navigator, dialogs),
            new ReservationsTabPresenter(view.Reservations, services, navigator, dialogs),
            new RoomsTabPresenter(view.Rooms, services, dialogs)
        ];

        var user = services.Auth.CurrentUser;
        _view.SetUser(user is null ? string.Empty : $"{user.FullName} ({user.Role.GetDescription()})");
        _view.SetManagerMenuVisible(services.Auth.IsManager);

        _view.FindRoomClicked += (_, _) => _navigator.FindRoom(walkIn: false);
        _view.WalkInClicked += (_, _) => _navigator.FindRoom(walkIn: true);
        _view.NewReservationClicked += (_, _) => _navigator.EditReservation(null);
        _view.RoomsClicked += (_, _) => RunForManager(_navigator.ShowRooms);
        _view.TariffsClicked += (_, _) => RunForManager(_navigator.ShowTariffs);
        _view.ReceiptsClicked += (_, _) => RunForManager(() => _navigator.ShowReceiptRegistry());
        _view.UsersClicked += (_, _) => RunForManager(_navigator.ShowUsers);
        _view.LogClicked += (_, _) => RunForManager(_navigator.ShowLog);
        _view.ChangePasswordClicked += (_, _) => ChangeOwnPassword();
        _view.LogoutClicked += (_, _) => Logout();
        _view.Activated += (_, _) => RefreshIfDayChanged();

        Services.Events.Changed += OnDataChanged;
        Refresh();
    }

    /// <summary>Пользователь нажал «Выход»: после закрытия формы нужно снова показать окно входа.</summary>
    public bool LogoutRequested { get; private set; }

    public void Refresh()
    {
        _shownDay = Today;
        Try(() => Services.Reservations.ExpireNoShows());

        foreach (var tab in _tabs)
            tab.Refresh();

        var occupancy = Services.Rooms.GetOccupancyToday();
        var departures = Services.Stays.GetDeparturesToday();
        _view.SetSummary(new TodaySummary(
            Today,
            occupancy.Sum(o => o.Living),
            occupancy.Sum(o => o.Free),
            Services.Reservations.GetArrivalsToday().Count,
            departures.Count(r => !r.Stay!.IsOverdue(Today)),
            departures.Count(r => r.Stay!.IsOverdue(Today))));
    }

    public void Dispose() => Services.Events.Changed -= OnDataChanged;

    private void OnDataChanged(object? sender, EventArgs e) => Refresh();

    private void RefreshIfDayChanged()
    {
        if (_shownDay != Today)
            Refresh();
    }

    private void RunForManager(Action action)
    {
        if (!Services.Auth.IsManager)
        {
            Dialogs.Error("Эта функция доступна только управляющему.");
            return;
        }

        action();
    }

    private void ChangeOwnPassword()
    {
        if (Services.Auth.CurrentUser is { } user && _navigator.ChangePassword(user.Login))
            Dialogs.Info("Пароль изменен.");
    }

    private void Logout()
    {
        LogoutRequested = true;
        _view.Close();
    }
}

/// <summary>Вкладка, которая умеет перечитать данные.</summary>
public interface IRefreshable
{
    void Refresh();
}
