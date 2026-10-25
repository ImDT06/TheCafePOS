using System.Windows;
using System.Windows.Input;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs
{
    public partial class PinApprovalModal : Window
    {
        private readonly IAuthService _auth;
        private readonly string _actionName;

        public PinApprovalModal(string actionName, IAuthService auth)
        {
            _auth = auth;
            InitializeComponent();
            _actionName = actionName;
            TxtActionTitle.Text = $"Hành động: {actionName}";
            Loaded += (s, e) => TxtUsername.Focus();
        }

        private void TxtPin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ValidateAndConfirm();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            ValidateAndConfirm();
        }

        private void ValidateAndConfirm()
        {
            try
            {
                _auth.Approve(TxtUsername.Text, TxtPin.Password, _actionName);
                DialogResult = true;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message, "Chưa được duyệt", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtPin.Clear();
                TxtPin.Focus();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
