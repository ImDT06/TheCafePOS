using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;

// Two columns so the whole configuration fits on one screen: kind, sugar/ice and sizes on the left, toppings on the right.
public sealed class ProductConfigurationWindow : Window
{
    private readonly ICatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly IAuthService _auth;
    public ProductConfigurationWindow(Product original, ICatalogService catalog, IInventoryService inventory, IAuthService auth)
    {
        _catalog = catalog;
        _inventory = inventory;
        _auth = auth;
        _auth.RequireManager();
        var product = JsonSerializer.Deserialize<Product>(JsonSerializer.Serialize(original))!;
        Title = "Cấu hình: " + product.Name; Width = 1000; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var form = new Grid();
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
        var right = new DockPanel { Margin = new Thickness(16, 0, 0, 0) };
        Grid.SetColumn(right, 1);
        form.Children.Add(left); form.Children.Add(right);
        TextBlock Section(string text, Thickness? margin = null) => new() { Text = text, FontWeight = FontWeights.Bold, FontSize = 15, Margin = margin ?? new Thickness(0, 0, 0, 6) };

        // Kind: one choice instead of two checkboxes that could both be ticked.
        left.Children.Add(Section("Loại món"));
        var kinds = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 4) };
        RadioButton Kind(string label, bool value) { var r = new RadioButton { Content = label, GroupName = "kind", IsChecked = value, Margin = new Thickness(0, 4, 8, 4) }; kinds.Children.Add(r); return r; }
        var drink = Kind("Đồ uống", !product.IsTopping && !product.IsRetailItem);
        var isTopping = Kind("Topping", product.IsTopping);
        var retail = Kind("Bánh / đồ ăn", !product.IsTopping && product.IsRetailItem);
        left.Children.Add(kinds);
        left.Children.Add(new TextBlock { Text = "Topping không trừ ly/nắp. Bánh/đồ ăn không trừ ly và không in tem ly.", Style = (Style)FindResource("Hint"), Margin = new Thickness(0, 0, 0, 4) });
        var soldSeparately = new CheckBox { Content = "Cho phép bán topping thành món riêng", IsChecked = product.SoldSeparately, Margin = new Thickness(0, 4, 0, 0) };
        left.Children.Add(soldSeparately);

        // Sugar and ice: the toggle and its default on one row each.
        left.Children.Add(Section("Đường & đá", new Thickness(0, 18, 0, 6)));
        var levels = new Grid();
        levels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        levels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        left.Children.Add(levels);
        (CheckBox Allow, ComboBox Level, ComboBox Named) LevelRow(string label, bool allow, int level, List<string>? choices, string named)
        {
            int row = levels.RowDefinitions.Count;
            levels.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var box = new CheckBox { Content = label, IsChecked = allow, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
            // Products with named choices (e.g. "Ít ngọt") pick a default by name; older ones by percentage.
            var levelBox = new ComboBox { ItemsSource = OrderConfigurationService.Levels, SelectedItem = level, ItemStringFormat = "{0}%", Margin = new Thickness(0, 4, 0, 4), Visibility = choices is null ? Visibility.Visible : Visibility.Collapsed };
            var namedBox = new ComboBox { ItemsSource = choices, SelectedItem = OrderConfigurationService.ResolveChoice(choices, named, named) ?? "", Margin = new Thickness(0, 4, 0, 4), Visibility = choices is null ? Visibility.Collapsed : Visibility.Visible };
            Grid.SetRow(box, row); Grid.SetRow(levelBox, row); Grid.SetRow(namedBox, row); Grid.SetColumn(levelBox, 1); Grid.SetColumn(namedBox, 1);
            levels.Children.Add(box); levels.Children.Add(levelBox); levels.Children.Add(namedBox);
            return (box, levelBox, namedBox);
        }
        var (sugar, defaultSugar, namedSugar) = LevelRow("Cho khách chọn mức đường", product.AllowSugar, product.DefaultSugar, product.SugarChoices, product.DefaultSugarChoice);
        var (ice, defaultIce, namedIce) = LevelRow("Cho khách chọn mức đá", product.AllowIce, product.DefaultIce, product.IceChoices, product.DefaultIceChoice);
        left.Children.Add(new TextBlock { Text = "Ô bên phải là mức mặc định khi bấm thêm nhanh.", Style = (Style)FindResource("Hint"), Margin = new Thickness(0, 2, 0, 0) });

        // Sizes as a compact table.
        left.Children.Add(Section("Size", new Thickness(0, 18, 0, 6)));
        var sizes = new Grid();
        sizes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        sizes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        sizes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        sizes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var (text, col) in new[] { ("Bán", 0), ("Phụ thu (đ)", 1), ("Bao bì", 2) })
        {
            var head = new TextBlock { Text = text, Foreground = (System.Windows.Media.Brush)FindResource("PosMuted"), FontSize = 12, Margin = new Thickness(0, 0, 0, 2) };
            Grid.SetColumn(head, col); sizes.Children.Add(head);
        }
        var rows = new List<(string Name, CheckBox Enabled, TextBox Extra, ComboBox Packaging)>();
        foreach (var name in new[] { "S", "M", "L", "XL" })
        {
            var size = product.Sizes.FirstOrDefault(s => s.Name == name);
            int row = sizes.RowDefinitions.Count;
            sizes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var enabled = new CheckBox { Content = name, IsChecked = size != null, VerticalAlignment = VerticalAlignment.Center };
            var extra = new TextBox { Text = (size?.ExtraPrice ?? 0).ToString("0"), Margin = new Thickness(0, 4, 8, 4) };
            Core.TouchInput.SetNumeric(extra, true);
            Core.TouchInput.SetLabel(extra, "Phụ thu size " + name);
            var packaging = new ComboBox { ItemsSource = _inventory.PackagingInventory, DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValue = size?.PackagingId, Margin = new Thickness(0, 4, 0, 4) };
            // A size that is not sold keeps its values but greys them out.
            extra.SetBinding(IsEnabledProperty, new System.Windows.Data.Binding(nameof(CheckBox.IsChecked)) { Source = enabled });
            packaging.SetBinding(IsEnabledProperty, new System.Windows.Data.Binding(nameof(CheckBox.IsChecked)) { Source = enabled });
            Grid.SetRow(enabled, row); Grid.SetRow(extra, row); Grid.SetRow(packaging, row); Grid.SetColumn(extra, 1); Grid.SetColumn(packaging, 2);
            sizes.Children.Add(enabled); sizes.Children.Add(extra); sizes.Children.Add(packaging);
            rows.Add((name, enabled, extra, packaging));
        }
        left.Children.Add(sizes);

        // Toppings: a two-column grid with select all / none.
        var toppingHead = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        DockPanel.SetDock(toppingHead, Dock.Top); right.Children.Add(toppingHead);
        var none = new Button { Content = "Bỏ chọn", Padding = new Thickness(10, 4, 10, 4), MinHeight = 32, Margin = new Thickness(6, 0, 0, 0) };
        var all = new Button { Content = "Chọn tất cả", Padding = new Thickness(10, 4, 10, 4), MinHeight = 32 };
        DockPanel.SetDock(none, Dock.Right); DockPanel.SetDock(all, Dock.Right);
        toppingHead.Children.Add(none); toppingHead.Children.Add(all);
        var toppingTitle = Section("Topping được thêm vào món", new Thickness(0)); toppingTitle.VerticalAlignment = VerticalAlignment.Center;
        toppingHead.Children.Add(toppingTitle);
        var toppingGrid = new UniformGrid { Columns = 2 };
        var toppings = new List<(string Id, CheckBox Box)>();
        foreach (var topping in _catalog.Products.Where(p => p.IsTopping && p.Id != product.Id).OrderBy(p => p.Name))
        {
            var box = new CheckBox { IsChecked = product.AllowedToppingIds.Contains(topping.Id), Margin = new Thickness(0, 3, 8, 3), Content = new TextBlock { Text = $"{topping.Name} · {topping.BasePrice:N0}đ", TextTrimming = TextTrimming.CharacterEllipsis }, ToolTip = topping.Name };
            toppingGrid.Children.Add(box); toppings.Add((topping.Id, box));
        }
        all.Click += (_, _) => toppings.ForEach(t => t.Box.IsChecked = true);
        none.Click += (_, _) => toppings.ForEach(t => t.Box.IsChecked = false);
        var toppingCount = new TextBlock { Style = (Style)FindResource("Hint"), Margin = new Thickness(0, 6, 0, 0) };
        void CountToppings() => toppingCount.Text = $"Đã chọn {toppings.Count(t => t.Box.IsChecked == true)}/{toppings.Count} topping";
        toppings.ForEach(t => { t.Box.Checked += (_, _) => CountToppings(); t.Box.Unchecked += (_, _) => CountToppings(); }); CountToppings();
        DockPanel.SetDock(toppingCount, Dock.Bottom); right.Children.Add(toppingCount);
        right.Children.Add(new Border
        {
            BorderBrush = (System.Windows.Media.Brush)FindResource("PosBorder"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 4, 6),
            Child = new ScrollViewer { Content = toppingGrid, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        });

        void Toggle()
        {
            bool beverage = drink.IsChecked == true;
            sizes.IsEnabled = right.IsEnabled = beverage;
            sugar.IsEnabled = beverage && (product.SugarChoices is null || product.SugarChoices.Count > 0);
            ice.IsEnabled = beverage && (product.IceChoices is null || product.IceChoices.Count > 0);
            defaultSugar.IsEnabled = defaultIce.IsEnabled = beverage;
            namedSugar.IsEnabled = beverage && product.SugarChoices?.Count > 0;
            namedIce.IsEnabled = beverage && product.IceChoices?.Count > 0;
            soldSeparately.IsEnabled = isTopping.IsChecked == true;
        }
        foreach (var kind in new[] { drink, isTopping, retail }) kind.Checked += (_, _) => Toggle();
        Toggle();

        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("PosDanger"), VerticalAlignment = VerticalAlignment.Center };
        var save = new Button { Content = "Lưu cấu hình", Style = (Style)FindResource("PrimaryButton"), MinWidth = 160 };
        var cancel = new Button { Content = "Hủy thay đổi", IsCancel = true, MinWidth = 140, Margin = new Thickness(0, 0, 12, 0) };
        var actions = new DockPanel();
        DockPanel.SetDock(save, Dock.Right); DockPanel.SetDock(cancel, Dock.Right);
        actions.Children.Add(save); actions.Children.Add(cancel); actions.Children.Add(status);
        Core.DialogLayout.Apply(this, "Cấu hình món", product.Name, form, actions);
        save.Click += (_, _) =>
        {
            try
            {
                var sold = new List<ProductSize>();
                if (drink.IsChecked == true)
                    foreach (var row in rows.Where(r => r.Enabled.IsChecked == true))
                    {
                        if (!decimal.TryParse(row.Extra.Text, out var extra)) throw new InvalidOperationException("Phụ thu size không hợp lệ.");
                        sold.Add(new ProductSize { Name = row.Name, ExtraPrice = extra, PackagingId = row.Packaging.SelectedValue as string ?? "" });
                    }
                product.IsRetailItem = retail.IsChecked == true; product.IsTopping = isTopping.IsChecked == true; product.SoldSeparately = !product.IsTopping || soldSeparately.IsChecked == true;
                product.AllowSugar = product.IsBeverage && sugar.IsChecked == true; product.AllowIce = product.IsBeverage && ice.IsChecked == true;
                product.DefaultSugar = (int)defaultSugar.SelectedItem; product.DefaultIce = (int)defaultIce.SelectedItem;
                product.DefaultSugarChoice = namedSugar.SelectedItem as string ?? "";
                product.DefaultIceChoice = namedIce.SelectedItem as string ?? "";
                product.Sizes = sold; product.AllowedToppingIds = !product.IsBeverage ? new() : toppings.Where(t => t.Box.IsChecked == true).Select(t => t.Id).ToList();
                _catalog.SaveProduct(product); DialogResult = true;
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
    }
}
