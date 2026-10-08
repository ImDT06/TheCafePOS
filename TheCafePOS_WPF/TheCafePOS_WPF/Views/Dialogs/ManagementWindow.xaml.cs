using System.Windows;
using System.Windows.Controls;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
namespace TheCafePOS_WPF.Views.Dialogs;
public partial class ManagementWindow : Window
{
    private string? _productId, _categoryId;
    private readonly DataStoreService _store = DataStoreService.Instance;
    public ManagementWindow()
    {
        AuthService.Instance.RequireManager();
        InitializeComponent(); Refresh();
        var bank = VietQRService.Settings;
        BankCode.Text = bank.BankId; BankAccount.Text = bank.AccountNo; BankOwner.Text = bank.AccountName;
    }
    private void Refresh()
    {
        FilterProducts();
        Categories.ItemsSource = _store.Categories.OrderBy(c => c.DisplayOrder).ToList();
        ProductCategory.ItemsSource = _store.Categories.ToList();
        Staff.ItemsSource = AuthService.Instance.Accounts.ToList();
    }
    private void FilterProducts()
    {
        if (Products == null) return;
        string keyword = MenuSearch?.Text.Trim() ?? "";
        Products.ItemsSource = _store.Products.Where(p => p.Name.Contains(keyword, StringComparison.CurrentCultureIgnoreCase)).OrderBy(p => p.Name).ToList();
    }
    private void MenuSearch_TextChanged(object sender, TextChangedEventArgs e) => FilterProducts();
    private void Run(Action action)
    {
        try { action(); Refresh(); Status.Text = "Đã lưu thành công."; }
        catch (Exception ex) { Status.Text = ex.Message; }
    }
    private void ProductSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Products.SelectedItem is not Product p) return;
        _productId = p.Id; ProductName.Text = p.Name; ProductPrice.Text = p.BasePrice.ToString("0"); ProductCategory.SelectedValue = p.CategoryId; ProductActive.IsChecked = p.IsActive;
    }
    private void NewProduct_Click(object sender, RoutedEventArgs e)
    {
        Products.SelectedItem = null; _productId = null; ProductName.Clear(); ProductPrice.Clear(); ProductActive.IsChecked = true;
    }
    private void SaveProduct_Click(object sender, RoutedEventArgs e) => Run(() =>
    {
        if (!decimal.TryParse(ProductPrice.Text, out var price)) throw new InvalidOperationException("Giá không hợp lệ.");
        var existing = _store.Products.FirstOrDefault(p => p.Id == _productId);
        var product = existing is null ? new Product() : System.Text.Json.JsonSerializer.Deserialize<Product>(System.Text.Json.JsonSerializer.Serialize(existing))!;
        product.Name = ProductName.Text.Trim(); product.BasePrice = price; product.CategoryId = ProductCategory.SelectedValue as string ?? ""; product.IsActive = ProductActive.IsChecked == true;
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
        _store.SaveCategory(new Category { Id = _categoryId ?? Guid.NewGuid().ToString(), Name = CategoryName.Text.Trim(), IsActive = CategoryActive.IsChecked == true, DisplayOrder = _store.Categories.FirstOrDefault(c => c.Id == _categoryId)?.DisplayOrder ?? _store.Categories.Count + 1 });
        NewCategory_Click(sender, e);
    });
    private void CreateStaff_Click(object sender, RoutedEventArgs e) => Run(() => { AuthService.Instance.CreateAccount(StaffName.Text, StaffPassword.Password, ((ComboBoxItem)StaffRole.SelectedItem).Content.ToString()!); StaffPassword.Clear(); StaffName.Clear(); });
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
    private void SaveBank_Click(object sender, RoutedEventArgs e) => Run(() => VietQRService.SaveSettings(new BankSettings { BankId = BankCode.Text.Trim(), AccountNo = BankAccount.Text.Trim(), AccountName = BankOwner.Text.Trim() }));
}
