using Hotel.Core.Models;
using Hotel.Core.Search;
using Hotel.Core.Services;
using Hotel.Core.Validation;
using Hotel.Presenters;
using Hotel.Views;

namespace Hotel.Tests;

// Поддельные представления: хранят то, что показал презентер, и позволяют «нажать кнопку» вызовом метода.

public sealed class FakeDialogs : IDialogService
{
    public List<string> Infos { get; } = [];
    public List<string> Errors { get; } = [];
    public List<string> Questions { get; } = [];
    public bool Answer { get; set; } = true;

    public void Info(string message) => Infos.Add(message);
    public void Error(string message) => Errors.Add(message);

    public bool Confirm(string message)
    {
        Questions.Add(message);
        return Answer;
    }
}

public sealed class FakeNavigator : INavigator
{
    public List<string> Calls { get; } = [];
    public Func<Guest?, string?, Guest?> OnEditGuest { get; set; } = (_, _) => null;
    public bool Result { get; set; } = true;

    public bool Login() => Record("Login");
    public void FindRoom(bool walkIn) => Record($"FindRoom:{walkIn}");
    public bool EditReservation(int? number, ReservationDetails? draft = null) => Record($"EditReservation:{number}:{draft?.RoomNumber}");
    public bool CheckIn(CheckInTarget target) => Record($"CheckIn:{target.ReservationNumber}:{target.RoomNumber}");

    public Guest? EditGuest(Guest? guest, string? passport = null)
    {
        Record($"EditGuest:{guest?.Id}:{passport}");
        return OnEditGuest(guest, passport);
    }

    public bool ExtendStay(int stayId) => Record($"ExtendStay:{stayId}");
    public bool CheckOut(int stayId) => Record($"CheckOut:{stayId}");
    public void ShowReceipts(IReadOnlyList<int> numbers) => Record($"ShowReceipts:{string.Join(",", numbers)}");
    public void ShowReceiptRegistry(int? guestId = null) => Record($"ShowReceiptRegistry:{guestId}");
    public void ShowRooms() => Record("ShowRooms");
    public bool EditRoom(Room? room) => Record($"EditRoom:{room?.Number}");
    public void ShowTariffs() => Record("ShowTariffs");
    public void ShowUsers() => Record("ShowUsers");
    public bool AddUser() => Record("AddUser");
    public bool ChangePassword(string login) => Record($"ChangePassword:{login}");
    public void ShowLog() => Record("ShowLog");

    private bool Record(string call)
    {
        Calls.Add(call);
        return Result;
    }
}

public abstract class FakeDialogView : IDialogView, IValidatingView
{
    public bool? Accepted { get; private set; }
    public List<ValidationError> Errors { get; } = [];

    public void Close(bool accepted) => Accepted = accepted;

    public void ShowErrors(IReadOnlyList<ValidationError> errors) => Errors.AddRange(errors);

    public void ClearErrors() => Errors.Clear();
}

