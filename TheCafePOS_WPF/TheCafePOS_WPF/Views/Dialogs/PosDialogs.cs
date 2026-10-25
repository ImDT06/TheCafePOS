using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs;

// Small code-only dialogs; they share the POS window style and keep touch-sized targets.
internal static class PosUi
{
    public static Brush Accent => Palette.Accent;
    public static Brush Money => Palette.Money;
    public static Brush Muted => Palette.Muted;
    public static Brush Danger => Palette.Danger;

    public static Window Dialog(Window owner, string title, double width)
    {
        var w = new Window { Title = title, Owner = owner, Width = width, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        if (Application.Current.TryFindResource("PosWindow") is Style style) w.Style = style;
        return w;
    }
    public static Button Button(string text, RoutedEventHandler click, string? style = null, double minHeight = 52)
    {
        var b = new Button { Content = text, MinHeight = minHeight, Margin = new Thickness(4), Padding = new Thickness(12, 6, 12, 6) };
        if (style != null && Application.Current.TryFindResource(style) is Style s) b.Style = s;
        b.Click += click;
        return b;
    }
    public static TextBlock Text(string text, double size = 14, bool bold = false, Brush? color = null) =>
        new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, Foreground = color ?? Brushes.Black, TextWrapping = TextWrapping.Wrap };
}

public sealed class DiscountModal
{
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = "";

    // Returns false when cancelled; Amount 0 means "remove discount".
    public bool Show(Window owner, decimal subtotal, decimal current, string currentReason)
    {
        var w = PosUi.Dialog(owner, "Giảm giá", 460);
        var amount = new TextBox { Text = current > 0 ? current.ToString("0") : "", FontSize = 20, Margin = new Thickness(0, 8, 0, 10) };
        TouchInput.SetNumeric(amount, true);
        MaterialDesignThemes.Wpf.HintAssist.SetHint(amount, "Số tiền giảm (đồng)");
        var reason = new ComboBox { IsEditable = true, Text = currentReason, FontSize = 16, Margin = new Thickness(0, 4, 0, 4), ItemsSource = new[] { "Khách quen", "Khuyến mãi khai trương", "Bù món lỗi / chờ lâu", "Nhân viên", "Voucher" } };
        MaterialDesignThemes.Wpf.HintAssist.SetHint(reason, "Lý do giảm giá");
        var preview = PosUi.Text("", 15, true, PosUi.Money);
        void Update()
        {
            decimal.TryParse(amount.Text, out var a);
            preview.Text = a > 0 && a < subtotal ? $"Còn phải thu: {subtotal - a:N0}đ" : a >= subtotal ? "Số tiền giảm phải nhỏ hơn tạm tính" : "";
            preview.Foreground = a >= subtotal ? PosUi.Danger : PosUi.Money;
        }
        amount.TextChanged += (_, _) => Update();
        var percents = new UniformGrid { Columns = 4, Margin = new Thickness(-4, 0, -4, 8) };
        foreach (var p in new[] { 5, 10, 15, 20 })
            percents.Children.Add(PosUi.Button($"{p}%", (_, _) => amount.Text = Math.Floor(subtotal * p / 100 / 1000 * 1000).ToString("0"), minHeight: 44));
        var error = PosUi.Text("", 13, color: PosUi.Danger);
        var actions = new UniformGrid { Columns = 3, Margin = new Thickness(-4, 12, -4, 0) };
        actions.Children.Add(PosUi.Button("Bỏ giảm giá", (_, _) => { Amount = 0; Reason = ""; w.DialogResult = true; }));
        actions.Children.Add(PosUi.Button("Hủy", (_, _) => w.DialogResult = false));
        var apply = PosUi.Button("Áp dụng", (_, _) =>
        {
            if (!decimal.TryParse(amount.Text, out var a) || a <= 0 || a >= subtotal) { error.Text = "Nhập số tiền giảm lớn hơn 0 và nhỏ hơn tạm tính."; FieldError.Mark(amount, error.Text); return; }
            if (string.IsNullOrWhiteSpace(reason.Text)) { error.Text = "Chọn hoặc nhập lý do giảm giá."; FieldError.Mark(reason, error.Text); return; }
            Amount = decimal.Truncate(a); Reason = reason.Text.Trim(); w.DialogResult = true;
        }, "PrimaryButton");
        apply.IsDefault = true;
        actions.Children.Add(apply);
        w.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                PosUi.Text("Giảm giá đơn hàng", 20, true), PosUi.Text($"Tạm tính {subtotal:N0}đ", 13, color: PosUi.Muted),
                new Border { Height = 12 }, PosUi.Text("Giảm nhanh theo %"), percents,
                amount, reason, preview, error, actions
            }
        };
        w.Loaded += (_, _) => { amount.Focus(); amount.SelectAll(); Update(); };
        return w.ShowDialog() == true;
    }
}

