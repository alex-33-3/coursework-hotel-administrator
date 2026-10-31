using Hotel.Presenters;
using Hotel.Views;

namespace Hotel.Forms;

/// <summary>
/// Главная форма: меню, панель «Сегодня» и вкладки. Каждая вкладка — отдельный элемент управления
/// со своим интерфейсом представления.
/// </summary>
internal sealed class MainForm : Form, IMainView
{
    private readonly GuestsTab _guests = new();
    private readonly ArrivalsTab _arrivals = new();
    private readonly DeparturesTab _departures = new();
    private readonly ReservationsTab _reservations = new();
    private readonly RoomsTab _rooms = new();
    private readonly ToolStripMenuItem _managerMenu = new("Управление");
    private readonly ToolStripStatusLabel _user = new();
    private readonly Label _summary = new()
    {
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 34,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(10, 0, 0, 0),
        BackColor = Palette.Accent,
        ForeColor = Palette.AccentText
    };

    public MainForm()
    {
        Text = "Администратор гостиницы";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1280, 760);
        MinimumSize = new Size(900, 500);
        _summary.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);

        var menu = new MenuStrip();
        var work = new ToolStripMenuItem("Работа");
        work.DropDownItems.Add(new ToolStripMenuItem("Подобрать номер…", null, (_, _) => Raise(FindRoomClicked), Keys.F2));
        work.DropDownItems.Add(new ToolStripMenuItem("Поселить без брони…", null, (_, _) => Raise(WalkInClicked), Keys.F3));
        work.DropDownItems.Add(new ToolStripMenuItem("Новая бронь…", null, (_, _) => Raise(NewReservationClicked), Keys.F4));
        work.DropDownItems.Add(new ToolStripSeparator());
        work.DropDownItems.Add("Сменить пароль…", null, (_, _) => Raise(ChangePasswordClicked));
        work.DropDownItems.Add("Выход из учетной записи", null, (_, _) => Raise(LogoutClicked));
        _managerMenu.DropDownItems.Add("Номера…", null, (_, _) => Raise(RoomsClicked));
        _managerMenu.DropDownItems.Add("Тарифы…", null, (_, _) => Raise(TariffsClicked));
        _managerMenu.DropDownItems.Add("Квитанции за период…", null, (_, _) => Raise(ReceiptsClicked));
        _managerMenu.DropDownItems.Add(new ToolStripSeparator());
        _managerMenu.DropDownItems.Add("Пользователи…", null, (_, _) => Raise(UsersClicked));
        _managerMenu.DropDownItems.Add("Журнал действий…", null, (_, _) => Raise(LogClicked));
        menu.Items.AddRange([work, _managerMenu]);

        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 2, 0, 2) };
        toolbar.Items.Add(new ToolStripButton("Подобрать номер (F2)", null, (_, _) => Raise(FindRoomClicked)));
        toolbar.Items.Add(new ToolStripButton("Поселить без брони (F3)", null, (_, _) => Raise(WalkInClicked)));
        toolbar.Items.Add(new ToolStripButton("Новая бронь (F4)", null, (_, _) => Raise(NewReservationClicked)));
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("Выход", null, (_, _) => Raise(LogoutClicked)) { Alignment = ToolStripItemAlignment.Right });

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };
        AddTab(tabs, "Гости", _guests);
        AddTab(tabs, "Заезд сегодня", _arrivals);
        AddTab(tabs, "Отъезд сегодня", _departures);
        AddTab(tabs, "Брони", _reservations);
        AddTab(tabs, "Номера", _rooms);

        var status = new StatusStrip();
        status.Items.Add(_user);

        Controls.Add(tabs);
        Controls.Add(_summary);
        Controls.Add(toolbar);
        Controls.Add(menu);
        Controls.Add(status);
        MainMenuStrip = menu;
    }

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

    public IGuestsTabView Guests => _guests;
    public IArrivalsTabView Arrivals => _arrivals;
    public IDeparturesTabView Departures => _departures;
    public IReservationsTabView Reservations => _reservations;
    public IRoomsTabView Rooms => _rooms;

    public void SetUser(string text) => _user.Text = "Пользователь: " + text;

    public void SetSummary(TodaySummary s) =>
        _summary.Text = $"Сегодня {Format.Date(s.Today)}   |   Проживает: {s.Living}   |   Свободно мест: {s.FreePlaces}   |   " +
                        $"Заезд сегодня: {s.Arrivals}   |   Отъезд сегодня: {s.Departures}   |   Не выехали в срок: {s.Overdue}";

    public void SetManagerMenuVisible(bool visible) => _managerMenu.Visible = visible;

    private static void AddTab(TabControl tabs, string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(8) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        tabs.TabPages.Add(page);
    }

    private void Raise(EventHandler? handler) => handler?.Invoke(this, EventArgs.Empty);
}
