using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class LoginWindow : Window
{
    private readonly IAuthService _auth;
    private readonly IDialogFactory _dialogs;
    // Set while signing in a staff member by PIN; null = username and password.
    private string? _pinUser;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private static readonly CultureInfo Vietnamese = new("vi-VN");

    public sealed record PinTile(string Username, string Initial);

    public LoginWindow(IAuthService auth, IDialogFactory dialogs)
    {
        _auth = auth;
        _dialogs = dialogs;
        InitializeComponent();
        if (_auth.NeedsSetup)
        {
            Welcome.Text = "Thiết lập quán";
            FormTitle.Text = "Tạo tài khoản chủ quán";
            Heading.Text = "Lần đầu sử dụng: tạo tài khoản chủ quán. Mật khẩu tối thiểu 8 ký tự.";
            Confirmation.Visibility = Visibility.Visible;
            FormFields.Height = double.NaN; // the extra confirm box needs room; there is no mode switch to keep steady
            ForgotPassword.Visibility = Visibility.Collapsed;
            SubmitText.Text = "Tạo tài khoản và đăng nhập";
        }
        else if (_auth.Accounts.Where(a => a is { Role: "Cashier", IsActive: true, HasPin: true }).ToList() is { Count: > 0 } staff)
        {
            PinAccounts.ItemsSource = staff.Select(a => new PinTile(a.Username, a.Username[..1].ToUpper(Vietnamese))).ToList();
            ModeSwitch.Visibility = Visibility.Visible;
            ModePin.IsChecked = true;
        }

        Tick();
        _clock.Tick += (_, _) => Tick();
        _clock.Start();
        Closed += (_, _) => _clock.Stop();
        // Caps Lock can change while either password box has focus.
        foreach (var box in new[] { Password, ConfirmPassword })
        {
            box.KeyUp += (_, _) => UpdateCapsLock();
            box.GotKeyboardFocus += (_, _) => UpdateCapsLock();
            box.LostKeyboardFocus += (_, _) => UpdateCapsLock();
        }
        Loaded += (_, _) =>
        {
            if (_pinUser is not null) Pin.Focus();
            else if (PasswordEntry.Visibility == Visibility.Visible) (Username.Text.Length > 0 ? (Control)Password : Username).Focus();
        };
    }

    /// <summary>The till was locked mid-shift: offer <paramref name="previousUser"/> first, but anyone may take over.</summary>
    public LoginWindow AsLockScreen(string? previousUser, string? reason)
    {
        Title = "TheCafePOS — Máy đang khóa";
        Welcome.Text = "Máy đang khóa";
        FormTitle.Text = "Mở khóa";
        Heading.Text = (reason is null ? "" : reason + " ") + "Người khác đăng nhập thì đơn đang làm được tạm giữ.";
        if (previousUser is null) return this;
        if (PinAccounts.ItemsSource is IEnumerable<PinTile> tiles && tiles.Any(t => t.Username == previousUser)) SelectPinUser(previousUser);
        else
        {
            if (ModeSwitch.Visibility == Visibility.Visible) ModePassword.IsChecked = true;
            Username.Text = previousUser;
            ShowError("");
        }
        return this;
    }

    private void Tick()
    {
        var now = DateTime.Now;
        ClockTime.Text = now.ToString("HH:mm");
        ClockDate.Text = Vietnamese.TextInfo.ToTitleCase(now.ToString("dddd, dd/MM/yyyy", Vietnamese));
    }

    // Shown as the box's helper text so the warning never pushes the layout down.
    private void UpdateCapsLock()
    {
        bool on = Keyboard.IsKeyToggled(Key.CapsLock);
        foreach (var box in new[] { Password, ConfirmPassword })
            MaterialDesignThemes.Wpf.HintAssist.SetHelperText(box, on && box.IsKeyboardFocused ? "Caps Lock đang bật" : null);
    }

    private void ShowError(string message)
    {
        Error.Text = message;
        ErrorBanner.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Input_Changed(object sender, RoutedEventArgs e) => ShowError("");

    private void ModePin_Checked(object sender, RoutedEventArgs e)
    {
        PinPanel.Visibility = Visibility.Visible;
        PasswordEntry.Visibility = Visibility.Hidden;
        ShowPinPick();
    }

    private void ModePassword_Checked(object sender, RoutedEventArgs e)
    {
        _pinUser = null;
        PinPanel.Visibility = Visibility.Hidden;
        PasswordEntry.Visibility = Visibility.Visible;
        ShowError("");
        Username.Focus();
    }

    private void ShowPinPick()
    {
        _pinUser = null;
        PinPick.Visibility = Visibility.Visible;
        PinEntry.Visibility = Visibility.Hidden;
        ShowError("");
    }

    private void PinUser_Click(object sender, RoutedEventArgs e) => SelectPinUser((string)((FrameworkElement)sender).Tag);

    private void SelectPinUser(string username)
    {
        _pinUser = username;
        PinTitle.Text = _pinUser;
        PinInitial.Text = _pinUser[..1].ToUpper(Vietnamese);
        PinPick.Visibility = Visibility.Hidden;
        PinEntry.Visibility = Visibility.Visible;
        ShowError("");
        Pin.Clear(); Pin.Focus();
    }

    private void ChangePinUser_Click(object sender, RoutedEventArgs e) => ShowPinPick();

    private void ForgotPassword_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this, "Nhân viên: nhờ chủ quán đặt lại mật khẩu trong Quản trị > Tài khoản.\nChủ quán: liên hệ người cài đặt phần mềm để được hỗ trợ.", "Quên mật khẩu", MessageBoxButton.OK, MessageBoxImage.Information);

    // Empty fields are caught here so they neither reach the service nor count as a failed attempt.
    private string? MissingInput()
    {
        if (_pinUser is not null) return Pin.Password.Length == 0 ? "Nhập mã PIN." : null;
        if (PinPanel.Visibility == Visibility.Visible) return "Chọn tên của bạn để đăng nhập bằng mã PIN.";
        if (string.IsNullOrWhiteSpace(Username.Text)) { Username.Focus(); return "Nhập tên đăng nhập."; }
        if (Password.Password.Length == 0) { Password.Focus(); return "Nhập mật khẩu."; }
        return null;
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        if (MissingInput() is { } missing) { ShowError(missing); return; }
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
                    _auth.Logout(); ShowError("Cần đổi mật khẩu tạm để tiếp tục."); return;
                }
            }
            // A PIN sign-in is someone taking a turn at the shared till; the next launch should ask again.
            _auth.RememberCurrentSession(_pinUser is null && RememberLogin.IsChecked == true);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            // Clear first: clearing a box raises the change event, which hides the banner.
            if (_pinUser is not null) { Pin.Clear(); Pin.Focus(); }
            else if (!_auth.NeedsSetup) { Password.Clear(); Password.Focus(); }
            ShowError(ex.Message);
        }
    }
}
