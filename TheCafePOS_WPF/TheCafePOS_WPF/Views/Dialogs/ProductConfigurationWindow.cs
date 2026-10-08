using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
namespace TheCafePOS_WPF.Views.Dialogs;

public sealed class ProductConfigurationWindow : Window
{
    public ProductConfigurationWindow(Product original)
    {
        AuthService.Instance.RequireManager();
        var product = JsonSerializer.Deserialize<Product>(JsonSerializer.Serialize(original))!;
        Title = "Cấu hình: " + product.Name; Width = 650; Height = 730; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(20) };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        CheckBox Flag(string label, bool value) { var box = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 8, 0, 8) }; root.Children.Add(box); return box; }
        var isTopping = Flag("Đây là topping (không trừ ly/nắp/ống hút)", product.IsTopping);
        var retail = Flag("Bánh / đồ ăn / hàng lưu niệm (không trừ ly, không in tem ly)", product.IsRetailItem);
        var soldSeparately = Flag("Cho phép bán topping thành món riêng", product.SoldSeparately);
        var sugar = Flag("Cho khách chọn mức đường", product.AllowSugar);
        var ice = Flag("Cho khách chọn mức đá", product.AllowIce);
        ComboBox Level(string label, int value)
        {
            root.Children.Add(new TextBlock { Text = label });
            var box = new ComboBox { ItemsSource = OrderConfigurationService.Levels, SelectedItem = value, Margin = new Thickness(0, 4, 0, 10) }; root.Children.Add(box); return box;
        }
        var defaultSugar = Level("Đường mặc định (%)", product.DefaultSugar);
        var defaultIce = Level("Đá mặc định (%)", product.DefaultIce);
        ComboBox NamedDefault(string label, List<string>? choices, string value, ComboBox legacy)
        {
            var box = new ComboBox { ItemsSource = choices, SelectedItem = value, Margin = new Thickness(0, 4, 0, 10) };
            if (choices != null)
            {
                var labelIndex = root.Children.IndexOf(legacy) - 1;
                root.Children[labelIndex].Visibility = legacy.Visibility = Visibility.Collapsed;
                root.Children.Add(new TextBlock { Text = label }); root.Children.Add(box);
                box.IsEnabled = choices.Count > 0;
            }
            return box;
        }
        var namedSugar = NamedDefault("Độ ngọt mặc định", product.SugarChoices, product.DefaultSugarChoice, defaultSugar);
        var namedIce = NamedDefault("Lượng đá mặc định", product.IceChoices, product.DefaultIceChoice, defaultIce);
        var drinks = new StackPanel(); root.Children.Add(drinks);
        drinks.Children.Add(new TextBlock { Text = "Size được bán — phụ thu so với giá cơ bản — bao bì", FontWeight = FontWeights.Bold });
        var rows = new List<(string Name, CheckBox Enabled, TextBox Extra, ComboBox Packaging)>();
        foreach (var name in new[] { "S", "M", "L", "XL" })
        {
            var size = product.Sizes.FirstOrDefault(s => s.Name == name);
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            var enabled = new CheckBox { Content = name, IsChecked = size != null, Width = 65, VerticalAlignment = VerticalAlignment.Center };
            var extra = new TextBox { Text = (size?.ExtraPrice ?? 0).ToString("0"), Width = 140, Padding = new Thickness(5) };
            TheCafePOS_WPF.Core.TouchInput.SetNumeric(extra, true);
            Core.TouchInput.SetLabel(extra, "Phụ thu size " + name);
            var packaging = new ComboBox { ItemsSource = DataStoreService.Instance.PackagingInventory, DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValue = size?.PackagingId, Width = 260, Margin = new Thickness(10, 0, 0, 0) };
            panel.Children.Add(enabled); panel.Children.Add(extra); panel.Children.Add(packaging); drinks.Children.Add(panel); rows.Add((name, enabled, extra, packaging));
        }
        drinks.Children.Add(new TextBlock { Text = "Topping được thêm vào món này", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 12, 0, 8) });
        var toppings = new List<(string Id, CheckBox Box)>();
        foreach (var topping in DataStoreService.Instance.Products.Where(p => p.IsTopping && p.Id != product.Id))
        {
            var box = new CheckBox { Content = $"{topping.Name} ({topping.BasePrice:N0}đ)", IsChecked = product.AllowedToppingIds.Contains(topping.Id), Margin = new Thickness(0, 5, 0, 5) };
            drinks.Children.Add(box); toppings.Add((topping.Id, box));
        }
        void Toggle()
        {
            bool beverage = isTopping.IsChecked != true && retail.IsChecked != true;
            drinks.IsEnabled = sugar.IsEnabled = ice.IsEnabled = defaultSugar.IsEnabled = defaultIce.IsEnabled = beverage;
            sugar.IsEnabled = beverage && (product.SugarChoices is null || product.SugarChoices.Count > 0);
            ice.IsEnabled = beverage && (product.IceChoices is null || product.IceChoices.Count > 0);
            namedSugar.IsEnabled = beverage && product.SugarChoices?.Count > 0;
            namedIce.IsEnabled = beverage && product.IceChoices?.Count > 0;
            soldSeparately.IsEnabled = !beverage;
        }
        retail.Checked += (_, _) => Toggle(); retail.Unchecked += (_, _) => Toggle();
        isTopping.Checked += (_, _) => Toggle(); isTopping.Unchecked += (_, _) => Toggle(); Toggle();
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) }; root.Children.Add(status);
        var save = new Button { Content = "Lưu cấu hình", Style = (Style)FindResource("PrimaryButton") };
        var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        actions.Children.Add(new Button { Content = "Hủy thay đổi", IsCancel = true, Margin = new Thickness(0, 0, 12, 0) }); actions.Children.Add(save);
        // Detach before transferring the form into the shared dialog layout.
        ((ScrollViewer)Content).Content = null;
        Core.DialogLayout.Apply(this, "Cấu hình món", product.Name + " · Size, tùy chọn và bao bì", root, actions);
        save.Click += (_, _) =>
        {
            try
            {
                var sizes = new List<ProductSize>();
                if (isTopping.IsChecked != true && retail.IsChecked != true)
                    foreach (var row in rows.Where(r => r.Enabled.IsChecked == true))
                    {
                        if (!decimal.TryParse(row.Extra.Text, out var extra)) throw new InvalidOperationException("Phụ thu size không hợp lệ.");
                        sizes.Add(new ProductSize { Name = row.Name, ExtraPrice = extra, PackagingId = row.Packaging.SelectedValue as string ?? "" });
                    }
                product.IsRetailItem = retail.IsChecked == true; product.IsTopping = isTopping.IsChecked == true; product.SoldSeparately = !product.IsTopping || soldSeparately.IsChecked == true;
                product.AllowSugar = product.IsBeverage && sugar.IsChecked == true; product.AllowIce = product.IsBeverage && ice.IsChecked == true;
                product.DefaultSugar = (int)defaultSugar.SelectedItem; product.DefaultIce = (int)defaultIce.SelectedItem;
                product.DefaultSugarChoice = namedSugar.SelectedItem as string ?? "";
                product.DefaultIceChoice = namedIce.SelectedItem as string ?? "";
                product.Sizes = sizes; product.AllowedToppingIds = !product.IsBeverage ? new() : toppings.Where(t => t.Box.IsChecked == true).Select(t => t.Id).ToList();
                DataStoreService.Instance.SaveProduct(product); DialogResult = true;
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
    }
}