public sealed class CustomerModal
{
    private readonly ICustomerService _customers;

    public CustomerModal(ICustomerService customers)
    {
        _customers = customers;
    }
    public string Phone { get; private set; } = "";
    public int RedeemPoints { get; private set; }

    // Look up or register a member by phone, then optionally redeem points (1 điểm = 1.000đ).
    public bool Show(Window owner, decimal payableBeforePoints, string currentPhone, int currentRedeem)
    {
        var w = PosUi.Dialog(owner, "Khách hàng thành viên", 480);
        var phone = new TextBox { Text = currentPhone, FontSize = 22, Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 8) };
        TouchInput.SetNumeric(phone, true);
        var name = new TextBox { FontSize = 16, Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 8) };
        var info = PosUi.Text("Nhập số điện thoại để tìm hoặc đăng ký.", 14, color: PosUi.Muted);
        var redeem = new TextBox { Text = currentRedeem > 0 ? currentRedeem.ToString() : "", FontSize = 18, Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 8) };
        TouchInput.SetNumeric(redeem, true);
        var registerPanel = new StackPanel { Visibility = Visibility.Collapsed, Children = { PosUi.Text("Tên khách (không bắt buộc)"), name } };
        var redeemPanel = new StackPanel { Visibility = Visibility.Collapsed };
        int maxRedeem = 0;
        Customer? member = null;
        void Lookup()
        {
            member = _customers.FindCustomer(phone.Text);
            bool valid = phone.Text.Count(char.IsDigit) >= 10;
            registerPanel.Visibility = valid && member is null ? Visibility.Visible : Visibility.Collapsed;
            redeemPanel.Visibility = member is { Points: > 0 } ? Visibility.Visible : Visibility.Collapsed;
            maxRedeem = member is null ? 0 : (int)Math.Min(member.Points, Math.Floor((payableBeforePoints - 1000) / DataStoreService.PointValue));
            info.Text = member is not null ? $"{member.Name}, hạng {member.Tier}: {member.Points} điểm, {member.VisitCount} lần mua"
                : valid ? "Chưa là thành viên — bấm Đăng ký để tích điểm từ đơn này." : "Nhập số điện thoại để tìm hoặc đăng ký.";
            info.Foreground = member is null ? PosUi.Muted : PosUi.Accent;
        }
        phone.TextChanged += (_, _) => Lookup();
        var useAll = PosUi.Button("Dùng tối đa", (_, _) => redeem.Text = maxRedeem.ToString(), minHeight: 40);
        redeemPanel.Children.Add(PosUi.Text("Đổi điểm (1 điểm = 1.000đ)"));
        redeemPanel.Children.Add(new DockPanel { Children = { useAll, redeem } });
        DockPanel.SetDock(useAll, Dock.Right);
        var error = PosUi.Text("", 13, color: PosUi.Danger);
        var actions = new UniformGrid { Columns = 3, Margin = new Thickness(-4, 12, -4, 0) };
        actions.Children.Add(PosUi.Button("Bỏ khách", (_, _) => { Phone = ""; RedeemPoints = 0; w.DialogResult = true; }));
        actions.Children.Add(PosUi.Button("Hủy", (_, _) => w.DialogResult = false));
        var ok = PosUi.Button("Xong", (_, _) =>
        {
            try
            {
                member ??= registerPanel.IsVisible ? _customers.RegisterCustomer(phone.Text, name.Text) : throw FieldError.Mark(phone, "Số điện thoại phải gồm 10 chữ số.");
                int points = 0;
                if (redeemPanel.IsVisible && redeem.Text.Trim().Length > 0 && (!int.TryParse(redeem.Text, out points) || points < 0 || points > maxRedeem))
                    throw FieldError.Mark(redeem, $"Đổi từ 0 đến {maxRedeem} điểm.");
                Phone = member.Phone; RedeemPoints = points; w.DialogResult = true;
            }
            catch (InvalidOperationException ex) { error.Text = ex.Message; }
        }, "PrimaryButton");
        ok.IsDefault = true;
        actions.Children.Add(ok);
        w.Content = new StackPanel { Margin = new Thickness(20), Children = { PosUi.Text("Khách hàng thành viên", 20, true), PosUi.Text("Tích 1 điểm cho mỗi 10.000đ thực thu", 13, color: PosUi.Muted), new Border { Height = 10 }, PosUi.Text("Số điện thoại"), phone, info, new Border { Height = 8 }, registerPanel, redeemPanel, error, actions } };
        w.Loaded += (_, _) => { phone.Focus(); phone.SelectAll(); Lookup(); };
        return w.ShowDialog() == true;
    }
}

