using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;
public sealed class PasswordWindow : Window
{
    private readonly IAuthService _auth;
    public PasswordWindow(IAuthService auth, string? resetUsername = null)
    {
        _auth = auth;
        Title = resetUsername is null ? "Đổi mật khẩu" : "Đặt lại mật khẩu: " + resetUsername;
        Width = 520; Height = 630; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = panel;
        PasswordBox Field(string text)
        {
            panel.Children.Add(new TextBlock { Text = text });
            var box = new PasswordBox { Padding = new Thickness(10), MinHeight = 44, Margin = new Thickness(0, 5, 0, 12) }; panel.Children.Add(box); return box;
        }
        var old = resetUsername is null ? Field("Mật khẩu hiện tại") : null;
        // Staff may change only their PIN here: leave the new password empty.
        bool staffPin = resetUsername is null && auth.CurrentUser is { Role: "Cashier", MustChangePassword: false };
        var password = Field(staffPin ? "Mật khẩu mới (bỏ trống nếu không đổi)" : "Mật khẩu mới (tối thiểu 8 ký tự)"); var confirm = Field("Nhập lại mật khẩu mới");
        var pin = staffPin ? Field("Mã PIN mới 4–6 số (bỏ trống nếu không đổi)") : null;
        if (pin is not null) pin.MaxLength = 6;
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; panel.Children.Add(error);
        var button = new Button { Content = "Lưu mật khẩu", Style = (Style)FindResource("PrimaryButton") };
        error.Foreground = PosUi.Danger;
        var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        actions.Children.Add(new Button { Content = "Hủy", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) }); actions.Children.Add(button);
        Content = null;
        Core.DialogLayout.Apply(this, resetUsername is null ? "Đổi mật khẩu" : "Đặt lại mật khẩu", resetUsername is null ? "Bảo vệ tài khoản bằng mật khẩu riêng của bạn" : $"Tài khoản {resetUsername} sẽ phải đổi mật khẩu ở lần đăng nhập tiếp theo.", panel, actions);
        button.Click += (_, _) =>
        {
            try
            {
                if (password.Password != confirm.Password) throw new InvalidOperationException("Mật khẩu nhập lại chưa khớp.");
                bool newPin = pin is { Password.Length: > 0 };
                if (newPin && !AuthService.IsValidPin(pin!.Password)) throw new InvalidOperationException("Mã PIN gồm 4–6 chữ số.");
                if (!newPin && password.Password.Length == 0 && pin is not null) throw new InvalidOperationException("Nhập mật khẩu mới hoặc mã PIN mới.");
                bool newPassword = password.Password.Length > 0 || pin is null;
                if (resetUsername is not null) _auth.ResetPassword(resetUsername, password.Password);
                else if (newPassword) _auth.ChangePassword(old!.Password, password.Password);
                if (newPin) _auth.ChangePin(newPassword ? password.Password : old!.Password, pin!.Password);
                DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
    }
}
