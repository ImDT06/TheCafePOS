using System.Windows;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class LoginWindow : Window
{
    private readonly IAuthService _auth;
    private readonly IDialogFactory _dialogs;
    // Set while signing in a staff member by PIN; null = username and password.
    private string? _pinUser;
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
        else if (_auth.Accounts.Where(a => a is { Role: "Cashier", IsActive: true, HasPin: true }).ToList() is { Count: > 0 } staff)
        {
            PinAccounts.ItemsSource = staff;
            PinStaff.Visibility = Visibility.Visible;
        }
        Loaded += (_, _) => Username.Focus();
    }
    private void PinUser_Click(object sender, RoutedEventArgs e)
    {
        _pinUser = (string)((FrameworkElement)sender).Tag;
        PinTitle.Text = "Mã PIN của " + _pinUser;
        PinEntry.Visibility = Visibility.Visible;
        PasswordEntry.Visibility = RememberLogin.Visibility = Visibility.Collapsed;
        Error.Text = "";
        Pin.Clear(); Pin.Focus();
    }
    private void UsePassword_Click(object sender, RoutedEventArgs e)
    {
        _pinUser = null;
        PinEntry.Visibility = Visibility.Collapsed;
        PasswordEntry.Visibility = RememberLogin.Visibility = Visibility.Visible;
        Error.Text = "";
        Username.Focus();
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
            if (_pinUser is not null) _auth.LoginWithPin(_pinUser, Pin.Password);
            else _auth.Login(Username.Text, Password.Password);
            if (_auth.CurrentUser!.MustChangePassword)
            {
                if (_dialogs.Create<PasswordWindow>(this).ShowDialog() != true)
                {
                    _auth.Logout(); Error.Text = "Cần đổi mật khẩu tạm để tiếp tục."; return;
                }
            }
            // A PIN sign-in is someone taking a turn at the shared till; the next launch should ask again.
            _auth.RememberCurrentSession(_pinUser is null && RememberLogin.IsChecked == true);
            DialogResult = true;
        }
        catch (Exception ex) { Error.Text = ex.Message; }
    }
}

