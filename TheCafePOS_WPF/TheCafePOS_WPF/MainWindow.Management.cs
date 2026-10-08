using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Views.Dialogs;
namespace TheCafePOS_WPF;
public partial class MainWindow
{
    private void Utilities_Click(object sender, RoutedEventArgs e) => UtilitiesPopup.IsOpen = !UtilitiesPopup.IsOpen;
    private void RefreshSession()
    {
        var user = AuthService.Instance.CurrentUser;
        var shift = DataStoreService.Instance.CurrentShift;
        TxtSession.Text = $"{user?.Username} ({user?.Role}) • {(shift.Status == "Open" ? $"Ca đang mở: {shift.CashierName}" : "Chưa mở ca")}";
        BtnManagement.Visibility = BtnReports.Visibility = AuthService.Instance.IsManager ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Finance_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        try { DataStoreService.Instance.RequireOpenShift(); new FinanceWindow { Owner = this }.ShowDialog(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        if (AuthService.Instance.CurrentUser is null) { MessageBox.Show(this, "Vui lòng đăng nhập."); return; }
        new PasswordWindow { Owner = this }.ShowDialog();
    }
    private void Management_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        try { new ManagementWindow { Owner = this }.ShowDialog(); _selectedCategoryId = "ALL"; RenderCategories(); RenderProducts(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        try { new ReportWindow { Owner = this }.ShowDialog(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void SwitchUser_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        if (_cartItems.Count > 0) { MessageBox.Show(this, "Hãy giữ hoặc hoàn tất đơn trước khi đổi nhân viên."); return; }
        try { AuthService.Instance.Logout(); }
        catch (Exception ex) { MessageBox.Show(this, "Không thể xóa phiên đăng nhập: " + ex.Message); return; }
        Hide();
        if (new LoginWindow().ShowDialog() == true) { RefreshSession(); Show(); Activate(); }
        else Close();
    }
    private void OpenShift_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        if (DataStoreService.Instance.CurrentShift.Status == "Open") { MessageBox.Show(this, "Ca hiện tại chưa đóng."); return; }
        var window = new Window { Owner = this, Title = "Mở ca làm việc", Width = 500, Height = 430, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = "Tiền mặt đầu ca (đồng)" });
        var input = new TextBox { Text = "0", Margin = new Thickness(0, 10, 0, 10), Padding = new Thickness(8) };
        TheCafePOS_WPF.Core.TouchInput.SetNumeric(input, true);
        Core.TouchInput.SetLabel(input, "Tiền mặt đầu ca");
        panel.Children.Add(input);
        var submit = new Button { Content = "Bắt đầu ca", Style = (Style)FindResource("PrimaryButton"), IsDefault = true };
        submit.Click += (_, _) =>
        {
            try
            {
                if (!decimal.TryParse(input.Text, out var cash)) throw new InvalidOperationException("Nhập số tiền hợp lệ.");
                DataStoreService.Instance.OpenShift(cash); window.DialogResult = true; RefreshSession();
            }
            catch (Exception ex) { MessageBox.Show(window, ex.Message); }
        };
        var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        actions.Children.Add(new Button { Content = "Hủy", IsCancel = true, Margin = new Thickness(0, 0, 12, 0) }); actions.Children.Add(submit);
        Core.DialogLayout.Apply(window, "Mở ca làm việc", "Đếm và nhập tiền mặt có sẵn trong ngăn kéo", panel, actions);
        window.ShowDialog();
    }
}


