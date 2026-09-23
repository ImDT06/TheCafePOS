using System;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheCafePOS_WPF.Services;

namespace TheCafePOS_WPF.Views.Dialogs
{
    public partial class VietQRModal : Window
    {
        private readonly decimal _amount;
        private readonly int _dailyOrderNumber;
        private readonly string _reference;

        public VietQRModal(decimal amount, int dailyOrderNumber, string reference)
        {
            InitializeComponent();
            _amount = amount;
            _dailyOrderNumber = dailyOrderNumber;
            _reference = reference;

            TxtOrderNumber.Text = $"Mã Đơn: #{_dailyOrderNumber:D2}";
            TxtAmount.Text = $"{_amount:N0}đ";

            LoadQRImage();
        }

        private void LoadQRImage()
        {
            try
            {
                string qrUrl = VietQRService.GenerateQRUrl(_amount, _reference);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(qrUrl, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                ImgQR.Source = bitmap;
            }
            catch (Exception ex)
            {
                TxtPaymentStatus.Text = "Không tải được mã QR: " + ex.Message;
            }
        }

        private void BtnSimulateSuccess_Click(object sender, RoutedEventArgs e)
        {
            var approval = new PinApprovalModal(AuthService.PaymentApprovalAction(_reference, _amount)) { Owner = this };
            if (approval.ShowDialog() == true) DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
