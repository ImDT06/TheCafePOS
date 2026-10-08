using System.Windows;
using System.Windows.Controls;
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
        if (!AuthService.Instance.IsManager) PermissionNote.Text = "Thu ngân được xem kho và lịch sử. Quản lý mới được nhập kho, kiểm kê hoặc điều chỉnh.";
        Refresh();
    }
    private void Refresh(string? selectedId = null)
    {
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
}
