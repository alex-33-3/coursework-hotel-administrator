using Hotel.Core;
using Hotel.Core.Models;
using Hotel.Forms;
using Hotel.Presenters;

namespace Hotel.Services;

/// <summary>
/// Открывает формы: создает форму, связывает ее с презентером и показывает модально поверх активного окна.
/// Единственное место, где презентеры встречаются с конкретными формами.
/// </summary>
internal sealed class Navigator : INavigator
{
    private readonly AppServices _services;
    private readonly IDialogService _dialogs;
    private readonly IReceiptPrinter _printer;

    public Navigator(AppServices services, IDialogService dialogs, IReceiptPrinter printer)
    {
        _services = services;
        _dialogs = dialogs;
        _printer = printer;
    }

    public bool Login() => Run(new LoginForm(), f => new LoginPresenter(f, _services.Auth)) is not null;

    public void FindRoom(bool walkIn) =>
        Run(new RoomSearchForm { Text = walkIn ? "Поселение без брони — подбор номера" : "Подбор номера" },
            f => new RoomSearchPresenter(f, _services, this, _dialogs, walkIn));

    public bool EditReservation(int? number, ReservationDetails? draft = null) =>
        Run(new ReservationEditForm(), f => new ReservationEditPresenter(f, _services, _dialogs, number, draft)) is not null;

    public bool CheckIn(CheckInTarget target) =>
        Run(new CheckInForm(), f => new CheckInPresenter(f, _services, this, _dialogs, target)) is not null;

    public Guest? EditGuest(Guest? guest, string? passport = null) =>
        Run(new GuestEditForm(), f => new GuestEditPresenter(f, _services, _dialogs, guest, passport))?.Result;

    public bool ExtendStay(int stayId) =>
        Run(new ExtendStayForm(), f => new ExtendStayPresenter(f, _services, _dialogs, stayId)) is not null;

    public bool CheckOut(int stayId) =>
        Run(new CheckOutForm(), f => new CheckOutPresenter(f, _services, _dialogs, stayId)) is not null;

    public void ShowReceipts(IReadOnlyList<int> numbers)
    {
        if (numbers.Count > 0)
            Run(new ReceiptForm(), f => new ReceiptPresenter(f, _services, _dialogs, _printer, numbers));
    }

    public void ShowReceiptRegistry(int? guestId = null) =>
        Run(new ReceiptsForm(), f => new ReceiptsPresenter(f, _services, this, _dialogs, guestId));

    public void ShowRooms() => Run(new RoomsForm(), f => new RoomsPresenter(f, _services, this, _dialogs));

    public bool EditRoom(Room? room) =>
        Run(new RoomEditForm(), f => new RoomEditPresenter(f, _services, _dialogs, room)) is not null;

    public void ShowTariffs() => Run(new TariffsForm(), f => new TariffsPresenter(f, _services, _dialogs));

    public void ShowUsers() => Run(new UsersForm(), f => new UsersPresenter(f, _services, this, _dialogs));

    public bool AddUser() => Run(new UserEditForm(), f => new UserEditPresenter(f, _services, _dialogs)) is not null;

    public bool ChangePassword(string login) =>
        Run(new PasswordForm(), f => new PasswordPresenter(f, _services, _dialogs, login)) is not null;

    public void ShowLog() => Run(new LogForm(), f => new LogPresenter(f, _services));

    /// <summary>
    /// Связывает форму с презентером и показывает ее. Возвращает презентер, если окно закрыто с результатом «ОК»,
    /// иначе <c>null</c>. Квитанции, выписанные в окне, показываются после его закрытия.
    /// </summary>
    private TPresenter? Run<TForm, TPresenter>(TForm form, Func<TForm, TPresenter> createPresenter)
        where TForm : Form
        where TPresenter : class
    {
        TPresenter? presenter = null;
        bool accepted;
        try
        {
            presenter = createPresenter(form);
            var owner = Application.OpenForms.Cast<Form>().LastOrDefault(f => f.Visible && f != form);
            accepted = form.ShowDialog(owner) == DialogResult.OK;
        }
        catch (HotelException ex)
        {
            // Например, бронь или гость изменены, пока окно открывалось.
            _dialogs.Error(ex.Message);
            return null;
        }
        finally
        {
            (presenter as IDisposable)?.Dispose();
            form.Dispose();
        }

        if (!accepted)
            return null;

        if (presenter is IIssuesReceipts issued)
        {
            if (issued.IssuedReceipts.Count > 0)
                ShowReceipts(issued.IssuedReceipts);
            else if (issued.Notice is { } notice)
                _dialogs.Info(notice);
        }

        return presenter;
    }
}

internal sealed class DialogService : IDialogService
{
    private const string Caption = "Администратор гостиницы";

    public void Info(string message) =>
        MessageBox.Show(Owner(), message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public void Error(string message) =>
        MessageBox.Show(Owner(), message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public bool Confirm(string message) =>
        MessageBox.Show(Owner(), message, Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    private static Form? Owner() => Application.OpenForms.Cast<Form>().LastOrDefault(f => f.Visible);
}
