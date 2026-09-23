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

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
