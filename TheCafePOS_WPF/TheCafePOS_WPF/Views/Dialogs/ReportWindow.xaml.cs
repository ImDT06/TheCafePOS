using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
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
        Summary.Text = $"{report.Orders.Count} đơn | Bán hàng: {report.Revenue:N0}đ | Hoàn: {report.Refunded:N0}đ | Thuần: {report.NetRevenue:N0}đ\nBán tiền mặt: {report.Cash:N0}đ · Hoàn tiền mặt: {report.CashRefunded:N0}đ | Bán QR: {report.Transfer:N0}đ · Hoàn QR: {report.TransferRefunded:N0}đ\nThu ngoài bán hàng: {report.CashIn:N0}đ | Chi ngoài bán hàng: {report.CashOut:N0}đ";
        return report;
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) { try { RefreshReport(); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }
    private void OrderSelected(object sender, SelectionChangedEventArgs e) => Items.ItemsSource = (Orders.SelectedItem as Order)?.Items;
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
