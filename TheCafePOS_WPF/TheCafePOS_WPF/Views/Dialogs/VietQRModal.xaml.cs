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
            Loaded += (_, _) => StartAutoConfirm();
            _amount = amount;
            _dailyOrderNumber = dailyOrderNumber;
            _reference = reference;

            TxtOrderNumber.Text = $"Mã Đơn: #{_dailyOrderNumber:D2}" + $"\nNội dung CK: {VietQRService.TransferMemo(reference)}";
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

        private System.Windows.Threading.DispatcherTimer? _poll;
        private bool _checking;

        // Every 4s ask SePay whether the transfer arrived; on a match, record a system approval and finish.
        private void StartAutoConfirm()
        {
            if (!SePayService.Enabled) return;
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
                    var txId = await SePayService.FindTransferAsync(memo, _amount, since);
                    if (txId is null) return;
                    _poll.Stop();
                    AuthService.Instance.ApproveBySystem(AuthService.PaymentApprovalAction(_reference, _amount), "SePay:" + txId);
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
            var approval = new PinApprovalModal(AuthService.PaymentApprovalAction(_reference, _amount)) { Owner = this };
            if (approval.ShowDialog() == true) DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
