using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Services;
namespace TheCafePOS_WPF.Views.Dialogs;
public sealed class PasswordWindow : Window
{
    public PasswordWindow(string? resetUsername = null)
    {
        Title = resetUsername is null ? "Đổi mật khẩu" : "Đặt lại mật khẩu: " + resetUsername;
        Width = 440; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = resetUsername is null ? "Nhập mật khẩu hiện tại và mật khẩu mới." : "Nhân viên phải đổi mật khẩu tạm ở lần đăng nhập tiếp theo.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        PasswordBox Field(string text)
        {
            panel.Children.Add(new TextBlock { Text = text });
            var box = new PasswordBox { Padding = new Thickness(10), MinHeight = 44, Margin = new Thickness(0, 5, 0, 12) }; panel.Children.Add(box); return box;
        }
        var old = resetUsername is null ? Field("Mật khẩu hiện tại") : null;
        var password = Field("Mật khẩu mới (tối thiểu 8 ký tự)"); var confirm = Field("Nhập lại mật khẩu mới");
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; panel.Children.Add(error);
        var button = new Button { Content = "Lưu mật khẩu", MinHeight = 48 }; panel.Children.Add(button);
        button.Click += (_, _) =>
        {
            try
            {
                if (password.Password != confirm.Password) throw new InvalidOperationException("Mật khẩu nhập lại chưa khớp.");
                if (resetUsername is null) AuthService.Instance.ChangePassword(old!.Password, password.Password);
                else AuthService.Instance.ResetPassword(resetUsername, password.Password);
                DialogResult = true;
            }
            catch (Exception ex) { error.Text = ex.Message; }
        };
    }
}
