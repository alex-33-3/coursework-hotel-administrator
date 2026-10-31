using Hotel.Core.Models;
using Hotel.Core.Search;
using Hotel.Core.Services;
using Hotel.Presenters;
using Hotel.Views;

namespace Hotel.Forms;

/// <summary>Вкладка главной формы: таблица на всю площадь, панели сверху и снизу.</summary>
internal abstract class TabView : UserControl
{
    protected TabView()
    {
        Grid.DoubleClick += (_, _) => OnGridDoubleClick();
    }

    protected DataGridView Grid { get; } = Ui.Grid();

    protected void Arrange(Control? top, Control bottom)
    {
        Controls.Add(Grid);
        if (top is not null)
            Controls.Add(top);
        bottom.Dock = DockStyle.Bottom;
        Controls.Add(bottom);
    }

    protected virtual void OnGridDoubleClick()
    {
    }

    protected void Raise(EventHandler? handler) => handler?.Invoke(this, EventArgs.Empty);

    /// <summary>Подсвечивает строки, для которых <paramref name="highlight"/> истинно.</summary>
    protected void Highlight<T>(Func<T, bool> highlight, Color color) where T : class
    {
        Grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewRow row in Grid.Rows)
            {
                if (row.DataBoundItem is T item && highlight(item))
                    row.DefaultCellStyle.BackColor = color;
            }
        };
    }
}

internal sealed class GuestsTab : TabView, IGuestsTabView
{
    private readonly ComboBox _filter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly ComboBox _field = Ui.EnumCombo<GuestField>();
    private readonly TextBox _text = new() { Width = 220, PlaceholderText = "Текст для поиска" };
    private readonly Label _status = Ui.Hint();

    public GuestsTab()
    {
        _filter.Items.AddRange([
            new Option<GuestListFilter>(GuestListFilter.Living, "Проживают"),
            new Option<GuestListFilter>(GuestListFilter.CheckedOut, "Выехали"),
            new Option<GuestListFilter>(GuestListFilter.All, "Все гости")
        ]);
        _filter.SelectedIndex = 0;
        _filter.SelectedIndexChanged += (_, _) => Raise(FilterChanged);
        _text.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Raise(SearchClicked);
            }
        };

        var top = Ui.Bar(
            Ui.Caption("Показать:"), _filter,
            Ui.Caption("   Поиск гостя по признаку:"), _field, _text,
            Ui.Button("Найти", () => Raise(SearchClicked)),
            Ui.Button("Сбросить", () => Raise(ResetClicked)));
        var bottom = Ui.Bar(
            Ui.Button("Изменить данные гостя…", () => Raise(EditClicked)),
            Ui.Button("Квитанции гостя…", () => Raise(ReceiptsClicked)),
            Ui.Button("Продлить проживание…", () => Raise(ExtendClicked)),
            Ui.Button("Выписать / досрочный отъезд…", () => Raise(CheckOutClicked)),
            _status);
        Arrange(top, bottom);
        Highlight<GuestGridRow>(r => r.IsOverdue, Palette.Overdue);
        Highlight<GuestGridRow>(r => !r.IsActive, SystemColors.Control);
    }

    public event EventHandler? FilterChanged;
    public event EventHandler? SearchClicked;
    public event EventHandler? ResetClicked;
    public event EventHandler? EditClicked;
    public event EventHandler? ReceiptsClicked;
    public event EventHandler? ExtendClicked;
    public event EventHandler? CheckOutClicked;

    public GuestListFilter Filter => (_filter.SelectedItem as Option<GuestListFilter>)?.Value ?? GuestListFilter.Living;

    public GuestField SearchField => _field.EnumValue<GuestField>() ?? GuestField.Any;

    public string SearchText
    {
        get => _text.Text;
        set => _text.Text = value;
    }

    public GuestGridRow? Selected => Ui.Selected<GuestGridRow>(Grid);

    public void ShowRows(IReadOnlyList<GuestGridRow> rows, string status)
    {
        Ui.Bind(Grid, rows);
        _status.Text = status;
    }

    protected override void OnGridDoubleClick() => Raise(EditClicked);
}