/// <summary>Cash handed over; Transfer &gt; 0 means the rest is paid by bank transfer (mixed payment).</summary>
public record CashPayment(decimal Cash, decimal Transfer);

// Cash step of checkout: total, quick notes, amount given and live change. Returns the amount or null.
public static class CashPaymentModal
{
    public static CashPayment? Show(Window owner, decimal total, bool allowTransfer = true)
    {
        var w = PosUi.Dialog(owner, "Thanh toán tiền mặt", 460);
        var amount = new TextBox { FontSize = 26, Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 8), HorizontalContentAlignment = HorizontalAlignment.Right };
        TouchInput.SetNumeric(amount, true);
        TouchInput.SetLabel(amount, "Tiền khách đưa");
        TouchInput.SetDue(amount, total);
        System.Windows.Automation.AutomationProperties.SetName(amount, "Tiền khách đưa, số nguyên đồng");
        var change = PosUi.Text("", 22, true, PosUi.Money);
        var error = PosUi.Text("", 13, color: PosUi.Danger);
        error.Visibility = Visibility.Collapsed;
        Button? pay = null, split = null;
        CashPayment? result = null;
        void Update()
        {
            bool ok = CashAmount.TryParse(amount.Text, out var a);
            error.Text = ok ? "" : "Nhập số tiền nguyên từ 0 đến 999.999.999.999đ, không dùng dấu phân cách.";
            error.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
            change.Text = !ok ? "Tiền thối: —" : a >= total ? $"Tiền thối: {a - total:N0}đ" : $"Còn thiếu {total - a:N0}đ";
            change.Foreground = ok && a < total ? PosUi.Danger : PosUi.Money;
            if (pay != null) pay.IsEnabled = ok && a >= total;
            if (split != null)
            {
                split.IsEnabled = ok && a > 0 && a < total;
                split.Content = ok && a > 0 && a < total ? $"Chuyển khoản {total - a:N0}đ còn lại" : "Phần còn lại chuyển khoản";
            }
        }
        amount.TextChanged += (_, _) => Update();
        var notes = new[] { 10000m, 20000m, 50000m, 100000m, 200000m, 500000m }.Select(n => Math.Ceiling(total / n) * n).Where(v => v > total).Distinct().OrderBy(v => v).Take(4);
        var quick = new UniformGrid { Columns = 5, Margin = new Thickness(-4, 0, -4, 8) };
        quick.Children.Add(PosUi.Button("Đúng số", (_, _) => amount.Text = total.ToString("0"), minHeight: 48));
        foreach (var n in notes) quick.Children.Add(PosUi.Button($"{n / 1000:0}k", (_, _) => amount.Text = n.ToString("0"), minHeight: 48));
        pay = PosUi.Button("Xác nhận đã thu (Enter)", (_, _) =>
        {
            if (!CashAmount.TryParse(amount.Text, out var a) || a < total) { Update(); amount.Focus(); return; }
            if (result is not null) return;
            result = new CashPayment(a, 0); w.DialogResult = true;
        }, "PrimaryButton", 56);
        // Customer is short on cash: keep what was handed over and take the rest by transfer.
        split = PosUi.Button("Phần còn lại chuyển khoản", (_, _) =>
        {
            if (!CashAmount.TryParse(amount.Text, out var a) || a <= 0 || a >= total || result is not null) return;
            result = new CashPayment(a, total - a); w.DialogResult = true;
        }, minHeight: 48);
        split.Visibility = allowTransfer ? Visibility.Visible : Visibility.Collapsed;
        pay.IsDefault = true;
        var back = PosUi.Button("Quay lại", (_, _) => w.DialogResult = false, minHeight: 56);
        back.IsCancel = true;
        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 12, -4, 0), Children = { back, pay } };
        w.Content = new StackPanel { Margin = new Thickness(20), Children = { PosUi.Text("Cần thu", 14, color: PosUi.Muted), PosUi.Text($"{total:N0}đ", 34, true, PosUi.Money), new Border { Height = 10 }, PosUi.Text("Tiền khách đưa"), amount, quick, error, change, split, actions } };
        w.Loaded += (_, _) => { amount.Text = total.ToString("0"); amount.Focus(); amount.SelectAll(); Update(); };
        return w.ShowDialog() == true ? result : null;
    }
}

