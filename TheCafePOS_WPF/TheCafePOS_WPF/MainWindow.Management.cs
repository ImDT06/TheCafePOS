using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.ViewModels;
using TheCafePOS_WPF.Views.Dialogs;
namespace TheCafePOS_WPF;
public partial class MainWindow
{
    private void Utilities_Click(object sender, RoutedEventArgs e) => UtilitiesPopup.IsOpen = !UtilitiesPopup.IsOpen;
    private void RefreshSession()
    {
        var user = _auth.CurrentUser;
        var shift = _shifts.CurrentShift;
        TxtSession.Text = $"{user?.Username} ({user?.RoleLabel}) • {(shift.Status == "Open" ? $"Ca đang mở: {shift.CashierName}" : "Chưa mở ca")}";
        BtnManagement.Visibility = BtnReports.Visibility = _auth.IsManager ? Visibility.Visible : Visibility.Collapsed;
        _vm.SetCheckoutError(null); // also re-evaluates the shift hint
    }
    private void Finance_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        try { _shifts.RequireOpenShift(); _dialogs.Create<FinanceWindow>(this).ShowDialog(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        if (_auth.CurrentUser is null) { MessageBox.Show(this, "Vui lòng đăng nhập."); return; }
        _dialogs.Create<PasswordWindow>(this).ShowDialog();
    }
    private void Management_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        try { _dialogs.Create<ManagementWindow>(this).ShowDialog(); _vm.ReloadMenu(); LoadStoreSettings(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void Reports_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        try { _dialogs.Create<ReportWindow>(this).ShowDialog(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void SwitchUser_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        if (_vm.HasItems) { MessageBox.Show(this, "Hãy giữ hoặc hoàn tất đơn trước khi đổi nhân viên."); return; }
        try { _auth.Logout(); }
        catch (Exception ex) { MessageBox.Show(this, "Không thể xóa phiên đăng nhập: " + ex.Message); return; }
        Hide();
        if (_dialogs.Create<LoginWindow>(null).ShowDialog() == true) { RefreshSession(); Show(); Activate(); }
        else Close();
    }
    private void OpenShift_Click(object sender, RoutedEventArgs e)
    {
        UtilitiesPopup.IsOpen = false;
        if (_shifts.CurrentShift.Status == "Open") { MessageBox.Show(this, "Ca hiện tại chưa đóng."); return; }
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
                _shifts.OpenShift(cash); window.DialogResult = true; RefreshSession();
            }
            catch (Exception ex) { MessageBox.Show(window, ex.Message); }
        };
        var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        actions.Children.Add(new Button { Content = "Hủy", IsCancel = true, Margin = new Thickness(0, 0, 12, 0) }); actions.Children.Add(submit);
        Core.DialogLayout.Apply(window, "Mở ca làm việc", "Đếm và nhập tiền mặt có sẵn trong ngăn kéo", panel, actions);
        window.ShowDialog();
    }
}


