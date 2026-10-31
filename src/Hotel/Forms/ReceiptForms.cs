using System.Drawing.Printing;
using Hotel.Core.Services;
using Hotel.Presenters;
using Hotel.Printing;
using Hotel.Views;

namespace Hotel.Forms;

/// <summary>Квитанция: предварительный просмотр бланка и печать. Если квитанций несколько — список слева.</summary>
internal sealed class ReceiptForm : DialogForm, IReceiptView
{
    private readonly ListBox _list = new() { Dock = DockStyle.Left, Width = 260, IntegralHeight = false };
    private readonly PrintPreviewControl _preview = new() { Dock = DockStyle.Fill, Zoom = 0.75, UseAntiAlias = true };
    private readonly Label _error = new() { Dock = DockStyle.Fill, ForeColor = Palette.Error, Visible = false };
    private ReceiptDocument? _document;

    public ReceiptForm()
    {
        Text = "Квитанция";
        AutoSize = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        Size = new Size(820, 800);
        _list.SelectedIndexChanged += (_, _) => SelectionChanged?.Invoke(this, EventArgs.Empty);

        var bar = Ui.Bar(
            Ui.Button("Печать…", () => PrintClicked?.Invoke(this, EventArgs.Empty)),
            Ui.Button("Крупнее", () => _preview.Zoom = Math.Min(2, _preview.Zoom + 0.15)),
            Ui.Button("Мельче", () => _preview.Zoom = Math.Max(0.3, _preview.Zoom - 0.15)),
            new Button { Text = "Закрыть", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(0, 4, 8, 4) });
        bar.Dock = DockStyle.Bottom;
        CancelButton = (IButtonControl)bar.Controls[^1];

        Controls.Add(_preview);
        Controls.Add(_error);
        Controls.Add(_list);
        Controls.Add(bar);
    }

    public event EventHandler? SelectionChanged;
    public event EventHandler? PrintClicked;

    public int? SelectedNumber => (_list.SelectedItem as Option<int>)?.Value;

    public void ShowList(IReadOnlyList<Option<int>> receipts)
    {
        _list.Items.Clear();
        _list.Items.AddRange([.. receipts]);
        _list.Visible = receipts.Count > 1;
        if (receipts.Count > 0)
            _list.SelectedIndex = 0;
    }

    public void ShowCard(ReceiptCard card)
    {
        Text = $"{card.Title} — {card.KindName}";
        _document?.Dispose();
        _document = new ReceiptDocument(card);
        try
        {
            _preview.Document = _document;
            _preview.InvalidatePreview();
        }
        catch (InvalidPrinterException ex)
        {
            // Без установленного принтера Windows не может разметить страницу для просмотра.
            _preview.Visible = false;
            _error.Visible = true;
            _error.Text = $"Предварительный просмотр недоступен: {ex.Message}";
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _document?.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Реестр квитанций за период или квитанции одного гостя.</summary>
internal sealed class ReceiptsForm : DialogForm, IReceiptsView
{
    private readonly DateTimePicker _from = Ui.DatePicker();
    private readonly DateTimePicker _to = Ui.DatePicker();
    private readonly FlowLayoutPanel _period;
    private readonly DataGridView _grid = Ui.Grid();
    private readonly Label _summary = Ui.Hint();

    public ReceiptsForm()
    {
        AutoSize = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        Size = new Size(1000, 560);
        _period = Ui.Bar(Ui.Caption("Выписаны с"), _from, Ui.Caption("по"), _to,
            Ui.Button("Показать", () => SearchClicked?.Invoke(this, EventArgs.Empty)));
        var bottom = Ui.Bar(Ui.Button("Открыть квитанцию…", () => OpenClicked?.Invoke(this, EventArgs.Empty)), _summary);
        bottom.Dock = DockStyle.Bottom;
        _grid.DoubleClick += (_, _) => OpenClicked?.Invoke(this, EventArgs.Empty);

        Controls.Add(_grid);
        Controls.Add(_period);
        Controls.Add(bottom);
    }

    public event EventHandler? SearchClicked;
    public event EventHandler? OpenClicked;

    public string Title
    {
        set => Text = value;
    }

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

    public ReceiptRow? Selected => Ui.Selected<ReceiptRow>(_grid);

    public void SetPeriodVisible(bool visible) => _period.Visible = visible;

    public void ShowRows(IReadOnlyList<ReceiptRow> rows, string summary)
    {
        Ui.Bind(_grid, rows);
        _summary.Text = summary;
    }
}
