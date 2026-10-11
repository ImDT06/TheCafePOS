using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class FinanceWindow : Window
{
    private readonly DataStoreService _store = DataStoreService.Instance;
    private string _refundId = Guid.NewGuid().ToString(), _cashId = Guid.NewGuid().ToString();
    public FinanceWindow()
    {
        _store.RequireOpenShift(); InitializeComponent(); OrderDate.SelectedDate = DateTime.Today; Refresh();
    }
    private void Refresh()
    {
        string query = Search.Text.Trim().TrimStart('#');
        Orders.ItemsSource = _store.CompletedOrders.Where(o => o.CreatedAt.Date == OrderDate.SelectedDate?.Date && (query.Length == 0 || o.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || o.DailyOrderNumber.ToString() == query || o.DailyOrderNumber.ToString("D2") == query)).OrderByDescending(o => o.CreatedAt).ToList();
        RefundHistory.ItemsSource = _store.Refunds.Where(r => r.ShiftId == _store.CurrentShift.Id).OrderByDescending(r => r.CreatedAt).ToList();
        CashHistory.ItemsSource = _store.CashEntries.Where(r => r.ShiftId == _store.CurrentShift.Id).OrderByDescending(r => r.CreatedAt).ToList();
    }
    private void Search_Click(object sender, RoutedEventArgs e) => Refresh();
    private void OrderSelected(object sender, SelectionChangedEventArgs e)
    {
        if (RefundAmount == null) return;
        RefundConfirmed.IsChecked = false; RefundReason.Clear(); RefundReference.Clear();
        if (Orders.SelectedItem is not Order order) { OrderDetails.Text = "Chọn đơn cần hoàn tiền."; RefundAmount.Clear(); return; }
        OrderDetails.Text = $"Đơn #{order.DailyOrderNumber:D2} · {order.CreatedAt:dd/MM/yyyy HH:mm}\n{order.PaymentMethod} · Đã hoàn: {order.RefundedAmount:N0}đ\n" + string.Join("\n", order.Items.Select(i => $"{i.Quantity} × {i.ProductName} — {i.TotalPrice:N0}đ"));
        Core.TouchInput.SetMaximum(RefundAmount, order.RefundableAmount);
        RefundAmount.Text = order.RefundableAmount.ToString("0");
    }
    private void CancelOrder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _store.RequireOpenShift();
            if (Orders.SelectedItem is not Order order) throw new InvalidOperationException("Chọn đơn cần hủy.");
            if (order.FulfillmentStatus != "Chờ làm") throw new InvalidOperationException("Đơn đã bắt đầu pha chế, hãy dùng hoàn tiền thay vì hủy.");
            if (string.IsNullOrWhiteSpace(RefundReason.Text)) throw new InvalidOperationException("Nhập lý do hủy đơn vào ô Lý do.");
            if (order.PaymentMethod == "VietQR" && string.IsNullOrWhiteSpace(RefundReference.Text)) throw new InvalidOperationException("Nhập mã giao dịch hoàn chuyển khoản.");
            if (RefundConfirmed.IsChecked != true) throw new InvalidOperationException("Xác nhận đã trả tiền cho khách trước khi ghi nhận.");
            if (MessageBox.Show(this, $"Hủy đơn {order.DailyOrderNumberFormatted} và hoàn {order.TotalAmount:N0}đ?", "Hủy đơn", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var action = DataStoreService.RefundAction(_refundId, order.Id, order.TotalAmount, DataStoreService.CancelReason(RefundReason.Text), RefundReference.Text);
            if (new PinApprovalModal(action) { Owner = this }.ShowDialog() != true) return;
            _store.CancelOrder(_refundId, order.Id, RefundReason.Text, RefundReference.Text);
            _refundId = Guid.NewGuid().ToString(); Refresh(); Status.Text = "Đã hủy đơn, hoàn đủ tiền và trả bao bì / nguyên liệu về kho.";
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void Refund_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _store.RequireOpenShift();
            if (Orders.SelectedItem is not Order order) throw new InvalidOperationException("Chọn đơn cần hoàn.");
            if (!decimal.TryParse(RefundAmount.Text, out var amount) || amount <= 0 || amount != decimal.Truncate(amount) || amount > order.RefundableAmount) throw new InvalidOperationException("Nhập số tiền nguyên dương không vượt số còn được hoàn.");
            if (string.IsNullOrWhiteSpace(RefundReason.Text)) throw new InvalidOperationException("Nhập lý do hoàn tiền.");
            if (order.PaymentMethod == "VietQR" && string.IsNullOrWhiteSpace(RefundReference.Text)) throw new InvalidOperationException("Nhập mã giao dịch hoàn chuyển khoản.");
            if (order.PaymentMethod == "Cash" && amount > _store.ExpectedCash) throw new InvalidOperationException("Ngăn kéo không đủ tiền dự kiến để hoàn. Bổ sung tiền trước.");
            if (RefundConfirmed.IsChecked != true) throw new InvalidOperationException("Xác nhận đã thực hiện trả tiền cho khách trước khi ghi nhận.");
            var action = DataStoreService.RefundAction(_refundId, order.Id, amount, RefundReason.Text, RefundReference.Text);
            if (new PinApprovalModal(action) { Owner = this }.ShowDialog() != true) return;
            _store.RefundOrder(_refundId, order.Id, amount, RefundReason.Text, RefundReference.Text);
            _refundId = Guid.NewGuid().ToString(); Refresh(); Status.Text = "Đã lưu phiếu hoàn tiền. Đơn gốc và tồn bao bì được giữ lại.";
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void Cash_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _store.RequireOpenShift();
            if (!decimal.TryParse(CashAmount.Text, out var amount) || amount <= 0 || amount != decimal.Truncate(amount)) throw new InvalidOperationException("Nhập số tiền nguyên dương.");
            if (string.IsNullOrWhiteSpace(CashReason.Text)) throw new InvalidOperationException("Nhập lý do thu/chi.");
            string type = ((ComboBoxItem)CashType.SelectedItem).Tag.ToString()!;
            if (type == "Out" && amount > _store.ExpectedCash) throw new InvalidOperationException("Khoản chi vượt tiền dự kiến trong ngăn kéo.");
            if (new PinApprovalModal(DataStoreService.CashAction(_cashId, type, amount, CashReason.Text)) { Owner = this }.ShowDialog() != true) return;
            _store.RecordCash(_cashId, type, amount, CashReason.Text);
            _cashId = Guid.NewGuid().ToString(); CashAmount.Clear(); CashReason.Clear(); Refresh(); Status.Text = "Đã lưu phiếu thu/chi vào ca hiện tại.";
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
}