public sealed class FakeLoginView : FakeDialogView, ILoginView
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Error { get; private set; }

    public event EventHandler? LoginClicked;

    public void ShowError(string message) => Error = message;

    public void ClickLogin() => LoginClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeCheckInView : FakeDialogView, ICheckInView
{
    public string PassportText { get; set; } = string.Empty;
    public int SelectedGuestIndex { get; set; } = -1;
    public string Header { get; private set; } = string.Empty;
    public IReadOnlyList<CheckInGuestRow> Guests { get; private set; } = [];
    public bool CanAdd { get; private set; }
    public string? Error { get; private set; }

    public event EventHandler? FindClicked;
    public event EventHandler? RegisterClicked;
    public event EventHandler? RemoveClicked;
    public event EventHandler? ConfirmClicked;

    public void SetHeader(string text) => Header = text;

    public void ShowGuests(IReadOnlyList<CheckInGuestRow> rows, string status, bool canAdd)
    {
        Guests = rows;
        CanAdd = canAdd;
        Error = null;
    }

    public void ShowError(string message) => Error = message;

    public void Find(string passport)
    {
        PassportText = passport;
        FindClicked?.Invoke(this, EventArgs.Empty);
    }

    public void ClickRegister() => RegisterClicked?.Invoke(this, EventArgs.Empty);
    public void ClickRemove() => RemoveClicked?.Invoke(this, EventArgs.Empty);
    public void ClickConfirm() => ConfirmClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeCheckOutView : FakeDialogView, ICheckOutView
{
    public string Title { get; private set; } = string.Empty;
    public IReadOnlyList<(string Caption, string Value)> Lines { get; private set; } = [];
    public string Total { get; private set; } = string.Empty;

    public event EventHandler? ConfirmClicked;

    public void SetTitle(string text) => Title = text;

    public void SetSettlement(IReadOnlyList<(string Caption, string Value)> lines, string total)
    {
        Lines = lines;
        Total = total;
    }

    public void ClickConfirm() => ConfirmClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeExtendStayView : FakeDialogView, IExtendStayView
{
    public DateOnly NewDeparture { get; set; }
    public string Cost { get; private set; } = string.Empty;

    public event EventHandler? DepartureChanged;
    public event EventHandler? SaveClicked;

    public void SetInfo(string text)
    {
    }

    public void SetCost(string text) => Cost = text;

    public void ShowError(string message)
    {
    }

    public void ChangeDeparture(DateOnly date)
    {
        NewDeparture = date;
        DepartureChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClickSave() => SaveClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeRoomSearchView : FakeDialogView, IRoomSearchView
{
    public DateOnly Arrival { get; set; }
    public DateOnly Departure { get; set; }
    public RoomClass? RoomClass { get; set; }
    public int Places { get; set; }
    public AvailabilityRow? Selected => Rows.FirstOrDefault(r => r.RoomNumber == SelectedRoom);
    public string? SelectedRoom { get; set; }
    public IReadOnlyList<AvailabilityRow> Rows { get; private set; } = [];
    public string Status { get; private set; } = string.Empty;
    public bool CheckInEnabled { get; private set; }

    public event EventHandler? SearchClicked;
    public event EventHandler? ReserveClicked;
    public event EventHandler? CheckInClicked;

    public void ShowRows(IReadOnlyList<AvailabilityRow> rows, string status)
    {
        Rows = rows;
        Status = status;
    }

    public void SetCheckInEnabled(bool enabled) => CheckInEnabled = enabled;

    public void ClickSearch() => SearchClicked?.Invoke(this, EventArgs.Empty);
    public void ClickReserve() => ReserveClicked?.Invoke(this, EventArgs.Empty);
    public void ClickCheckIn() => CheckInClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeReservationEditView : FakeDialogView, IReservationEditView
{
    public string Title { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public DateOnly Arrival { get; set; }
    public DateOnly Departure { get; set; }
    public int Places { get; set; }
    public string Note { get; set; } = string.Empty;
    public IReadOnlyList<Option<string>> Rooms { get; private set; } = [];
    public string Cost { get; private set; } = string.Empty;

    public event EventHandler? InputChanged;
    public event EventHandler? SaveClicked;

    public void SetRooms(IReadOnlyList<Option<string>> rooms) => Rooms = rooms;
    public void SetCost(string text) => Cost = text;
    public void Changed() => InputChanged?.Invoke(this, EventArgs.Empty);
    public void ClickSave() => SaveClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeGuestEditView : FakeDialogView, IGuestEditView
{
    public string Title { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string Citizenship { get; set; } = string.Empty;
    public string PassportNumber { get; set; } = string.Empty;
    public string PassportIssuedBy { get; set; } = string.Empty;
    public DateOnly? PassportIssueDate { get; set; }
    public string Phone { get; set; } = string.Empty;

    public event EventHandler? SaveClicked;

    public void ClickSave() => SaveClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeGuestsTabView : IGuestsTabView
{
    public GuestListFilter Filter { get; set; } = GuestListFilter.Living;
    public GuestField SearchField { get; set; } = GuestField.Any;
    public string SearchText { get; set; } = string.Empty;
    public GuestGridRow? Selected => Rows.Count > 0 ? Rows[0] : null;
    public IReadOnlyList<GuestGridRow> Rows { get; private set; } = [];
    public string Status { get; private set; } = string.Empty;

    public event EventHandler? FilterChanged;
    public event EventHandler? SearchClicked;
    public event EventHandler? ResetClicked;
    public event EventHandler? EditClicked;
    public event EventHandler? ReceiptsClicked;
    public event EventHandler? ExtendClicked;
    public event EventHandler? CheckOutClicked;

    public void ShowRows(IReadOnlyList<GuestGridRow> rows, string status)
    {
        Rows = rows;
        Status = status;
    }

    public void Search(GuestField field, string text)
    {
        SearchField = field;
        SearchText = text;
        SearchClicked?.Invoke(this, EventArgs.Empty);
    }

    public void ChangeFilter(GuestListFilter filter)
    {
        Filter = filter;
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClickReset() => ResetClicked?.Invoke(this, EventArgs.Empty);
    public void ClickEdit() => EditClicked?.Invoke(this, EventArgs.Empty);
    public void ClickReceipts() => ReceiptsClicked?.Invoke(this, EventArgs.Empty);
    public void ClickExtend() => ExtendClicked?.Invoke(this, EventArgs.Empty);
    public void ClickCheckOut() => CheckOutClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeArrivalsTabView : IArrivalsTabView
{
    public ReservationRow? Selected => Rows.Count > 0 ? Rows[0] : null;
    public IReadOnlyList<ReservationRow> Rows { get; private set; } = [];
    public event EventHandler? CheckInClicked;
    public event EventHandler? CancelClicked;
    public void ShowRows(IReadOnlyList<ReservationRow> rows) => Rows = rows;
    public void ClickCheckIn() => CheckInClicked?.Invoke(this, EventArgs.Empty);
    public void ClickCancel() => CancelClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeDeparturesTabView : IDeparturesTabView
{
    public DepartureRow? Selected => Rows.Count > 0 ? Rows[0] : null;
    public IReadOnlyList<DepartureRow> Rows { get; private set; } = [];
    public event EventHandler? CheckOutClicked;
    public event EventHandler? ExtendClicked;
    public void ShowRows(IReadOnlyList<DepartureRow> rows) => Rows = rows;
    public void ClickCheckOut() => CheckOutClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeReservationsTabView : IReservationsTabView
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public ReservationStatus? Status { get; set; }
    public string SearchText { get; set; } = string.Empty;
    public ReservationRow? Selected => Rows.Count > 0 ? Rows[0] : null;
    public IReadOnlyList<ReservationRow> Rows { get; private set; } = [];

    public event EventHandler? SearchClicked;
    public event EventHandler? NewClicked;
    public event EventHandler? EditClicked;
    public event EventHandler? CancelClicked;
    public event EventHandler? CheckInClicked;

    public void ShowRows(IReadOnlyList<ReservationRow> rows) => Rows = rows;
    public void ClickSearch() => SearchClicked?.Invoke(this, EventArgs.Empty);
    public void ClickCancel() => CancelClicked?.Invoke(this, EventArgs.Empty);
}

public sealed class FakeRoomsTabView : IRoomsTabView
{
    public RoomClass? ClassFilter { get; set; }
    public IReadOnlyList<RoomOccupancyRow> Rows { get; private set; } = [];
    public string Total { get; private set; } = string.Empty;
    public event EventHandler? FilterChanged;

    public void ShowRows(IReadOnlyList<RoomOccupancyRow> rows, string total)
    {
        Rows = rows;
        Total = total;
    }

    public void ChangeFilter(RoomClass? roomClass)
    {
        ClassFilter = roomClass;
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class FakeMainView : IMainView
{
    public FakeGuestsTabView GuestsTab { get; } = new();
    public FakeArrivalsTabView ArrivalsTab { get; } = new();
    public FakeDeparturesTabView DeparturesTab { get; } = new();
    public FakeReservationsTabView ReservationsTab { get; } = new();
    public FakeRoomsTabView RoomsTab { get; } = new();

    public IGuestsTabView Guests => GuestsTab;
    public IArrivalsTabView Arrivals => ArrivalsTab;
    public IDeparturesTabView Departures => DeparturesTab;
    public IReservationsTabView Reservations => ReservationsTab;
    public IRoomsTabView Rooms => RoomsTab;

    public string User { get; private set; } = string.Empty;
    public TodaySummary? Summary { get; private set; }
    public bool ManagerMenuVisible { get; private set; }
    public bool Closed { get; private set; }

    public event EventHandler? FindRoomClicked;
    public event EventHandler? WalkInClicked;
    public event EventHandler? NewReservationClicked;
    public event EventHandler? RoomsClicked;
    public event EventHandler? TariffsClicked;
    public event EventHandler? ReceiptsClicked;
    public event EventHandler? UsersClicked;
    public event EventHandler? ChangePasswordClicked;
    public event EventHandler? LogClicked;
    public event EventHandler? LogoutClicked;
    public event EventHandler? Activated;

    public void SetUser(string text) => User = text;
    public void SetSummary(TodaySummary summary) => Summary = summary;
    public void SetManagerMenuVisible(bool visible) => ManagerMenuVisible = visible;
    public void Close() => Closed = true;

    public void ClickRooms() => RoomsClicked?.Invoke(this, EventArgs.Empty);
    public void ClickWalkIn() => WalkInClicked?.Invoke(this, EventArgs.Empty);
    public void ClickLogout() => LogoutClicked?.Invoke(this, EventArgs.Empty);
    public void Activate() => Activated?.Invoke(this, EventArgs.Empty);
}
