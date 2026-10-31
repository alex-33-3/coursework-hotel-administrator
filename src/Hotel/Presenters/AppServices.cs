using Hotel.Core;
using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Services;
using Hotel.Core.Storage;
using Hotel.Views;

namespace Hotel.Presenters;

/// <summary>Сервисы ядра, которые нужны презентерам, — одной зависимостью вместо десятка параметров конструктора.</summary>
public sealed record AppServices(
    IRoomService Rooms,
    ITariffService Tariffs,
    IGuestService Guests,
    IReservationService Reservations,
    IStayService Stays,
    IReceiptService Receipts,
    IAuthService Auth,
    IClock Clock,
    IAuditLog Log,
    IHotelEvents Events);

/// <summary>Сообщения пользователю. В тестах — подделка, которая запоминает сообщения и отвечает на вопросы.</summary>
public interface IDialogService
{
    void Info(string message);
    void Error(string message);
    bool Confirm(string message);
}

/// <summary>Открытие форм: каждая форма создается вместе со своим презентером. В тестах — подделка.</summary>
public interface INavigator
{
    bool Login();

    /// <summary>Подбор номера. <paramref name="walkIn"/> — поселение без брони: приезд сегодня.</summary>
    void FindRoom(bool walkIn);

    /// <summary>Новая бронь (<paramref name="number"/> = <c>null</c>, можно с заготовкой) или изменение брони.</summary>
    bool EditReservation(int? number, ReservationDetails? draft = null);

    bool CheckIn(CheckInTarget target);

    /// <summary>Регистрация нового гостя (<paramref name="guest"/> = <c>null</c>) или изменение данных. Возвращает сохраненного гостя.</summary>
    Guest? EditGuest(Guest? guest, string? passport = null);

    bool ExtendStay(int stayId);

    bool CheckOut(int stayId);

    void ShowReceipts(IReadOnlyList<int> numbers);

    /// <summary>Реестр квитанций за период или квитанции одного гостя.</summary>
    void ShowReceiptRegistry(int? guestId = null);

    void ShowRooms();

    bool EditRoom(Room? room);

    void ShowTariffs();

    void ShowUsers();

    bool AddUser();

    bool ChangePassword(string login);

    void ShowLog();
}

/// <summary>
/// Презентер операции, после которой выписываются квитанции. Навигатор показывает их, когда окно операции закрыто,
/// чтобы квитанция открылась поверх главного окна, а не поверх закрывающегося диалога.
/// </summary>
public interface IIssuesReceipts
{
    IReadOnlyList<int> IssuedReceipts { get; }

    /// <summary>Сообщение, если квитанция не понадобилась (например, выписка без доплаты).</summary>
    string? Notice { get; }
}

/// <summary>Куда заселять: по брони или в выбранный номер до даты отъезда.</summary>
public sealed record CheckInTarget(int? ReservationNumber, string? RoomNumber = null, DateOnly? Departure = null);

/// <summary>Общая часть презентеров: обработка ошибок предметной области и хранилища.</summary>
public abstract class Presenter
{
    protected Presenter(AppServices services, IDialogService dialogs)
    {
        Services = services;
        Dialogs = dialogs;
    }

    protected AppServices Services { get; }
    protected IDialogService Dialogs { get; }

    protected DateOnly Today => Services.Clock.Today;

    /// <summary>
    /// Выполняет действие. Ошибки ввода показываются у полей формы (если она это умеет), остальные ошибки
    /// предметной области и записи файлов — сообщением. Возвращает <c>true</c>, если действие выполнено.
    /// </summary>
    protected bool Try(Action action, IValidatingView? view = null)
    {
        view?.ClearErrors();
        try
        {
            action();
            return true;
        }
        catch (DataValidationException ex) when (view is not null)
        {
            view.ShowErrors(ex.Errors);
        }
        catch (HotelException ex)
        {
            Dialogs.Error(ex.Message);
        }
        catch (StorageException ex)
        {
            Services.Log.Write($"Ошибка записи {ex.Location}: {ex.Message}");
            Dialogs.Error($"Не удалось сохранить данные в файл {ex.Location}:{Environment.NewLine}{ex.Message}");
        }

        return false;
    }
}
