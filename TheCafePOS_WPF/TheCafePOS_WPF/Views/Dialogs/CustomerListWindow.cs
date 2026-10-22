using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs;

// Member list with search, tier / inactivity filters, add / edit / delete and purchase history.
public sealed class CustomerListWindow : Window
{
    private readonly ICustomerService _customers;
    private readonly IAuthService _auth;
    private readonly TextBox _search = new() { Padding = new Thickness(8), FontSize = 15 };
    private readonly ComboBox _filter = new() { Width = 220, Margin = new Thickness(8, 0, 0, 0), FontSize = 14, ItemsSource = new[] { "Tất cả", "Lâu chưa quay lại (30+ ngày)", "Kim cương", "Vàng", "Bạc", "Mới" }, SelectedIndex = 0 };
    private readonly DataGrid _grid = new() { IsReadOnly = true, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Single };
    private readonly TextBox _phone = new() { Margin = new Thickness(0, 8, 0, 8), MaxLength = 15 };
    private readonly TextBox _name = new() { Margin = new Thickness(0, 0, 0, 8), MaxLength = 80 };
    private readonly Button _delete;
    private readonly TextBlock _detail = PosUi.Text("Chọn khách để xem chi tiết.", 14, color: PosUi.Muted);
    private readonly DataGrid _history = new() { IsReadOnly = true, AutoGenerateColumns = false, Height = 260 };
    private readonly TextBlock _summary = PosUi.Text("", 13, color: PosUi.Muted);

