using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs;

// Shop information, cashier discount limit and delivery-app channels.
public sealed class StoreSettingsWindow : Window
{
    private readonly IStoreSettingsService _settings;
    private readonly IAuthService _auth;
    public StoreSettingsWindow(IStoreSettingsService settings, IAuthService auth)
    {
        _settings = settings;
        _auth = auth;
        _auth.RequireManager();
        var s = JsonSerializer.Deserialize<StoreSettings>(JsonSerializer.Serialize(_settings.Settings))!;
        Title = "Cài đặt cửa hàng"; Width = 760; Height = 720; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current.TryFindResource("PosWindow") is Style style) Style = style;
        TextBox Field(string value) => new() { Text = value, Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 8) };
        var name = Field(s.ShopName); var address = Field(s.Address); var phone = Field(s.Phone); var hours = Field(s.OpeningHours);
        var limit = Field(s.CashierDiscountLimitPercent.ToString()); TouchInput.SetNumeric(limit, true);
        var vat = Field(s.VatPercent.ToString()); TouchInput.SetNumeric(vat, true);
        var vatIncluded = new CheckBox { Content = "Giá trên thực đơn đã gồm VAT (hóa đơn chỉ hiển thị phần thuế)", IsChecked = s.PricesIncludeVat, Margin = new Thickness(0, 0, 0, 8) };
        var footer = Field(s.ReceiptFooter);
        var channels = new ObservableCollection<SalesChannel>(s.Channels);
        var grid = new DataGrid { ItemsSource = channels, AutoGenerateColumns = false, CanUserAddRows = false, Height = 180 };
        EmptyState.SetText(grid, "Chưa có kênh — bấm + Thêm kênh");
        grid.Columns.Add(new DataGridTextColumn { Header = "Tên kênh", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Phụ thu giá (%)", Binding = new Binding("MarkupPercent"), Width = 130 });
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Đang dùng", Binding = new Binding("IsActive"), Width = 90, ElementStyle = (Style)Application.Current.FindResource("MaterialDesignDataGridCheckBoxColumnStyle"), EditingElementStyle = (Style)Application.Current.FindResource("MaterialDesignDataGridCheckBoxColumnEditingStyle") });
        var add = PosUi.Button("+ Thêm kênh", (_, _) => channels.Add(new SalesChannel { Name = "Kênh mới", MarkupPercent = 20 }), minHeight: 40);
        var remove = PosUi.Button("Xóa kênh đang chọn", (_, _) => { if (grid.SelectedItem is SalesChannel c) channels.Remove(c); }, "DangerButton", 40);
        var save = PosUi.Button("Lưu cài đặt", (_, _) =>
        {
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            try
            {
                if (!int.TryParse(limit.Text, out var l) || l is < 0 or > 100) throw FieldError.Mark(limit, "Nhập từ 0 đến 100.");
                if (!int.TryParse(vat.Text, out var v) || v is < 0 or > 10) throw FieldError.Mark(vat, "Nhập VAT từ 0 đến 10%.");
                _settings.SaveSettings(new StoreSettings { ShopName = name.Text.Trim(), Address = address.Text.Trim(), Phone = phone.Text.Trim(), OpeningHours = hours.Text.Trim(), CashierDiscountLimitPercent = l, VatPercent = v, PricesIncludeVat = vatIncluded.IsChecked == true, ReceiptFooter = footer.Text.Trim(), Channels = channels.Select(c => new SalesChannel { Name = c.Name.Trim(), MarkupPercent = c.MarkupPercent, IsActive = c.IsActive }).ToList() });
                DialogResult = true;
            }
            catch (InvalidOperationException ex) { Toast.Show(this, ex.Message, true); }
        }, "PrimaryButton", 48);
        save.IsDefault = true;
        var form = new StackPanel
        {
            Children =
            {
                PosUi.Text("Thông tin cửa hàng", 17, true), PosUi.Text("Hiển thị trên tiêu đề màn bán hàng và file báo cáo.", 13, color: PosUi.Muted),
                PosUi.Text("Tên quán"), name, PosUi.Text("Địa chỉ"), address,
                new UniformGrid { Columns = 2, Children = { new StackPanel { Margin = new Thickness(0, 0, 6, 0), Children = { PosUi.Text("Số điện thoại"), phone } }, new StackPanel { Margin = new Thickness(6, 0, 0, 0), Children = { PosUi.Text("Giờ mở cửa"), hours } } } },
                PosUi.Text("Phân quyền giảm giá", 17, true), PosUi.Text("Thu ngân được tự giảm tối đa % này của tạm tính; vượt mức cần quản lý duyệt. Quản lý không bị giới hạn. Đặt 0 để mọi khoản giảm của thu ngân đều cần duyệt.", 13, color: PosUi.Muted),
                PosUi.Text("Hạn mức thu ngân (%)"), limit,
                PosUi.Text("Thuế & hóa đơn", 17, true), PosUi.Text("VAT tính trên số tiền sau mọi khoản giảm, làm tròn đến đồng. Đặt 0 để tắt.", 13, color: PosUi.Muted),
                PosUi.Text("Thuế VAT (%)"), vat, vatIncluded, PosUi.Text("Lời chào cuối hóa đơn"), footer,
                PosUi.Text("Kênh bán (app giao hàng)", 17, true), PosUi.Text("Đơn từ app được cộng % phụ thu vào giá, ghi nhận thanh toán qua nền tảng (không vào ngăn kéo), không áp dụng khuyến mãi/điểm của quán.", 13, color: PosUi.Muted),
                grid, new UniformGrid { Columns = 2, Margin = new Thickness(-4, 6, -4, 0), Children = { add, remove } }
            }
        };
        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(save, Dock.Bottom);
        root.Children.Add(save);
        root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }
}