internal sealed class ArrivalsTab : TabView, IArrivalsTabView
{
    public ArrivalsTab()
    {
        Arrange(Ui.Bar(Ui.Hint("Брони с датой приезда сегодня. Заселение — только в день приезда; " +
                              "брони без заселения на следующий день получают состояние «Незаезд».")),
            Ui.Bar(
                Ui.Button("Заселить по брони…", () => Raise(CheckInClicked)),
                Ui.Button("Отменить бронь", () => Raise(CancelClicked))));
    }

    public event EventHandler? CheckInClicked;
    public event EventHandler? CancelClicked;

    public ReservationRow? Selected => Ui.Selected<ReservationRow>(Grid);

    public void ShowRows(IReadOnlyList<ReservationRow> rows) => Ui.Bind(Grid, rows);

    protected override void OnGridDoubleClick() => Raise(CheckInClicked);
}

internal sealed class DeparturesTab : TabView, IDeparturesTabView
{
    public DeparturesTab()
    {
        Arrange(Ui.Bar(Ui.Hint("Постояльцы, отъезжающие сегодня, и не выехавшие в срок (выделены). Расчетный час — 12:00: " +
                              "при выезде позже оформляется задержка и дополнительная квитанция.")),
            Ui.Bar(
                Ui.Button("Выписать…", () => Raise(CheckOutClicked)),
                Ui.Button("Продлить проживание…", () => Raise(ExtendClicked))));
        Highlight<DepartureRow>(r => r.IsOverdue, Palette.Overdue);
    }

    public event EventHandler? CheckOutClicked;
    public event EventHandler? ExtendClicked;

    public DepartureRow? Selected => Ui.Selected<DepartureRow>(Grid);

    public void ShowRows(IReadOnlyList<DepartureRow> rows) => Ui.Bind(Grid, rows);

    protected override void OnGridDoubleClick() => Raise(CheckOutClicked);
}

internal sealed class ReservationsTab : TabView, IReservationsTabView
{
    private readonly DateTimePicker _from = Ui.DatePicker();
    private readonly DateTimePicker _to = Ui.DatePicker();
    private readonly ComboBox _status = Ui.EnumCombo<ReservationStatus>("Любое состояние");
    private readonly TextBox _text = new() { Width = 200, PlaceholderText = "Контакт, телефон, номер" };

    public ReservationsTab()
    {
        Arrange(
            Ui.Bar(Ui.Caption("Период с"), _from, Ui.Caption("по"), _to, Ui.Caption("Состояние"), _status, _text,
                Ui.Button("Показать", () => Raise(SearchClicked))),
            Ui.Bar(
                Ui.Button("Новая бронь…", () => Raise(NewClicked)),
                Ui.Button("Изменить…", () => Raise(EditClicked)),
                Ui.Button("Отменить бронь", () => Raise(CancelClicked)),
                Ui.Button("Заселить по брони…", () => Raise(CheckInClicked))));
        Highlight<ReservationRow>(r => !r.CanChange, SystemColors.Control);
    }

    public event EventHandler? SearchClicked;
    public event EventHandler? NewClicked;
    public event EventHandler? EditClicked;
    public event EventHandler? CancelClicked;
    public event EventHandler? CheckInClicked;

    public DateOnly From
    {
        get => _from.GetDate();
        set => _from.SetDate(value);
    }

    public DateOnly To
    {
        get => _to.GetDate();
        set => _to.SetDate(value);
    }

    public ReservationStatus? Status => _status.EnumValue<ReservationStatus>();

    public string SearchText => _text.Text;

    public ReservationRow? Selected => Ui.Selected<ReservationRow>(Grid);

    public void ShowRows(IReadOnlyList<ReservationRow> rows) => Ui.Bind(Grid, rows);

    protected override void OnGridDoubleClick() => Raise(EditClicked);
}

internal sealed class RoomsTab : TabView, IRoomsTabView
{
    private readonly ComboBox _class = Ui.EnumCombo<RoomClass>("Все классы");
    private readonly Label _total = Ui.Hint();

    public RoomsTab()
    {
        _class.SelectedIndexChanged += (_, _) => Raise(FilterChanged);
        Arrange(Ui.Bar(Ui.Caption("Класс номера:"), _class), Ui.Bar(_total));
    }

    public event EventHandler? FilterChanged;

    public RoomClass? ClassFilter => _class.EnumValue<RoomClass>();

    public void ShowRows(IReadOnlyList<RoomOccupancyRow> rows, string total)
    {
        Ui.Bind(Grid, rows);
        _total.Text = total;
    }
}
