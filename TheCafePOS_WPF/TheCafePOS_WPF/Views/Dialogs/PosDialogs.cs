using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

namespace TheCafePOS_WPF.Views.Dialogs;

// Small code-only dialogs; they share the POS window style and keep touch-sized targets.
internal static class PosUi
{
    public static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x12, 0x63, 0x4B));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));

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
        var amount = new TextBox { Text = current > 0 ? current.ToString("0") : "", FontSize = 20, Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 10) };
        TouchInput.SetNumeric(amount, true);
        var reason = new ComboBox { IsEditable = true, Text = currentReason, FontSize = 16, Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 4), ItemsSource = new[] { "Khách quen", "Khuyến mãi khai trương", "Bù món lỗi / chờ lâu", "Nhân viên", "Voucher" } };
        var preview = PosUi.Text("", 15, true, PosUi.Accent);
        void Update()
        {
            decimal.TryParse(amount.Text, out var a);
            preview.Text = a > 0 && a < subtotal ? $"Còn phải thu: {subtotal - a:N0}đ" : a >= subtotal ? "Số tiền giảm phải nhỏ hơn tạm tính" : "";
            preview.Foreground = a >= subtotal ? Brushes.Firebrick : PosUi.Accent;
        }
        amount.TextChanged += (_, _) => Update();
        var percents = new UniformGrid { Columns = 4, Margin = new Thickness(-4, 0, -4, 8) };
        foreach (var p in new[] { 5, 10, 15, 20 })
            percents.Children.Add(PosUi.Button($"{p}%", (_, _) => amount.Text = Math.Floor(subtotal * p / 100 / 1000 * 1000).ToString("0"), minHeight: 44));
        var error = PosUi.Text("", 13, color: Brushes.Firebrick);
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
                PosUi.Text("Giảm giá đơn hàng", 20, true), PosUi.Text($"Tạm tính {subtotal:N0}đ · cần quản lý duyệt bằng mật khẩu", 13, color: PosUi.Muted),
                new Border { Height = 12 }, PosUi.Text("Giảm nhanh theo %"), percents,
                PosUi.Text("Số tiền giảm (đồng)"), amount, PosUi.Text("Lý do"), reason, preview, error, actions
            }
        };
        w.Loaded += (_, _) => { amount.Focus(); amount.SelectAll(); Update(); };
        return w.ShowDialog() == true;
    }
}

public sealed class CustomerModal
{
    public string Phone { get; private set; } = "";
    public int RedeemPoints { get; private set; }

    // Look up or register a member by phone, then optionally redeem points (1 điểm = 1.000đ).
    public bool Show(Window owner, decimal payableBeforePoints, string currentPhone, int currentRedeem)
    {
        var store = DataStoreService.Instance;
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
            member = store.FindCustomer(phone.Text);
            bool valid = phone.Text.Count(char.IsDigit) >= 10;
            registerPanel.Visibility = valid && member is null ? Visibility.Visible : Visibility.Collapsed;
            redeemPanel.Visibility = member is { Points: > 0 } ? Visibility.Visible : Visibility.Collapsed;
            maxRedeem = member is null ? 0 : (int)Math.Min(member.Points, Math.Floor((payableBeforePoints - 1000) / DataStoreService.PointValue));
            info.Text = member is not null ? $"{member.Name} · hạng {member.Tier} · {member.Points} điểm · {member.VisitCount} lần mua"
                : valid ? "Chưa là thành viên — bấm Đăng ký để tích điểm từ đơn này." : "Nhập số điện thoại để tìm hoặc đăng ký.";
            info.Foreground = member is null ? PosUi.Muted : PosUi.Accent;
        }
        phone.TextChanged += (_, _) => Lookup();
        var useAll = PosUi.Button("Dùng tối đa", (_, _) => redeem.Text = maxRedeem.ToString(), minHeight: 40);
        redeemPanel.Children.Add(PosUi.Text("Đổi điểm (1 điểm = 1.000đ)"));
        redeemPanel.Children.Add(new DockPanel { Children = { useAll, redeem } });
        DockPanel.SetDock(useAll, Dock.Right);
        var error = PosUi.Text("", 13, color: Brushes.Firebrick);
        var actions = new UniformGrid { Columns = 3, Margin = new Thickness(-4, 12, -4, 0) };
        actions.Children.Add(PosUi.Button("Bỏ khách", (_, _) => { Phone = ""; RedeemPoints = 0; w.DialogResult = true; }));
        actions.Children.Add(PosUi.Button("Hủy", (_, _) => w.DialogResult = false));
        var ok = PosUi.Button("Xong", (_, _) =>
        {
            try
            {
                member ??= registerPanel.IsVisible ? store.RegisterCustomer(phone.Text, name.Text) : throw FieldError.Mark(phone, "Số điện thoại phải gồm 10 chữ số.");
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

public static class OrderCompleteModal
{
    // Big confirmation so the cashier can read the call number to the customer; Enter starts the next order.
    public static void Show(Window owner, Order order)
    {
        var w = PosUi.Dialog(owner, "Thanh toán thành công", 440);
        var next = PosUi.Button("Đơn mới (Enter)", (_, _) => w.Close(), "PrimaryButton", 60);
        next.IsDefault = true;
        var panel = new StackPanel { Margin = new Thickness(24), HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = "✓", FontSize = 44, Foreground = PosUi.Accent, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = "Thanh toán thành công", FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = $"#{order.DailyOrderNumber:D2}", FontSize = 64, FontWeight = FontWeights.Bold, Foreground = PosUi.Accent, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = $"{order.ServiceType} · {(order.PaymentMethod == "Cash" ? "Tiền mặt" : "VietQR")}", Foreground = PosUi.Muted, FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 16) });
        void Row(string label, string value, bool strong = false)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            var v = PosUi.Text(value, strong ? 22 : 15, strong, strong ? PosUi.Accent : null); DockPanel.SetDock(v, Dock.Right);
            row.Children.Add(v); row.Children.Add(PosUi.Text(label, 15, color: PosUi.Muted));
            panel.Children.Add(row);
        }
        if (order.SubtotalAmount > order.TotalAmount) Row("Tạm tính", $"{order.SubtotalAmount:N0}đ");
        if (order.PromotionAmount > 0) Row(order.PromotionName, $"−{order.PromotionAmount:N0}đ");
        if (order.LoyaltyAmount > 0) Row($"Đổi {order.PointsRedeemed} điểm", $"−{order.LoyaltyAmount:N0}đ");
        if (order.DiscountAmount > 0) Row("Giảm giá", $"−{order.DiscountAmount:N0}đ");
        Row("Tổng thu", $"{order.TotalAmount:N0}đ", true);
        if (order.PaymentMethod == "Cash") { Row("Khách đưa", $"{order.CashGiven:N0}đ"); Row("Tiền thối", $"{order.ChangeReturned:N0}đ", true); }
        if (DataStoreService.Instance.FindCustomer(order.CustomerPhone) is { } member)
            panel.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(0xEF, 0xF7, 0xF2)), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0),
                Child = PosUi.Text($"👤 {member.Name} · +{order.PointsEarned} điểm · hiện có {member.Points} điểm ({member.Tier})", 14, true, PosUi.Accent) });
        panel.Children.Add(new Border { Height = 16 });
        panel.Children.Add(next);
        w.Content = panel;
        w.ShowDialog();
    }
}

