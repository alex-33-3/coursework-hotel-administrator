using System.ComponentModel;
using Hotel.Core.Validation;
using Hotel.Views;

namespace Hotel.Forms;

/// <summary>Цвета интерфейса — в одном месте, формы не задают цвета сами.</summary>
internal static class Palette
{
    public static readonly Color Accent = Color.FromArgb(31, 95, 139);
    public static readonly Color AccentText = Color.White;
    public static readonly Color Panel = Color.FromArgb(240, 245, 249);
    public static readonly Color Overdue = Color.FromArgb(253, 226, 222);
    public static readonly Color Inactive = Color.FromArgb(120, 120, 120);
    public static readonly Color Error = Color.FromArgb(178, 34, 34);
    public static readonly Color Hint = Color.FromArgb(90, 90, 90);
}

/// <summary>Построение типовых элементов форм: таблицы, кнопки, поля с подписями.</summary>
internal static class Ui
{
    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            EnableHeadersVisualStyles = false
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Palette.Panel;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
        return grid;
    }

    /// <summary>Показывает строки в таблице, сохраняя выделение по позиции, если строк хватает.</summary>
    public static void Bind<T>(DataGridView grid, IReadOnlyList<T> rows)
    {
        var index = grid.CurrentRow?.Index ?? 0;
        grid.DataSource = new BindingList<T>(rows.ToList());
        if (rows.Count > 0 && grid.Columns.Count > 0)
        {
            var firstVisible = grid.Columns.GetFirstColumn(DataGridViewElementStates.Visible);
            if (firstVisible is not null)
                grid.CurrentCell = grid.Rows[Math.Min(index, rows.Count - 1)].Cells[firstVisible.Index];
        }
    }

    public static T? Selected<T>(DataGridView grid) where T : class => grid.CurrentRow?.DataBoundItem as T;

    public static Button Button(string text, Action onClick)
    {
        var button = new Button { Text = text, AutoSize = true, Padding = new Padding(8, 2, 8, 2), Margin = new Padding(0, 4, 8, 4) };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static FlowLayoutPanel Bar(params Control[] controls)
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(0, 4, 0, 4)
        };
        bar.Controls.AddRange(controls);
        return bar;
    }

    public static Label Caption(string text) =>
        new() { Text = text, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 6, 0) };

    public static Label Hint(string text = "") =>
        new() { Text = text, AutoSize = true, ForeColor = Palette.Hint, Margin = new Padding(0, 6, 8, 6), MaximumSize = new Size(900, 0) };

    /// <summary>Таблица «подпись — поле» в две колонки.</summary>
    public static TableLayoutPanel Fields()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Padding = new Padding(0, 0, 16, 0)
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return table;
    }

    public static T AddField<T>(TableLayoutPanel table, string caption, T control) where T : Control
    {
        table.RowCount++;
        control.Margin = new Padding(0, 4, 0, 4);
        if (control is TextBox or ComboBox or DateTimePicker && control.Width < 260)
            control.Width = 260;
        table.Controls.Add(Caption(caption), 0, table.RowCount - 1);
        table.Controls.Add(control, 1, table.RowCount - 1);
        return control;
    }

    public static DateTimePicker DatePicker(bool optional = false) =>
        new() { Format = DateTimePickerFormat.Short, ShowCheckBox = optional, Width = 160 };

    public static DateOnly GetDate(this DateTimePicker picker) => DateOnly.FromDateTime(picker.Value);

    public static void SetDate(this DateTimePicker picker, DateOnly date) => picker.Value = date.ToDateTime(TimeOnly.MinValue);

    public static NumericUpDown Number(int min, int max) => new() { Minimum = min, Maximum = max, Width = 80 };

    /// <summary>Выпадающий список перечисления с русскими названиями; <paramref name="anyText"/> — пункт «любой» (null).</summary>
    public static ComboBox EnumCombo<T>(string? anyText = null) where T : struct, Enum
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        if (anyText is not null)
            combo.Items.Add(new Presenters.Option<T?>(null, anyText));
        foreach (var value in Enum.GetValues<T>())
            combo.Items.Add(new Presenters.Option<T?>(value, Core.Models.EnumExtensions.GetDescription(value)));
        combo.SelectedIndex = 0;
        return combo;
    }

    public static T? EnumValue<T>(this ComboBox combo) where T : struct, Enum => (combo.SelectedItem as Presenters.Option<T?>)?.Value;

    public static void SelectEnum<T>(this ComboBox combo, T value) where T : struct, Enum
    {
        foreach (var item in combo.Items)
        {
            if (item is Presenters.Option<T?> option && Equals(option.Value, value))
                combo.SelectedItem = item;
        }
    }
}

