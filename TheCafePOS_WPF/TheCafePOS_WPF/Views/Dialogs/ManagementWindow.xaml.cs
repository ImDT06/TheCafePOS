using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class ManagementWindow : Window
{
    private string? _productId, _categoryId;
    private string _productImageUrl = "";
    private readonly DataStoreService _store = DataStoreService.Instance;
    public ManagementWindow()
    {
        AuthService.Instance.RequireManager();
        InitializeComponent(); Refresh();
        var bank = VietQRService.Settings;
        BankCode.Text = bank.BankId; BankAccount.Text = bank.AccountNo; BankOwner.Text = bank.AccountName; LoadPromoAndBackup(); SePayToken.Password = bank.SePayToken;
    }
    private void Refresh()
    {
        FilterProducts();
        Categories.ItemsSource = _store.Categories.OrderBy(c => c.DisplayOrder).ToList();
        // Rebinding drops the selection; restore it so a later save keeps the product's category.
        var categoryId = ProductCategory.SelectedValue;
        ProductCategory.ItemsSource = _store.Categories.ToList();
        ProductCategory.SelectedValue = categoryId;
        var staffName = (Staff.SelectedItem as StaffAccount)?.Username;
        Staff.ItemsSource = AuthService.Instance.Accounts.ToList();
        Staff.SelectedItem = AuthService.Instance.Accounts.FirstOrDefault(a => a.Username == staffName);
        if (Staff.SelectedItem is null && StaffName.IsReadOnly) NewStaff_Click(this, new RoutedEventArgs());
    }
    private bool ConfirmDelete(string message, string title) =>
        MessageBox.Show(this, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    private void FilterProducts()
    {
        if (Products == null) return;
        string keyword = MenuSearch?.Text.Trim() ?? "";
        Products.ItemsSource = _store.Products.Where(p => p.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)).OrderBy(p => p.Name).ToList();
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
        ProductImage.Source = MainWindow.LoadProductImage(url);
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
        var existing = _store.Products.FirstOrDefault(p => p.Id == _productId);
        var product = existing is null ? new Product() : System.Text.Json.JsonSerializer.Deserialize<Product>(System.Text.Json.JsonSerializer.Serialize(existing))!;
        product.Name = ProductName.Text.Trim(); product.BasePrice = price; product.CategoryId = ProductCategory.SelectedValue as string ?? ""; product.IsActive = ProductActive.IsChecked == true; product.IsSoldOut = ProductSoldOut.IsChecked == true; product.ImageUrl = _productImageUrl;
        _store.SaveProduct(product);
        NewProduct_Click(sender, e);
    });
    private void CategorySelected(object sender, SelectionChangedEventArgs e)
    {
        if (Categories.SelectedItem is not Category c) return;
        _categoryId = c.Id; CategoryName.Text = c.Name; CategoryActive.IsChecked = c.IsActive;
    }
    private void ConfigureProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_store.Products.FirstOrDefault(p => p.Id == _productId) is not Product product) { Status.Text = "Lưu và chọn món trước khi cấu hình."; return; }
        new ProductConfigurationWindow(product) { Owner = this }.ShowDialog(); Refresh();
    }
    private void NewCategory_Click(object sender, RoutedEventArgs e) { Categories.SelectedItem = null; _categoryId = null; CategoryName.Clear(); CategoryActive.IsChecked = true; }
    private void SaveCategory_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (string.IsNullOrWhiteSpace(CategoryName.Text)) throw FieldError.Mark(CategoryName, "Nhập tên danh mục.");
        _store.SaveCategory(new Category { Id = _categoryId ?? Guid.NewGuid().ToString(), Name = CategoryName.Text.Trim(), IsActive = CategoryActive.IsChecked == true, DisplayOrder = _store.Categories.FirstOrDefault(c => c.Id == _categoryId)?.DisplayOrder ?? _store.Categories.Count + 1 });
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
                AuthService.Instance.ResetPassword(acc.Username, StaffPassword.Password);
            if (acc.Role != "Owner") AuthService.Instance.UpdateAccountRole(acc.Username, role);
        }
        else
        {
            AuthService.Instance.CreateAccount(StaffName.Text, StaffPassword.Password, role);
        }
        NewStaff_Click(sender, e);
    });
    private void DeleteStaff_Click(object sender, RoutedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount acc) { Status.Text = "Chọn nhân viên trước."; return; }
        if (ConfirmDelete($"Xóa tài khoản {acc.Username}? Lịch sử đơn và ca vẫn được giữ.", "Xóa tài khoản"))
            Run(() => { AuthService.Instance.DeleteAccount(acc.Username); NewStaff_Click(sender, e); });
    }
    private void DeleteProduct_Click(object sender, RoutedEventArgs e)
    {
        if (_productId is not string productId) { Status.Text = "Chọn món trước."; return; }
        if (ConfirmDelete($"Xóa vĩnh viễn món {ProductName.Text}? Nếu chỉ tạm ngưng, hãy bỏ chọn \"Đang bán\".", "Xóa món"))
            Run(() => { _store.DeleteProduct(productId); NewProduct_Click(sender, e); });
    }
    private void DeleteCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_categoryId is not string categoryId) { Status.Text = "Chọn danh mục trước."; return; }
        int count = _store.Products.Count(p => p.CategoryId == categoryId);
        string message = count == 0 ? $"Xóa danh mục {CategoryName.Text}?" : $"Danh mục {CategoryName.Text} đang có {count} món. Xóa luôn cả {count} món này?\nLịch sử đơn và báo cáo vẫn được giữ. Nếu chỉ muốn tạm ẩn, hãy bỏ chọn \"Đang hiển thị\".";
        if (ConfirmDelete(message, "Xóa danh mục"))
            Run(() => { _store.DeleteCategory(categoryId, deleteProducts: true); NewCategory_Click(sender, e); });
    }
    private void ResetPassword_Click(object sender, RoutedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount account) { Status.Text = "Chọn nhân viên trước."; return; }
        new PasswordWindow(account.Username) { Owner = this }.ShowDialog(); Refresh();
    }
    private void ToggleStaff_Click(object sender, RoutedEventArgs e)
    {
        if (Staff.SelectedItem is not StaffAccount account) { Status.Text = "Chọn nhân viên trước."; return; }
        if (MessageBox.Show(this, $"{(account.IsActive ? "Khóa" : "Mở khóa")} tài khoản {account.Username}?", "Tài khoản", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        Run(() => AuthService.Instance.SetAccountActive(account.Username, !account.IsActive));
    }
    private static readonly (DayOfWeek Day, string Label)[] Days = { (DayOfWeek.Monday, "T2"), (DayOfWeek.Tuesday, "T3"), (DayOfWeek.Wednesday, "T4"), (DayOfWeek.Thursday, "T5"), (DayOfWeek.Friday, "T6"), (DayOfWeek.Saturday, "T7"), (DayOfWeek.Sunday, "CN") };
    private void LoadPromoAndBackup()
    {
        var promo = _store.Promotions.FirstOrDefault() ?? new Promotion();
        PromoActive.IsChecked = promo.IsActive; PromoName.Text = promo.Name; PromoPercent.Text = promo.Percent.ToString();
        PromoStart.Text = promo.Start.ToString(@"hh\:mm"); PromoEnd.Text = promo.End.ToString(@"hh\:mm");
        PromoDays.Children.Clear();
        foreach (var (day, label) in Days) PromoDays.Children.Add(new CheckBox { Content = label, Tag = day, IsChecked = promo.Days.Contains(day), Margin = new Thickness(0, 0, 12, 6) });
        RefreshBackupInfo();
    }
    private void RefreshBackupInfo()
    {
        BackupInfo.Text = BackupService.LastBackup is DateTime last ? $"Lần sao lưu gần nhất: {last:dd/MM/yyyy HH:mm}" : "Chưa có bản sao lưu nào.";
        MemberInfo.Text = $"{_store.Customers.Count} thành viên · {_store.Customers.Sum(c => c.Points):N0} điểm chưa đổi. Tích 1 điểm / 10.000đ, đổi 1 điểm = 1.000đ.";
    }
    private void SavePromo_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (!int.TryParse(PromoPercent.Text, out var percent) || percent is <= 0 or >= 100) throw FieldError.Mark(PromoPercent, "Nhập mức giảm từ 1 đến 99.");
        if (!TimeSpan.TryParse(PromoStart.Text, out var start)) throw FieldError.Mark(PromoStart, "Nhập giờ dạng 14:00.");
        if (!TimeSpan.TryParse(PromoEnd.Text, out var end) || end <= start) throw FieldError.Mark(PromoEnd, "Giờ kết thúc phải sau giờ bắt đầu.");
        var id = _store.Promotions.FirstOrDefault()?.Id ?? Guid.NewGuid().ToString();
        _store.SavePromotion(new Promotion { Id = id, Name = PromoName.Text.Trim(), Percent = percent, Start = start, End = end, IsActive = PromoActive.IsChecked == true,
            Days = PromoDays.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (DayOfWeek)c.Tag).ToList() });
    });
    private void BackupNow_Click(object sender, RoutedEventArgs e)
    {
        try { BackupService.BackupNow(); RefreshBackupInfo(); Toast.Show(this, "Đã sao lưu dữ liệu."); }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void OpenBackups_Click(object sender, RoutedEventArgs e)
    {
        System.IO.Directory.CreateDirectory(BackupService.Folder);
        System.Diagnostics.Process.Start("explorer.exe", BackupService.Folder);
    }
    private void SaveBank_Click(object sender, RoutedEventArgs e) => Run(() => VietQRService.SaveSettings(new BankSettings { BankId = BankCode.Text.Trim(), AccountNo = BankAccount.Text.Trim(), AccountName = BankOwner.Text.Trim(), SePayToken = SePayToken.Password.Trim() }));
}
