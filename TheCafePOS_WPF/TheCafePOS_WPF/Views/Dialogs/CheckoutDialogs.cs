using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs;

// Enter a voucher code; the view model validates it so the dialog shows the same message checkout would.
public static class VoucherModal
{
    /// <param name="apply">Applies a code (empty removes it); returns null on success or the refusal reason.</param>
    public static bool Show(Window owner, string current, Func<string, string?> apply)
    {
        var w = PosUi.Dialog(owner, "Voucher", 440);
        var code = new TextBox { Text = current, FontSize = 22, Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 8), CharacterCasing = CharacterCasing.Upper, MaxLength = 20 };
        MaterialDesignThemes.Wpf.HintAssist.SetHint(code, "Mã voucher");
        TouchInput.SetLabel(code, "Mã voucher");
        var error = PosUi.Text("", 13, color: PosUi.Danger);
        var actions = new UniformGrid { Columns = 3, Margin = new Thickness(-4, 12, -4, 0) };
        actions.Children.Add(PosUi.Button("Bỏ voucher", (_, _) => { apply(""); w.DialogResult = true; }));
        actions.Children.Add(PosUi.Button("Hủy", (_, _) => w.DialogResult = false));
        var ok = PosUi.Button("Áp dụng", (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(code.Text)) { error.Text = "Nhập mã voucher."; FieldError.Mark(code, error.Text); return; }
            if (apply(code.Text) is { } refused) { error.Text = refused; FieldError.Mark(code, refused); return; }
            w.DialogResult = true;
        }, "PrimaryButton");
        ok.IsDefault = true;
        actions.Children.Add(ok);
        w.Content = new StackPanel { Margin = new Thickness(20), Children = { PosUi.Text("Áp dụng voucher", 20, true), PosUi.Text("Voucher tính trên tạm tính sau khuyến mãi tự động, mỗi đơn dùng một mã.", 13, color: PosUi.Muted), new Border { Height = 10 }, code, error, actions } };
        w.Loaded += (_, _) => { code.Focus(); code.SelectAll(); };
        return w.ShowDialog() == true;
    }
}

// Pick the lines that go to a separate bill; they are held as a new order to be paid on their own.
public static class SplitBillModal
{
    public static List<OrderItem>? Show(Window owner, IReadOnlyList<OrderItem> lines)
    {
        var w = PosUi.Dialog(owner, "Tách hóa đơn", 520);
        var boxes = lines.Select(l => new CheckBox
        {
            Tag = l, Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(8, 0, 0, 0), VerticalContentAlignment = VerticalAlignment.Center,
            Content = new DockPanel { Width = 400, Children = { Right(PosUi.Text($"{l.TotalPrice:N0}đ", 15, true)), PosUi.Text($"{l.Quantity}× {l.ProductName}" + (string.IsNullOrWhiteSpace(l.OptionsSummary) ? "" : $" · {l.OptionsSummary}"), 15) } }
        }).ToList();
        var summary = PosUi.Text("", 14, true, PosUi.Accent);
        var error = PosUi.Text("", 13, color: PosUi.Danger);
        void Update()
        {
            var moving = boxes.Where(b => b.IsChecked == true).Select(b => (OrderItem)b.Tag).ToList();
            summary.Text = moving.Count == 0 ? "Chọn món chuyển sang hóa đơn mới."
                : $"Hóa đơn mới: {moving.Sum(i => i.Quantity)} món, {moving.Sum(i => i.TotalPrice):N0}đ · Còn lại: {lines.Except(moving).Sum(i => i.TotalPrice):N0}đ";
        }
        foreach (var b in boxes) { b.Checked += (_, _) => Update(); b.Unchecked += (_, _) => Update(); }
        List<OrderItem>? result = null;
        var ok = PosUi.Button("Tách sang đơn giữ", (_, _) =>
        {
            var moving = boxes.Where(b => b.IsChecked == true).Select(b => (OrderItem)b.Tag).ToList();
            if (moving.Count == 0 || moving.Count == lines.Count) { error.Text = "Chọn ít nhất một món và giữ lại ít nhất một món."; return; }
            result = moving; w.DialogResult = true;
        }, "PrimaryButton");
        ok.IsDefault = true;
        var cancel = PosUi.Button("Hủy", (_, _) => w.DialogResult = false);
        cancel.IsCancel = true;
        var list = new StackPanel();
        foreach (var b in boxes) list.Children.Add(b);
        w.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                PosUi.Text("Tách hóa đơn", 20, true),
                PosUi.Text("Món được chọn chuyển sang một đơn giữ mới để thanh toán riêng. Muốn tách 1 ly trong dòng nhiều ly, dùng \"Tách 1 sản phẩm\" ở menu ⋯ trước.", 13, color: PosUi.Muted),
                new Border { Height = 10 }, new ScrollViewer { Content = list, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                summary, error, new UniformGrid { Columns = 2, Margin = new Thickness(-4, 12, -4, 0), Children = { cancel, ok } }
            }
        };
        w.Loaded += (_, _) => Update();
        return w.ShowDialog() == true ? result : null;
    }

    private static UIElement Right(UIElement e) { DockPanel.SetDock(e, Dock.Right); return e; }
}

