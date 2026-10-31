using Hotel.Core.Models;
using Hotel.Core.Validation;
using Hotel.Presenters;
using Hotel.Views;

namespace Hotel.Forms;

/// <summary>Подбор номера: условия сверху, подходящие номера в таблице, действия снизу.</summary>
internal sealed class RoomSearchForm : DialogForm, IRoomSearchView
{
    private readonly DateTimePicker _arrival = Ui.DatePicker();
    private readonly DateTimePicker _departure = Ui.DatePicker();
    private readonly ComboBox _class = Ui.EnumCombo<RoomClass>("Любой класс");
    private readonly NumericUpDown _places = Ui.Number(1, RoomValidator.MaxPlaces);
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Label _status = Ui.Hint();
    private readonly Button _checkIn;

    public RoomSearchForm()
    {
        Text = "Подбор номера";
        AutoSize = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        Size = new Size(1000, 560);
        MinimumSize = new Size(800, 400);

        MapField("Arrival", _arrival);
        MapField("Departure", _departure);
        MapField("Places", _places);

        _checkIn = Ui.Button("Поселить сейчас…", () => Raise(CheckInClicked));
        var top = Ui.Bar(Ui.Caption("Приезд"), _arrival, Ui.Caption("Отъезд"), _departure, Ui.Caption("Класс"), _class,
            Ui.Caption("Мест"), _places, Ui.Button("Подобрать", () => Raise(SearchClicked)));
        var bottom = Ui.Bar(Ui.Button("Забронировать…", () => Raise(ReserveClicked)), _checkIn,
            new Button { Text = "Закрыть", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(0, 4, 8, 4) });
        bottom.Dock = DockStyle.Bottom;
        CancelButton = (IButtonControl)bottom.Controls[^1];

        Controls.Add(_grid);
        ErrorText.Dock = DockStyle.Bottom;
        Controls.Add(ErrorText);
        Controls.Add(_status);
        _status.Dock = DockStyle.Top;
        Controls.Add(top);
        Controls.Add(bottom);
        _arrival.ValueChanged += (_, _) => SetCheckInEnabled(DateOnly.FromDateTime(DateTime.Today) == Arrival);
    }

    public event EventHandler? SearchClicked;
    public event EventHandler? ReserveClicked;
    public event EventHandler? CheckInClicked;

    public DateOnly Arrival
    {
        get => _arrival.GetDate();
        set => _arrival.SetDate(value);
    }

    public DateOnly Departure
    {
        get => _departure.GetDate();
        set => _departure.SetDate(value);
    }

    public RoomClass? RoomClass => _class.EnumValue<RoomClass>();

    public int Places
    {
        get => (int)_places.Value;
        set => _places.Value = value;
    }

    public AvailabilityRow? Selected => Ui.Selected<AvailabilityRow>(_grid);

    public void ShowRows(IReadOnlyList<AvailabilityRow> rows, string status)
    {
        Ui.Bind(_grid, rows);
        _status.Text = status;
    }

    public void SetCheckInEnabled(bool enabled) => _checkIn.Enabled = enabled;

    private void Raise(EventHandler? handler) => handler?.Invoke(this, EventArgs.Empty);
}

internal sealed class ReservationEditForm : DialogForm, IReservationEditView
{
    private readonly TextBox _contact = new();
    private readonly TextBox _phone = new();
    private readonly ComboBox _room = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker _arrival = Ui.DatePicker();
    private readonly DateTimePicker _departure = Ui.DatePicker();
    private readonly NumericUpDown _places = Ui.Number(1, RoomValidator.MaxPlaces);
    private readonly TextBox _note = new() { Multiline = true, Height = 50 };
    private readonly Label _cost = Ui.Hint();