public static class OrderCompleteModal
{
    // Big confirmation so the cashier can read the call number to the customer; Enter starts the next order.
    public static void Show(Window owner, Order order, Customer? member, Action? printReceipt = null)
    {
        var w = PosUi.Dialog(owner, "Thanh toán thành công", 440);
        var next = PosUi.Button("Đơn mới (Enter)", (_, _) => w.Close(), "PrimaryButton", 60);
        next.IsDefault = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        var countdown = PosUi.Text("Tự về bán hàng sau 3 giây", 13, color: PosUi.Muted);
        var panel = new StackPanel { Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle, Width = 52, Height = 52, Foreground = PosUi.Accent, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "Thanh toán thành công", FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = $"#{order.DailyOrderNumber:D2}", FontSize = 64, FontWeight = FontWeights.Bold, Foreground = PosUi.Accent, HorizontalAlignment = HorizontalAlignment.Center });
        var paymentLabel = order.PaymentMethod switch { "Cash" => "Tiền mặt", "VietQR" => "Chuyển khoản", "Mixed" => "Tiền mặt + chuyển khoản", _ => order.Channel };
        panel.Children.Add(new TextBlock { Text = $"{order.ServiceType}, {paymentLabel}", Foreground = PosUi.Muted, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) });
        void Row(string label, string value, bool strong = false)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var v = PosUi.Text(value, strong ? 22 : 15, strong, strong ? PosUi.Money : null); DockPanel.SetDock(v, Dock.Right);
            row.Children.Add(v); row.Children.Add(PosUi.Text(label, 15, color: PosUi.Muted));
            panel.Children.Add(row);
        }
        if (order.SubtotalAmount > order.TotalAmount) Row("Tạm tính", $"{order.SubtotalAmount:N0}đ");
        if (order.PromotionAmount > 0) Row(order.PromotionName, $"−{order.PromotionAmount:N0}đ");
        if (order.LoyaltyAmount > 0) Row($"Đổi {order.PointsRedeemed} điểm", $"−{order.LoyaltyAmount:N0}đ");
        if (order.VoucherAmount > 0) Row($"Voucher {order.VoucherCode}", $"−{order.VoucherAmount:N0}đ");
        if (order.DiscountAmount > 0) Row(string.IsNullOrWhiteSpace(order.DiscountReason) ? "Giảm giá" : $"Giảm giá ({order.DiscountReason})", $"−{order.DiscountAmount:N0}đ");
        if (order.VatPercent > 0) Row(order.VatIncluded ? $"Đã gồm VAT {order.VatPercent}%" : $"VAT {order.VatPercent}%", order.VatIncluded ? $"{order.VatAmount:N0}đ" : $"+{order.VatAmount:N0}đ");
        Row("Tổng thu", $"{order.TotalAmount:N0}đ", true);
        if (order.PaymentMethod == "Mixed") Row("Chuyển khoản", $"{order.TransferAmount:N0}đ");
        if (order.PaymentMethod is "Cash" or "Mixed") { Row("Khách đưa", $"{order.CashGiven:N0}đ"); Row("Tiền thối", $"{order.ChangeReturned:N0}đ", true); }
        if (member is not null)
            panel.Children.Add(new Border { Background = Palette.AccentSoft, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0),
                Child = PosUi.Text($"{member.Name}: +{order.PointsEarned} điểm, hiện có {member.Points} điểm (hạng {member.Tier})", 14, true, PosUi.Accent) });
        panel.Children.Add(new Border { Height = 16 });
        if (printReceipt is not null)
        {
            var receipt = PosUi.Button("In hóa đơn", (_, _) => { timer.Stop(); countdown.Text = ""; printReceipt(); }, minHeight: 48);
            panel.Children.Add(receipt);
        }
        panel.Children.Add(next);
        countdown.HorizontalAlignment = HorizontalAlignment.Center;
        countdown.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(countdown);
        int remaining = 3;
        timer.Tick += (_, _) =>
        {
            if (--remaining == 0) { timer.Stop(); w.Close(); }
            else countdown.Text = $"Tự về bán hàng sau {remaining} giây";
        };
        w.Loaded += (_, _) => timer.Start();
        w.Closed += (_, _) => timer.Stop();
        w.Content = panel;
        w.ShowDialog();
    }
}