// Receipt preview with print and save; the text comes from ReceiptService so paper and file match.
public sealed class ReceiptWindow : Window
{
    private readonly Order _order;
    private readonly string _text;

    public ReceiptWindow(IStoreSettingsService settings, ICustomerService customers, Order order, bool reprint)
    {
        _order = order;
        _text = ReceiptService.Text(order, settings.Settings, customers.FindCustomer(order.CustomerPhone), reprint);
        Title = $"Hóa đơn #{order.DailyOrderNumber:D2}"; Width = 520; Height = 760; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current.TryFindResource("PosWindow") is Style style) Style = style;
        var paper = new Border
        {
            Background = Brushes.White, BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(1), Padding = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center,
            Child = new TextBlock { Text = _text, FontFamily = new FontFamily("Consolas"), FontSize = 13, Foreground = Brushes.Black }
        };
        var print = PosUi.Button("In hóa đơn", (_, _) => Print(), "PrimaryButton", 48);
        print.IsDefault = true;
        var save = PosUi.Button("Lưu file .txt", (_, _) => Save(), minHeight: 48);
        var close = PosUi.Button("Đóng", (_, _) => Close(), minHeight: 48);
        close.IsCancel = true;
        var actions = new UniformGrid { Columns = 3, Margin = new Thickness(-4, 12, -4, 0), Children = { close, save, print } };
        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(actions, Dock.Bottom);
        root.Children.Add(actions);
        root.Children.Add(new ScrollViewer { Content = paper, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }

    private void Print()
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        try
        {
            var doc = new FlowDocument(new Paragraph(new Run(_text)) { Margin = new Thickness(0) })
            {
                FontFamily = new FontFamily("Consolas"), FontSize = 10, PagePadding = new Thickness(12),
                PageWidth = dialog.PrintableAreaWidth, ColumnWidth = dialog.PrintableAreaWidth
            };
            dialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, Title);
            Toast.Show(this, "Đã gửi hóa đơn tới máy in.");
        }
        catch (Exception ex) { Toast.Show(this, "Không in được: " + ex.Message, true); }
    }

    private void Save()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { FileName = $"hoa-don-{_order.CreatedAt:yyyyMMdd}-{_order.DailyOrderNumber:D2}.txt", Filter = "Văn bản (*.txt)|*.txt" };
        if (dialog.ShowDialog(this) != true) return;
        try { System.IO.File.WriteAllText(dialog.FileName, _text, new System.Text.UTF8Encoding(true)); Toast.Show(this, "Đã lưu hóa đơn."); }
        catch (Exception ex) { Toast.Show(this, "Không lưu được: " + ex.Message, true); }
    }
}
