using System.Linq;
using System.Windows;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs
{
    public partial class HeldOrdersModal : Window
    {
        private readonly IHoldOrderService _holds;
        public HeldOrderInfo? SelectedHeldOrder { get; private set; }

        public HeldOrdersModal(IHoldOrderService holds)
        {
            _holds = holds;
            InitializeComponent();
            LbHeldOrders.ItemsSource = _holds.HeldOrders;
            if (_holds.HeldOrders.Count > 0)
            {
                LbHeldOrders.SelectedIndex = 0;
            }
            UpdateActions();
        }

        private List<HeldOrderInfo> Selected => LbHeldOrders.SelectedItems.Cast<HeldOrderInfo>().ToList();

        // Recall needs exactly one order; discard works on every ticked order.
        private void UpdateActions()
        {
            int count = LbHeldOrders.SelectedItems.Count;
            BtnRecall.IsEnabled = count == 1;
            BtnDiscard.IsEnabled = count > 0;
            BtnDiscard.Content = count > 1 ? $"Hủy {count} đơn giữ" : "Hủy đơn giữ";
            BtnSelectAll.Visibility = _holds.HeldOrders.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            BtnSelectAll.Content = count == _holds.HeldOrders.Count && count > 0 ? "Bỏ chọn" : "Chọn tất cả";
        }

        private void LbHeldOrders_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => UpdateActions();

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (LbHeldOrders.SelectedItems.Count == _holds.HeldOrders.Count) LbHeldOrders.UnselectAll();
            else LbHeldOrders.SelectAll();
        }

        private void BtnRecall_Click(object sender, RoutedEventArgs e)
        {
            if (Selected is [var item])
            {
                try { SelectedHeldOrder = _holds.RecallOrder(item.Id); DialogResult = true; }
                catch (System.Exception ex) { MessageBox.Show(this, ex.Message); }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn đơn cần khôi phục!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Customers left without paying: remove the held carts after confirmation.
        private void BtnDiscard_Click(object sender, RoutedEventArgs e)
        {
            var items = Selected;
            if (items.Count == 0) return;
            string question = items is [var one]
                ? $"Hủy đơn giữ lúc {one.HeldTime:HH:mm} ({one.Items.Count} dòng món, {one.TotalAmount:N0}đ)? Không thể khôi phục."
                : $"Hủy {items.Count} đơn giữ (tổng {items.Sum(i => i.TotalAmount):N0}đ)? Không thể khôi phục.";
            if (MessageBox.Show(this, question, "Hủy đơn giữ", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            int done = 0;
            try
            {
                foreach (var item in items) { _holds.DiscardOrder(item.Id); done++; }
                TheCafePOS_WPF.Core.Toast.Show(this, done > 1 ? $"Đã hủy {done} đơn giữ." : "Đã hủy đơn giữ.");
            }
            catch (System.Exception ex) { TheCafePOS_WPF.Core.Toast.Show(this, (done > 0 ? $"Đã hủy {done} đơn, dừng lại vì: " : "") + ex.Message, true); }
            LbHeldOrders.UnselectAll();
            if (_holds.HeldOrders.Count > 0) LbHeldOrders.SelectedIndex = 0;
            UpdateActions();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
