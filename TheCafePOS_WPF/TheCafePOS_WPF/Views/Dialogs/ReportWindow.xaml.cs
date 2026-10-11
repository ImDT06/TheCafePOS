using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class ReportWindow : Window
{
    public ReportWindow()
    {
        AuthService.Instance.RequireManager(); InitializeComponent();
        From.SelectedDate = DateTime.Today; To.SelectedDate = DateTime.Today;
        var store = DataStoreService.Instance;
        ShiftFilter.ItemsSource = new[] { new { Id = "", Label = "Tất cả ca" } }.Concat(store.Shifts.Append(store.CurrentShift).DistinctBy(s => s.Id).Select(s => new { Id = s.Id, Label = $"{s.StartTime:dd/MM HH:mm} — {s.CashierName}" })).ToList();
        ShiftFilter.SelectedIndex = 0; Refresh_Click(this, new RoutedEventArgs());
    }
    private SalesReport RefreshReport()
    {
        AuthService.Instance.RequireManager();
        if (From.SelectedDate is not DateTime from || To.SelectedDate is not DateTime to) throw new InvalidOperationException("Chọn ngày bắt đầu và kết thúc.");
        var shiftId = ShiftFilter.SelectedValue as string;
        var store = DataStoreService.Instance;
        var report = ReportService.Build(store.CompletedOrders, from, to, string.IsNullOrEmpty(shiftId) ? null : shiftId, store.Refunds, store.CashEntries);
        Orders.ItemsSource = report.Orders; Items.ItemsSource = null; TopProducts.ItemsSource = report.Products;
        Shifts.ItemsSource = store.Shifts.Where(s => s.EndTime?.Date >= from.Date && s.EndTime?.Date <= to.Date && (string.IsNullOrEmpty(shiftId) || s.Id == shiftId)).ToList();
        Refunds.ItemsSource = report.Refunds; CashEntries.ItemsSource = report.CashEntries;
        RenderInsights(report, from, to, string.IsNullOrEmpty(shiftId) ? null : shiftId);
        Summary.Text = $"{report.Orders.Count} đơn | Bán hàng: {report.Revenue:N0}đ | Hoàn: {report.Refunded:N0}đ | Thuần: {report.NetRevenue:N0}đ\nBán tiền mặt: {report.Cash:N0}đ · Hoàn tiền mặt: {report.CashRefunded:N0}đ | Bán QR: {report.Transfer:N0}đ · Hoàn QR: {report.TransferRefunded:N0}đ\nThu ngoài bán hàng: {report.CashIn:N0}đ | Chi ngoài bán hàng: {report.CashOut:N0}đ";
        RevenueMetric.Text = $"{report.Revenue:N0}đ";
        NetMetric.Text = $"{report.NetRevenue:N0}đ";
        NetMetric.Foreground = new System.Windows.Media.SolidColorBrush(report.NetRevenue < 0 ? System.Windows.Media.Color.FromRgb(185, 28, 28) : System.Windows.Media.Color.FromRgb(18, 99, 75));
        OrderMetric.Text = report.Orders.Count.ToString("N0");
        AverageMetric.Text = $"{report.AverageOrderValue:N0}đ";
        AppliedRange.Text = $"Đang xem: {from:dd/MM/yyyy} – {to:dd/MM/yyyy} · {ShiftFilter.Text}";
        EmptyReport.Visibility = report.Orders.Count + report.Refunds.Count + report.CashEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var daily = report.Daily.TakeLast(31).ToList();
        decimal scale = Math.Max(1, daily.Select(d => Math.Max(d.Revenue, d.Refunded)).DefaultIfEmpty(0).Max());
        DailyChart.ItemsSource = daily.Select(d => new {
            Label = $"{d.Date:dd/MM/yyyy} · {d.OrderCount} đơn", Detail = $"Thuần: {d.NetRevenue:N0}đ",
            SalesPercent = (double)(100 * d.Revenue / scale), RefundPercent = (double)(100 * d.Refunded / scale),
            SalesLabel = $"Bán hàng: {d.Revenue:N0}đ", RefundLabel = $"Hoàn tiền: {d.Refunded:N0}đ"
        }).ToList();
        decimal topRevenue = Math.Max(1, report.Products.Select(p => p.Revenue).DefaultIfEmpty(0).Max());
        ProductChart.ItemsSource = report.Products.Take(5).Select((p, index) => new {
            Label = $"{index + 1}. {p.ProductName}", Detail = $"{p.Quantity:N0} món · {p.Revenue:N0}đ",
            Percent = (double)(100 * p.Revenue / topRevenue)
        }).ToList();
        return report;
    }
    private void QuickRange_Click(object sender, RoutedEventArgs e)
    {
        var today = DateTime.Today;
        From.SelectedDate = ((sender as Button)?.Tag as string) switch
        {
            "week" => today.AddDays(-6),
            "month" => new DateTime(today.Year, today.Month, 1),
            _ => today
        };
        To.SelectedDate = today;
        ShiftFilter.SelectedIndex = 0;
        Refresh_Click(sender, e);
    }
    private void ExportDaily_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = RefreshReport();
            var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"doanh-thu-theo-ngay-{From.SelectedDate:yyyyMMdd}-{To.SelectedDate:yyyyMMdd}.csv" };
            if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, ReportService.ToDailyCsv(report), new UTF8Encoding(true));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) { try { RefreshReport(); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
    private void OrderSelected(object sender, SelectionChangedEventArgs e) => Items.ItemsSource = (Orders.SelectedItem as Order)?.Items;
    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = RefreshReport();
            var dialog = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"bao-cao-{From.SelectedDate:yyyyMMdd}-{To.SelectedDate:yyyyMMdd}.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            string Pay(string m) => m switch { "Cash" => "Tiền mặt", "VietQR" => "VietQR", "Platform" => "Nền tảng", _ => m };
            XlsxWriter.Write(dialog.FileName, new (string, IReadOnlyList<string>, IEnumerable<object?[]>)[]
            {
                ("Tổng quan", new[] { "Chỉ số", "Giá trị" }, new object?[][]
                {
                    new object?[] { "Cửa hàng", DataStoreService.Instance.Settings.DisplayName },
                    new object?[] { "Từ ngày", From.SelectedDate }, new object?[] { "Đến ngày", To.SelectedDate },
                    new object?[] { "Số đơn", report.Orders.Count }, new object?[] { "Doanh thu bán hàng", report.Revenue },
                    new object?[] { "Hoàn tiền", report.Refunded }, new object?[] { "Doanh thu thuần", report.NetRevenue },
                    new object?[] { "Tiền mặt", report.Cash }, new object?[] { "VietQR", report.Transfer },
                    new object?[] { "Giảm giá thủ công", report.Orders.Sum(o => o.DiscountAmount) }, new object?[] { "Khuyến mãi", report.Orders.Sum(o => o.PromotionAmount) },
                    new object?[] { "Đổi điểm", report.Orders.Sum(o => o.LoyaltyAmount) }, new object?[] { "Giá trị TB/đơn", Math.Round(report.AverageOrderValue) }
                }),
                ("Đơn hàng", new[] { "Mã đơn", "Thời gian", "Kênh", "Loại đơn", "Thanh toán", "Tạm tính", "Phụ thu kênh", "Khuyến mãi", "Đổi điểm", "Giảm giá", "Thực thu", "Đã hoàn", "Khách", "Trạng thái" },
                    report.Orders.Select(o => new object?[] { o.DailyOrderNumberFormatted, o.CreatedAt, o.Channel, o.ServiceType, Pay(o.PaymentMethod), o.SubtotalAmount, o.ChannelSurcharge, o.PromotionAmount, o.LoyaltyAmount, o.DiscountAmount, o.TotalAmount, o.RefundedAmount, o.CustomerPhone, o.IsCancelled ? "Đã hủy" : o.RefundStatus })),
                ("Chi tiết món", new[] { "Mã đơn", "Món", "Size", "Số lượng", "Thành tiền" },
                    report.Orders.SelectMany(o => o.Items.Select(i => new object?[] { o.DailyOrderNumberFormatted, i.ProductName, i.Size, i.Quantity, i.TotalPrice }))),
                ("Món bán", new[] { "Món", "Số lượng", "Doanh thu" }, report.Products.Select(p => new object?[] { p.ProductName, p.Quantity, p.Revenue })),
                ("Hoàn tiền", new[] { "Thời gian", "Mã đơn", "Số tiền", "Phương thức", "Lý do", "Mã GD", "Nhân viên", "Người duyệt" },
                    report.Refunds.Select(r => new object?[] { r.CreatedAt, r.OrderId, r.Amount, Pay(r.Method), r.Reason, r.Reference, r.Operator, r.Approver })),
                ("Thu chi", new[] { "Thời gian", "Loại", "Số tiền", "Lý do", "Nhân viên", "Người duyệt" },
                    report.CashEntries.Select(c => new object?[] { c.CreatedAt, c.Type == "In" ? "Thu" : "Chi", c.Amount, c.Reason, c.Operator, c.Approver }))
            });
            TheCafePOS_WPF.Core.Toast.Show(this, "Đã xuất file Excel.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = RefreshReport();
            var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = $"doanh-thu-{DateTime.Now:yyyyMMdd}.csv" };
            if (dialog.ShowDialog(this) == true) File.WriteAllText(dialog.FileName, ReportService.ToCsv(report), new UTF8Encoding(true));
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
}
