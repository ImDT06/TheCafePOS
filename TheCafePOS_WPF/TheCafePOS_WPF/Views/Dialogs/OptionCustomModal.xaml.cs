using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class OptionCustomModal : Window
{
    private readonly ICatalogService _catalog;
    private sealed class ToppingChoice
    {
        public Product Product { get; init; } = null!;
        public int Count { get; set; }
        public TextBlock Label { get; init; } = null!;
        public Button Card { get; init; } = null!;
        public Button Minus { get; init; } = null!;
        public Grid Surface { get; init; } = null!;
    }
    // Requests cashiers type most often; picked ones are joined in front of the free-text note.
    public static readonly string[] QuickNoteOptions = { "Đá để riêng", "Topping để riêng", "Không ống hút", "Túi riêng" };
    private readonly List<System.Windows.Controls.Primitives.ToggleButton> _quickNotes = new();
    private readonly Product _product;
    private readonly bool _singleUnit;
    private readonly bool _editing;
    private readonly List<ToppingChoice> _toppings = new();
    private int _quantity, _sugar, _ice;
    private string _size = "";
    private string? _sugarChoice, _iceChoice;
    private bool _ready;
    public OrderItem ResultItem { get; private set; } = null!;
    public OptionCustomModal(Product product, ICatalogService catalog, int dailyOrderNumber = 1, OrderItem? existing = null, bool singleUnit = false)
    {
        _catalog = catalog;
        _product = product; _singleUnit = singleUnit; _editing = existing != null;
        InitializeComponent();
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
        MinHeight = Math.Min(MinHeight, MaxHeight);
        MinWidth = Math.Min(MinWidth, MaxWidth);
        Height = Math.Min(Height, MaxHeight); Width = Math.Min(Width, MaxWidth);
        Heading.Text = product.Name;
        ProductPhoto.Source = Core.ProductImages.Load(product.ImageUrl);
        PhotoFrame.Visibility = ProductPhoto.Source is null ? Visibility.Collapsed : Visibility.Visible;
        ContextLabel.Text = singleUnit ? "Tách 1 món để chỉnh riêng" : _editing ? "Sửa món trong giỏ" : $"Thêm món vào đơn #{dailyOrderNumber:D2}";
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
        var (picked, rest) = SplitNote(existing?.Note ?? "");
        foreach (var option in QuickNoteOptions)
        {
            var chip = new System.Windows.Controls.Primitives.ToggleButton { Content = option, IsChecked = picked.Contains(option), Style = (Style)FindResource("NoteChip") };
            _quickNotes.Add(chip);
            QuickNotes.Children.Add(chip);
        }
        Note.Text = rest;
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
        foreach (var topping in _catalog.Products.Where(p => product.IsBeverage && p.IsTopping && p.IsActive && product.AllowedToppingIds.Contains(p.Id) && _catalog.Categories.Any(c => c.Id == p.CategoryId && c.IsActive)))
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
        foreach (var topping in _toppings) ToppingRows.Children.Add(topping.Surface);
        KeyHint.Visibility = singleUnit ? Visibility.Collapsed : Visibility.Visible;
        PreviewKeyDown += QuantityKeys;
        _ready = true; UpdateTotal();
    }

    /// <summary>Splits a saved note into the quick-note chips it contains and the remaining free text.</summary>
    public static (HashSet<string> Picked, string FreeText) SplitNote(string note)
    {
        var parts = note.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var picked = parts.Where(p => QuickNoteOptions.Contains(p)).ToHashSet();
        return (picked, string.Join(", ", parts.Where(p => !picked.Contains(p))));
    }

    /// <summary>Chips first, in their on-screen order, then the free text.</summary>
    public static string JoinNote(IEnumerable<string> picked, string rest) =>
        string.Join(", ", QuickNoteOptions.Where(picked.Contains).Append(rest.Trim()).Where(p => p.Length > 0));

    // 1–9 set the quantity, +/− adjust it; ignored while typing a note.
    private void QuantityKeys(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_singleUnit || System.Windows.Input.Keyboard.FocusedElement is TextBox) return;
        int digit = e.Key switch
        {
            >= System.Windows.Input.Key.D1 and <= System.Windows.Input.Key.D9 => e.Key - System.Windows.Input.Key.D0,
            >= System.Windows.Input.Key.NumPad1 and <= System.Windows.Input.Key.NumPad9 => e.Key - System.Windows.Input.Key.NumPad0,
            _ => 0
        };
        if (digit > 0) _quantity = digit;
        else if (e.Key is System.Windows.Input.Key.Add or System.Windows.Input.Key.OemPlus) _quantity = Math.Min(999, _quantity + 1);
        else if (e.Key is System.Windows.Input.Key.Subtract or System.Windows.Input.Key.OemMinus) _quantity = Math.Max(1, _quantity - 1);
        else return;
        e.Handled = true;
        UpdateTotal();
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
    // All toppings in one scrolling grid: tapping a card adds a portion, the minus only shows once chosen.
    private void AddTopping(Product product, int count)
    {
        var header = new DockPanel();
        var photo = Core.ProductImages.Load(product.ImageUrl);
        if (photo != null) { var image = new Image { Source = photo, Width = 30, Height = 30, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 8, 0) }; DockPanel.SetDock(image, Dock.Left); header.Children.Add(image); }
        var label = new TextBlock { FontSize = 15, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 40, 0) };
        DockPanel.SetDock(label, Dock.Right); header.Children.Add(label);
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock { Text = product.Name, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = product.Name });
        labels.Children.Add(new TextBlock { Text = $"+{product.BasePrice:N0}đ", FontSize = 11, Foreground = Core.Palette.Muted });
        header.Children.Add(labels);
        var card = new Button { Content = header, Style = (Style)FindResource("ToppingCard"), ToolTip = "Thêm 1 phần " + product.Name };
        var minus = new Button { Content = "−", FontSize = 18, Style = (Style)FindResource("Action"), MinWidth = 40, MinHeight = 40, Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Bớt " + product.Name };
        var surface = new Grid { Margin = new Thickness(0, 0, 6, 6), Children = { card, minus } };
        var choice = new ToppingChoice { Product = product, Count = Math.Clamp(count, 0, 10), Label = label, Card = card, Minus = minus, Surface = surface };
        card.Click += (_, _) => { choice.Count = Math.Min(10, choice.Count + 1); UpdateTotal(); };
        minus.Click += (_, _) => { choice.Count = Math.Max(0, choice.Count - 1); UpdateTotal(); };
        System.Windows.Automation.AutomationProperties.SetName(card, "Thêm " + product.Name);
        System.Windows.Automation.AutomationProperties.SetName(minus, "Bớt " + product.Name);
        _toppings.Add(choice);
    }
    private OrderItem BuildItem() => OrderConfigurationService.Create(_product, _size, _sugar, _ice, _quantity,
        _toppings.Where(t => t.Count > 0).Select(t => (t.Product.Id, t.Count)),
        JoinNote(_quickNotes.Where(c => c.IsChecked == true).Select(c => (string)c.Content), Note.Text), _catalog.Products, _sugarChoice, _iceChoice);
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
            topping.Label.Text = topping.Count > 0 ? $"×{topping.Count}" : "";
            topping.Minus.Visibility = topping.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            topping.Card.IsEnabled = topping.Count < 10;
            topping.Card.Background = topping.Count > 0 ? Core.Palette.AccentSoft : Core.Palette.Surface;
            topping.Card.BorderBrush = topping.Count > 0 ? Core.Palette.Accent : Core.Palette.Border;
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
