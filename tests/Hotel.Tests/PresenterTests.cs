using Hotel.Core.Models;
using Hotel.Core.Search;
using Hotel.Core.Services;
using Hotel.Presenters;

namespace Hotel.Tests;

public class PresenterTests
{
    [Fact]
    public void Login_WrongPassword_ShowsError_ThenCorrectClosesWithSuccess()
    {
        var hotel = new TestHotel();
        hotel.Auth.AddUser("admin", "Ковалева Анна", "admin123", UserRole.Administrator);
        var view = new FakeLoginView { Login = "admin", Password = "wrong" };
        _ = new LoginPresenter(view, hotel.Auth);

        view.ClickLogin();
        Assert.Equal("Неверный логин или пароль.", view.Error);
        Assert.Null(view.Accepted);

        view.Password = "admin123";
        view.ClickLogin();
        Assert.True(view.Accepted);
        Assert.Equal("admin", hotel.Auth.CurrentUser!.Login);
    }

    [Fact]
    public void Main_AdministratorHasNoManagerMenu_AndCannotOpenRooms()
    {
        var hotel = new TestHotel();
        hotel.Auth.AddUser("admin", "Ковалева Анна", "admin123", UserRole.Administrator);
        hotel.Auth.Login("admin", "admin123");
        var view = new FakeMainView();
        var navigator = new FakeNavigator();
        var dialogs = new FakeDialogs();
        using var presenter = new MainPresenter(view, hotel.App, navigator, dialogs);

        Assert.False(view.ManagerMenuVisible);
        view.ClickRooms();
        Assert.DoesNotContain("ShowRooms", navigator.Calls);
        Assert.Single(dialogs.Errors);
    }

    [Fact]
    public void Main_ShowsTodaySummary_AndRefreshesOnDataChange()
    {
        var hotel = new TestHotel();
        hotel.Auth.AddUser("boss", "Литвинов Олег", "manager123", UserRole.Manager);
        hotel.Auth.Login("boss", "manager123");
        var view = new FakeMainView();
        using var presenter = new MainPresenter(view, hotel.App, new FakeNavigator(), new FakeDialogs());
        Assert.True(view.ManagerMenuVisible);
        Assert.Equal((0, 6), (view.Summary!.Living, view.Summary.FreePlaces));

        hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(2));

        Assert.Equal((1, 5), (view.Summary!.Living, view.Summary.FreePlaces));
        Assert.Single(view.GuestsTab.Rows);
        Assert.Equal(3, view.RoomsTab.Rows.Count);

