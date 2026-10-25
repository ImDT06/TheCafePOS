using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Views.Dialogs;

internal static class FormUi
{
    public static TextBox Box(bool numeric = false)
    {
        var b = new TextBox { Padding = new Thickness(6), Margin = new Thickness(0, 4, 0, 8) };
        if (numeric) TouchInput.SetNumeric(b, true);
        return b;
    }
    public static DataGrid Grid(string empty, double height = double.NaN)
    {
        var g = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Single, Height = height };
        EmptyState.SetText(g, empty);
        return g;
    }
    public static UIElement Pair(string leftLabel, UIElement left, string rightLabel, UIElement right) => new UniformGrid
    {
        Columns = 2,
        Children = { new StackPanel { Margin = new Thickness(0, 0, 6, 0), Children = { PosUi.Text(leftLabel), left } }, new StackPanel { Margin = new Thickness(6, 0, 0, 0), Children = { PosUi.Text(rightLabel), right } } }
    };
    public static Border Card(UIElement content, double width = double.NaN) => new()
    {
        Width = width, Padding = new Thickness(14), Background = Brushes.White, CornerRadius = new CornerRadius(8),
        Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
    };
    public static void Style(Window w)
    {
        if (Application.Current.TryFindResource("PosWindow") is Style style) w.Style = style;
    }
}

// Voucher codes: value, minimum order, validity dates and usage limit.
public sealed class VouchersWindow : Window
{
    private readonly IVoucherService _vouchers;
    private readonly DataGrid _grid = FormUi.Grid("Chưa có voucher — nhập thông tin bên phải rồi bấm Lưu");
    private readonly TextBox _code = FormUi.Box(), _description = FormUi.Box(), _value = FormUi.Box(true), _max = FormUi.Box(true), _min = FormUi.Box(true), _limit = FormUi.Box(true);
    private readonly ComboBox _type = new() { ItemsSource = new[] { Voucher.Amount, Voucher.Percent }, Margin = new Thickness(0, 4, 0, 8), MinHeight = 36 };
    private readonly DatePicker _from = new() { Margin = new Thickness(0, 4, 0, 8) }, _to = new() { Margin = new Thickness(0, 4, 0, 8) };
    private readonly CheckBox _active = new() { Content = "Đang bật", Margin = new Thickness(0, 0, 0, 8) };
    private readonly StackPanel _maxRow;
    private readonly TextBlock _selected = PosUi.Text("Voucher mới", 15, true);
    private string? _id;