/// <summary>
/// Основа диалогов: фиксированная рамка, кнопки «ОК/Отмена», закрытие с результатом.
/// Небольшие диалоги собирают содержимое через <see cref="Compose"/> — столбец строк с автоматическим размером,
/// и форма подстраивается под него. Окна с таблицами задают размер сами и раскладывают элементы через Dock.
/// Ошибки ввода показываются значком у поля (<see cref="ErrorProvider"/>) и списком под полями.
/// </summary>
internal class DialogForm : Form, IDialogView, IValidatingView
{
    private readonly ErrorProvider _errors = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
    private readonly Dictionary<string, Control> _fields = [];

    public DialogForm()
    {
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Font;
        Padding = new Padding(12);
    }

    /// <summary>Список ошибок ввода. В <see cref="Compose"/> добавляется сам, в окнах с Dock его размещает форма.</summary>
    protected Label ErrorText { get; } = new()
    {
        AutoSize = true,
        ForeColor = Palette.Error,
        MaximumSize = new Size(520, 0),
        Margin = new Padding(0, 6, 0, 6)
    };

    public void Close(bool accepted)
    {
        DialogResult = accepted ? DialogResult.OK : DialogResult.Cancel;
        Close();
    }

    public void ShowErrors(IReadOnlyList<ValidationError> errors)
    {
        ClearErrors();
        foreach (var error in errors)
        {
            if (_fields.TryGetValue(error.Field, out var control) && string.IsNullOrEmpty(_errors.GetError(control)))
                _errors.SetError(control, error.Message);
        }

        ErrorText.Text = string.Join(Environment.NewLine, errors.Select(e => "– " + e.Message));
        if (errors.Count > 0 && _fields.TryGetValue(errors[0].Field, out var first))
            first.Focus();
    }

    public void ClearErrors()
    {
        foreach (var control in _fields.Values)
            _errors.SetError(control, string.Empty);
        ErrorText.Text = string.Empty;
    }

    /// <summary>Связывает имя поля из ошибки проверки с элементом формы.</summary>
    protected void MapField(string field, Control control) => _fields[field] = control;

    /// <summary>Раскладывает строки сверху вниз, добавляет под ними список ошибок; форма принимает размер содержимого.</summary>
    protected void Compose(params Control[] rows)
    {
        var root = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Location = new Point(Padding.Left, Padding.Top)
        };
        foreach (var row in rows.Take(rows.Length - 1).Append(ErrorText).Append(rows[^1]))
        {
            row.Dock = DockStyle.None;
            row.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            root.RowCount++;
            root.Controls.Add(row, 0, root.RowCount - 1);
        }

        Controls.Add(root);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    /// <summary>Панель с кнопками: основное действие (Enter) и «Отмена» (Esc).</summary>
    protected FlowLayoutPanel OkCancel(string okText, Action onOk, string cancelText = "Отмена")
    {
        var ok = Ui.Button(okText, onOk);
        var cancel = new Button { Text = cancelText, AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(0, 4, 8, 4) };
        AcceptButton = ok;
        CancelButton = cancel;
        var bar = Ui.Bar(ok, cancel);
        bar.WrapContents = false;
        return bar;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _errors.Dispose();
        base.Dispose(disposing);
    }
}
