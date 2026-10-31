using Hotel.Core.Models;
using Hotel.Core.Services;
using Hotel.Core.Validation;
using Hotel.Presenters;
using Hotel.Views;

namespace Hotel.Forms;

/// <summary>Окно управляющего со списком: таблица и кнопки снизу.</summary>
internal abstract class ListForm : DialogForm
{
    protected ListForm(string title, Size size)
    {
        Text = title;
        AutoSize = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        Size = size;
    }

    protected DataGridView Grid { get; } = Ui.Grid();

    protected void Arrange(params Control[] buttons)
    {
        var bar = Ui.Bar([.. buttons,
            new Button { Text = "Закрыть", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(0, 4, 8, 4) }]);
        bar.Dock = DockStyle.Bottom;
        CancelButton = (IButtonControl)bar.Controls[^1];
        Controls.Add(Grid);
        Controls.Add(bar);
    }
}

internal sealed class RoomsForm : ListForm, IRoomsView
{
    public RoomsForm() : base("Номерной фонд", new Size(760, 480))
    {
        Arrange(
            Ui.Button("Добавить…", () => AddClicked?.Invoke(this, EventArgs.Empty)),
            Ui.Button("Изменить…", () => EditClicked?.Invoke(this, EventArgs.Empty)),
            Ui.Button("Удалить", () => DeleteClicked?.Invoke(this, EventArgs.Empty)));
        Grid.DoubleClick += (_, _) => EditClicked?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? AddClicked;
    public event EventHandler? EditClicked;
    public event EventHandler? DeleteClicked;

    public RoomRow? Selected => Ui.Selected<RoomRow>(Grid);

    public void ShowRows(IReadOnlyList<RoomRow> rows) => Ui.Bind(Grid, rows);
}

internal sealed class RoomEditForm : DialogForm, IRoomEditView
{
    private readonly TextBox _number = new();
    private readonly NumericUpDown _floor = Ui.Number(1, RoomValidator.MaxFloor);
    private readonly ComboBox _class = Ui.EnumCombo<RoomClass>();
    private readonly NumericUpDown _places = Ui.Number(1, RoomValidator.MaxPlaces);
    private readonly TextBox _description = new();

    public RoomEditForm()
    {
        var fields = Ui.Fields();
        MapField(nameof(Room.Number), Ui.AddField(fields, "Номер комнаты", _number));
        MapField(nameof(Room.Floor), Ui.AddField(fields, "Этаж", _floor));
        MapField(nameof(Room.Class), Ui.AddField(fields, "Класс", _class));
        MapField(nameof(Room.Places), Ui.AddField(fields, "Число мест", _places));
        MapField(nameof(Room.Description), Ui.AddField(fields, "Описание", _description));
        Compose(fields, OkCancel("Сохранить", () => SaveClicked?.Invoke(this, EventArgs.Empty)));
    }

    public event EventHandler? SaveClicked;

    public string Title
    {
        set => Text = value;
    }

    public string Number
    {
        get => _number.Text;
        set => _number.Text = value;
    }

    public bool NumberEditable
    {
        set => _number.ReadOnly = !value;
    }

    public int Floor
    {
        get => (int)_floor.Value;
        set => _floor.Value = value;
    }

    public RoomClass RoomClass
    {
        get => _class.EnumValue<RoomClass>() ?? RoomClass.Standard;
        set => _class.SelectEnum(value);
    }

    public int Places
    {
        get => (int)_places.Value;
        set => _places.Value = value;
    }

    public string Description
    {
        get => _description.Text;
        set => _description.Text = value;
    }
}

internal sealed class TariffsForm : ListForm, ITariffsView
{
    private readonly NumericUpDown _price = new() { Minimum = 0, Maximum = TariffService.MaxPrice, DecimalPlaces = 2, Increment = 5, Width = 110 };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Palette.Error, Margin = new Padding(0, 8, 0, 0) };

