using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Views.Dialogs;
namespace TheCafePOS_WPF;
public partial class MainWindow
{
    private void EditCartItem_Click(object sender, RoutedEventArgs e) => EditCartItem(sender, false);
    private void SplitCartItem_Click(object sender, RoutedEventArgs e) => EditCartItem(sender, true);
    private void EditCartItem(object sender, bool split)
    {
        if (sender is not Button { Tag: OrderItem item }) return;
        if (split && item.Quantity <= 1) { MessageBox.Show(this, "Dòng cần ít nhất 2 sản phẩm để tách."); return; }
        var product = DataStoreService.Instance.Products.FirstOrDefault(p => p.Id == item.ProductId && p.IsActive);
        if (product is null) { MessageBox.Show(this, "Món đã ngừng bán. Hãy xóa và chọn món khác."); return; }
        var draft = OrderConfigurationService.Clone(item);
        if (split) draft.Quantity = 1;
        var modal = new OptionCustomModal(product, _currentDailyOrderNumber, draft, split) { Owner = this };
        if (modal.ShowDialog() != true) return;
        int index = _cartItems.IndexOf(item);
        if (split)
        {
            item.Quantity--;
            _cartItems.Insert(index + 1, modal.ResultItem);
        }
        else _cartItems[index] = modal.ResultItem;
        LbCartItems.Items.Refresh(); UpdateCartTotal();
    }
}
