using System.Drawing.Printing;
using Hotel.Core.Models;
using Hotel.Core.Services;
using Hotel.Presenters;

namespace Hotel.Printing;

/// <summary>
/// Бланк квитанции для печати и предварительного просмотра. Один и тот же документ показывается в форме
/// квитанции (<see cref="PrintPreviewControl"/>) и отправляется на принтер.
/// </summary>
internal sealed class ReceiptDocument : PrintDocument
{
    public const string HotelName = "Гостиница «Уют»";

    private readonly ReceiptCard _card;

    public ReceiptDocument(ReceiptCard card)
    {
        _card = card;
        DocumentName = card.Title;
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        base.OnPrintPage(e);
        var g = e.Graphics!;
        var area = e.MarginBounds;
        using var title = new Font("Segoe UI", 16, FontStyle.Bold);
        using var bold = new Font("Segoe UI", 10, FontStyle.Bold);
        using var normal = new Font("Segoe UI", 10);
        using var small = new Font("Segoe UI", 8.5f);
        using var pen = new Pen(Color.Black, 1);

        var receipt = _card.Receipt;
        var stay = _card.Stay;
        var guest = _card.Guest;
        float y = area.Top;
        float left = area.Left;
        float width = area.Width;

        void Line(string text, Font font, float gap = 4)
        {
            var size = g.MeasureString(text, font, (int)width);
            g.DrawString(text, font, Brushes.Black, new RectangleF(left, y, width, size.Height));
            y += size.Height + gap;
        }

        void Pair(string caption, string value)
        {
            g.DrawString(caption, normal, Brushes.Black, left, y);
            var size = g.MeasureString(value, bold, (int)(width - 190));
            g.DrawString(value, bold, Brushes.Black, new RectangleF(left + 190, y, width - 190, size.Height));
            y += Math.Max(size.Height, normal.GetHeight(g)) + 3;
        }

        Line(HotelName, bold, 2);
        Line("Квитанция об оплате услуг проживания", small, 12);
        Line($"{_card.Title} — {_card.KindName}", title, 10);

        Pair("Дата и время", Format.DateTime(receipt.IssuedAt));
        Pair("Гость", guest.FullName);
        Pair("Паспорт", $"{guest.PassportNumber}, выдан {guest.PassportIssuedBy} {Format.Date(guest.PassportIssueDate)}");
        Pair("Гражданство", guest.Citizenship);
        Pair("Номер", _card.RoomText);
        Pair("Регистрационная карточка", $"№ {stay.Id}");
        Pair("Период проживания", Format.Period(stay.Arrival, stay.Departure) + (stay.CheckedOutAt is { } at
            ? $" (выезд {Format.DateTime(at)})"
            : string.Empty));
        y += 10;

        // Таблица строк: наименование, количество, цена, сумма.
        float[] widths = [width - 270, 70, 100, 100];
        string[] headers = ["Наименование", "Кол-во", "Цена", "Сумма"];
        void Row(IReadOnlyList<string> cells, Font font)
        {
            var height = Math.Max(g.MeasureString(cells[0], font, (int)widths[0] - 6).Height, font.GetHeight(g)) + 6;
            float x = left;
            for (var i = 0; i < cells.Count; i++)
            {
                g.DrawRectangle(pen, x, y, widths[i], height);
                var format = new StringFormat { Alignment = i == 0 ? StringAlignment.Near : StringAlignment.Far };
                g.DrawString(cells[i], font, Brushes.Black, new RectangleF(x + 3, y + 3, widths[i] - 6, height - 6), format);
                x += widths[i];
            }

            y += height;
        }

        Row(headers, bold);
        foreach (var line in receipt.Lines)
            Row([line.Description, line.Quantity.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture),
                Format.Money(line.Price), Format.Money(line.Amount)], normal);
        y += 8;

        var totalCaption = receipt.Kind == ReceiptKind.Refund ? "Итого к возврату гостю:" : "Итого к оплате:";
        Line($"{totalCaption} {Format.Money(receipt.Total)}", title, 24);

        Pair("Оформил", $"{receipt.IssuedBy}  ____________________");
        y += 12;
        Pair("Подпись гостя", "____________________");
        e.HasMorePages = false;
    }
}

/// <summary>Печать квитанции через стандартный диалог выбора принтера.</summary>
internal sealed class ReceiptPrinter : IReceiptPrinter
{
    public bool Print(ReceiptCard card)
    {
        using var document = new ReceiptDocument(card);
        using var dialog = new PrintDialog { Document = document, UseEXDialog = true };
        if (dialog.ShowDialog(Form.ActiveForm) != DialogResult.OK)
            return false;

        try
        {
            document.Print();
            return true;
        }
        catch (InvalidPrinterException ex)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }
}
