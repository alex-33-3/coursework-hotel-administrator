using System.Globalization;
using Hotel.Core;
using Hotel.Core.Logging;
using Hotel.Core.Models;
using Hotel.Core.Occupancy;
using Hotel.Core.Services;
using Hotel.Core.Storage;
using Hotel.Forms;
using Hotel.Presenters;
using Hotel.Printing;
using Hotel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Hotel;

internal static class Program
{
    private const string Caption = "Администратор гостиницы";

    /// <summary>
    /// Точка входа: загрузка данных (с предложением отложить поврежденный файл), демонстрационные данные при первом
    /// запуске, затем сеансы работы: вход → главная форма → «Выход» возвращает к окну входа.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        ConfigureCulture();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        using var services = ConfigureServices(AppPaths.NextTo(AppContext.BaseDirectory));
        var log = services.GetRequiredService<IAuditLog>();
        Application.ThreadException += (_, e) =>
        {
            log.Write($"Непредвиденная ошибка: {e.Exception}");
            MessageBox.Show($"Непредвиденная ошибка: {e.Exception.Message}\nПодробности записаны в журнал.", Caption,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        var context = services.GetRequiredService<HotelContext>();
        if (!TryLoad(context, log))
            return;

        log.Write("Программа запущена");
        SeedIfNew(context, services.GetRequiredService<IClock>(), log);

        var app = services.GetRequiredService<AppServices>();
        var managerCreated = false;
        if (TrySave(() => managerCreated = app.Auth.EnsureManager()) && managerCreated)
        {
            MessageBox.Show($"Учетных записей не было, поэтому создан управляющий.\nЛогин: {AuthService.DefaultManagerLogin}, " +
                            $"пароль: {AuthService.DefaultManagerPassword}.\nСмените пароль: «Работа» → «Сменить пароль».",
                "Первый запуск", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        var navigator = services.GetRequiredService<INavigator>();
        var dialogs = services.GetRequiredService<IDialogService>();
        while (navigator.Login())
        {
            bool logout;
            using (var form = new MainForm())
            using (var presenter = new MainPresenter(form, app, navigator, dialogs))
            {
                Application.Run(form);
                logout = presenter.LogoutRequested;
            }

            app.Auth.Logout();
            if (!logout)
                break;
        }

        log.Write("Программа закрыта");
    }

    private static ServiceProvider ConfigureServices(AppPaths paths)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAuditLog>(sp => new FileAuditLog(paths.LogFile, sp.GetRequiredService<IClock>()));
        services.AddSingleton(_ => new HotelContext(new HotelRepositories(
            new JsonRepository<Room>(paths.File("rooms")),
            new JsonRepository<Tariff>(paths.File("tariffs")),
            new JsonRepository<Guest>(paths.File("guests")),
            new JsonRepository<Reservation>(paths.File("reservations")),
            new JsonRepository<Stay>(paths.File("stays")),
            new JsonRepository<Receipt>(paths.File("receipts")),
            new JsonRepository<UserAccount>(paths.File("users")))));
        services.AddSingleton<IHotelEvents>(sp => sp.GetRequiredService<HotelContext>());

        services.AddSingleton<OccupancyCalculator>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IUserContext>(sp => sp.GetRequiredService<IAuthService>());
        services.AddSingleton<ITariffService, TariffService>();
        services.AddSingleton<IRoomService, RoomService>();
        services.AddSingleton<IGuestService, GuestService>();
        services.AddSingleton<IReservationService, ReservationService>();
        services.AddSingleton<IStayService, StayService>();
        services.AddSingleton<IReceiptService, ReceiptService>();
        services.AddSingleton<AppServices>();

        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IReceiptPrinter, ReceiptPrinter>();
        services.AddSingleton<INavigator, Navigator>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private static void ConfigureCulture()
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Загружает данные. Если файл поврежден — предлагает отложить его и начать с пустым (ФТ-22);
    /// отказ закрывает программу без аварийного завершения.
    /// </summary>
    private static bool TryLoad(HotelContext context, IAuditLog log)
    {
        while (true)
        {
            try
            {
                context.Load();
                return true;
            }
            catch (StorageException ex)
            {
                log.Write($"Ошибка загрузки {ex.Location}: {ex.Message}");
                var answer = MessageBox.Show(
                    $"Не удалось загрузить данные из файла:\n{ex.Location}\n\n{ex.Message}\n\n" +
                    "Начать с пустым файлом? Поврежденный файл будет сохранен рядом с расширением .corrupt-<дата>.\n" +
                    "«Нет» — закрыть программу.",
                    "Ошибка данных", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                if (answer != DialogResult.Yes)
                    return false;

                try
                {
                    context.ResetStorage(ex.Location);
                    log.Write($"Поврежденный файл отложен: {ex.Location}");
                }
                catch (Exception resetError) when (resetError is IOException or UnauthorizedAccessException)
                {
                    MessageBox.Show($"Не удалось переименовать файл: {resetError.Message}", "Ошибка данных",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
        }
    }

    private static void SeedIfNew(HotelContext context, IClock clock, IAuditLog log)
    {
        if (!context.IsNewStorage)
            return;

        DefaultData.Seed(context, clock.Now);
        if (TrySave(() => context.SaveChanges()))
            log.Write("Созданы демонстрационные данные");
    }

    private static bool TrySave(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (StorageException ex)
        {
            MessageBox.Show($"Не удалось сохранить данные в файл {ex.Location}:\n{ex.Message}", "Ошибка данных",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }
}

internal sealed record AppPaths(string DataDirectory, string LogFile)
{
    public string File(string name) => Path.Combine(DataDirectory, name + ".json");

    public static AppPaths NextTo(string baseDirectory) =>
        new(Path.Combine(baseDirectory, "data"), Path.Combine(baseDirectory, "logs", "app.log"));
}
