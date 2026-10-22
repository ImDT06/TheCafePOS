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
        private readonly IDialogFactory _dialogs;
        private readonly IVietQRService _bank;
        private readonly ISePayService _sePay;
        private readonly decimal _amount;
        private readonly int _dailyOrderNumber;
        private readonly string _reference;

        public VietQRModal(decimal amount, int dailyOrderNumber, string reference, IAuthService auth, IDialogFactory dialogs, IVietQRService bank, ISePayService sePay)
        {
            _auth = auth;
            _dialogs = dialogs;
            _bank = bank;
            _sePay = sePay;
            InitializeComponent();
            Loaded += (_, _) => StartAutoConfirm();
            _amount = amount;
            _dailyOrderNumber = dailyOrderNumber;
            _reference = reference;

            TxtOrderNumber.Text = $"Mã đơn: #{_dailyOrderNumber:D2}" + $"\nNội dung CK: {VietQRService.TransferMemo(reference)}";
            TxtAmount.Text = $"{_amount:N0}đ";

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
                TxtPaymentStatus.Text = _qrError;
                TxtPaymentStatus.Foreground = PosUi.Danger;
                QrFrame.Visibility = Visibility.Collapsed;
            }
        }
        private string? _qrError;

        private System.Windows.Threading.DispatcherTimer? _poll;
        private bool _checking;

        // Every 4s ask SePay whether the transfer arrived; on a match, record a system approval and finish.
        private void StartAutoConfirm()
        {
            if (!_sePay.Enabled || _qrError is not null) return;
            var since = DateTime.Now.AddMinutes(-2);
            string memo = VietQRService.TransferMemo(_reference);
            TxtPaymentStatus.Text = "Đang chờ khách chuyển khoản… (tự xác nhận qua SePay)";
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
                catch (Exception ex) { TxtPaymentStatus.Text = "Chưa kiểm tra được SePay (" + ex.Message + "). Có thể dùng xác nhận thủ công."; }
                finally { _checking = false; }
            };
            _poll.Start();
            Closed += (_, _) => _poll.Stop();
        }

        private void BtnSimulateSuccess_Click(object sender, RoutedEventArgs e)
        {
            var approval = _dialogs.Create<PinApprovalModal>(this, AuthService.PaymentApprovalAction(_reference, _amount));
            if (approval.ShowDialog() == true) DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
