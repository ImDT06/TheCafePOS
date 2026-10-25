using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs
{
    public partial class ReprintStickerModal : Window
    {
        public sealed class CupChoice
        {
            public PrintedStickerInfo Sticker { get; init; } = new();
            public bool Selected { get; set; } = true;
        }

        private readonly IOrderService _orders;
        private readonly IAuthService _auth;
        private readonly IStickerPrinterService _stickers;
        private readonly List<CupChoice> _cups;
        public string SelectedReason { get; private set; } = "Rách ly / Kẹt máy dập nắp";

        /// <param name="cups">One sticker from the log, or every cup of an order when reprinting from the barista queue.</param>
        public ReprintStickerModal(IReadOnlyList<PrintedStickerInfo> cups, IOrderService orders, IAuthService auth, IStickerPrinterService stickers)
        {
            _orders = orders;
            _auth = auth;
            _stickers = stickers;
            InitializeComponent();
            _cups = cups.Select(s => new CupChoice { Sticker = s }).ToList();
            var first = cups[0];
            if (_cups.Count == 1) TxtStickerDetails.Text = $"Đơn {first.DailyOrderFormatted} · {first.ProductName} ({first.IndexFormatted})";
            else
            {
                TxtStickerDetails.Text = $"Đơn {first.DailyOrderFormatted} · {_cups.Count} ly";
                CupList.ItemsSource = _cups;
                CupPanel.Visibility = Visibility.Visible;
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (RbReason1.IsChecked == true) SelectedReason = "Rách ly / Kẹt máy dập";
            else if (RbReason2.IsChecked == true) SelectedReason = "Làm đổ ly / Pha lại";
            else if (RbReason3.IsChecked == true) SelectedReason = "Tem mờ / Đèn in lỗi";
            else if (RbReason4.IsChecked == true) SelectedReason = "Bị mất tem dán";

            var chosen = _cups.Where(c => c.Selected).Select(c => c.Sticker).ToList();
            if (chosen.Count == 0) { MessageBox.Show(this, "Chọn ít nhất một ly để in lại."); return; }
            try
            {
                foreach (var sticker in chosen) _stickers.ReprintSticker(sticker, SelectedReason, _auth.CurrentUser?.Username ?? "");
                _orders.Save();
            }
            catch (System.Exception ex) { MessageBox.Show(this, ex.Message); return; }

            MessageBox.Show($"Đã tạo dữ liệu {chosen.Count} tem in lại: {SelectedReason}. Chưa kết nối máy in.", "Thông Báo", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
