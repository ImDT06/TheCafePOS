using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

namespace TheCafePOS_WPF.Views.Dialogs;

// "Phân tích" tab: peak hours, comparison with the previous period, mix and discount breakdown.
public partial class ReportWindow
{
    private static readonly Brush Bar = new SolidColorBrush(Color.FromRgb(0x12, 0x63, 0x4B));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x52, 0x66, 0x5F));

    private void RenderInsights(SalesReport report, DateTime from, DateTime to, string? shiftId)
    {
        var store = DataStoreService.Instance;
        var orders = report.Orders;
        var panel = new StackPanel { Margin = new Thickness(16) };

        // Same-length period immediately before, e.g. today vs yesterday, this week vs last week.
        int days = (to.Date - from.Date).Days + 1;
        var previous = ReportService.Build(store.CompletedOrders, from.AddDays(-days), to.AddDays(-days), null, store.Refunds, store.CashEntries);
        var compare = new UniformGrid { Columns = 4, Margin = new Thickness(-6, 0, -6, 16) };
        compare.Children.Add(Metric("Doanh thu thuần", $"{report.NetRevenue:N0}đ", Change(report.NetRevenue, previous.NetRevenue)));
        compare.Children.Add(Metric("Số đơn", $"{orders.Count}", Change(orders.Count, previous.Orders.Count)));
        compare.Children.Add(Metric("Giá trị TB/đơn", $"{report.AverageOrderValue:N0}đ", Change(report.AverageOrderValue, previous.AverageOrderValue)));
        compare.Children.Add(Metric("Khách thành viên", $"{orders.Count(o => !string.IsNullOrEmpty(o.CustomerPhone))} đơn", $"{Percent(orders.Count(o => !string.IsNullOrEmpty(o.CustomerPhone)), orders.Count)} tổng đơn"));
        panel.Children.Add(Title($"So với {days} ngày trước đó ({from.AddDays(-days):dd/MM} – {to.AddDays(-days):dd/MM})"));
        panel.Children.Add(compare);

        panel.Children.Add(Title("Doanh thu theo giờ — xem giờ cao điểm để xếp ca"));
        var hourly = Enumerable.Range(6, 17).Select(h => (Hour: h, Revenue: orders.Where(o => o.CreatedAt.Hour == h).Sum(o => o.TotalAmount), Count: orders.Count(o => o.CreatedAt.Hour == h))).ToList();
        decimal maxHour = Math.Max(1, hourly.Max(h => h.Revenue));
        var chart = new Grid { Height = 170, Margin = new Thickness(0, 4, 0, 16) };
        foreach (var (h, i) in hourly.Select((h, i) => (h, i)))
        {
            chart.ColumnDefinitions.Add(new ColumnDefinition());
            var column = new DockPanel { Margin = new Thickness(2, 0, 2, 0), ToolTip = $"{h.Hour}:00–{h.Hour + 1}:00 · {h.Count} đơn · {h.Revenue:N0}đ" };
            var label = new TextBlock { Text = $"{h.Hour}h", FontSize = 11, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center };
            DockPanel.SetDock(label, Dock.Bottom);
            column.Children.Add(label);
            column.Children.Add(new Border { Background = h.Revenue == maxHour && h.Revenue > 0 ? Brushes.DarkOrange : Bar, CornerRadius = new CornerRadius(3, 3, 0, 0), VerticalAlignment = VerticalAlignment.Bottom, Height = (double)(h.Revenue / maxHour) * 140 });
            Grid.SetColumn(column, i);
            chart.Children.Add(column);
        }
        panel.Children.Add(chart);

        var mix = new UniformGrid { Columns = 3, Margin = new Thickness(-6, 0, -6, 16) };
        mix.Children.Add(Breakdown("Phương thức thanh toán", new[] { ("Tiền mặt", report.Cash), ("VietQR", report.Transfer) }));
        mix.Children.Add(Breakdown("Loại đơn", new[] { ("Mang đi", orders.Where(o => o.ServiceType == "Mang đi").Sum(o => o.TotalAmount)), ("Tại chỗ", orders.Where(o => o.ServiceType == "Tại chỗ").Sum(o => o.TotalAmount)) }));
        mix.Children.Add(Breakdown("Các khoản giảm", new[] { ("Khuyến mãi giờ", orders.Sum(o => o.PromotionAmount)), ("Đổi điểm", orders.Sum(o => o.LoyaltyAmount)), ("Giảm thủ công", orders.Sum(o => o.DiscountAmount)) }, showShare: false));
        panel.Children.Add(mix);

        // Active menu items with no or very few sales in the period are candidates to promote or drop.
        var sold = report.Products.ToDictionary(p => p.ProductName, p => p.Quantity);
        var slow = store.Products.Where(p => p.IsActive && (!p.IsTopping || p.SoldSeparately))
            .Select(p => (p.Name, Qty: sold.GetValueOrDefault(p.Name))).OrderBy(p => p.Qty).ThenBy(p => p.Name).Take(8).ToList();
        panel.Children.Add(Title("Món bán chậm nhất trong kỳ (đang bán)"));
        panel.Children.Add(new TextBlock { Text = slow.Count == 0 ? "Chưa có món." : string.Join("   ·   ", slow.Select(s => $"{s.Name}: {s.Qty}")), TextWrapping = TextWrapping.Wrap, Foreground = Muted });

        InsightsHost.Content = panel;
    }

    private static string Change(decimal now, decimal before) =>
        before == 0 ? (now == 0 ? "Không đổi" : "Kỳ trước chưa có dữ liệu") : $"{(now >= before ? "▲" : "▼")} {Math.Abs((now - before) / before):P0} so với kỳ trước";
    private static string Percent(decimal part, decimal whole) => whole == 0 ? "0%" : (part / whole).ToString("P0");

    private static TextBlock Title(string text) => new() { Text = text, FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) };

    private static Border Card(UIElement child) => new() { Background = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(6), Child = child };

    private static Border Metric(string label, string value, string note)
    {
        bool down = note.StartsWith("▼");
        return Card(new StackPanel { Children =
        {
            new TextBlock { Text = label, Foreground = Muted },
            new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeights.Bold },
            new TextBlock { Text = note, FontSize = 12, Foreground = down ? PosUi.Danger : note.StartsWith("▲") ? Bar : Muted }
        } });
    }

    private static Border Breakdown(string title, (string Label, decimal Value)[] rows, bool showShare = true)
    {
        decimal total = rows.Sum(r => r.Value);
        var stack = new StackPanel { Children = { new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) } } };
        foreach (var (label, value) in rows)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var v = new TextBlock { Text = $"{value:N0}đ" + (showShare ? $" ({Percent(value, total)})" : "") };
            DockPanel.SetDock(v, Dock.Right);
            row.Children.Add(v);
            row.Children.Add(new TextBlock { Text = label, Foreground = Muted });
            stack.Children.Add(row);
        }
        return Card(stack);
    }
}
