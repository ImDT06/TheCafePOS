using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class ManagementWindow : Window
{
    private readonly ICatalogService _catalog;
    private readonly ICustomerService _customers;
    private readonly IAuthService _auth;
    private readonly IDialogFactory _dialogs;
    private readonly IVietQRService _bank;
    private readonly IBackupService _backup;
    public ManagementWindow(ICatalogService catalog, ICustomerService customers, IAuthService auth, IDialogFactory dialogs, IVietQRService bank, IBackupService backup)
    {
        _catalog = catalog;
        _customers = customers;
        _auth = auth;
        _dialogs = dialogs;
        _bank = bank;
        _backup = backup;
        _auth.RequireManager();
        InitializeComponent(); Refresh();
        // Tools open inside their tabs. Built without an owner: this window is not shown yet.
        IngredientsHost.Content = Embed<IngredientWindow>();
        var packaging = _dialogs.Create<PackagingStatusModal>(null);
        packaging.BtnClose.Visibility = Visibility.Collapsed;
        PackagingHost.Content = Embedded.Take(packaging, this);
        PurchasingHost.Content = Embed<PurchasingWindow>();
        StockReportHost.Content = Embed<StockReportWindow>();
        PromotionsHost.Content = Embed<PromotionsWindow>();
        VouchersHost.Content = Embed<VouchersWindow>();
        CustomersHost.Content = Embed<CustomerListWindow>();
        StoreSettingsHost.Content = Embed<StoreSettingsWindow>();
        // Stock, recipes and member points change inside those tools; the menu list and member summary follow.
        CustomersHost.Unloaded += (_, _) => RefreshBackupInfo();
        IngredientsHost.Unloaded += (_, _) => Refresh();
        var bankSettings = _bank.Settings;
        BankCode.Text = bankSettings.BankId; BankAccount.Text = bankSettings.AccountNo; BankOwner.Text = bankSettings.AccountName; LoadPromoAndBackup(); SePayToken.Password = bankSettings.SePayToken;
    }
    private const string AllCategories = "";
    private FrameworkElement Embed<T>() where T : Window => Embedded.Take(_dialogs.Create<T>(null), this);

    private void Refresh()
    {
        var filterId = MenuCategoryFilter.SelectedValue as string ?? AllCategories;
        MenuCategoryFilter.ItemsSource = new[] { new Category { Id = AllCategories, Name = "Tất cả danh mục" } }.Concat(_catalog.Categories.OrderBy(c => c.DisplayOrder)).ToList();
        MenuCategoryFilter.SelectedValue = filterId;
        if (MenuCategoryFilter.SelectedItem is null) MenuCategoryFilter.SelectedIndex = 0;
        FilterProducts();
        Categories.ItemsSource = _catalog.Categories.OrderBy(c => c.DisplayOrder)
            .Select((c, i) => new CategoryAdminRow(c, i + 1, _catalog.Products.Count(p => p.CategoryId == c.Id))).ToList();
        var staffName = (Staff.SelectedItem as StaffAccount)?.Username;
        Staff.ItemsSource = _auth.Accounts.ToList();
        Staff.SelectedItem = _auth.Accounts.FirstOrDefault(a => a.Username == staffName);
        if (Staff.SelectedItem is null && StaffName.IsReadOnly) NewStaff_Click(this, new RoutedEventArgs());
    }
    private bool ConfirmDelete(string message, string title) =>
        MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    private void FilterProducts()
    {
        if (Products == null) return;
        string keyword = MenuSearch?.Text.Trim() ?? "";
        string categoryId = MenuCategoryFilter?.SelectedValue as string ?? AllCategories;
        var categories = _catalog.Categories.ToDictionary(c => c.Id);
        // Grouped like the till's chips, so each category reads as one block.
        Products.ItemsSource = _catalog.Products
            .Where(p => p.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) && (categoryId == AllCategories || p.CategoryId == categoryId))
            .OrderBy(p => categories.TryGetValue(p.CategoryId, out var c) ? c.DisplayOrder : int.MaxValue).ThenBy(p => p.Name)
            .Select(p => new ProductAdminRow(p, categories.TryGetValue(p.CategoryId, out var c) ? c.Name : "—"))
            .ToList();
    }
    private void MenuSearch_TextChanged(object sender, TextChangedEventArgs e) => FilterProducts();
    private void MenuCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => FilterProducts();
    private void Run(Action action)
    {
        try { action(); Refresh(); Status.Text = ""; Toast.Show(this, "Đã lưu thành công."); }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void NewCategory_Click(object sender, RoutedEventArgs e) => EditCategory("");
    private void EditCategory(string categoryId) => ShowEditor(CategoryEditorHost, CategoryPlaceholder, new CategoryEditorPanel(categoryId, _catalog));
    private void CategoryRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: CategoryAdminRow row }) EditCategory(row.Category.Id);
    }
    private void CategoryRow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter || sender is not DataGridRow { Item: CategoryAdminRow row }) return;
        e.Handled = true;
        EditCategory(row.Category.Id);
    }
    private void EditCategoryMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CategoryAdminRow row }) EditCategory(row.Category.Id);
    }
    private void ToggleCategoryMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: CategoryAdminRow row }) return;
        var category = System.Text.Json.JsonSerializer.Deserialize<Category>(System.Text.Json.JsonSerializer.Serialize(row.Category))!;
        category.IsActive = !category.IsActive;
        Run(() => _catalog.SaveCategory(category));
    }
    private void NewProduct_Click(object sender, RoutedEventArgs e) => EditProduct("");
    private void EditProduct(string productId) => ShowEditor(ProductEditorHost, ProductPlaceholder, new ProductEditorPanel(productId, _catalog, _dialogs));
    // The editor replaces the add prompt in the side column; closing it brings the prompt back.
    private void ShowEditor(ContentControl host, FrameworkElement placeholder, EditorPanel editor)
    {
        editor.Done += changed =>
        {
            if (host.Content != editor) return;
            host.Content = null; placeholder.Visibility = Visibility.Visible;
            Refresh(); // also after a cancel: sizes or a recipe may have been saved from inside the editor
            if (changed) { Status.Text = ""; Toast.Show(this, "Đã lưu thành công."); }
        };
        host.Content = editor; placeholder.Visibility = Visibility.Collapsed;
    }
    private void ProductRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is DataGridRow { Item: ProductAdminRow row }) EditProduct(row.Product.Id);
    }
    private void ProductRow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter || sender is not DataGridRow { Item: ProductAdminRow row }) return;
        e.Handled = true;
        EditProduct(row.Product.Id);
    }
    private void EditProductMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProductAdminRow row }) EditProduct(row.Product.Id);
    }
    private void ToggleSoldOutMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProductAdminRow row }) Run(() => _catalog.SetProductSoldOut(row.Product.Id, !row.Product.IsSoldOut));
    }
    private void StaffSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount acc) return;
        StaffName.Text = acc.Username;
        StaffName.IsReadOnly = true;
        StaffPassword.Clear();
        StaffPin.Clear();
        SaveStaff.Content = "Cập nhật tài khoản";
    }
    private void NewStaff_Click(object sender, RoutedEventArgs e)
    {
        Staff.SelectedItem = null;
        StaffName.Clear();
        StaffName.IsReadOnly = false;
        StaffPassword.Clear();
        StaffPin.Clear();
        SaveStaff.Content = "Tạo tài khoản";
    }
    private void CreateStaff_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (Staff.SelectedItem is null && string.IsNullOrWhiteSpace(StaffName.Text)) throw FieldError.Mark(StaffName, "Nhập tên đăng nhập.");
        if ((Staff.SelectedItem is null || StaffPassword.Password.Length > 0) && StaffPassword.Password.Length < 8) throw FieldError.Mark(StaffPassword, "Mật khẩu tối thiểu 8 ký tự.");
        if (StaffPin.Password.Length > 0 && !AuthService.IsValidPin(StaffPin.Password)) throw FieldError.Mark(StaffPin, "Mã PIN gồm 4–6 chữ số.");
        if (Staff.SelectedItem is StaffAccount acc)
        {
            if (!string.IsNullOrEmpty(StaffPassword.Password))
                _auth.ResetPassword(acc.Username, StaffPassword.Password);
        }
        else
        {
            _auth.CreateAccount(StaffName.Text, StaffPassword.Password, "Cashier");
        }
        if (StaffPin.Password.Length > 0) _auth.SetPin((Staff.SelectedItem as StaffAccount)?.Username ?? StaffName.Text.Trim(), StaffPin.Password);
        NewStaff_Click(sender, e);
    });
    private void DeleteStaff_Click(object sender, RoutedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount acc) { Status.Text = "Chọn nhân viên trước."; return; }
        if (ConfirmDelete($"Xóa tài khoản {acc.Username}? Lịch sử đơn và ca vẫn được giữ.", "Xóa tài khoản"))
            Run(() => { _auth.DeleteAccount(acc.Username); NewStaff_Click(sender, e); });
    }
    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount account) { Status.Text = "Chọn nhân viên trước."; return; }
        _dialogs.Create<PasswordWindow>(this, account.Username).ShowDialog(); Refresh();
    }
    private void ToggleStaff_Click(object sender, RoutedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount account) { Status.Text = "Chọn nhân viên trước."; return; }
        if (MessageBox.Show(this, $"{(account.IsActive ? "Khóa" : "Mở khóa")} tài khoản {account.Username}?", "Tài khoản", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Run(() => _auth.SetAccountActive(account.Username, !account.IsActive));
    }
    private void LoadPromoAndBackup()
    {
        RefreshBackupInfo();
    }
    private void RefreshBackupInfo()
    {
        BackupInfo.Text = _backup.LastBackup is DateTime last ? $"Lần sao lưu gần nhất: {last:dd/MM/yyyy HH:mm}" : "Chưa có bản sao lưu nào.";
        CloudInfo.Text = CloudText();
        MemberInfo.Text = $"{_customers.Customers.Count} thành viên, {_customers.Customers.Sum(c => c.Points):N0} điểm chưa đổi. Tích 1 điểm / 10.000đ, đổi 1 điểm = 1.000đ.";
    }
    private string CloudText()
    {
        if (_backup.CloudProblem is { } problem) return "Sao lưu cloud: " + problem;
        var status = _backup.CloudStatus;
        int pending = _backup.PendingUploads;
        string last = status.LastUploadAt is DateTime at ? $"Đã đưa lên Supabase lúc {at:dd/MM/yyyy HH:mm}" : "Chưa đưa bản nào lên Supabase";
        string queue = pending > 0 ? $"; còn {pending} bản chờ tải lên" : "; đã tải lên đủ";
        return last + queue + (status.LastError.Length > 0 ? ". Lỗi gần nhất: " + status.LastError : ".");
    }
    private async void BackupNow_Click(object sender, RoutedEventArgs e)
    {
        try { _backup.BackupNow(); }
        catch (Exception ex) { Status.Text = ex.Message; return; }
        RefreshBackupInfo();
        if (_backup.CloudProblem is not null) { Toast.Show(this, "Đã sao lưu trên máy."); return; }
        BackupNowButton.IsEnabled = false;
        CloudInfo.Text = "Đang đưa lên Supabase…";
        try { await _backup.UploadPendingAsync(); }
        finally { BackupNowButton.IsEnabled = true; }
        RefreshBackupInfo();
        bool uploaded = _backup.PendingUploads == 0;
        Toast.Show(this, uploaded ? "Đã sao lưu và đưa lên Supabase." : "Đã sao lưu trên máy; chưa đưa lên được Supabase.", !uploaded);
    }
    private void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Title = "Chọn bản sao lưu để khôi phục", Filter = "Bản sao lưu TheCafePOS (*.db)|*.db", InitialDirectory = _backup.Folder };
        if (picker.ShowDialog(this) != true) return;
        var confirm = MessageBox.Show(this,
            $"Thay toàn bộ dữ liệu hiện tại bằng bản sao lưu:\n{System.IO.Path.GetFileName(picker.FileName)}\n\nDữ liệu hiện tại sẽ được giữ lại thành một bản sao lưu an toàn. Ứng dụng sẽ đóng sau khi khôi phục.",
            "Khôi phục dữ liệu", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes) return;
        string safetyCopy;
        try { safetyCopy = _backup.Restore(picker.FileName); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không khôi phục được"); return; }
        MessageBox.Show(this, $"Đã khôi phục dữ liệu. Bản dữ liệu trước đó được giữ tại:\n{safetyCopy}\n\nHãy mở lại TheCafePOS.", "Khôi phục xong");
        // Every service still holds the old data in memory; exiting now (without the usual save-on-close) keeps the restored file intact.
        Environment.Exit(0);
    }
    private void OpenBackups_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(_backup.Folder);
        System.Diagnostics.Process.Start("explorer.exe", _backup.Folder);
    }
    private void SaveBank_Click(object sender, RoutedEventArgs e) => Run(() => _bank.SaveSettings(new BankSettings { BankId = BankCode.Text.Trim(), AccountNo = BankAccount.Text.Trim(), AccountName = BankOwner.Text.Trim(), SePayToken = SePayToken.Password.Trim() }));
}

/// <summary>One row of the menu list in the management window.</summary>
public sealed record ProductAdminRow(Product Product, string CategoryName)
{
    public string Name => Product.Name;
    public string ImageUrl => Product.ImageUrl;
    public string FormattedPrice => Product.FormattedPrice;
    public string Status => !Product.IsActive ? "Ẩn" : Product.IsSoldOut ? "Hết hàng" : "Đang bán";
    public string SoldOutMenuText => Product.IsSoldOut ? "Báo có hàng lại" : "Đánh dấu hết hàng";
}

/// <summary>One row of the category list in the management window.</summary>
public sealed record CategoryAdminRow(Category Category, int Position, int Count)
{
    public string Name => Category.Name;
    public string ProductCount => $"{Count} món";
    public string Status => Category.IsActive ? "Đang hiển thị" : "Ẩn";
    public string ToggleMenuText => Category.IsActive ? "Ẩn khỏi màn bán hàng" : "Hiện lại trên màn bán hàng";
}