    public ReservationEditForm()
    {
        var fields = Ui.Fields();
        MapField(nameof(ReservationDetails.ContactName), Ui.AddField(fields, "Контактное лицо (ФИО)", _contact));
        MapField(nameof(ReservationDetails.Phone), Ui.AddField(fields, "Телефон", _phone));
        MapField(nameof(ReservationDetails.RoomNumber), Ui.AddField(fields, "Номер", _room));
        MapField("Arrival", Ui.AddField(fields, "Дата приезда", _arrival));
        MapField("Departure", Ui.AddField(fields, "Дата отъезда", _departure));
        MapField(nameof(ReservationDetails.Places), Ui.AddField(fields, "Число мест", _places));
        MapField(nameof(ReservationDetails.Note), Ui.AddField(fields, "Примечание", _note));
        fields.RowCount++;
        fields.Controls.Add(_cost, 1, fields.RowCount - 1);
        _cost.MaximumSize = new Size(420, 0);

        foreach (var control in new Control[] { _room, _arrival, _departure, _places })
        {
            void Changed(object? s, EventArgs e) => InputChanged?.Invoke(this, EventArgs.Empty);
            if (control is ComboBox combo)
                combo.SelectedIndexChanged += Changed;
            else if (control is DateTimePicker picker)
                picker.ValueChanged += Changed;
            else
                ((NumericUpDown)control).ValueChanged += Changed;
        }

        Compose(fields, OkCancel("Сохранить бронь", () => SaveClicked?.Invoke(this, EventArgs.Empty)));
    }

    public event EventHandler? InputChanged;
    public event EventHandler? SaveClicked;

    public string Title
    {
        set => Text = value;
    }

    public string ContactName
    {
        get => _contact.Text;
        set => _contact.Text = value;
    }

    public string Phone
    {
        get => _phone.Text;
        set => _phone.Text = value;
    }

    public string? RoomNumber
    {
        get => (_room.SelectedItem as Option<string>)?.Value;
        set => _room.SelectedItem = _room.Items.OfType<Option<string>>().FirstOrDefault(o => o.Value == value);
    }

    public DateOnly Arrival
    {
        get => _arrival.GetDate();
        set => _arrival.SetDate(value);
    }

    public DateOnly Departure
    {
        get => _departure.GetDate();
        set => _departure.SetDate(value);
    }

    public int Places
    {
        get => (int)_places.Value;
        set => _places.Value = value;
    }

    public string Note
    {
        get => _note.Text;
        set => _note.Text = value;
    }

    public void SetRooms(IReadOnlyList<Option<string>> rooms)
    {
        _room.Items.Clear();
        _room.Items.AddRange([.. rooms]);
    }

    public void SetCost(string text) => _cost.Text = text;
}

/// <summary>Поселение: сведения о номере и сроке, поиск гостя по паспорту, список заселяемых гостей.</summary>
internal sealed class CheckInForm : DialogForm, ICheckInView
{
    private readonly Label _header = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 0, 0, 8) };
    private readonly TextBox _passport = new() { Width = 180, PlaceholderText = "MP1234567" };
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Label _status = Ui.Hint();
    private readonly Label _error = new() { AutoSize = true, ForeColor = Palette.Error };
    private readonly Button _find;
    private readonly Button _register;

    public CheckInForm()
    {
        Text = "Поселение";
        AutoSize = false;
        Size = new Size(860, 480);
        _header.Font = new Font(Font, FontStyle.Bold);
        _grid.Dock = DockStyle.Fill;

        _find = Ui.Button("Найти и добавить", () => Raise(FindClicked));
        _register = Ui.Button("Зарегистрировать нового гостя…", () => Raise(RegisterClicked));
        _passport.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Raise(FindClicked);
            }
        };
        var search = Ui.Bar(Ui.Caption("Паспорт гостя:"), _passport, _find, _register, _error);
        var list = Ui.Bar(Ui.Button("Убрать из списка", () => Raise(RemoveClicked)), _status);

        Controls.Add(_grid);
        Controls.Add(list);
        list.Dock = DockStyle.Bottom;
        Controls.Add(search);
        Controls.Add(_header);
        var buttons = OkCancel("Оформить поселение и квитанции", () => Raise(ConfirmClicked));
        buttons.Dock = DockStyle.Bottom;
        Controls.Add(buttons);
        AcceptButton = null;
    }

    public event EventHandler? FindClicked;
    public event EventHandler? RegisterClicked;
    public event EventHandler? RemoveClicked;
    public event EventHandler? ConfirmClicked;

    public string PassportText
    {
        get => _passport.Text;
        set => _passport.Text = value;
    }

    public int SelectedGuestIndex => _grid.CurrentRow?.Index ?? -1;

    public void SetHeader(string text) => _header.Text = text;

    public void ShowGuests(IReadOnlyList<CheckInGuestRow> rows, string status, bool canAdd)
    {
        Ui.Bind(_grid, rows);
        _status.Text = status;
        _error.Text = string.Empty;
        _find.Enabled = canAdd;
        _register.Enabled = canAdd;
    }

    public void ShowError(string message) => _error.Text = message;

    private void Raise(EventHandler? handler) => handler?.Invoke(this, EventArgs.Empty);
}