    public VouchersWindow(IVoucherService vouchers, IAuthService auth)
    {
        _vouchers = vouchers;
        auth.RequireManager();
        Title = "Voucher"; Width = 1080; Height = 760; MinWidth = 860; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FormUi.Style(this);
        _code.CharacterCasing = CharacterCasing.Upper; _code.MaxLength = 20;
        IngredientWindow.AddColumns(_grid, ("Mã", "Code", "", false), ("Giá trị", "ValueLabel", "", false), ("Đơn tối thiểu", "MinSubtotal", "N0", false), ("Hiệu lực", "PeriodLabel", "", false), ("Đã dùng", "UsageLabel", "", false), ("Mô tả", "Description", "", true));
        _grid.Columns.Insert(0, new DataGridCheckBoxColumn { Header = "Bật", Binding = new System.Windows.Data.Binding("IsActive"), IsReadOnly = true });
        _grid.SelectionChanged += (_, _) => { if (_grid.SelectedItem is Voucher v) Load(v); };
        _maxRow = new StackPanel { Children = { PosUi.Text("Giảm tối đa (đồng, 0 = không giới hạn)"), _max } };
        _type.SelectionChanged += (_, _) => _maxRow.Visibility = _type.SelectedItem as string == Voucher.Percent ? Visibility.Visible : Visibility.Collapsed;
        var save = PosUi.Button("Lưu voucher", (_, _) => Save(), "PrimaryButton", 46);
        var delete = PosUi.Button("Xóa voucher", (_, _) => Delete(), "DangerButton", 40);
        var newBtn = PosUi.Button("+ Voucher mới", (_, _) => { _grid.SelectedItem = null; Load(new Voucher()); _id = null; }, minHeight: 44);
        var form = new StackPanel
        {
            Children =
            {
                _selected, PosUi.Text("Mã (khách đọc cho nhân viên)"), _code, PosUi.Text("Mô tả"), _description,
                PosUi.Text("Loại"), _type, PosUi.Text("Giá trị (đồng hoặc %)"), _value, _maxRow,
                PosUi.Text("Tạm tính tối thiểu (đồng)"), _min,
                FormUi.Pair("Từ ngày", _from, "Đến hết ngày", _to),
                PosUi.Text("Số lượt dùng tối đa (0 = không giới hạn)"), _limit, _active, save, new Border { Height = 10 }, delete
            }
        };
        var side = FormUi.Card(form, 380);
        side.Margin = new Thickness(14, 0, 0, 0);
        DockPanel.SetDock(side, Dock.Right);
        var left = new DockPanel();
        DockPanel.SetDock(newBtn, Dock.Top);
        newBtn.HorizontalAlignment = HorizontalAlignment.Left;
        left.Children.Add(newBtn); left.Children.Add(_grid);
        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 10), Children = { PosUi.Text("Voucher", 24, true), PosUi.Text("Nhân viên nhập mã ở màn bán hàng. Voucher tính sau khuyến mãi tự động, không áp dụng cho đơn app giao hàng. Hủy đơn sẽ trả lại lượt dùng.", 13, color: PosUi.Muted) } };
        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head); root.Children.Add(new DockPanel { Children = { side, left } });
        Content = root;
        Loaded += (_, _) => { Refresh(); Load(new Voucher()); _id = null; };
    }

    private void Refresh() { _grid.ItemsSource = _vouchers.Vouchers.OrderByDescending(v => v.IsActive).ThenBy(v => v.Code).ToList(); _grid.SelectedItem = _vouchers.Vouchers.FirstOrDefault(v => v.Id == _id); }

    private void Load(Voucher v)
    {
        _id = v.Id; _selected.Text = string.IsNullOrEmpty(v.Code) ? "Voucher mới" : $"{v.Code} · {v.UsageLabel}";
        _code.Text = v.Code; _description.Text = v.Description; _type.SelectedItem = v.Type; _value.Text = v.Value.ToString("0");
        _max.Text = v.MaxDiscount.ToString("0"); _min.Text = v.MinSubtotal.ToString("0"); _from.SelectedDate = v.ValidFrom; _to.SelectedDate = v.ValidTo;
        _limit.Text = v.UsageLimit.ToString(); _active.IsChecked = v.IsActive;
    }

    private void Save()
    {
        try
        {
            if (!decimal.TryParse(_value.Text, out var value)) throw FieldError.Mark(_value, "Nhập giá trị voucher.");
            if (!decimal.TryParse(string.IsNullOrWhiteSpace(_max.Text) ? "0" : _max.Text, out var max)) throw FieldError.Mark(_max, "Nhập số tiền hợp lệ.");
            if (!decimal.TryParse(string.IsNullOrWhiteSpace(_min.Text) ? "0" : _min.Text, out var min)) throw FieldError.Mark(_min, "Nhập số tiền hợp lệ.");
            if (!int.TryParse(string.IsNullOrWhiteSpace(_limit.Text) ? "0" : _limit.Text, out var limit)) throw FieldError.Mark(_limit, "Nhập số lượt là số nguyên.");
            if (_from.SelectedDate is not DateTime from || _to.SelectedDate is not DateTime to) throw new InvalidOperationException("Chọn ngày bắt đầu và kết thúc.");
            var id = _id ?? Guid.NewGuid().ToString();
            _vouchers.SaveVoucher(new Voucher { Id = id, Code = _code.Text, Description = _description.Text, Type = _type.SelectedItem as string ?? Voucher.Amount, Value = value, MaxDiscount = max, MinSubtotal = min, ValidFrom = from, ValidTo = to, UsageLimit = limit, IsActive = _active.IsChecked == true });
            _id = id; Refresh(); Toast.Show(Embedded.HostOf(this), "Đã lưu voucher.");
        }
        catch (InvalidOperationException ex) { Toast.Show(Embedded.HostOf(this), ex.Message, true); }
    }

    private void Delete()
    {
        if (_id is null || _vouchers.Vouchers.All(v => v.Id != _id)) { Toast.Show(Embedded.HostOf(this), "Chọn voucher trước.", true); return; }
        if (MessageBox.Show(Embedded.HostOf(this), $"Xóa voucher {_code.Text}?", "Xóa voucher", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { _vouchers.DeleteVoucher(_id); _id = null; Refresh(); Load(new Voucher()); _id = null; Toast.Show(Embedded.HostOf(this), "Đã xóa voucher."); }
        catch (InvalidOperationException ex) { Toast.Show(Embedded.HostOf(this), ex.Message, true); }
    }
}

// Suppliers and purchase receipts: receiving goods raises stock and records the unit cost.
public sealed class PurchasingWindow : Window
{
    private record StockOption(string Type, string Id, string Name, string Unit)
    {
        public string Label => $"{Type} · {Name} ({Unit})";
    }

    private readonly IPurchasingService _purchasing;
    private readonly IInventoryService _inventory;
    private readonly DataGrid _suppliers = FormUi.Grid("Chưa có nhà cung cấp");
    private readonly DataGrid _lines = FormUi.Grid("Chưa có dòng hàng — chọn mặt hàng, số lượng, đơn giá rồi bấm Thêm dòng", 220);
    private readonly DataGrid _history = FormUi.Grid("Chưa có phiếu nhập");
    private readonly ComboBox _supplier = new() { DisplayMemberPath = "Name", Margin = new Thickness(0, 4, 0, 8), MinHeight = 36 };
    private readonly ComboBox _item = new() { DisplayMemberPath = "Label", Margin = new Thickness(0, 4, 0, 8), MinHeight = 36 };
    private readonly TextBox _qty = FormUi.Box(true), _cost = FormUi.Box(true), _note = FormUi.Box();
    private readonly TextBox _sName = FormUi.Box(), _sPhone = FormUi.Box(true), _sAddress = FormUi.Box(), _sNote = FormUi.Box();
    private readonly CheckBox _sActive = new() { Content = "Đang giao dịch", Margin = new Thickness(0, 0, 0, 8) };
    private readonly CheckBox _paid = new() { Content = "Trả bằng tiền mặt trong ngăn kéo (ghi phiếu chi của ca)", Margin = new Thickness(0, 4, 0, 8) };
    private readonly TextBlock _total = PosUi.Text("", 18, true, PosUi.Accent);
    private readonly ObservableCollection<PurchaseLine> _draft = new();
    private string _receiptId = Guid.NewGuid().ToString();
    private string? _supplierId;

    public PurchasingWindow(IPurchasingService purchasing, IInventoryService inventory, IAuthService auth)
    {
        _purchasing = purchasing;
        _inventory = inventory;
        auth.RequireManager();
        Title = "Nhập hàng"; Width = 1180; Height = 800; MinWidth = 920; MinHeight = 600; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FormUi.Style(this);
        TouchInput.SetNumeric(_sPhone, true);
        IngredientWindow.AddColumns(_lines, ("Loại", "ItemType", "", false), ("Mặt hàng", "ItemName", "", true), ("Số lượng", "Quantity", "0.##", false), ("ĐVT", "Unit", "", false), ("Đơn giá", "UnitCost", "N0", false), ("Thành tiền", "LineTotal", "N0", false));
        _lines.ItemsSource = _draft;
        _draft.CollectionChanged += (_, _) => _total.Text = $"Tổng phiếu: {Math.Round(_draft.Sum(l => l.LineTotal), 0, MidpointRounding.AwayFromZero):N0}đ";
        IngredientWindow.AddColumns(_history, ("Số phiếu", "Code", "", false), ("Ngày", "CreatedAt", "dd/MM/yyyy HH:mm", false), ("Nhà cung cấp", "SupplierName", "", false), ("Mặt hàng", "ItemsLabel", "", true), ("Tổng tiền", "TotalAmount", "N0", false), ("Người nhập", "Operator", "", false));
        _history.Columns.Add(new DataGridCheckBoxColumn { Header = "Chi từ ngăn kéo", Binding = new System.Windows.Data.Binding("PaidFromDrawer"), IsReadOnly = true });
        IngredientWindow.AddColumns(_suppliers, ("Tên", "Name", "", true), ("Điện thoại", "Phone", "", false), ("Địa chỉ", "Address", "", false));
        _suppliers.SelectionChanged += (_, _) => LoadSupplier(_suppliers.SelectedItem as Supplier);
        _item.SelectionChanged += (_, _) =>
        {
            if (_item.SelectedItem is not StockOption o) return;
            TouchInput.SetUnit(_qty, o.Unit);
            decimal last = o.Type == PurchaseLine.Ingredient ? _inventory.Ingredients.FirstOrDefault(i => i.Id == o.Id)?.UnitCost ?? 0 : _inventory.PackagingInventory.FirstOrDefault(p => p.Id == o.Id)?.UnitCost ?? 0;
            if (last > 0) _cost.Text = last.ToString("0");
        };

        var addLine = PosUi.Button("Thêm dòng", (_, _) => AddLine(), minHeight: 44);
        var removeLine = PosUi.Button("Xóa dòng chọn", (_, _) => { if (_lines.SelectedItem is PurchaseLine l) _draft.Remove(l); }, "DangerButton", 44);
        var saveReceipt = PosUi.Button("Lưu phiếu nhập", (_, _) => SaveReceipt(), "PrimaryButton", 52);
        var receiptForm = new DockPanel();
        var receiptTop = new StackPanel
        {
            Children =
            {
                PosUi.Text("Nhà cung cấp"), _supplier,
                new Grid { ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) }, new ColumnDefinition(), new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } },
            }
        };
        var row = (Grid)receiptTop.Children[2];
        void Cell(UIElement e, int col) { Grid.SetColumn(e, col); row.Children.Add(e); }
        Cell(new StackPanel { Margin = new Thickness(0, 0, 6, 0), Children = { PosUi.Text("Mặt hàng"), _item } }, 0);
        Cell(new StackPanel { Margin = new Thickness(0, 0, 6, 0), Children = { PosUi.Text("Số lượng"), _qty } }, 1);
        Cell(new StackPanel { Margin = new Thickness(0, 0, 6, 0), Children = { PosUi.Text("Đơn giá (đồng)"), _cost } }, 2);
        addLine.VerticalAlignment = VerticalAlignment.Bottom; addLine.Margin = new Thickness(0, 0, 0, 8);
        Cell(addLine, 3);
        var receiptBottom = new StackPanel { Children = { new DockPanel { Children = { removeLine, _total } }, PosUi.Text("Ghi chú (số hóa đơn NCC…)"), _note, _paid, saveReceipt } };
        DockPanel.SetDock(removeLine, Dock.Right);
        DockPanel.SetDock(receiptTop, Dock.Top); DockPanel.SetDock(receiptBottom, Dock.Bottom);
        receiptForm.Children.Add(receiptTop); receiptForm.Children.Add(receiptBottom); receiptForm.Children.Add(_lines);

        var saveSupplier = PosUi.Button("Lưu nhà cung cấp", (_, _) => SaveSupplier(), "PrimaryButton", 44);
        var deleteSupplier = PosUi.Button("Xóa nhà cung cấp", (_, _) => DeleteSupplier(), "DangerButton", 40);
        var newSupplier = PosUi.Button("+ Nhà cung cấp mới", (_, _) => { _suppliers.SelectedItem = null; LoadSupplier(null); }, minHeight: 44);
        var supplierForm = FormUi.Card(new StackPanel { Children = { PosUi.Text("Tên"), _sName, PosUi.Text("Điện thoại"), _sPhone, PosUi.Text("Địa chỉ"), _sAddress, PosUi.Text("Ghi chú"), _sNote, _sActive, saveSupplier, new Border { Height = 10 }, deleteSupplier } }, 360);
        supplierForm.Margin = new Thickness(14, 0, 0, 0);
        DockPanel.SetDock(supplierForm, Dock.Right); DockPanel.SetDock(newSupplier, Dock.Top);
        newSupplier.HorizontalAlignment = HorizontalAlignment.Left;
        var supplierTab = new DockPanel { Children = { supplierForm, newSupplier, _suppliers } };

        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Phiếu nhập mới", Content = new Border { Padding = new Thickness(0, 12, 0, 0), Child = receiptForm } },
                new TabItem { Header = "Lịch sử nhập", Content = new Border { Padding = new Thickness(0, 12, 0, 0), Child = _history } },
                new TabItem { Header = "Nhà cung cấp", Content = new Border { Padding = new Thickness(0, 12, 0, 0), Child = supplierTab } }
            }
        };
        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 10), Children = { PosUi.Text("Nhập hàng", 24, true), PosUi.Text("Phiếu nhập cộng tồn nguyên liệu/bao bì, cập nhật đơn giá vốn (dùng để tính giá trị hao hụt) và có thể chi tiền từ ngăn kéo.", 13, color: PosUi.Muted) } };
        var root = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head); root.Children.Add(tabs);
        Content = root;
        Loaded += (_, _) => { Refresh(); LoadSupplier(null); };
    }

    private void Refresh()
    {
        _suppliers.ItemsSource = _purchasing.Suppliers.OrderByDescending(s => s.IsActive).ThenBy(s => s.Name).ToList();
        _supplier.ItemsSource = _purchasing.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
        _item.ItemsSource = _inventory.Ingredients.OrderBy(i => i.Name).Select(i => new StockOption(PurchaseLine.Ingredient, i.Id, i.Name, i.Unit))
            .Concat(_inventory.PackagingInventory.OrderBy(p => p.Name).Select(p => new StockOption(PurchaseLine.Packaging, p.Id, p.Name, p.Unit))).ToList();
        _history.ItemsSource = _purchasing.PurchaseReceipts.OrderByDescending(r => r.CreatedAt).ToList();
        _total.Text = $"Tổng phiếu: {Math.Round(_draft.Sum(l => l.LineTotal), 0, MidpointRounding.AwayFromZero):N0}đ";
    }

    private void AddLine()
    {
        try
        {
            if (_item.SelectedItem is not StockOption o) throw new InvalidOperationException("Chọn mặt hàng.");
            if (!decimal.TryParse(_qty.Text, out var qty) || qty <= 0) throw FieldError.Mark(_qty, "Số lượng phải lớn hơn 0.");
            if (!decimal.TryParse(_cost.Text, out var cost) || cost < 0 || cost != decimal.Truncate(cost)) throw FieldError.Mark(_cost, "Đơn giá là số nguyên không âm.");
            if (_draft.Any(l => l.ItemType == o.Type && l.ItemId == o.Id)) throw new InvalidOperationException("Mặt hàng đã có trên phiếu, hãy xóa dòng cũ để sửa.");
            _draft.Add(new PurchaseLine { ItemType = o.Type, ItemId = o.Id, ItemName = o.Name, Unit = o.Unit, Quantity = qty, UnitCost = cost });
            _qty.Clear(); _cost.Clear(); _item.SelectedItem = null;
        }
        catch (InvalidOperationException ex) { Toast.Show(Embedded.HostOf(this), ex.Message, true); }
    }

    private void SaveReceipt()
    {
        try
        {
            if (_supplier.SelectedItem is not Supplier s) throw new InvalidOperationException("Chọn nhà cung cấp.");
            var receipt = _purchasing.CreatePurchaseReceipt(_receiptId, s.Id, _draft.ToList(), _note.Text, _paid.IsChecked == true);
            _receiptId = Guid.NewGuid().ToString();
            _draft.Clear(); _note.Clear(); _paid.IsChecked = false;
            Refresh();
            Toast.Show(Embedded.HostOf(this), $"Đã lưu phiếu {receipt.Code}: {receipt.TotalAmount:N0}đ.");
        }
        catch (InvalidOperationException ex) { Toast.Show(Embedded.HostOf(this), ex.Message, true); }
    }

    private void LoadSupplier(Supplier? s)
    {
        _supplierId = s?.Id;
        _sName.Text = s?.Name ?? ""; _sPhone.Text = s?.Phone ?? ""; _sAddress.Text = s?.Address ?? ""; _sNote.Text = s?.Note ?? ""; _sActive.IsChecked = s?.IsActive ?? true;
    }

    private void SaveSupplier()
    {
        try
        {
            var id = _supplierId ?? Guid.NewGuid().ToString();
            _purchasing.SaveSupplier(new Supplier { Id = id, Name = _sName.Text, Phone = _sPhone.Text, Address = _sAddress.Text, Note = _sNote.Text, IsActive = _sActive.IsChecked == true });
            _supplierId = id; Refresh(); _suppliers.SelectedItem = _purchasing.Suppliers.FirstOrDefault(x => x.Id == id); Toast.Show(Embedded.HostOf(this), "Đã lưu nhà cung cấp.");
        }
        catch (InvalidOperationException ex) { Toast.Show(Embedded.HostOf(this), ex.Message, true); }
    }

    private void DeleteSupplier()
    {
        if (_supplierId is null) { Toast.Show(Embedded.HostOf(this), "Chọn nhà cung cấp trước.", true); return; }
        if (MessageBox.Show(Embedded.HostOf(this), $"Xóa nhà cung cấp {_sName.Text}?", "Xóa nhà cung cấp", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try { _purchasing.DeleteSupplier(_supplierId); LoadSupplier(null); Refresh(); Toast.Show(Embedded.HostOf(this), "Đã xóa."); }
        catch (InvalidOperationException ex) { Toast.Show(Embedded.HostOf(this), ex.Message, true); }
    }
}

// Items under their warning threshold and stock lost outside sales over a period.
public sealed class StockReportWindow : Window
{
    private readonly IPurchasingService _purchasing;
    private readonly DataGrid _low = FormUi.Grid("Không có mặt hàng nào dưới ngưỡng cảnh báo");
    private readonly DataGrid _loss = FormUi.Grid("Không có hao hụt trong khoảng ngày này");
    private readonly DatePicker _from = new() { SelectedDate = DateTime.Today.AddDays(-30), Width = 170, Margin = new Thickness(0, 0, 8, 0) }, _to = new() { SelectedDate = DateTime.Today, Width = 170, Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBlock _lossSummary = PosUi.Text("", 15, true, PosUi.Danger), _lowSummary = PosUi.Text("", 15, true);

    public StockReportWindow(IPurchasingService purchasing, IAuthService auth)
    {
        _purchasing = purchasing;
        auth.RequireManager();
        Title = "Cảnh báo tồn kho & hao hụt"; Width = 980; Height = 700; MinWidth = 760; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FormUi.Style(this);
        IngredientWindow.AddColumns(_low, ("Loại", "Kind", "", false), ("Mặt hàng", "Name", "", true), ("Tồn hiện tại", "StockLabel", "", false), ("Ngưỡng cảnh báo", "Threshold", "0.##", false));
        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Triggers.Add(new DataTrigger { Binding = new System.Windows.Data.Binding("IsOut"), Value = true, Setters = { new Setter(BackgroundProperty, Palette.DangerSoft) } });
        _low.RowStyle = rowStyle;
        IngredientWindow.AddColumns(_loss, ("Loại", "Kind", "", false), ("Mặt hàng", "Name", "", true), ("Số lượng mất", "QuantityLabel", "", false), ("Số lần", "Events", "", false), ("Giá trị (đồng)", "Value", "N0", false));
        var view = PosUi.Button("Xem", (_, _) => RefreshLoss(), "PrimaryButton", 40);
        var range = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8), Children = { PosUi.Text("Từ "), _from, PosUi.Text("đến "), _to, view } };
        var lossTab = new DockPanel();
        var lossTop = new StackPanel { Children = { range, PosUi.Text("Gồm kiểm kê thiếu, điều chỉnh giảm và hao hụt ghi nhận (hỏng, đổ, hết hạn). Giá trị tính theo đơn giá phiếu nhập gần nhất.", 13, color: PosUi.Muted), _lossSummary } };
        DockPanel.SetDock(lossTop, Dock.Top);
        lossTab.Children.Add(lossTop); lossTab.Children.Add(_loss);
        var lowTab = new DockPanel();
        var lowTop = new StackPanel { Children = { _lowSummary, PosUi.Text("Đỏ: đã hết. Nhập hàng ở Quản trị → Nhập hàng, chỉnh ngưỡng ở Kho nguyên liệu / Kho bao bì.", 13, color: PosUi.Muted) } };
        DockPanel.SetDock(lowTop, Dock.Top);
        lowTab.Children.Add(lowTop); lowTab.Children.Add(_low);
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Sắp hết hàng", Content = new Border { Padding = new Thickness(0, 12, 0, 0), Child = lowTab } },
                new TabItem { Header = "Hao hụt", Content = new Border { Padding = new Thickness(0, 12, 0, 0), Child = lossTab } }
            }
        };
        var root = new DockPanel { Margin = new Thickness(18) };
        var head = PosUi.Text("Cảnh báo tồn kho & hao hụt", 24, true);
        DockPanel.SetDock(head, Dock.Top);
        root.Children.Add(head); root.Children.Add(tabs);
        Content = root;
        Loaded += (_, _) =>
        {
            var low = _purchasing.LowStockItems();
            _low.ItemsSource = low;
            _lowSummary.Text = low.Count == 0 ? "Tồn kho ổn định." : $"{low.Count} mặt hàng chạm ngưỡng, {low.Count(i => i.IsOut)} đã hết.";
            RefreshLoss();
        };
    }

    private void RefreshLoss()
    {
        try
        {
            if (_from.SelectedDate is not DateTime from || _to.SelectedDate is not DateTime to) throw new InvalidOperationException("Chọn khoảng ngày.");
            if (from > to) throw new InvalidOperationException("Ngày bắt đầu phải trước ngày kết thúc.");
            var rows = _purchasing.ShrinkageReport(from, to);
            _loss.ItemsSource = rows;
            _lossSummary.Text = $"Tổng giá trị hao hụt: {rows.Sum(r => r.Value):N0}đ ({rows.Count} mặt hàng)";
        }
        catch (InvalidOperationException ex) { Toast.Show(Embedded.HostOf(this), ex.Message, true); }
    }
}