        view.ClickLogout();
        Assert.True(presenter.LogoutRequested);
        Assert.True(view.Closed);
    }

    [Fact]
    public void Main_NewDay_ExpiresNoShowsOnActivate()
    {
        var hotel = new TestHotel();
        var reservation = hotel.Reserve("201", TestHotel.Today, TestHotel.Day(2));
        var view = new FakeMainView();
        using var presenter = new MainPresenter(view, hotel.App, new FakeNavigator(), new FakeDialogs());
        Assert.Single(view.ArrivalsTab.Rows);

        hotel.Clock.Now = TestHotel.Now.AddDays(1);
        view.Activate();

        Assert.Equal(ReservationStatus.NoShow, reservation.Status);
        Assert.Empty(view.ArrivalsTab.Rows);
    }

    [Fact]
    public void GuestsTab_SearchByField_ShowsMatchesAndStatus()
    {
        var hotel = new TestHotel();
        hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(2));
        hotel.CheckIn(hotel.Register("Сидорова", "HB2345678"), "202", TestHotel.Day(2));
        var view = new FakeGuestsTabView();
        var presenter = new GuestsTabPresenter(view, hotel.App, new FakeNavigator(), new FakeDialogs());
        presenter.Refresh();
        Assert.Equal(2, view.Rows.Count);

        view.Search(GuestField.Room, "202");

        Assert.Equal("Сидорова Иван Сергеевич", Assert.Single(view.Rows).FullName);
        Assert.StartsWith("Найдено: 1 из 2", view.Status);

        view.ClickReset();
        Assert.Equal(2, view.Rows.Count);
    }

    [Fact]
    public void GuestsTab_CheckOutGuestWithoutActiveStay_ShowsError()
    {
        var hotel = new TestHotel();
        hotel.Register("Петров", "MP1234567");
        var view = new FakeGuestsTabView();
        var navigator = new FakeNavigator();
        var dialogs = new FakeDialogs();
        _ = new GuestsTabPresenter(view, hotel.App, navigator, dialogs);
        view.ChangeFilter(GuestListFilter.All);

        view.ClickCheckOut();

        Assert.Empty(navigator.Calls);
        Assert.Single(dialogs.Errors);
    }

    [Fact]
    public void RoomSearch_FindsRooms_AndCheckInOnlyForToday()
    {
        var hotel = new TestHotel();
        var view = new FakeRoomSearchView();
        var navigator = new FakeNavigator();
        var dialogs = new FakeDialogs();
        using var presenter = new RoomSearchPresenter(view, hotel.App, navigator, dialogs, walkIn: true);
        Assert.Equal(3, view.Rows.Count);
        Assert.True(view.CheckInEnabled);

        view.Arrival = TestHotel.Day(5);
        view.Departure = TestHotel.Day(7);
        view.Places = 2;
        view.RoomClass = RoomClass.Standard;
        view.ClickSearch();
        Assert.Equal(["201", "202"], view.Rows.Select(r => r.RoomNumber));
        Assert.Equal("240,00 BYN", view.Rows[0].Total);
        Assert.False(view.CheckInEnabled);

        view.SelectedRoom = "202";
        view.ClickCheckIn();
        Assert.Empty(navigator.Calls);
        view.ClickReserve();
        Assert.Equal(["EditReservation::202"], navigator.Calls);
    }

    [Fact]
    public void RoomSearch_InvalidDates_ShowsFieldErrors()
    {
        var hotel = new TestHotel();
        var view = new FakeRoomSearchView();
        using var presenter = new RoomSearchPresenter(view, hotel.App, new FakeNavigator(), new FakeDialogs(), walkIn: false);

        view.Departure = view.Arrival;
        view.ClickSearch();

        Assert.Contains(view.Errors, e => e.Field == "Departure");
        Assert.Empty(view.Rows);
    }

    [Fact]
    public void ReservationEdit_ShowsCost_AndSavesReservation()
    {
        var hotel = new TestHotel();
        var view = new FakeReservationEditView();
        _ = new ReservationEditPresenter(view, hotel.App, new FakeDialogs(), null, null);
        Assert.Equal(3, view.Rooms.Count);

        view.ContactName = "Иванова Мария";
        view.Phone = "+375291112233";
        view.RoomNumber = "201";
        view.Arrival = TestHotel.Day(3);
        view.Departure = TestHotel.Day(5);
        view.Places = 2;
        view.Changed();
        Assert.Contains("240,00 BYN", view.Cost);

        view.ClickSave();
        Assert.True(view.Accepted);
        Assert.Equal("Иванова Мария", Assert.Single(hotel.Context.Reservations).ContactName);
    }

    [Fact]
    public void ReservationEdit_InvalidPhone_ShowsFieldError()
    {
        var hotel = new TestHotel();
        var view = new FakeReservationEditView();
        _ = new ReservationEditPresenter(view, hotel.App, new FakeDialogs(), null,
            new ReservationDetails
            {
                ContactName = "Иванова",
                Phone = "123",
                RoomNumber = "201",
                Arrival = TestHotel.Day(1),
                Departure = TestHotel.Day(2),
                Places = 1
            });

        view.ClickSave();

        Assert.Null(view.Accepted);
        Assert.Contains(view.Errors, e => e.Field == nameof(ReservationDetails.Phone));
    }

    [Fact]
    public void CheckIn_ByReservation_FindsGuestRegistersNew_AndIssuesReceipts()
    {
        var hotel = new TestHotel();
        var reservation = hotel.Reserve("201", TestHotel.Today, TestHotel.Day(2), places: 2);
        var known = hotel.Register("Петров", "MP1234567");
        var navigator = new FakeNavigator { OnEditGuest = (_, passport) => hotel.Register("Петрова", passport!) };
        var dialogs = new FakeDialogs();
        var view = new FakeCheckInView();
        var presenter = new CheckInPresenter(view, hotel.App, navigator, dialogs, new CheckInTarget(reservation.Number));
        Assert.Contains("мест по брони: 2", view.Header);

        view.Find("mp 1234567");
        view.Find("MP7654321");
        Assert.Equal([known.FullName, "Петрова Иван Сергеевич"], view.Guests.Select(g => g.FullName));
        Assert.False(view.CanAdd);

        view.ClickConfirm();

        Assert.True(view.Accepted);
        Assert.Equal(2, presenter.IssuedReceipts.Count);
        Assert.Equal(ReservationStatus.CheckedIn, reservation.Status);
    }

    [Fact]
    public void CheckIn_GuestAlreadyLiving_NotAdded()
    {
        var hotel = new TestHotel();
        hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "202", TestHotel.Day(1));
        var view = new FakeCheckInView();
        _ = new CheckInPresenter(view, hotel.App, new FakeNavigator(), new FakeDialogs(), new CheckInTarget(null, "201", TestHotel.Day(2)));

        view.Find("MP1234567");

        Assert.Empty(view.Guests);
        Assert.Contains("уже проживает в номере 202", view.Error);
    }

    [Fact]
    public void CheckOut_Early_ShowsRefund_AndIssuesRefundReceipt()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(3));
        hotel.Clock.Now = TestHotel.Day(1).ToDateTime(new TimeOnly(11, 0));
        var view = new FakeCheckOutView();
        var presenter = new CheckOutPresenter(view, hotel.App, new FakeDialogs(), stay.Id);
        Assert.StartsWith("Досрочный отъезд", view.Title);
        Assert.Contains("Возврат гостю: 120,00 BYN", view.Total);

        view.ClickConfirm();

        Assert.True(view.Accepted);
        var receipt = hotel.Receipts.GetCard(Assert.Single(presenter.IssuedReceipts)).Receipt;
        Assert.Equal(ReceiptKind.Refund, receipt.Kind);
    }

    [Fact]
    public void CheckOut_OnTime_NoReceiptButNotice()
    {
        var hotel = new TestHotel();
        hotel.Clock.Now = TestHotel.Now.AddDays(-1);
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Today);
        hotel.Clock.Now = TestHotel.Now;
        var view = new FakeCheckOutView();
        var presenter = new CheckOutPresenter(view, hotel.App, new FakeDialogs(), stay.Id);
        Assert.Contains("Расчет сошелся", view.Total);

        view.ClickConfirm();

        Assert.Empty(presenter.IssuedReceipts);
        Assert.NotNull(presenter.Notice);
    }

    [Fact]
    public void ExtendStay_ShowsCost_AndIssuesReceipt()
    {
        var hotel = new TestHotel();
        var stay = hotel.CheckIn(hotel.Register("Петров", "MP1234567"), "201", TestHotel.Day(2));
        var view = new FakeExtendStayView();
        var presenter = new ExtendStayPresenter(view, hotel.App, new FakeDialogs(), stay.Id);
        Assert.Equal(TestHotel.Day(3), view.NewDeparture);

        view.ChangeDeparture(TestHotel.Day(4));
        Assert.Contains("120,00 BYN", view.Cost);
        view.ClickSave();

        Assert.True(view.Accepted);
        Assert.Single(presenter.IssuedReceipts);
        Assert.Equal(TestHotel.Day(4), stay.Departure);
    }

    [Fact]
    public void GuestEdit_RegistersGuest_OrShowsFieldErrors()
    {
        var hotel = new TestHotel();
        var view = new FakeGuestEditView();
        var presenter = new GuestEditPresenter(view, hotel.App, new FakeDialogs(), null, "mp1234567");
        Assert.Equal("mp1234567", view.PassportNumber);
        Assert.Equal(Guest.DefaultCitizenship, view.Citizenship);

        view.ClickSave();
        Assert.Contains(view.Errors, e => e.Field == nameof(Guest.LastName));
        Assert.Null(presenter.Result);

        view.LastName = "Петров";
        view.FirstName = "Иван";
        view.BirthDate = new DateOnly(1985, 3, 14);
        view.PassportIssuedBy = "Первомайским РУВД г. Минска";
        view.PassportIssueDate = new DateOnly(2019, 6, 2);
        view.Phone = "+375";
        view.ClickSave();

        Assert.True(view.Accepted);
        Assert.Equal("MP1234567", presenter.Result!.PassportNumber);
        Assert.Null(presenter.Result.Phone);
    }

    [Fact]
    public void ReservationsTab_CancelAsksConfirmation()
    {
        var hotel = new TestHotel();
        var reservation = hotel.Reserve("201", TestHotel.Day(3), TestHotel.Day(5));
        var view = new FakeReservationsTabView();
        var dialogs = new FakeDialogs { Answer = false };
        var presenter = new ReservationsTabPresenter(view, hotel.App, new FakeNavigator(), dialogs);
        presenter.Refresh();

        view.ClickCancel();
        Assert.Equal(ReservationStatus.Reserved, reservation.Status);

        dialogs.Answer = true;
        view.ClickCancel();
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
    }
}
