using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class ReportWindow : Window
{
    private readonly ICatalogService _catalog;
    private readonly IShiftService _shifts;
    private readonly IFinanceService _finance;
    private readonly IStoreSettingsService _settings;
    private readonly IOrderService _orders;
    private readonly IAuthService _auth;
    public ReportWindow(ICatalogService catalog, IShiftService shifts, IFinanceService finance, IStoreSettingsService settings, IOrderService orders, IAuthService auth)
    {
        _catalog = catalog;
        _shifts = shifts;
        _finance = finance;
        _settings = settings;
        _orders = orders;
        _auth = auth;
        _auth.RequireManager(); InitializeComponent();
        From.SelectedDate = DateTime.Today; To.SelectedDate = DateTime.Today;
        ShiftFilter.ItemsSource = new[] { new { Id = "", Label = "Tất cả ca" } }.Concat(_shifts.Shifts.Append(_shifts.CurrentShift).DistinctBy(s => s.Id).Select(s => new { Id = s.Id, Label = $"{s.StartTime:dd/MM HH:mm} — {s.CashierName}" })).ToList();
        ShiftFilter.SelectedIndex = 0; Refresh_Click(this, new RoutedEventArgs());
    }
    private SalesReport RefreshReport()
    {
        _auth.RequireManager();
        if (From.SelectedDate is not DateTime from || To.SelectedDate is not DateTime to) throw new InvalidOperationException("Chọn ngày bắt đầu và kết thúc.");
        var shiftId = ShiftFilter.SelectedValue as string;
        var report = ReportService.Build(_orders.CompletedOrders, from, to, string.IsNullOrEmpty(shiftId) ? null : shiftId, _finance.Refunds, _finance.CashEntries);
        Orders.ItemsSource = report.Orders; Items.ItemsSource = null; TopProducts.ItemsSource = report.Products;
        Shifts.ItemsSource = _shifts.Shifts.Where(s => s.EndTime?.Date >= from.Date && s.EndTime?.Date <= to.Date && (string.IsNullOrEmpty(shiftId) || s.Id == shiftId)).ToList();
        Refunds.ItemsSource = report.Refunds; CashEntries.ItemsSource = report.CashEntries;
        RenderInsights(report, from, to, string.IsNullOrEmpty(shiftId) ? null : shiftId);
        Summary.Text = $"Tiền mặt: bán {report.Cash:N0}đ, hoàn {report.CashRefunded:N0}đ\nChuyển khoản: bán {report.Transfer:N0}đ, hoàn {report.TransferRefunded:N0}đ\nNgoài bán hàng: thu {report.CashIn:N0}đ, chi {report.CashOut:N0}đ";
        RevenueMetric.Text = $"{report.Revenue:N0}đ";
        NetMetric.Text = $"{report.NetRevenue:N0}đ";
        NetMetric.Foreground = report.NetRevenue < 0 ? Core.Palette.Danger : Core.Palette.Money;
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
    private void Refresh_Click(object sender, RoutedEventArgs e) { try { RefreshReport(); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
    private void OrderSelected(object sender, SelectionChangedEventArgs e) => Items.ItemsSource = (Orders.SelectedItem as Order)?.Items;
    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = RefreshReport();
            var dialog = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = $"bao-cao-{From.SelectedDate:yyyyMMdd}-{To.SelectedDate:yyyyMMdd}.xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            XlsxWriter.Write(dialog.FileName, ReportService.ToWorkbook(report, _settings.Settings.DisplayName, From.SelectedDate, To.SelectedDate));
            TheCafePOS_WPF.Core.Toast.Show(this, "Đã xuất file Excel.");
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
}
