using System.Windows;
using TheCafePOS_WPF.Services;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        if (AuthService.Instance.NeedsSetup)
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
            if (AuthService.Instance.NeedsSetup)
            {
                if (Password.Password != ConfirmPassword.Password) throw new InvalidOperationException("Mật khẩu nhập lại chưa khớp.");
                AuthService.Instance.CreateAccount(Username.Text, Password.Password, "Owner");
            }
            AuthService.Instance.Login(Username.Text, Password.Password);
            if (AuthService.Instance.CurrentUser!.MustChangePassword)
            {
                if (new PasswordWindow { Owner = this }.ShowDialog() != true)
                {
                    AuthService.Instance.Logout(); Error.Text = "Cần đổi mật khẩu tạm để tiếp tục."; return;
                }
            }
            AuthService.Instance.RememberCurrentSession(RememberLogin.IsChecked == true);
            DialogResult = true;
        }
        catch (Exception ex) { Error.Text = ex.Message; }
    }
}

