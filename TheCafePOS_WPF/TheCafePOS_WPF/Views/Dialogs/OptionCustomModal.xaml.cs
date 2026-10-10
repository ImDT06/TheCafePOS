using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class OptionCustomModal : Window
{
    private sealed class ToppingChoice
    {
        public Product Product { get; init; } = null!;
        public int Count { get; set; }
        public TextBlock Label { get; init; } = null!;
        public Button Minus { get; init; } = null!;
        public Button Plus { get; init; } = null!;
        public Border Surface { get; init; } = null!;
    }
    private readonly Product _product;
    private readonly bool _singleUnit;
    private readonly bool _editing;
    private readonly List<ToppingChoice> _toppings = new();
    private int _quantity, _sugar, _ice;
    private int _toppingPage, _toppingsPerPage = 6;
    private string _size = "";
    private string? _sugarChoice, _iceChoice;
    private bool _ready;
    public OrderItem ResultItem { get; private set; } = null!;
    public OptionCustomModal(Product product, int dailyOrderNumber = 1, OrderItem? existing = null, bool singleUnit = false)
    {
        _product = product; _singleUnit = singleUnit; _editing = existing != null;
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
        MinHeight = Math.Min(MinHeight, MaxHeight);
        MinWidth = Math.Min(MinWidth, MaxWidth);
        Height = Math.Min(Height, MaxHeight); Width = Math.Min(Width, MaxWidth);
        Heading.Text = product.Name;
        ProductPhoto.Source = MainWindow.LoadProductImage(product.ImageUrl);
        PhotoFrame.Visibility = ProductPhoto.Source is null ? Visibility.Collapsed : Visibility.Visible;
        ContextLabel.Text = singleUnit ? "TÁCH 1 SẢN PHẨM ĐỂ CHỈNH RIÊNG" : _editing ? "CHỈNH SỬA MÓN TRONG GIỎ" : $"THÊM MÓN · ĐƠN #{dailyOrderNumber:D2}";
        PriceNotice.Text = _editing ? "Giá khi lưu được tính theo thực đơn hiện tại." : $"Giá từ {product.BasePrice + (!product.IsBeverage ? 0 : product.Sizes.Select(s => s.ExtraPrice).DefaultIfEmpty(0).Min()):N0}đ";
        _quantity = singleUnit ? 1 : Math.Clamp(existing?.Quantity ?? 1, 1, 999);
        _size = product.Sizes.FirstOrDefault(s => s.Name == existing?.Size)?.Name ?? product.Sizes.FirstOrDefault(s => s.Name == product.DefaultSize)?.Name ?? product.Sizes.FirstOrDefault()?.Name ?? "";
        _sugar = OrderConfigurationService.Levels.Contains(existing?.SugarPercent ?? product.DefaultSugar) ? existing?.SugarPercent ?? product.DefaultSugar : product.DefaultSugar;
        _ice = OrderConfigurationService.Levels.Contains(existing?.IcePercent ?? product.DefaultIce) ? existing?.IcePercent ?? product.DefaultIce : product.DefaultIce;
        _sugarChoice = product.SugarChoices is { Count: > 0 }
            ? OrderConfigurationService.ResolveChoice(product.SugarChoices, existing?.SugarChoice, product.DefaultSugarChoice)
            : null;
        _iceChoice = product.IceChoices is { Count: > 0 }
            ? OrderConfigurationService.ResolveChoice(product.IceChoices, existing?.IceChoice, product.DefaultIceChoice)
            : null;
        Note.Text = existing?.Note ?? "";
        DrinkOptions.Visibility = !product.IsBeverage ? Visibility.Collapsed : Visibility.Visible;
        SugarPanel.Visibility = product.AllowSugar ? Visibility.Visible : Visibility.Collapsed;
        IcePanel.Visibility = product.AllowIce ? Visibility.Visible : Visibility.Collapsed;
        QuantityLabel.Text = !product.IsBeverage ? "Số phần" : "Số ly";
        // A lone size is information, not a choice; keep it compact instead of a full-width button.
        if (product.Sizes.Count == 1) { SizeChoices.HorizontalAlignment = HorizontalAlignment.Left; SizeChoices.MinWidth = 160; }
        foreach (var size in product.Sizes)
        {
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = size.Name, FontSize = 17, TextAlignment = TextAlignment.Center });
            label.Children.Add(new TextBlock { Text = $"{product.BasePrice + size.ExtraPrice:N0}đ", FontSize = 12, Margin = new Thickness(0, 4, 0, 0), TextAlignment = TextAlignment.Center });
            AddChoice(SizeChoices, "size", label, size.Name == _size, () => _size = size.Name);
        }
        foreach (int level in OrderConfigurationService.Levels)
        {
            string label = level == 0 ? "Không" : $"{level}%";
            if (product.SugarChoices is null) AddChoice(SugarChoices, "sugar", label, level == _sugar, () => _sugar = level);
            if (product.IceChoices is null) AddChoice(IceChoices, "ice", label, level == _ice, () => _ice = level);
        }
        foreach (var choice in product.SugarChoices ?? new())
            AddChoice(SugarChoices, "sugar", choice, choice == _sugarChoice, () => _sugarChoice = choice);
        foreach (var choice in product.IceChoices ?? new())
            AddChoice(IceChoices, "ice", choice, choice == _iceChoice, () => _iceChoice = choice);
        foreach (var topping in DataStoreService.Instance.Products.Where(p => product.IsBeverage && p.IsTopping && p.IsActive && product.AllowedToppingIds.Contains(p.Id) && DataStoreService.Instance.Categories.Any(c => c.Id == p.CategoryId && c.IsActive)))
            AddTopping(topping, existing?.SelectedToppings.FirstOrDefault(t => t.ProductId == topping.Id)?.Quantity ?? 0);
        if (_toppings.Count == 0) { ToppingSection.Visibility = Visibility.Collapsed; ToppingColumn.Width = new GridLength(0); }
        bool missingTopping = existing?.SelectedToppings.Any(t => !_toppings.Any(c => c.Product.Id == t.ProductId)) == true;
        bool missingSize = existing is { IsBeverage: true } && existing.Size != _size;
        bool changedOptions = existing != null &&
            ((product.AllowSugar && product.SugarChoices != null && existing.SugarChoice != _sugarChoice) ||
             (product.AllowIce && product.IceChoices != null && existing.IceChoice != _iceChoice));
        if (missingTopping || missingSize || changedOptions)
        {
            ChangeNotice.Text = "Tùy chọn món đã thay đổi. Vui lòng kiểm tra size, đường, đá và topping trước khi lưu.";
            ChangeNotice.Visibility = Visibility.Visible;
        }
        _ready = true; UpdateTotal(); ShowToppingPage();
        Loaded += (_, _) => ResizeToppingPage();
    }
    private void Note_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Notes wrap within the wide static field; pasted line breaks must not push text out of view.
        var box = (TextBox)sender;
        var normalized = box.Text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        if (normalized == box.Text) return;
        int caret = box.CaretIndex;
        box.Text = normalized;
        box.CaretIndex = Math.Min(caret, normalized.Length);
    }
    private void AddChoice(Panel panel, string group, object content, bool selected, Action select)
    {
        if (content is string text) content = new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
        var button = new RadioButton { Content = content, GroupName = group, IsChecked = selected, Style = (Style)FindResource("Choice") };
        button.Checked += (_, _) => { select(); if (_ready) UpdateTotal(); };
        panel.Children.Add(button);
    }
    private void AddTopping(Product product, int count)
    {
        var surface = new Border { VerticalAlignment = VerticalAlignment.Top, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(6), Margin = new Thickness(0, 0, 6, 6) };
        var content = new StackPanel(); surface.Child = content;
        var header = new DockPanel();
        var photo = MainWindow.LoadProductImage(product.ImageUrl);
        if (photo != null) header.Children.Add(new Image { Source = photo, Width = 40, Height = 40, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 6, 0) });
        var labels = new StackPanel(); header.Children.Add(labels); content.Children.Add(header);
        labels.Children.Add(new TextBlock { Text = product.Name, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 34, ToolTip = product.Name, Foreground = Brushes.DarkSlateGray });
        labels.Children.Add(new TextBlock { Text = $"+{product.BasePrice:N0}đ / phần", FontSize = 11, Foreground = Brushes.SlateGray, Margin = new Thickness(0, 0, 0, 2) });
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var minus = new Button { Content = "−", Style = (Style)FindResource("Action"), ToolTip = "Bớt " + product.Name };
        var label = new TextBlock { Width = 40, FontSize = 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var plus = new Button { Content = "+", Style = (Style)FindResource("Action"), ToolTip = "Thêm " + product.Name };
        controls.Children.Add(minus); controls.Children.Add(label); controls.Children.Add(plus); content.Children.Add(controls);
        var choice = new ToppingChoice { Product = product, Count = Math.Clamp(count, 0, 10), Label = label, Minus = minus, Plus = plus, Surface = surface };
        minus.Click += (_, _) => { choice.Count = Math.Max(0, choice.Count - 1); UpdateTotal(); };
        plus.Click += (_, _) => { choice.Count = Math.Min(10, choice.Count + 1); UpdateTotal(); };
        System.Windows.Automation.AutomationProperties.SetName(minus, "Bớt " + product.Name);
        System.Windows.Automation.AutomationProperties.SetName(plus, "Thêm " + product.Name);
        _toppings.Add(choice);
    }
    private void ToppingArea_SizeChanged(object sender, SizeChangedEventArgs e) { if (_ready) ResizeToppingPage(); }
    private void ResizeToppingPage()
    {
        if (ToppingSection.ActualHeight <= 0 || _toppings.Count == 0) return;
        double available = ToppingSection.ActualHeight - 28;
        int rows = Math.Clamp((int)(available / 120), 1, 3);
        if (_toppings.Count > rows * 2) rows = Math.Clamp((int)((available - 54) / 120), 1, 3);
        int capacity = rows * 2;
        if (_toppingsPerPage != capacity)
        {
            int firstVisible = _toppingPage * _toppingsPerPage;
            _toppingsPerPage = capacity; _toppingPage = firstVisible / capacity;
        }
        ToppingRows.Rows = rows;
        ShowToppingPage();
    }
    private void ShowToppingPage()
    {
        int pages = Math.Max(1, (_toppings.Count + _toppingsPerPage - 1) / _toppingsPerPage);
        _toppingPage = Math.Clamp(_toppingPage, 0, pages - 1);
        ToppingRows.Children.Clear();
        foreach (var topping in _toppings.Skip(_toppingPage * _toppingsPerPage).Take(_toppingsPerPage))
            ToppingRows.Children.Add(topping.Surface);
        ToppingPager.Visibility = pages > 1 ? Visibility.Visible : Visibility.Collapsed;
        ToppingPageText.Text = $"{_toppingPage + 1} / {pages}";
        PreviousToppings.IsEnabled = _toppingPage > 0;
        NextToppings.IsEnabled = _toppingPage + 1 < pages;
    }
    private void PreviousToppings_Click(object sender, RoutedEventArgs e) { _toppingPage--; ShowToppingPage(); }
    private void NextToppings_Click(object sender, RoutedEventArgs e) { _toppingPage++; ShowToppingPage(); }
    private OrderItem BuildItem() => OrderConfigurationService.Create(_product, _size, _sugar, _ice, _quantity,
        _toppings.Where(t => t.Count > 0).Select(t => (t.Product.Id, t.Count)), Note.Text, DataStoreService.Instance.Products, _sugarChoice, _iceChoice);
    private void MinusQuantity_Click(object sender, RoutedEventArgs e) { if (!_singleUnit) _quantity = Math.Max(1, _quantity - 1); UpdateTotal(); }
    private void PlusQuantity_Click(object sender, RoutedEventArgs e) { if (!_singleUnit) _quantity = Math.Min(999, _quantity + 1); UpdateTotal(); }
    private void UpdateTotal()
    {
        QuantityText.Text = _quantity.ToString();
        MinusQuantity.IsEnabled = !_singleUnit && _quantity > 1;
        PlusQuantity.IsEnabled = !_singleUnit && _quantity < 999;
        QuantityHint.Text = _singleUnit ? "Chỉ sửa sản phẩm này" : _quantity > 1 ? "Cùng tùy chọn" : "";
        int portions = _toppings.Sum(t => t.Count);
        ToppingSummary.Text = portions > 0 ? $"Đã chọn {portions} phần / ly" : "Số phần / ly";
        foreach (var topping in _toppings)
        {
            topping.Label.Text = topping.Count.ToString(); topping.Minus.IsEnabled = topping.Count > 0; topping.Plus.IsEnabled = topping.Count < 10;
            topping.Surface.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(topping.Count > 0 ? "#EFF7F2" : "#FFFFFF"));
            topping.Surface.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(topping.Count > 0 ? "#12634B" : "#E2E8F0"));
        }
        try
        {
            var item = BuildItem();
            Total.Text = $"{item.TotalPrice:N0}đ";
            Breakdown.Text = $"{item.UnitPrice:N0}đ × {item.Quantity} {(!_product.IsBeverage ? "phần" : "ly")}";
        // Only worth showing when it explains the total; for a single unit it just repeats it.
        Breakdown.Visibility = item.Quantity > 1 ? Visibility.Visible : Visibility.Collapsed;
            ConfirmButton.Content = _singleUnit ? "Tách và lưu 1 sản phẩm" : _editing ? "Lưu thay đổi" : $"Thêm {_quantity} {(!_product.IsBeverage ? "phần" : "ly")} vào đơn";
            ConfirmButton.IsEnabled = true; Error.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { Total.Text = "—"; Breakdown.Text = ""; ConfirmButton.IsEnabled = false; Error.Text = ex.Message; Error.Visibility = Visibility.Visible; }
    }
    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        try { ResultItem = BuildItem(); DialogResult = true; }
        catch (Exception ex) { Error.Text = ex.Message; Error.Visibility = Visibility.Visible; }
    }
}