    public TariffsForm() : base("Тарифы", new Size(560, 360))
    {
        Arrange(Ui.Caption("Цена за место в сутки:"), _price,
            Ui.Button("Сохранить цену", () => SaveClicked?.Invoke(this, EventArgs.Empty)), _error);
        Controls.Add(Ui.Bar(Ui.Hint("Новая цена действует для новых броней и поселений. Уже оформленные брони и проживания не пересчитываются.")));
        Grid.SelectionChanged += (_, _) =>
        {
            _error.Text = string.Empty;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler? SaveClicked;

    public TariffRow? Selected => Ui.Selected<TariffRow>(Grid);

    public decimal Price
    {
        get => _price.Value;
        set => _price.Value = Math.Clamp(value, _price.Minimum, _price.Maximum);
    }

    public void ShowRows(IReadOnlyList<TariffRow> rows) => Ui.Bind(Grid, rows);

    public void ShowError(string message) => _error.Text = message;
}

internal sealed class UsersForm : ListForm, IUsersView
{
    public UsersForm() : base("Пользователи", new Size(640, 400))
    {
        Arrange(
            Ui.Button("Добавить…", () => AddClicked?.Invoke(this, EventArgs.Empty)),
            Ui.Button("Сменить пароль…", () => ChangePasswordClicked?.Invoke(this, EventArgs.Empty)),
            Ui.Button("Удалить", () => DeleteClicked?.Invoke(this, EventArgs.Empty)));
    }

    public event EventHandler? AddClicked;
    public event EventHandler? ChangePasswordClicked;
    public event EventHandler? DeleteClicked;

    public UserRow? Selected => Ui.Selected<UserRow>(Grid);

    public void ShowRows(IReadOnlyList<UserRow> rows) => Ui.Bind(Grid, rows);
}

internal sealed class UserEditForm : DialogForm, IUserEditView
{
    private readonly TextBox _login = new();
    private readonly TextBox _fullName = new();
    private readonly ComboBox _role = Ui.EnumCombo<UserRole>();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly TextBox _repeat = new() { UseSystemPasswordChar = true };

    public UserEditForm()
    {
        Text = "Новый пользователь";
        var fields = Ui.Fields();
        MapField("Login", Ui.AddField(fields, "Логин", _login));
        MapField("FullName", Ui.AddField(fields, "ФИО", _fullName));
        MapField("Role", Ui.AddField(fields, "Роль", _role));
        MapField("Password", Ui.AddField(fields, "Пароль", _password));
        MapField("PasswordRepeat", Ui.AddField(fields, "Повтор пароля", _repeat));
        Compose(fields, OkCancel("Создать", () => SaveClicked?.Invoke(this, EventArgs.Empty)));
    }

    public event EventHandler? SaveClicked;

    public string Login => _login.Text;
    public string FullName => _fullName.Text;
    public UserRole Role => _role.EnumValue<UserRole>() ?? UserRole.Administrator;
    public string Password => _password.Text;
    public string PasswordRepeat => _repeat.Text;
}

internal sealed class PasswordForm : DialogForm, IPasswordView
{
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly TextBox _repeat = new() { UseSystemPasswordChar = true };

    public PasswordForm()
    {
        var fields = Ui.Fields();
        MapField("Password", Ui.AddField(fields, "Новый пароль", _password));
        MapField("PasswordRepeat", Ui.AddField(fields, "Повтор пароля", _repeat));
        Compose(fields, OkCancel("Сохранить", () => SaveClicked?.Invoke(this, EventArgs.Empty)));
    }

    public event EventHandler? SaveClicked;

    public string Title
    {
        set => Text = value;
    }

    public string Password => _password.Text;
    public string PasswordRepeat => _repeat.Text;
}

internal sealed class LogForm : ListForm, ILogView
{
    private readonly TextBox _text = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Font = new Font(FontFamily.GenericMonospace, 9.5f)
    };

    public LogForm() : base("Журнал действий (новые записи сверху)", new Size(1000, 600))
    {
        Arrange();
        Controls.Remove(Grid);
        Controls.Add(_text);
        _text.BringToFront();
    }

    public void ShowLines(IReadOnlyList<string> lines) => _text.Lines = [.. lines];
}
