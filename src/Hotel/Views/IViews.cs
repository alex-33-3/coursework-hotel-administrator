using Hotel.Core.Models;
using Hotel.Core.Search;
using Hotel.Core.Services;
using Hotel.Core.Validation;
using Hotel.Presenters;

namespace Hotel.Views;

// Интерфейсы представлений (пассивное представление): форма только показывает данные и сообщает о действиях
// пользователя событиями, всё решает презентер. Типы Windows Forms в интерфейсах не используются —
// поэтому презентеры проверяются тестами с поддельными представлениями.

/// <summary>Представление, которое показывает ошибки ввода рядом с полями.</summary>
public interface IValidatingView
{
    void ShowErrors(IReadOnlyList<ValidationError> errors);
    void ClearErrors();
}

/// <summary>Диалоговое окно: закрывается с результатом «принято» или «отменено».</summary>
public interface IDialogView
{
    void Close(bool accepted);
}

public interface ILoginView : IDialogView
{
    string Login { get; }
    string Password { get; }
    event EventHandler? LoginClicked;
    void ShowError(string message);
}

public interface IMainView
{
    IGuestsTabView Guests { get; }
    IArrivalsTabView Arrivals { get; }
    IDeparturesTabView Departures { get; }
    IReservationsTabView Reservations { get; }
    IRoomsTabView Rooms { get; }

    void SetUser(string text);
    void SetSummary(TodaySummary summary);
    void SetManagerMenuVisible(bool visible);
    void Close();

    event EventHandler? FindRoomClicked;
    event EventHandler? WalkInClicked;
    event EventHandler? NewReservationClicked;
    event EventHandler? RoomsClicked;
    event EventHandler? TariffsClicked;
    event EventHandler? ReceiptsClicked;
    event EventHandler? UsersClicked;
    event EventHandler? ChangePasswordClicked;
    event EventHandler? LogClicked;
    event EventHandler? LogoutClicked;
    event EventHandler? Activated;
}

public interface IGuestsTabView
{
    GuestListFilter Filter { get; }
    GuestField SearchField { get; }
    string SearchText { get; set; }
    GuestGridRow? Selected { get; }

    void ShowRows(IReadOnlyList<GuestGridRow> rows, string status);

    event EventHandler? FilterChanged;
    event EventHandler? SearchClicked;
    event EventHandler? ResetClicked;
    event EventHandler? EditClicked;
    event EventHandler? ReceiptsClicked;
    event EventHandler? ExtendClicked;
    event EventHandler? CheckOutClicked;
}

public interface IArrivalsTabView
{
    ReservationRow? Selected { get; }
    void ShowRows(IReadOnlyList<ReservationRow> rows);
    event EventHandler? CheckInClicked;
    event EventHandler? CancelClicked;
}

public interface IDeparturesTabView
{
    DepartureRow? Selected { get; }
    void ShowRows(IReadOnlyList<DepartureRow> rows);
    event EventHandler? CheckOutClicked;
    event EventHandler? ExtendClicked;
}

public interface IReservationsTabView
{
    DateOnly From { get; set; }
    DateOnly To { get; set; }
    ReservationStatus? Status { get; }
    string SearchText { get; }
    ReservationRow? Selected { get; }

    void ShowRows(IReadOnlyList<ReservationRow> rows);

    event EventHandler? SearchClicked;
    event EventHandler? NewClicked;
    event EventHandler? EditClicked;
    event EventHandler? CancelClicked;
    event EventHandler? CheckInClicked;
}

public interface IRoomsTabView
{
    RoomClass? ClassFilter { get; }
    void ShowRows(IReadOnlyList<RoomOccupancyRow> rows, string total);
    event EventHandler? FilterChanged;
}

public interface IRoomSearchView : IDialogView, IValidatingView
{
    DateOnly Arrival { get; set; }
    DateOnly Departure { get; set; }
    RoomClass? RoomClass { get; }
    int Places { get; set; }
    AvailabilityRow? Selected { get; }

    void ShowRows(IReadOnlyList<AvailabilityRow> rows, string status);
    void SetCheckInEnabled(bool enabled);

    event EventHandler? SearchClicked;
    event EventHandler? ReserveClicked;
    event EventHandler? CheckInClicked;
}

