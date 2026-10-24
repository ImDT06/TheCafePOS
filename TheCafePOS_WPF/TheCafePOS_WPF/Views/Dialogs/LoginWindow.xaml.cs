using System.Windows;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class LoginWindow : Window
{
    private readonly IAuthService _auth;
    private readonly IDialogFactory _dialogs;
    public LoginWindow(IAuthService auth, IDialogFactory dialogs)
    {
        _auth = auth;
        _dialogs = dialogs;
        InitializeComponent();
        if (_auth.NeedsSetup)
        {
            Heading.Text = "Lần đầu sử dụng: tạo tài khoản chủ quán. Mật khẩu tối thiểu 8 ký tự.";
            Confirmation.Visibility = Visibility.Visible;
            Submit.Content = "Tạo tài khoản và đăng nhập";
        }
        Loaded += (_, _) => Username.Focus();
    }
    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_auth.NeedsSetup)
            {
                if (Password.Password != ConfirmPassword.Password) throw new InvalidOperationException("Mật khẩu nhập lại chưa khớp.");
                _auth.CreateAccount(Username.Text, Password.Password, "Owner");
            }
            _auth.Login(Username.Text, Password.Password);
            if (_auth.CurrentUser!.MustChangePassword)
            {
                if (_dialogs.Create<PasswordWindow>(this).ShowDialog() != true)
                {
                    _auth.Logout(); Error.Text = "Cần đổi mật khẩu tạm để tiếp tục."; return;
                }
            }
            _auth.RememberCurrentSession(RememberLogin.IsChecked == true);
            DialogResult = true;
        }
        catch (Exception ex) { Error.Text = ex.Message; }
    }
}

