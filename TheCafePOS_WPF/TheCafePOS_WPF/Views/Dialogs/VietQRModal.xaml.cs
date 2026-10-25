using System;
using System.IO;
using System.Net.Http;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs
{
    public partial class VietQRModal : Window
    {
        private readonly IAuthService _auth;
        private readonly IVietQRService _bank;
        private readonly ISePayService _sePay;
        private readonly decimal _amount;
        private readonly int _dailyOrderNumber;
        private readonly string _reference;

        public VietQRModal(decimal amount, int dailyOrderNumber, string reference, IAuthService auth, IVietQRService bank, ISePayService sePay)
        {
            _auth = auth;
            _bank = bank;
            _sePay = sePay;
            InitializeComponent();
            Loaded += (_, _) => StartAutoConfirm();
            _amount = amount;
            _dailyOrderNumber = dailyOrderNumber;
            _reference = reference;

            TxtOrderNumber.Text = $"Đơn #{_dailyOrderNumber:D2} · Khách quét mã bằng ứng dụng ngân hàng";
            TxtAmount.Text = $"{_amount:N0}đ";
            var account = _bank.Settings;
            TxtBank.Text = Or(account.BankId);
            TxtAccountNo.Text = Or(account.AccountNo);
            TxtAccountName.Text = Or(account.AccountName);
            TxtMemo.Text = VietQRService.TransferMemo(reference);

            LoadQRImage();
        }

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("TheCafePOS/1.0");
            return http;
        }

        // Download the bytes ourselves: a BitmapImage pointed at a remote URI fails silently and leaves the QR blank.
        private async void LoadQRImage()
        {
            try
            {
                string qrUrl = _bank.GenerateQRUrl(_amount, _reference);
                var bytes = await Http.GetByteArrayAsync(qrUrl);
                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(bytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                ImgQR.Source = bitmap;
                QrLoading.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                _qrError = ex is InvalidOperationException ? ex.Message : "Không tải được mã QR. Kiểm tra kết nối mạng rồi mở lại.";
                TxtQrError.Text = _qrError;
                QrFrame.Visibility = Visibility.Collapsed;
                QrError.Visibility = Visibility.Visible;
                SetStatus("Chưa nhận chuyển khoản qua mã QR được. Quay lại để chọn cách thanh toán khác.", danger: true);
            }
        }
        private string? _qrError;
        private static string Or(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

        private void SetStatus(string text, bool danger = false)
        {
            TxtPaymentStatus.Text = text;
            StatusBar.Background = (Brush)FindResource(danger ? "PosDangerSoft" : "PosWarningSoft");
            StatusDot.Fill = (Brush)FindResource(danger ? "PosDanger" : "PosWarning");
            TxtPaymentStatus.Foreground = (Brush)FindResource(danger ? "PosDanger" : "PosWarningInk");
        }

        private System.Windows.Threading.DispatcherTimer? _poll;
        private bool _checking;

        // Every 4s ask SePay whether the transfer arrived; on a match, record a system approval and finish.
        private void StartAutoConfirm()
        {
            if (!_sePay.Enabled || _qrError is not null) return;
            var since = DateTime.Now.AddMinutes(-2);
            string memo = VietQRService.TransferMemo(_reference);
            SetStatus("Đang chờ khách chuyển khoản · tự xác nhận khi tiền về");
            _poll = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _poll.Tick += async (_, _) =>
            {
                if (_checking) return;
                _checking = true;
                try
                {
                    var txId = await _sePay.FindTransferAsync(memo, _amount, since);
                    if (txId is null) return;
                    _poll.Stop();
                    _auth.ApproveBySystem(AuthService.PaymentApprovalAction(_reference, _amount), "SePay:" + txId);
                    SystemSounds.Asterisk.Play();
                    DialogResult = true;
                }
                catch (Exception ex) { SetStatus("Chưa kiểm tra được giao dịch tự động (" + ex.Message + "). Chủ quán có thể xác nhận thủ công.", danger: true); }
                finally { _checking = false; }
            };
            _poll.Start();
            Closed += (_, _) => _poll.Stop();
        }

        private void BtnSimulateSuccess_Click(object sender, RoutedEventArgs e)
        {
            string question = $"Đã xem bill chuyển khoản của khách?\n\n• Số tiền: {_amount:N0}đ\n• Người nhận: {Or(_bank.Settings.AccountName)}\n• Thời gian: vừa chuyển\n\nTên của bạn sẽ được ghi lại là người xác nhận.";
            if (MessageBox.Show(this, question, "Xác nhận đã nhận tiền", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try { _auth.ConfirmTransferByStaff(AuthService.PaymentApprovalAction(_reference, _amount)); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
            _poll?.Stop();
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