public interface IReservationEditView : IDialogView, IValidatingView
{
    string Title { set; }
    string ContactName { get; set; }
    string Phone { get; set; }
    string? RoomNumber { get; set; }
    DateOnly Arrival { get; set; }
    DateOnly Departure { get; set; }
    int Places { get; set; }
    string Note { get; set; }

    void SetRooms(IReadOnlyList<Option<string>> rooms);
    void SetCost(string text);

    event EventHandler? InputChanged;
    event EventHandler? SaveClicked;
}

public interface ICheckInView : IDialogView
{
    string PassportText { get; set; }
    int SelectedGuestIndex { get; }

    void SetHeader(string text);
    void ShowGuests(IReadOnlyList<CheckInGuestRow> rows, string status, bool canAdd);
    void ShowError(string message);

    event EventHandler? FindClicked;
    event EventHandler? RegisterClicked;
    event EventHandler? RemoveClicked;
    event EventHandler? ConfirmClicked;
}

public interface IGuestEditView : IDialogView, IValidatingView
{
    string Title { set; }
    string LastName { get; set; }
    string FirstName { get; set; }
    string MiddleName { get; set; }
    DateOnly? BirthDate { get; set; }
    string Citizenship { get; set; }
    string PassportNumber { get; set; }
    string PassportIssuedBy { get; set; }
    DateOnly? PassportIssueDate { get; set; }
    string Phone { get; set; }

    event EventHandler? SaveClicked;
}

public interface IExtendStayView : IDialogView
{
    DateOnly NewDeparture { get; set; }
    void SetInfo(string text);
    void SetCost(string text);
    void ShowError(string message);
    event EventHandler? DepartureChanged;
    event EventHandler? SaveClicked;
}

public interface ICheckOutView : IDialogView
{
    void SetTitle(string text);
    void SetSettlement(IReadOnlyList<(string Caption, string Value)> lines, string total);
    event EventHandler? ConfirmClicked;
}

public interface IReceiptView
{
    void ShowList(IReadOnlyList<Option<int>> receipts);
    int? SelectedNumber { get; }
    void ShowCard(ReceiptCard card);
    event EventHandler? SelectionChanged;
    event EventHandler? PrintClicked;
}

public interface IReceiptsView
{
    string Title { set; }
    DateOnly From { get; set; }
    DateOnly To { get; set; }
    ReceiptRow? Selected { get; }

    void SetPeriodVisible(bool visible);
    void ShowRows(IReadOnlyList<ReceiptRow> rows, string summary);

    event EventHandler? SearchClicked;
    event EventHandler? OpenClicked;
}

public interface IRoomsView
{
    RoomRow? Selected { get; }
    void ShowRows(IReadOnlyList<RoomRow> rows);
    event EventHandler? AddClicked;
    event EventHandler? EditClicked;
    event EventHandler? DeleteClicked;
}

public interface IRoomEditView : IDialogView, IValidatingView
{
    string Title { set; }
    string Number { get; set; }
    bool NumberEditable { set; }
    int Floor { get; set; }
    RoomClass RoomClass { get; set; }
    int Places { get; set; }
    string Description { get; set; }
    event EventHandler? SaveClicked;
}

public interface ITariffsView
{
    TariffRow? Selected { get; }
    decimal Price { get; set; }
    void ShowRows(IReadOnlyList<TariffRow> rows);
    void ShowError(string message);
    event EventHandler? SelectionChanged;
    event EventHandler? SaveClicked;
}

public interface IUsersView
{
    UserRow? Selected { get; }
    void ShowRows(IReadOnlyList<UserRow> rows);
    event EventHandler? AddClicked;
    event EventHandler? ChangePasswordClicked;
    event EventHandler? DeleteClicked;
}

public interface IUserEditView : IDialogView, IValidatingView
{
    string Login { get; }
    string FullName { get; }
    UserRole Role { get; }
    string Password { get; }
    string PasswordRepeat { get; }
    event EventHandler? SaveClicked;
}

public interface IPasswordView : IDialogView, IValidatingView
{
    string Title { set; }
    string Password { get; }
    string PasswordRepeat { get; }
    event EventHandler? SaveClicked;
}

public interface ILogView
{
    void ShowLines(IReadOnlyList<string> lines);
}