internal sealed class GuestEditForm : DialogForm, IGuestEditView
{
    private readonly TextBox _lastName = new();
    private readonly TextBox _firstName = new();
    private readonly TextBox _middleName = new();
    private readonly DateTimePicker _birthDate = Ui.DatePicker(optional: true);
    private readonly ComboBox _citizenship = new() { DropDownStyle = ComboBoxStyle.DropDown };
    private readonly TextBox _passport = new() { CharacterCasing = CharacterCasing.Upper };
    private readonly TextBox _issuedBy = new();
    private readonly DateTimePicker _issueDate = Ui.DatePicker(optional: true);
    private readonly TextBox _phone = new();

    public GuestEditForm()
    {
        _citizenship.Items.AddRange([Guest.DefaultCitizenship, "Российская Федерация", "Республика Польша", "Литовская Республика",
            "Латвийская Республика", "Украина", "Республика Казахстан", "Германия", "Китайская Народная Республика"]);

        var fields = Ui.Fields();
        MapField(nameof(Guest.LastName), Ui.AddField(fields, "Фамилия", _lastName));
        MapField(nameof(Guest.FirstName), Ui.AddField(fields, "Имя", _firstName));
        MapField(nameof(Guest.MiddleName), Ui.AddField(fields, "Отчество", _middleName));
        MapField(nameof(Guest.BirthDate), Ui.AddField(fields, "Дата рождения", _birthDate));
        MapField(nameof(Guest.Citizenship), Ui.AddField(fields, "Гражданство", _citizenship));
        MapField(nameof(Guest.PassportNumber), Ui.AddField(fields, "Серия и номер паспорта", _passport));
        MapField(nameof(Guest.PassportIssuedBy), Ui.AddField(fields, "Кем выдан", _issuedBy));
        MapField(nameof(Guest.PassportIssueDate), Ui.AddField(fields, "Дата выдачи", _issueDate));
        MapField(nameof(Guest.Phone), Ui.AddField(fields, "Телефон", _phone));

        Compose(fields, OkCancel("Сохранить", () => SaveClicked?.Invoke(this, EventArgs.Empty)));
    }

    public event EventHandler? SaveClicked;

    public string Title
    {
        set => Text = value;
    }

    public string LastName
    {
        get => _lastName.Text;
        set => _lastName.Text = value;
    }

    public string FirstName
    {
        get => _firstName.Text;
        set => _firstName.Text = value;
    }

    public string MiddleName
    {
        get => _middleName.Text;
        set => _middleName.Text = value;
    }

    public DateOnly? BirthDate
    {
        get => GetOptional(_birthDate);
        set => SetOptional(_birthDate, value, new DateOnly(1990, 1, 1));
    }

    public string Citizenship
    {
        get => _citizenship.Text;
        set => _citizenship.Text = value;
    }

    public string PassportNumber
    {
        get => _passport.Text;
        set => _passport.Text = value;
    }