public sealed class OrderQueueWindow : Window
{
    private readonly WrapPanel _cards = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock _summary = PosUi.Text("", 14, color: PosUi.Muted);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private static readonly string[] Finished = { "Đã giao", "Dừng phục vụ", "Legacy" };

    public OrderQueueWindow()
    {
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
        var store = DataStoreService.Instance;
        var open = store.CompletedOrders.Where(o => !Finished.Contains(o.FulfillmentStatus) && o.CreatedAt.Date == DateTime.Today)
            .OrderBy(o => o.CreatedAt).ToList();
        _summary.Text = open.Count == 0 ? "Không có đơn đang chờ — tự cập nhật mỗi 5 giây" : $"{open.Count} đơn chưa giao · cũ nhất ở bên trái · tự cập nhật mỗi 5 giây";
        _cards.Children.Clear();
        if (open.Count == 0) _cards.Children.Add(PosUi.Text("☕  Chưa có đơn nào cần pha chế.", 18, color: PosUi.Muted));
        foreach (var order in open) _cards.Children.Add(Card(order));
    }

    private Border Card(Order order)
    {
        int waited = (int)(DateTime.Now - order.CreatedAt).TotalMinutes;
        var statusColor = order.FulfillmentStatus switch { "Chờ giao" => PosUi.Accent, "Đang làm" => Brushes.DarkOrange, _ => Brushes.SlateGray };
        var body = new StackPanel();
        var top = new DockPanel();
        var time = PosUi.Text($"{waited} phút", 13, waited >= 10, waited >= 10 ? Brushes.Firebrick : PosUi.Muted); DockPanel.SetDock(time, Dock.Right);
        top.Children.Add(time);
        top.Children.Add(PosUi.Text($"#{order.DailyOrderNumber:D2} · {order.ServiceType}", 20, true));
        body.Children.Add(top);
        body.Children.Add(new Border { Background = statusColor, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(0, 4, 0, 8), HorizontalAlignment = HorizontalAlignment.Left, Child = PosUi.Text(order.FulfillmentStatus, 12, true, Brushes.White) });
        foreach (var item in order.Items)
        {
            string done = item.ReadyQuantity >= item.Quantity ? "✓ " : "";
            var line = PosUi.Text($"{done}{item.Quantity}× {item.ProductName}", 15, true, item.ReadyQuantity >= item.Quantity ? PosUi.Muted : null);
            body.Children.Add(line);
            var detail = string.Join(" · ", new[] { item.Size, item.SugarChoice, item.IceChoice }.Where(s => !string.IsNullOrWhiteSpace(s)).Concat(item.Toppings.Select(t => "+" + t)));
            if (!string.IsNullOrWhiteSpace(item.Note)) detail += (detail.Length > 0 ? " · " : "") + "Ghi chú: " + item.Note;
            if (detail.Length > 0) body.Children.Add(PosUi.Text(detail, 13, color: PosUi.Muted));
            body.Children.Add(new Border { Height = 6 });
        }
        var actions = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 6, -4, 0) };
        if (order.FulfillmentStatus == "Chờ làm") actions.Children.Add(PosUi.Button("Bắt đầu làm", (_, _) => Act(order, "start")));
        if (order.Items.Any(i => i.ReadyQuantity < i.Quantity)) actions.Children.Add(PosUi.Button("Đã làm xong", (_, _) => Act(order, "all-ready"), "PrimaryButton"));
        else actions.Children.Add(PosUi.Button("Đã giao khách", (_, _) => Act(order, "all-delivered"), "PrimaryButton"));
        body.Children.Add(actions);
        return new Border { Width = 320, Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(14), CornerRadius = new CornerRadius(10), Background = Brushes.White, BorderBrush = statusColor, BorderThickness = new Thickness(2, 6, 2, 2), Child = body };
    }

    private void Act(Order order, string action)
    {
        try { DataStoreService.Instance.UpdateFulfillment(order.Id, action); Refresh(); }
        catch (Exception ex) { Toast.Show(this, ex.Message, true); }
    }
}
