using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using TheCafePOS_WPF.Services;

namespace TheCafePOS_WPF.Views.Dialogs
{
    public partial class BlindDropModal : Window
    {
        private readonly decimal _expectedCash;

        public BlindDropModal(decimal expectedCash)
        {
            InitializeComponent();
            _expectedCash = expectedCash;
            TxtInitialCash.Text = $"{DataStoreService.Instance.CurrentShift.InitialCash:N0}đ";
        }

        private void TxtActualCashInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void BtnConfirmDrop_Click(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TxtActualCashInput.Text, out decimal actualCash) || actualCash < 0)
            {
                MessageBox.Show("Vui lòng nhập số tiền hợp lệ!", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal diff = actualCash - _expectedCash;

            // Show result after employee submits blind drop
            PnlResult.Visibility = Visibility.Visible;
            TxtExpectedCash.Text = $"Hệ thống ghi nhận: {_expectedCash:N0}đ";
            TxtDiscrepancy.Text = $"Chênh lệch thực tế: {diff:N0}đ ({(diff >= 0 ? "Thừa" : "Thiếu")})";

            BtnConfirmDrop.Content = "HOÀN TẤT CHỐT CA";
            BtnConfirmDrop.Click -= BtnConfirmDrop_Click;
            BtnConfirmDrop.Click += (s, ev) =>
            {
                try { DataStoreService.Instance.CloseShift(actualCash); DialogResult = true; }
                catch (System.Exception ex) { MessageBox.Show(this, ex.Message); }
            };
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