    public string PassportIssuedBy
    {
        get => _issuedBy.Text;
        set => _issuedBy.Text = value;
    }

    public DateOnly? PassportIssueDate
    {
        get => GetOptional(_issueDate);
        set => SetOptional(_issueDate, value, DateOnly.FromDateTime(DateTime.Today));
    }

    public string Phone
    {
        get => _phone.Text;
        set => _phone.Text = value;
    }

    private static DateOnly? GetOptional(DateTimePicker picker) => picker.Checked ? picker.GetDate() : null;

    /// <summary>
    /// Пустая дата — снятый флажок и подсказка <paramref name="placeholder"/>. До создания окна элемент
    /// при снятом флажке показывает сегодняшнюю дату, поэтому значение применяется, когда окно создано.
    /// </summary>
    private static void SetOptional(DateTimePicker picker, DateOnly? value, DateOnly placeholder)
    {
        void Apply()
        {
            picker.SetDate(value ?? placeholder);
            picker.Checked = value is not null;
        }

        if (picker.IsHandleCreated)
            Apply();
        else
            picker.HandleCreated += (_, _) => Apply();
    }
}

internal sealed class ExtendStayForm : DialogForm, IExtendStayView
{
    private readonly Label _info = new() { AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
    private readonly DateTimePicker _departure = Ui.DatePicker();
    private readonly Label _cost = new() { AutoSize = true, Font = new Font(DefaultFont, FontStyle.Bold) };
    private readonly Label _error = new() { AutoSize = true, ForeColor = Palette.Error, MaximumSize = new Size(480, 0) };

    public ExtendStayForm()
    {
        Text = "Продление проживания";
        var fields = Ui.Fields();
        Ui.AddField(fields, "Новая дата отъезда", _departure);
        fields.RowCount += 2;
        fields.Controls.Add(_cost, 1, fields.RowCount - 2);
        fields.Controls.Add(_error, 1, fields.RowCount - 1);
        _departure.ValueChanged += (_, _) => DepartureChanged?.Invoke(this, EventArgs.Empty);

        Compose(_info, fields, OkCancel("Продлить и выписать квитанцию", () => SaveClicked?.Invoke(this, EventArgs.Empty)));
    }

    public event EventHandler? DepartureChanged;
    public event EventHandler? SaveClicked;

    public DateOnly NewDeparture
    {
        get => _departure.GetDate();
        set => _departure.SetDate(value);
    }

    public void SetInfo(string text) => _info.Text = text;

    public void SetCost(string text)
    {
        _cost.Text = text;
        _error.Text = string.Empty;
    }

    public void ShowError(string message) => _error.Text = message;
}

/// <summary>Выписка: таблица окончательного расчета и итог (доплата, возврат или ноль).</summary>
internal sealed class CheckOutForm : DialogForm, ICheckOutView
{
    private readonly Label _title = new() { AutoSize = true, Padding = new Padding(0, 0, 0, 8) };
    private readonly TableLayoutPanel _lines = Ui.Fields();
    private readonly Label _total = new() { AutoSize = true, Padding = new Padding(0, 10, 0, 0) };

    public CheckOutForm()
    {
        Text = "Выписка гостя";
        _title.Font = new Font(Font.FontFamily, 11, FontStyle.Bold);
        _total.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
        _total.ForeColor = Palette.Accent;

        Compose(_title, _lines, _total, OkCancel("Выписать", () => ConfirmClicked?.Invoke(this, EventArgs.Empty)));
    }

    public event EventHandler? ConfirmClicked;

    public void SetTitle(string text) => _title.Text = text;

    public void SetSettlement(IReadOnlyList<(string Caption, string Value)> lines, string total)
    {
        _lines.Controls.Clear();
        _lines.RowCount = 0;
        foreach (var (caption, value) in lines)
            Ui.AddField(_lines, caption, new Label { Text = value, AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        _total.Text = total;
    }
}