public sealed class OrderQueueWindow : Window
{
    private readonly IOrderService _orders;
    private readonly IStickerPrinterService _stickers;
    private readonly IDialogFactory _dialogs;
    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _summary = PosUi.Text("", 14, color: PosUi.Muted);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private static readonly string[] Finished = { "Đã giao", "Dừng phục vụ", "Legacy" };

    public OrderQueueWindow(IOrderService orders, IStickerPrinterService stickers, IDialogFactory dialogs)
    {
        _orders = orders;
        _stickers = stickers;
        _dialogs = dialogs;
        Title = "Hàng chờ pha chế"; Width = 1100; Height = 720; MinWidth = 700; MinHeight = 480; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current.TryFindResource("PosWindow") is Style style) Style = style;
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = PosUi.Button("Đóng (Esc)", (_, _) => Close()); DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        header.Children.Add(new StackPanel { Children = { PosUi.Text("Hàng chờ pha chế", 24, true), _summary } });
        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        var scroll = new ScrollViewer { Content = _cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(scroll);
        Content = root;
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { Refresh(); _timer.Start(); };
        Closed += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        var open = _orders.CompletedOrders.Where(o => !Finished.Contains(o.FulfillmentStatus) && o.CreatedAt.Date == DateTime.Today)
            .OrderBy(o => o.CreatedAt).ToList();
        _summary.Text = open.Count == 0 ? "Không có đơn đang chờ." : $"{open.Count} đơn chưa giao, cũ nhất ở bên trái.";
        _cards.Children.Clear();
        if (open.Count == 0) _cards.Children.Add(PosUi.Text("Chưa có đơn nào cần pha chế.", 18, color: PosUi.Muted));
        foreach (var order in open) _cards.Children.Add(Card(order));
    }

