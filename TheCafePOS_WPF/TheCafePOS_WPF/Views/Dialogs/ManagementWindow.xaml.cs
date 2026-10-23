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
    private string? _productId, _categoryId;
    private string _productImageUrl = "";
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
        var bankSettings = _bank.Settings;
        BankCode.Text = bankSettings.BankId; BankAccount.Text = bankSettings.AccountNo; BankOwner.Text = bankSettings.AccountName; LoadPromoAndBackup(); SePayToken.Password = bankSettings.SePayToken;
    }
    private void Refresh()
    {
        FilterProducts();
        Categories.ItemsSource = _catalog.Categories.OrderBy(c => c.DisplayOrder).ToList();
        // Rebinding drops the selection; restore it so a later save keeps the product's category.
        var categoryId = ProductCategory.SelectedValue;
        ProductCategory.ItemsSource = _catalog.Categories.ToList();
        ProductCategory.SelectedValue = categoryId;
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
        Products.ItemsSource = _catalog.Products.Where(p => p.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)).OrderBy(p => p.Name).ToList();
    }
    private void MenuSearch_TextChanged(object sender, TextChangedEventArgs e) => FilterProducts();
    private void Run(Action action)
    {
        try { action(); Refresh(); Status.Text = ""; Toast.Show(this, "Đã lưu thành công."); }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void ProductSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Products.SelectedItem is not Product p) return;
        _productId = p.Id; ProductName.Text = p.Name; ProductPrice.Text = p.BasePrice.ToString("0"); ProductCategory.SelectedValue = p.CategoryId; ProductActive.IsChecked = p.IsActive; ProductSoldOut.IsChecked = p.IsSoldOut;
        ShowProductImage(p.ImageUrl);
    }
    private void ShowProductImage(string url)
    {
        _productImageUrl = url;
        ProductImage.Source = Core.ProductImages.Load(url);
        ProductImageHint.Visibility = ProductImage.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }
    private void PickImage_Click(object sender, RoutedEventArgs e)
    {
        string imagesDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Images");
        System.IO.Directory.CreateDirectory(imagesDir);
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Chọn ảnh minh họa", Filter = "Ảnh (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", InitialDirectory = imagesDir };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            // Images already in the bundled folder are referenced directly instead of duplicated.
            // Also matches a pick from the repo's Images folder, which the build copies here under the same name.
            if (System.IO.File.Exists(System.IO.Path.Combine(imagesDir, System.IO.Path.GetFileName(dialog.FileName))))
            {
                ShowProductImage("Images/" + System.IO.Path.GetFileName(dialog.FileName));
                return;
            }
            // Copy next to the bundled images so the product keeps its photo if the original file moves.
            string name = "custom-" + Guid.NewGuid().ToString("N")[..8] + System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(AppContext.BaseDirectory, "Images"));
            System.IO.File.Copy(dialog.FileName, System.IO.Path.Combine(AppContext.BaseDirectory, "Images", name));
            ShowProductImage("Images/" + name);
            if (ProductImage.Source is null) { Status.Text = "Không đọc được file ảnh."; ShowProductImage(""); }
        }
        catch (Exception ex) { Status.Text = "Không thể chép ảnh: " + ex.Message; }
    }
    private void RemoveImage_Click(object sender, RoutedEventArgs e) => ShowProductImage("");
    private void NewProduct_Click(object sender, RoutedEventArgs e)
    {
        Products.SelectedItem = null; _productId = null; ProductName.Clear(); ProductPrice.Clear(); ProductActive.IsChecked = true; ProductSoldOut.IsChecked = false; ShowProductImage("");
    }
    private void SaveProduct_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (string.IsNullOrWhiteSpace(ProductName.Text)) throw FieldError.Mark(ProductName, "Nhập tên món.");
        if (!decimal.TryParse(ProductPrice.Text, out var price) || price < 0) throw FieldError.Mark(ProductPrice, "Giá phải là số không âm.");
        if (ProductCategory.SelectedValue is null) throw FieldError.Mark(ProductCategory, "Chọn danh mục cho món.");
        var existing = _catalog.Products.FirstOrDefault(p => p.Id == _productId);
        var product = existing is null ? new Product() : System.Text.Json.JsonSerializer.Deserialize<Product>(System.Text.Json.JsonSerializer.Serialize(existing))!;
        product.Name = ProductName.Text.Trim(); product.BasePrice = price; product.CategoryId = ProductCategory.SelectedValue as string ?? ""; product.IsActive = ProductActive.IsChecked == true; product.IsSoldOut = ProductSoldOut.IsChecked == true; product.ImageUrl = _productImageUrl;
        _catalog.SaveProduct(product);
        NewProduct_Click(sender, e);
    });
    private void CategorySelected(object sender, SelectionChangedEventArgs e)
    {
        if (Categories.SelectedItem is not Category c) return;
        _categoryId = c.Id; CategoryName.Text = c.Name; CategoryActive.IsChecked = c.IsActive;
    }
    private void ConfigureProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_catalog.Products.FirstOrDefault(p => p.Id == _productId) is not Product product) { Status.Text = "Lưu và chọn món trước khi cấu hình."; return; }
        _dialogs.Create<ProductConfigurationWindow>(this, product).ShowDialog(); Refresh();
    }
    private void NewCategory_Click(object sender, RoutedEventArgs e) { Categories.SelectedItem = null; _categoryId = null; CategoryName.Clear(); CategoryActive.IsChecked = true; }
    private void SaveCategory_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (string.IsNullOrWhiteSpace(CategoryName.Text)) throw FieldError.Mark(CategoryName, "Nhập tên danh mục.");
        _catalog.SaveCategory(new Category { Id = _categoryId ?? Guid.NewGuid().ToString(), Name = CategoryName.Text.Trim(), IsActive = CategoryActive.IsChecked == true, DisplayOrder = _catalog.Categories.FirstOrDefault(c => c.Id == _categoryId)?.DisplayOrder ?? _catalog.Categories.Count + 1 });
        NewCategory_Click(sender, e);
    });
    private void StaffSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount acc) return;
        StaffName.Text = acc.Username;
        StaffName.IsReadOnly = true;
        StaffPassword.Clear();
        StaffRole.SelectedIndex = acc.Role == "Manager" ? 1 : 0;
        StaffRole.IsEnabled = acc.Role != "Owner";
        SaveStaff.Content = "Cập nhật tài khoản";
    }
    private void NewStaff_Click(object sender, RoutedEventArgs e)
    {
        Staff.SelectedItem = null;
        StaffName.Clear();
        StaffName.IsReadOnly = false;
        StaffPassword.Clear();
        StaffRole.SelectedIndex = 0;
        StaffRole.IsEnabled = true;
        SaveStaff.Content = "Tạo tài khoản";
    }
    private void CreateStaff_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        var role = ((ComboBoxItem)StaffRole.SelectedItem).Content.ToString()!;
        if (Staff.SelectedItem is null && string.IsNullOrWhiteSpace(StaffName.Text)) throw FieldError.Mark(StaffName, "Nhập tên đăng nhập.");
        if ((Staff.SelectedItem is null || StaffPassword.Password.Length > 0) && StaffPassword.Password.Length < 8) throw FieldError.Mark(StaffPassword, "Mật khẩu tối thiểu 8 ký tự.");
        if (Staff.SelectedItem is StaffAccount acc)
        {
            if (!string.IsNullOrEmpty(StaffPassword.Password))
                _auth.ResetPassword(acc.Username, StaffPassword.Password);
            if (acc.Role != "Owner") _auth.UpdateAccountRole(acc.Username, role);
        }
        else
        {
            _auth.CreateAccount(StaffName.Text, StaffPassword.Password, role);
        }
        NewStaff_Click(sender, e);
    });
    private void DeleteStaff_Click(object sender, RoutedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount acc) { Status.Text = "Chọn nhân viên trước."; return; }
        if (ConfirmDelete($"Xóa tài khoản {acc.Username}? Lịch sử đơn và ca vẫn được giữ.", "Xóa tài khoản"))
            Run(() => { _auth.DeleteAccount(acc.Username); NewStaff_Click(sender, e); });
    }
    private void DeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_productId is not string productId) { Status.Text = "Chọn món trước."; return; }
        if (ConfirmDelete($"Xóa vĩnh viễn món {ProductName.Text}? Nếu chỉ tạm ngưng, hãy bỏ chọn \"Đang bán\".", "Xóa món"))
            Run(() => { _catalog.DeleteProduct(productId); NewProduct_Click(sender, e); });
    }
    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_categoryId is not string categoryId) { Status.Text = "Chọn danh mục trước."; return; }
        int count = _catalog.Products.Count(p => p.CategoryId == categoryId);
        string message = count == 0 ? $"Xóa danh mục {CategoryName.Text}?" : $"Danh mục {CategoryName.Text} đang có {count} món. Xóa luôn cả {count} món này?\nLịch sử đơn và báo cáo vẫn được giữ. Nếu chỉ muốn tạm ẩn, hãy bỏ chọn \"Đang hiển thị\".";
        if (ConfirmDelete(message, "Xóa danh mục"))
            Run(() => { _catalog.DeleteCategory(categoryId, deleteProducts: true); NewCategory_Click(sender, e); });
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
    private void OpenIngredients_Click(object sender, RoutedEventArgs e) { _dialogs.Create<IngredientWindow>(this).ShowDialog(); Refresh(); }
    private void Recipe_Click(object sender, RoutedEventArgs e)
    {
        if (_catalog.Products.FirstOrDefault(p => p.Id == _productId) is not Product product) { Status.Text = "Lưu và chọn món trước khi khai báo công thức."; return; }
        if (_dialogs.Create<RecipeWindow>(this, product).ShowDialog() == true) { Refresh(); Toast.Show(this, "Đã lưu công thức."); }
    }
    private void OpenStoreSettings_Click(object sender, RoutedEventArgs e) { if (_dialogs.Create<StoreSettingsWindow>(this).ShowDialog() == true) Toast.Show(this, "Đã lưu cài đặt cửa hàng."); }
    private void OpenPromotions_Click(object sender, RoutedEventArgs e) => _dialogs.Create<PromotionsWindow>(this).ShowDialog();
    private void OpenVouchers_Click(object sender, RoutedEventArgs e) => _dialogs.Create<VouchersWindow>(this).ShowDialog();
    private void OpenPurchasing_Click(object sender, RoutedEventArgs e) { _dialogs.Create<PurchasingWindow>(this).ShowDialog(); Refresh(); }
    private void OpenStockReport_Click(object sender, RoutedEventArgs e) => _dialogs.Create<StockReportWindow>(this).ShowDialog();
    private void OpenCustomers_Click(object sender, RoutedEventArgs e) { _dialogs.Create<CustomerListWindow>(this).ShowDialog(); RefreshBackupInfo(); }
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