    public CustomerListWindow(ICustomerService customers, IAuthService auth)
    {
        _customers = customers;
        _auth = auth;
        _auth.RequireManager();
        Title = "Khách hàng thành viên"; Width = 1100; Height = 720; MinWidth = 820; MinHeight = 520; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current.TryFindResource("PosWindow") is Style style) Style = style;
        EmptyState.SetText(_grid, "Chưa có khách nào khớp bộ lọc");
        EmptyState.SetText(_history, "Chưa có đơn");
        foreach (var (header, path, format) in new[] { ("Số điện thoại", "Phone", ""), ("Tên", "Name", ""), ("Hạng", "Tier", ""), ("Điểm", "Points", "N0"), ("Tổng chi", "TotalSpent", "N0"), ("Lần mua", "VisitCount", ""), ("Lần gần nhất", "LastVisit", "dd/MM/yyyy") })
            _grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(path) { StringFormat = format.Length > 0 ? "{0:" + format + "}" : null }, Width = path == "Name" ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto });
        foreach (var (header, path, format) in new[] { ("Đơn", "DailyOrderNumberFormatted", ""), ("Ngày", "CreatedAt", "dd/MM HH:mm"), ("Thực thu", "TotalAmount", "N0"), ("+Điểm", "PointsEarned", ""), ("−Điểm", "PointsRedeemed", "") })
            _history.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(path) { StringFormat = format.Length > 0 ? "{0:" + format + "}" : null } });

        var add = PosUi.Button("+ Thêm khách", (_, _) => { _grid.SelectedItem = null; ShowSelected(); _phone.Focus(); }, minHeight: 48);
        add.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(add, Dock.Right);
        var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(_filter, Dock.Right);
        top.Children.Add(add);
        top.Children.Add(_filter);
        top.Children.Add(_search);
        TouchInput.SetLabel(_search, "Tìm theo tên hoặc số điện thoại");
        MaterialDesignThemes.Wpf.HintAssist.SetHint(_search, "Tìm theo tên hoặc số điện thoại");

        MaterialDesignThemes.Wpf.HintAssist.SetHint(_phone, "Số điện thoại");
        MaterialDesignThemes.Wpf.HintAssist.SetHint(_name, "Tên khách");
        var save = PosUi.Button("Lưu", (_, _) => Save(), "PrimaryButton", 44);
        _delete = PosUi.Button("Xóa khách", (_, _) => Delete(), "DangerButton", 44);
        _delete.Margin = new Thickness(0, 8, 0, 0);
        var side = new Border
        {
            Width = 380, Margin = new Thickness(14, 0, 0, 0), Padding = new Thickness(14), Background = System.Windows.Media.Brushes.White, CornerRadius = new CornerRadius(8),
            Child = new StackPanel { Children = { PosUi.Text("Chi tiết khách", 17, true), _detail, new Border { Height = 4 }, _phone, _name, save, _delete, new Border { Height = 12 }, PosUi.Text("Lịch sử mua", 14, true), _history } }
        };
        DockPanel.SetDock(side, Dock.Right);
        var body = new DockPanel();
        body.Children.Add(side);
        body.Children.Add(_grid);

        var root = new DockPanel { Margin = new Thickness(18) };
        var titleBlock = new StackPanel { Children = { PosUi.Text("Khách hàng thành viên", 24, true), _summary } };
        DockPanel.SetDock(titleBlock, Dock.Top); DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(titleBlock); root.Children.Add(top); root.Children.Add(body);
        Content = root;

        _search.TextChanged += (_, _) => Refresh();
        _filter.SelectionChanged += (_, _) => Refresh();
        _grid.SelectionChanged += (_, _) => ShowSelected();
        Loaded += (_, _) => { Refresh(); _search.Focus(); };
    }

    private void Refresh()
    {
        string q = _search.Text.Trim();
        string digits = new(q.Where(char.IsDigit).ToArray());
        var list = _customers.Customers.Where(c => q.Length == 0 || c.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || digits.Length > 0 && c.Phone.Contains(digits));
        list = _filter.SelectedIndex switch
        {
            1 => list.Where(c => c.LastVisit is null || c.LastVisit < DateTime.Now.AddDays(-30)),
            > 1 => list.Where(c => c.Tier == (string)_filter.SelectedItem),
            _ => list
        };
        var selected = (_grid.SelectedItem as Customer)?.Phone;
        _grid.ItemsSource = list.OrderByDescending(c => c.TotalSpent).ToList();
        _grid.SelectedItem = _customers.FindCustomer(selected ?? "");
        _summary.Text = $"{_customers.Customers.Count} thành viên, {_customers.Customers.Sum(c => c.Points):N0} điểm chưa đổi, tổng chi {_customers.Customers.Sum(c => c.TotalSpent):N0}đ";
    }

    private void ShowSelected()
    {
        _delete.IsEnabled = _grid.SelectedItem is Customer;
        if (_grid.SelectedItem is not Customer c) { _detail.Text = "Khách mới: nhập số điện thoại và tên rồi bấm Lưu."; _phone.Clear(); _name.Clear(); _history.ItemsSource = null; return; }
        var orders = _customers.CustomerOrders(c.Phone);
        _detail.Text = $"Hạng {c.Tier}, {c.Points:N0} điểm, {c.VisitCount} lần mua, trung bình {(orders.Count == 0 ? 0 : orders.Average(o => o.TotalAmount)):N0}đ/đơn\nThành viên từ {c.CreatedAt:dd/MM/yyyy}";
        _phone.Text = c.Phone;
        _name.Text = c.Name;
        _history.ItemsSource = orders;
    }

    private void Save()
    {
        try
        {
            if (_grid.SelectedItem is Customer c) { _customers.UpdateCustomer(c.Phone, _name.Text, _phone.Text); Refresh(); Toast.Show(this, "Đã lưu thông tin khách."); return; }
            var created = _customers.RegisterCustomer(_phone.Text, _name.Text);
            _search.Clear(); Refresh(); _grid.SelectedItem = created; _grid.ScrollIntoView(created);
            Toast.Show(this, $"Đã thêm {created.Name}.");
        }
        catch (InvalidOperationException ex) { FieldError.Mark(ex.Message.Contains("điện thoại") ? _phone : _name, ex.Message); }
    }

    private void Delete()
    {
        if (_grid.SelectedItem is not Customer c) return;
        if (MessageBox.Show(this, $"Xóa thành viên {c.Name} ({c.Phone})? {c.Points:N0} điểm chưa đổi sẽ mất. Các đơn đã bán vẫn giữ trong báo cáo.", "Xóa khách", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { _customers.DeleteCustomer(c.Phone); _grid.SelectedItem = null; Refresh(); ShowSelected(); Toast.Show(this, $"Đã xóa {c.Name}."); }
        catch (InvalidOperationException ex) { Toast.Show(this, ex.Message, true); }
    }
}