    private Border Card(Order order)
    {
        int waited = (int)(DateTime.Now - order.CreatedAt).TotalMinutes;
        var statusColor = order.FulfillmentStatus switch { "Chờ giao" => PosUi.Accent, "Đang làm" => Brushes.DarkOrange, _ => Brushes.SlateGray };
        var body = new StackPanel();
        var top = new DockPanel();
        var time = PosUi.Text($"{waited} phút", 13, waited >= 10, waited >= 10 ? PosUi.Danger : PosUi.Muted); DockPanel.SetDock(time, Dock.Right);
        top.Children.Add(time);
        top.Children.Add(PosUi.Text($"#{order.DailyOrderNumber:D2} · {order.ServiceType}", 20, true));
        body.Children.Add(top);
        body.Children.Add(new Border { Background = statusColor, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 4, 0, 8), HorizontalAlignment = HorizontalAlignment.Left, Child = PosUi.Text(order.FulfillmentStatus, 12, true, Brushes.White) });
        foreach (var item in order.Items)
        {
            string done = item.ReadyQuantity >= item.Quantity ? "✓ " : "";
            var line = PosUi.Text($"{done}{item.Quantity}× {item.ProductName}", 15, true, item.ReadyQuantity >= item.Quantity ? PosUi.Muted : null);
            body.Children.Add(line);
            var detail = string.Join(", ", new[] { item.Size, item.SugarChoice, item.IceChoice }.Where(s => !string.IsNullOrWhiteSpace(s)).Concat(item.Toppings.Select(t => "+" + t)));
            if (!string.IsNullOrWhiteSpace(item.Note)) detail += (detail.Length > 0 ? ", " : "") + "Ghi chú: " + item.Note;
            if (detail.Length > 0) body.Children.Add(PosUi.Text(detail, 13, color: PosUi.Muted));
            body.Children.Add(new Border { Height = 6 });
        }
        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 6, -4, 0) };
        if (order.FulfillmentStatus == "Chờ làm") actions.Children.Add(PosUi.Button("Bắt đầu làm", (_, _) => Act(order, "start")));
        if (order.Items.Any(i => i.ReadyQuantity < i.Quantity)) actions.Children.Add(PosUi.Button("Đã làm xong", (_, _) => Act(order, "all-ready"), "PrimaryButton"));
        else actions.Children.Add(PosUi.Button("Đã giao khách", (_, _) => Act(order, "all-delivered"), "PrimaryButton"));
        body.Children.Add(actions);
        // The sticker printer sits at the bar, so a jammed or smudged sticker is reprinted from here.
        var cups = _stickers.StickersForOrder(order);
        if (cups.Count > 0)
        {
            var reprint = PosUi.Button("In lại tem", (_, _) => Reprint(cups));
            reprint.Margin = new Thickness(0, 8, 0, 0);
            body.Children.Add(reprint);
        }
        return new Border { Width = 320, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(14), CornerRadius = new CornerRadius(10), Background = Brushes.White, BorderBrush = statusColor, BorderThickness = new Thickness(2, 6, 2, 2), Child = body };
    }

    private void Reprint(IReadOnlyList<PrintedStickerInfo> cups)
    {
        _timer.Stop(); // a refresh would rebuild the card under the open dialog
        _dialogs.Create<ReprintStickerModal>(this, cups).ShowDialog();
        _timer.Start();
    }

    private void Act(Order order, string action)
    {
        try { _orders.UpdateFulfillment(order.Id, action); Refresh(); }
        catch (Exception ex) { Toast.Show(this, ex.Message, true); }
    }
}