// List of promotions; each can be a time-window %, buy X get Y, or a fixed-price combo.
public sealed class PromotionsWindow : Window
{
    private readonly ICatalogService _catalog;
    private readonly IPromotionService _promotions;
    private readonly IAuthService _auth;
    private readonly ListBox _list = new() { DisplayMemberPath = "Name", MinWidth = 240 };
    private readonly ComboBox _type = new() { ItemsSource = new[] { "Giờ vàng", "Mua X tặng Y", "Combo" }, Margin = new Thickness(0, 4, 0, 8), MinHeight = 36 };
    private readonly TextBox _name = Box(), _percent = Box(true), _buy = Box(true), _free = Box(true), _comboPrice = Box(true), _start = Box(), _end = Box();
    private readonly CheckBox _active = new() { Content = "Đang bật", Margin = new Thickness(0, 0, 0, 8) };
    private readonly ListBox _products = new() { SelectionMode = SelectionMode.Multiple, Height = 150, DisplayMemberPath = "Name" };
    private readonly WrapPanel _days = new();
    private readonly StackPanel _percentRow, _bxgyRow, _comboRow;
    private readonly TextBlock _productHint = PosUi.Text("", 12, color: PosUi.Muted);
    private string? _id;

    private static TextBox Box(bool numeric = false) { var b = new TextBox { Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 8) }; if (numeric) TouchInput.SetNumeric(b, true); return b; }
    private static readonly (DayOfWeek Day, string Label)[] Days = { (DayOfWeek.Monday, "T2"), (DayOfWeek.Tuesday, "T3"), (DayOfWeek.Wednesday, "T4"), (DayOfWeek.Thursday, "T5"), (DayOfWeek.Friday, "T6"), (DayOfWeek.Saturday, "T7"), (DayOfWeek.Sunday, "CN") };

    public PromotionsWindow(ICatalogService catalog, IPromotionService promotions, IAuthService auth)
    {
        _catalog = catalog;
        _promotions = promotions;
        _auth = auth;
        _auth.RequireManager();
        Title = "Chương trình khuyến mãi"; Width = 980; Height = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current.TryFindResource("PosWindow") is Style style) Style = style;
        EmptyState.SetText(_list, "Chưa có chương trình");
        _products.ItemsSource = _catalog.Products.Where(p => !p.IsTopping || p.SoldSeparately).OrderBy(p => p.Name).ToList();
        foreach (var (day, label) in Days) _days.Children.Add(new CheckBox { Content = label, Tag = day, Margin = new Thickness(0, 0, 12, 6) });
        _percentRow = new StackPanel { Children = { PosUi.Text("Mức giảm (%)"), _percent } };
        _bxgyRow = new StackPanel { Children = { new UniformGrid { Columns = 2, Children = { new StackPanel { Margin = new Thickness(0, 0, 6, 0), Children = { PosUi.Text("Mua (X)"), _buy } }, new StackPanel { Margin = new Thickness(6, 0, 0, 0), Children = { PosUi.Text("Tặng (Y) — món rẻ nhất"), _free } } } } } };
        _comboRow = new StackPanel { Children = { PosUi.Text("Giá combo (đồng)"), _comboPrice } };
        _type.SelectionChanged += (_, _) => UpdateTypeRows();
        _list.SelectionChanged += (_, _) => { if (_list.SelectedItem is Promotion p) Load(p); };

        var save = PosUi.Button("Lưu chương trình", (_, _) => Save(), "PrimaryButton", 46);
        var del = PosUi.Button("Xóa chương trình", (_, _) => Delete(), "DangerButton", 40);
        var form = new StackPanel
        {
            Children =
            {
                PosUi.Text("Loại chương trình"), _type, _active, PosUi.Text("Tên hiển thị"), _name, _percentRow, _bxgyRow, _comboRow,
                PosUi.Text("Món áp dụng"), _productHint, _products,
                new UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 0), Children = { new StackPanel { Margin = new Thickness(0, 0, 6, 0), Children = { PosUi.Text("Từ (giờ:phút)"), _start } }, new StackPanel { Margin = new Thickness(6, 0, 0, 0), Children = { PosUi.Text("Đến (giờ:phút)"), _end } } } },
                PosUi.Text("Ngày áp dụng (không chọn = mọi ngày)"), _days, save, new Border { Height = 10 }, del
            }
        };
        var newBtn = PosUi.Button("+ Chương trình mới", (_, _) => { _list.SelectedItem = null; Load(new Promotion { IsActive = true, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 0) }); _id = null; }, minHeight: 44);
        var left = new DockPanel { Margin = new Thickness(0, 0, 14, 0) };
        DockPanel.SetDock(newBtn, Dock.Bottom);
        left.Children.Add(newBtn); left.Children.Add(_list);
        var side = new Border { Padding = new Thickness(14), Background = System.Windows.Media.Brushes.White, CornerRadius = new CornerRadius(8), Child = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 10), Children = { PosUi.Text("Chương trình khuyến mãi", 24, true), PosUi.Text("Tất cả chương trình đang bật và đúng khung giờ được cộng dồn, tự áp dụng khi bán tại quầy (không cần duyệt).", 13, color: PosUi.Muted) } };
        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(head, Dock.Top); DockPanel.SetDock(left, Dock.Left);
        root.Children.Add(head); root.Children.Add(left); root.Children.Add(side);
        Content = root;
        Loaded += (_, _) => { Refresh(); Load(_promotions.Promotions.FirstOrDefault() ?? new Promotion { IsActive = true }); };
    }

    private void UpdateTypeRows()
    {
        string type = _type.SelectedItem as string ?? "Giờ vàng";
        _percentRow.Visibility = type == "Giờ vàng" ? Visibility.Visible : Visibility.Collapsed;
        _bxgyRow.Visibility = type == "Mua X tặng Y" ? Visibility.Visible : Visibility.Collapsed;
        _comboRow.Visibility = type == "Combo" ? Visibility.Visible : Visibility.Collapsed;
        _products.Visibility = type == "Giờ vàng" ? Visibility.Collapsed : Visibility.Visible;
        _productHint.Text = type switch { "Mua X tặng Y" => "Không chọn = mọi đồ uống.", "Combo" => "Chọn từ 2 món; mỗi combo gồm 1 phần mỗi món.", _ => "Giờ vàng áp dụng cho cả đơn." };
    }

    private void Refresh() { _list.ItemsSource = null; _list.ItemsSource = _promotions.Promotions.ToList(); _list.SelectedItem = _promotions.Promotions.FirstOrDefault(p => p.Id == _id); }

    private void Load(Promotion p)
    {
        _id = p.Id; _type.SelectedItem = p.Type; _active.IsChecked = p.IsActive; _name.Text = p.Name;
        _percent.Text = p.Percent.ToString(); _buy.Text = p.BuyQuantity.ToString(); _free.Text = p.FreeQuantity.ToString(); _comboPrice.Text = p.ComboPrice.ToString("0");
        _start.Text = p.Start.ToString(@"hh\:mm"); _end.Text = p.End.ToString(@"hh\:mm");
        foreach (CheckBox c in _days.Children) c.IsChecked = p.Days.Contains((DayOfWeek)c.Tag);
        _products.SelectedItems.Clear();
        foreach (var product in ((List<Product>)_products.ItemsSource).Where(x => p.ProductIds.Contains(x.Id))) _products.SelectedItems.Add(product);
        UpdateTypeRows();
    }

    private void Save()
    {
        try
        {
            string type = _type.SelectedItem as string ?? "Giờ vàng";
            if (string.IsNullOrWhiteSpace(_name.Text)) throw FieldError.Mark(_name, "Nhập tên chương trình.");
            int.TryParse(_percent.Text, out var percent); int.TryParse(_buy.Text, out var buy); int.TryParse(_free.Text, out var free); decimal.TryParse(_comboPrice.Text, out var combo);
            if (!TimeSpan.TryParse(_start.Text, out var start)) throw FieldError.Mark(_start, "Nhập giờ dạng 07:00.");
            if (!TimeSpan.TryParse(_end.Text, out var end)) throw FieldError.Mark(_end, "Nhập giờ dạng 22:00.");
            var promo = new Promotion
            {
                Id = _id ?? Guid.NewGuid().ToString(), Type = type, Name = _name.Text.Trim(), IsActive = _active.IsChecked == true, Percent = percent, BuyQuantity = buy, FreeQuantity = free, ComboPrice = combo, Start = start, End = end,
                Days = _days.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (DayOfWeek)c.Tag).ToList(),
                ProductIds = type == "Giờ vàng" ? new() : _products.SelectedItems.Cast<Product>().Select(x => x.Id).ToList()
            };
            _promotions.SavePromotion(promo);
            _id = promo.Id; Refresh(); Toast.Show(this, "Đã lưu chương trình.");
        }
        catch (InvalidOperationException ex) { Toast.Show(this, ex.Message, true); }
    }

    private void Delete()
    {
        if (_id is null || _promotions.Promotions.All(p => p.Id != _id)) { Toast.Show(this, "Chọn chương trình trước.", true); return; }
        if (MessageBox.Show(this, $"Xóa chương trình {_name.Text}?", "Xóa khuyến mãi", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        _promotions.DeletePromotion(_id); _id = null; Refresh(); Load(_promotions.Promotions.FirstOrDefault() ?? new Promotion { IsActive = true });
    }
}
