using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs;

// Ingredient catalog, receiving, stock count and movement history.
public sealed class IngredientWindow : Window
{
    private readonly ICatalogService _catalog;
    private readonly IInventoryService _inventory;
    private readonly IAuthService _auth;
    private readonly DataGrid _grid = new() { IsReadOnly = true, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Single };
    private readonly DataGrid _log = new() { IsReadOnly = true, AutoGenerateColumns = false, Height = 200 };
    private readonly TextBox _name = Box(), _unit = Box(), _warn = Box(true), _qty = Box(true), _reason = Box();
    private readonly TextBlock _summary = PosUi.Text("", 13, color: PosUi.Muted), _selected = PosUi.Text("Thêm mới", 15, true);
    private string? _id;

    private static TextBox Box(bool numeric = false)
    {
        var b = new TextBox { Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 8) };
        if (numeric) TouchInput.SetNumeric(b, true);
        return b;
    }

    public IngredientWindow(ICatalogService catalog, IInventoryService inventory, IAuthService auth)
    {
        _catalog = catalog;
        _inventory = inventory;
        _auth = auth;
        _auth.RequireManager();
        Title = "Kho nguyên liệu"; Width = 1120; Height = 760; MinWidth = 880; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current.TryFindResource("PosWindow") is Style style) Style = style;
        EmptyState.SetText(_grid, "Chưa có nguyên liệu — nhập thông tin bên phải rồi bấm Lưu");
        EmptyState.SetText(_log, "Chưa có biến động");
        AddColumns(_grid, ("Nguyên liệu", "Name", "", true), ("Tồn", "StockLabel", "", false), ("Ngưỡng", "WarningThreshold", "0.##", false));
        AddColumns(_log, ("Lúc", "At", "dd/MM HH:mm", false), ("Nguyên liệu", "IngredientName", "", false), ("Loại", "Type", "", false), ("Thay đổi", "Change", "+0.##;-0.##", false), ("Sau", "After", "0.##", false), ("Lý do", "Reason", "", true));
        // Low stock rows are tinted, like the packaging screen.
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Triggers.Add(new DataTrigger { Binding = new Binding("IsLow"), Value = true, Setters = { new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xED))) } });
        _grid.RowStyle = rowStyle;
        _grid.SelectionChanged += (_, _) => Select(_grid.SelectedItem as Ingredient);

        var newBtn = PosUi.Button("+ Nguyên liệu mới", (_, _) => { _grid.SelectedItem = null; Select(null); }, minHeight: 44);
        var save = PosUi.Button("Lưu nguyên liệu", (_, _) => Save(), "PrimaryButton", 44);
        var receive = PosUi.Button("Nhập kho (+)", (_, _) => Adjust("Nhập kho"), "PrimaryButton", 44);
        var count = PosUi.Button("Kiểm kê (= tồn thực tế)", (_, _) => Adjust("Kiểm kê"), minHeight: 44);
        var delete = PosUi.Button("Xóa nguyên liệu", (_, _) => Delete(), "DangerButton", 40);
        var form = new StackPanel
        {
            Children =
            {
                _selected, PosUi.Text("Tên"), _name, PosUi.Text("Đơn vị (g, ml, cái…)"), _unit, PosUi.Text("Cảnh báo khi tồn ≤"), _warn, save,
                new Border { Height = 1, Background = Brushes.Gainsboro, Margin = new Thickness(0, 14, 0, 10) },
                PosUi.Text("Cập nhật tồn", 15, true), PosUi.Text("Số lượng"), _qty, PosUi.Text("Lý do / nhà cung cấp"), _reason,
                new UniformGrid { Columns = 2, Margin = new Thickness(-4, 0, -4, 0), Children = { receive, count } },
                new Border { Height = 16 }, delete
            }
        };
        var side = new Border { Width = 360, Margin = new Thickness(14, 0, 0, 0), Padding = new Thickness(14), Background = Brushes.White, CornerRadius = new CornerRadius(8), Child = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        DockPanel.SetDock(side, Dock.Right);
        var left = new DockPanel();
        var logPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Children = { PosUi.Text("Biến động gần đây", 14, true), _log } };
        DockPanel.SetDock(newBtn, Dock.Top); DockPanel.SetDock(logPanel, Dock.Bottom);
        newBtn.HorizontalAlignment = HorizontalAlignment.Left;
        left.Children.Add(newBtn); left.Children.Add(logPanel); left.Children.Add(_grid);
        var body = new DockPanel { Children = { side, left } };
        var head = new StackPanel { Children = { PosUi.Text("Kho nguyên liệu", 24, true), _summary, PosUi.Text("Bán hàng tự trừ theo công thức món (Quản trị → chọn món → Công thức). Hết nguyên liệu: món tự chuyển Hết hàng, nhập kho lại sẽ tự mở bán.", 13, color: PosUi.Muted) }, Margin = new Thickness(0, 0, 0, 10) };
        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head); root.Children.Add(body);
        Content = root;
        Loaded += (_, _) => Refresh();
    }

    internal static void AddColumns(DataGrid grid, params (string Header, string Path, string Format, bool Star)[] columns)
    {
        foreach (var (header, path, format, star) in columns)
            grid.Columns.Add(new DataGridTextColumn { Header = header, Binding = new Binding(path) { StringFormat = format.Length > 0 ? "{0:" + format + "}" : null }, Width = star ? new DataGridLength(1, DataGridLengthUnitType.Star) : DataGridLength.Auto });
    }

    private void Refresh()
    {
        _grid.ItemsSource = _inventory.Ingredients.OrderBy(i => !i.IsLow).ThenBy(i => i.Name).ToList();
        _grid.SelectedItem = _inventory.Ingredients.FirstOrDefault(i => i.Id == _id);
        _log.ItemsSource = _inventory.IngredientMovements.AsEnumerable().Reverse().Take(200).ToList();
        int low = _inventory.Ingredients.Count(i => i.IsLow);
        _summary.Text = $"{_inventory.Ingredients.Count} nguyên liệu, {low} sắp hết (tô vàng), {_catalog.Products.Count(p => p.SoldOutByStock)} món tạm hết do thiếu nguyên liệu";
    }

    private void Select(Ingredient? i)
    {
        _id = i?.Id;
        _selected.Text = i is null ? "Thêm mới" : $"{i.Name} · tồn {i.StockLabel}";
        _name.Text = i?.Name ?? ""; _unit.Text = i?.Unit ?? "g"; _warn.Text = i?.WarningThreshold.ToString("0.##") ?? "0";
        _qty.Clear(); _reason.Clear();
    }

    private void Save()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_name.Text)) throw FieldError.Mark(_name, "Nhập tên nguyên liệu.");
            if (!decimal.TryParse(_warn.Text, out var warn) || warn < 0) throw FieldError.Mark(_warn, "Ngưỡng phải là số không âm.");
            var id = _id ?? Guid.NewGuid().ToString();
            _inventory.SaveIngredient(new Ingredient { Id = id, Name = _name.Text, Unit = _unit.Text, WarningThreshold = warn });
            _id = id; Refresh(); Toast.Show(this, "Đã lưu nguyên liệu.");
        }
        catch (InvalidOperationException ex) { Toast.Show(this, ex.Message, true); }
    }

    private void Adjust(string type)
    {
        try
        {
            if (_id is null) throw new InvalidOperationException("Chọn nguyên liệu trước.");
            if (!decimal.TryParse(_qty.Text, out var qty)) throw FieldError.Mark(_qty, "Nhập số lượng.");
            _inventory.AdjustIngredient(_id, type, qty, _reason.Text);
            Refresh(); Select(_inventory.Ingredients.First(i => i.Id == _id)); Toast.Show(this, type == "Nhập kho" ? "Đã nhập kho." : "Đã cập nhật tồn kiểm kê.");
        }
        catch (InvalidOperationException ex) { Toast.Show(this, ex.Message, true); }
    }

    private void Delete()
    {
        if (_id is null) { Toast.Show(this, "Chọn nguyên liệu trước.", true); return; }
        if (MessageBox.Show(this, $"Xóa nguyên liệu {_name.Text}?", "Xóa nguyên liệu", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { _inventory.DeleteIngredient(_id); _id = null; Select(null); Refresh(); Toast.Show(this, "Đã xóa."); }
        catch (InvalidOperationException ex) { Toast.Show(this, ex.Message, true); }
    }
}

// Edits one product's recipe as rows of ingredient + amount (for size M).
public sealed class RecipeWindow : Window
{
    private readonly IInventoryService _inventory;
    private sealed class Row { public string IngredientId { get; set; } = ""; public decimal Amount { get; set; } }
    private readonly ObservableCollection<Row> _rows;

    public RecipeWindow(Product product, IInventoryService inventory)
    {
        _inventory = inventory;
        Title = "Công thức — " + product.Name; Width = 620; Height = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (Application.Current.TryFindResource("PosWindow") is Style style) Style = style;
        _rows = new(product.Recipe.Select(r => new Row { IngredientId = r.IngredientId, Amount = r.Amount }));
        var grid = new DataGrid { ItemsSource = _rows, AutoGenerateColumns = false, CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single };
        EmptyState.SetText(grid, _inventory.Ingredients.Count == 0 ? "Chưa có nguyên liệu — tạo trong Kho nguyên liệu trước" : "Chưa có dòng — bấm + Thêm nguyên liệu");
        grid.Columns.Add(new DataGridComboBoxColumn { Header = "Nguyên liệu", ItemsSource = _inventory.Ingredients.OrderBy(i => i.Name).ToList(), DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValueBinding = new Binding("IngredientId"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "Định lượng (size M)", Binding = new Binding("Amount"), Width = 150 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Đơn vị", Binding = new Binding("IngredientId") { Converter = new UnitConverter(_inventory) }, IsReadOnly = true, Width = 70 });
        var add = PosUi.Button("+ Thêm nguyên liệu", (_, _) => _rows.Add(new Row { IngredientId = _inventory.Ingredients.FirstOrDefault()?.Id ?? "", Amount = 1 }), minHeight: 40);
        var remove = PosUi.Button("Xóa dòng đang chọn", (_, _) => { if (grid.SelectedItem is Row r) _rows.Remove(r); }, "DangerButton", 40);
        var save = PosUi.Button("Lưu công thức", (_, _) =>
        {
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            try
            {
                _inventory.SaveRecipe(product.Id, _rows.Select(r => new RecipeLine { IngredientId = r.IngredientId, Amount = r.Amount }).ToList());
                DialogResult = true;
            }
            catch (InvalidOperationException ex) { Toast.Show(this, ex.Message, true); }
        }, "PrimaryButton", 48);
        save.IsDefault = true;
        var hint = PosUi.Text(product.IsBeverage ? "Nhập định lượng cho size M. Size S ×0.8, L ×1.3, XL ×1.6 được tính tự động." : "Định lượng cho 1 phần.", 13, color: PosUi.Muted);
        var buttons = new UniformGrid { Columns = 2, Margin = new Thickness(-4, 8, -4, 0), Children = { add, remove } };
        var root = new DockPanel { Margin = new Thickness(18) };
        var head = new StackPanel { Children = { PosUi.Text(product.Name, 20, true), hint }, Margin = new Thickness(0, 0, 0, 10) };
        DockPanel.SetDock(head, Dock.Top); DockPanel.SetDock(save, Dock.Bottom); DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(head); root.Children.Add(save); root.Children.Add(buttons); root.Children.Add(grid);
        Content = root;
    }

    private sealed class UnitConverter : IValueConverter
    {
        private readonly IInventoryService _ingredients;
        public UnitConverter(IInventoryService ingredients) => _ingredients = ingredients;
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c) => _ingredients.Ingredients.FirstOrDefault(i => i.Id == value as string)?.Unit ?? "";
        public object ConvertBack(object value, Type t, object p, System.Globalization.CultureInfo c) => Binding.DoNothing;
    }
}
