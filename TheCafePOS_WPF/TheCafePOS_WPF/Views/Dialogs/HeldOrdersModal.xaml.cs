using System.Windows;
using TheCafePOS_WPF.Services;

namespace TheCafePOS_WPF.Views.Dialogs
{
    public partial class HeldOrdersModal : Window
    {
        public HeldOrderInfo? SelectedHeldOrder { get; private set; }

        public HeldOrdersModal()
        {
            InitializeComponent();
            LbHeldOrders.ItemsSource = HoldOrderService.Instance.HeldOrders;
            if (HoldOrderService.Instance.HeldOrders.Count > 0)
            {
                LbHeldOrders.SelectedIndex = 0;
            }
        }

        private void BtnRecall_Click(object sender, RoutedEventArgs e)
        {
            if (LbHeldOrders.SelectedItem is HeldOrderInfo item)
            {
                try { SelectedHeldOrder = HoldOrderService.Instance.RecallOrder(item.Id); DialogResult = true; }
                catch (System.Exception ex) { MessageBox.Show(this, ex.Message); }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn đơn cần khôi phục!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Customer left without paying: remove the held cart after confirmation.
        private void BtnDiscard_Click(object sender, RoutedEventArgs e)
        {
            if (LbHeldOrders.SelectedItem is not HeldOrderInfo item) return;
            if (MessageBox.Show(this, $"Hủy đơn giữ lúc {item.HeldTime:HH:mm} ({item.Items.Count} dòng món, {item.TotalAmount:N0}đ)? Không thể khôi phục.", "Hủy đơn giữ", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try
            {
                HoldOrderService.Instance.DiscardOrder(item.Id);
                if (HoldOrderService.Instance.HeldOrders.Count > 0) LbHeldOrders.SelectedIndex = 0;
                TheCafePOS_WPF.Core.Toast.Show(this, "Đã hủy đơn giữ.");
            }
            catch (System.Exception ex) { TheCafePOS_WPF.Core.Toast.Show(this, ex.Message, true); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
