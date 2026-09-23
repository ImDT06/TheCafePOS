using System.Windows;
using TheCafePOS_WPF.Services;

namespace TheCafePOS_WPF.Views.Dialogs
{
    public partial class ReprintStickerModal : Window
    {
        private readonly PrintedStickerInfo _stickerInfo;
        public string SelectedReason { get; private set; } = "Rách ly / Kẹt máy dập nắp";

        public ReprintStickerModal(PrintedStickerInfo stickerInfo)
        {
            InitializeComponent();
            _stickerInfo = stickerInfo;
            TxtStickerDetails.Text = $"{stickerInfo.DailyOrderFormatted} - {stickerInfo.ProductName} ({stickerInfo.IndexFormatted})";
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (RbReason1.IsChecked == true) SelectedReason = "Rách ly / Kẹt máy dập";
            else if (RbReason2.IsChecked == true) SelectedReason = "Làm đổ ly / Pha lại";
            else if (RbReason3.IsChecked == true) SelectedReason = "Tem mờ / Đèn in lỗi";
            else if (RbReason4.IsChecked == true) SelectedReason = "Bị mất tem dán";

            try
            {
                StickerPrinterService.Instance.ReprintSticker(_stickerInfo, SelectedReason, AuthService.Instance.CurrentUser?.Username ?? "");
                DataStoreService.Instance.Save();
            }
            catch (System.Exception ex) { MessageBox.Show(this, ex.Message); return; }

            MessageBox.Show($"Đã tạo dữ liệu tem in lại: {SelectedReason}. Chưa kết nối máy in.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
