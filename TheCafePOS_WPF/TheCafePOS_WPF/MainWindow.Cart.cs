using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.ViewModels;
using TheCafePOS_WPF.Views.Dialogs;
namespace TheCafePOS_WPF;
public partial class MainWindow
{
    private void EditCartItem_Click(object sender, RoutedEventArgs e) => EditCartItem(sender, false);
    // Rare actions live behind "⋯" so each cart row stays one line.
    private void CartItemMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: OrderItem item } button) return;
        // Styled popup instead of the system context menu, matching the app's cards and touch sizes.
        var popup = new System.Windows.Controls.Primitives.Popup { PlacementTarget = button, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, HorizontalOffset = -170 };
        Button Item(MaterialDesignThemes.Wpf.PackIconKind icon, string text, string hint, bool enabled, Action run)
        {
            var b = new Button { HorizontalContentAlignment = HorizontalAlignment.Left, Background = System.Windows.Media.Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(12, 8, 12, 8), MinHeight = 44, IsEnabled = enabled,
                Content = new DockPanel { Children =
                {
                    new MaterialDesignThemes.Wpf.PackIcon { Kind = icon, Width = 20, Height = 20, Margin = new Thickness(0, 2, 10, 0), VerticalAlignment = VerticalAlignment.Top },
                    new StackPanel { Children = { new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 14 }, new TextBlock { Text = hint, FontSize = 12, Foreground = System.Windows.Media.Brushes.Gray } } }
                } } };
            b.Click += (_, _) => { popup.IsOpen = false; run(); };
            return b;
        }
        popup.Child = new Border
        {
            Background = System.Windows.Media.Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(4), Margin = new Thickness(8), Width = 230,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDF, 0xE7, 0xE2)), BorderThickness = new Thickness(1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.18 },
            Child = new StackPanel { Children =
            {
                Item(MaterialDesignThemes.Wpf.PackIconKind.PencilOutline, "Sửa tùy chọn", "Size, đường, đá, topping, ghi chú", true, () => EditCartItem(new Button { Tag = item }, false)),
                Item(MaterialDesignThemes.Wpf.PackIconKind.CallSplit, "Tách 1 sản phẩm", item.Quantity > 1 ? "Để chỉnh riêng 1 ly trong dòng này" : "Cần số lượng từ 2 trở lên", item.Quantity > 1, () => EditCartItem(new Button { Tag = item }, true))
            } }
        };
        popup.IsOpen = true;
    }
    private void SplitCartItem_Click(object sender, RoutedEventArgs e) => EditCartItem(sender, true);
    private void EditCartItem(object sender, bool split)
    {
        if (sender is not Button { Tag: OrderItem item }) return;
        if (split && item.Quantity <= 1) { MessageBox.Show(this, "Dòng cần ít nhất 2 sản phẩm để tách."); return; }
        var product = _vm.FindActiveProduct(item.ProductId);
        if (product is null) { MessageBox.Show(this, "Món đã ngừng bán. Hãy xóa và chọn món khác."); return; }
        var draft = OrderConfigurationService.Clone(item);
        if (split) draft.Quantity = 1;
        var modal = _dialogs.Create<OptionCustomModal>(this, product, _vm.CurrentDailyOrderNumber, draft, split);
        if (modal.ShowDialog() != true) return;
        if (split) _vm.SplitItem(item, modal.ResultItem);
        else _vm.ReplaceItem(item, modal.ResultItem);
    }
}
