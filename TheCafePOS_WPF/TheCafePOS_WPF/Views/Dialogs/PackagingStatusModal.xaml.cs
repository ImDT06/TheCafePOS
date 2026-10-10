using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

namespace TheCafePOS_WPF.Views.Dialogs;

public partial class PackagingStatusModal : Window
{
    private readonly DataStoreService _store = DataStoreService.Instance;
    public PackagingStatusModal()
    {
        InitializeComponent();
        Editor.Visibility = AuthService.Instance.IsManager ? Visibility.Visible : Visibility.Collapsed;
        ManageTab.Visibility = Editor.Visibility;
        if (!AuthService.Instance.IsManager) PermissionNote.Text = "Thu ngân được xem kho và lịch sử. Quản lý mới được nhập kho, kiểm kê hoặc điều chỉnh.";
        Refresh();
    }
    private void Refresh(string? selectedId = null)
    {
        PackList.ItemsSource = _store.PackagingInventory.ToList();
        DgPackaging.ItemsSource = _store.PackagingInventory.ToList();
        DgPackaging.SelectedItem = _store.PackagingInventory.FirstOrDefault(p => p.Id == selectedId) ?? _store.PackagingInventory.FirstOrDefault();
        Summary.Text = $"{_store.PackagingInventory.Count} loại bao bì • {_store.PackagingInventory.Count(p => p.IsLowStock)} loại chạm ngưỡng cảnh báo (tô vàng)";
        RefreshHistory(); UpdatePreview();
    }
    private void RefreshHistory()
    {
        if (History == null || OnlySelected == null) return;
        var id = (DgPackaging.SelectedItem as PackagingItem)?.Id;
        History.ItemsSource = _store.StockMovements.Where(m => OnlySelected.IsChecked != true || m.PackagingId == id).OrderByDescending(m => m.CreatedAt).ToList();
    }
    private void UpdatePreview()
    {
        if (Preview == null || Quantity == null || Operation == null || DgPackaging == null) return;
        Preview.Text = "";
        Core.TouchInput.SetAllowNegative(Quantity, (Operation.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Điều chỉnh");
        Core.TouchInput.SetUnit(Quantity, (DgPackaging.SelectedItem as PackagingItem)?.Unit ?? "đơn vị");
        if (DgPackaging.SelectedItem is not PackagingItem item || !int.TryParse(Quantity.Text, out var quantity)) return;
        long after = ((ComboBoxItem)Operation.SelectedItem).Content.ToString() == "Kiểm kê" ? quantity : (long)item.StockQuantity + quantity;
        Preview.Text = $"Tồn: {item.StockQuantity} → {after} ({after - item.StockQuantity:+0;-0;0})";
    }
    private void PackagingSelected(object sender, SelectionChangedEventArgs e) { RefreshHistory(); UpdatePreview(); }
    private void PreviewChanged(object sender, RoutedEventArgs e) => UpdatePreview();
    private void HistoryFilterChanged(object sender, RoutedEventArgs e) => RefreshHistory();
    private void SaveStock_Click(object sender, RoutedEventArgs e)
    {
        var id = (DgPackaging.SelectedItem as PackagingItem)?.Id;
        try
        {
            if (id is null) throw new InvalidOperationException("Vui lòng chọn bao bì.");
            if (!int.TryParse(Quantity.Text, out int quantity)) throw new InvalidOperationException("Số lượng phải là số nguyên hợp lệ.");
            _store.UpdateStock(id, ((ComboBoxItem)Operation.SelectedItem).Content.ToString()!, quantity, Reason.Text);
            Quantity.Clear(); Reason.Clear(); Status.Text = "Đã lưu tồn kho và lịch sử thay đổi.";
        }
        catch (Exception ex) { Status.Text = ex.Message; }
        Refresh(id);
    }
    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    private void PackList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PackList.SelectedItem is not PackagingItem p) return;
        PackName.Text = p.Name;
        PackUnit.Text = p.Unit;
        PackWarning.Text = p.WarningThreshold.ToString();
        SavePack.Content = "Cập nhật bao bì";
    }
    private void NewPack_Click(object sender, RoutedEventArgs e)
    {
        PackList.SelectedItem = null;
        PackName.Clear();
        PackUnit.Clear();
        PackWarning.Clear();
        SavePack.Content = "Thêm bao bì";
    }
    private void SavePack_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(PackName.Text)) throw FieldError.Mark(PackName, "Nhập tên bao bì.");
            if (string.IsNullOrWhiteSpace(PackUnit.Text)) throw FieldError.Mark(PackUnit, "Nhập đơn vị tính.");
            if (!int.TryParse(PackWarning.Text, out var warning) || warning < 0) throw FieldError.Mark(PackWarning, "Ngưỡng cảnh báo phải là số nguyên không âm.");
            var id = (PackList.SelectedItem as PackagingItem)?.Id ?? Guid.NewGuid().ToString();
            _store.SavePackagingItem(new PackagingItem { Id = id, Name = PackName.Text, Unit = PackUnit.Text, WarningThreshold = warning });
            Refresh(id);
            NewPack_Click(sender, e);
            Status.Text = ""; Toast.Show(this, "Đã lưu bao bì. Dùng tab Tồn kho để nhập số lượng.");
        }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void DeletePack_Click(object sender, RoutedEventArgs e)
    {
        if (PackList.SelectedItem is not PackagingItem p) { Status.Text = "Chọn bao bì trước."; return; }
        if (MessageBox.Show(this, $"Xóa bao bì {p.Name}?", "Xóa bao bì", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
        {
            try
            {
                _store.DeletePackagingItem(p.Id);
                Status.Text = ""; Toast.Show(this, "Đã xóa bao bì.");
                Refresh();
                NewPack_Click(sender, e);
            }
            catch (Exception ex) { Status.Text = ex.Message; }
        }
    }
}
