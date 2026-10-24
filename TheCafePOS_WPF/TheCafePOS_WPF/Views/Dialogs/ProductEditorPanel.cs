using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;

// Add or edit one menu item in the management side panel; an empty id adds a new one.
public sealed class ProductEditorPanel : EditorPanel
{
    private readonly ICatalogService _catalog;
    private readonly string _productId;
    private readonly Image _image = new() { Stretch = System.Windows.Media.Stretch.Uniform, Margin = new Thickness(4) };
    private readonly TextBlock _imageHint;
    private readonly TextBox _name, _price;
    private readonly ComboBox _category;
    private readonly CheckBox _active, _soldOut;
    private string _imageUrl = "";

    public ProductEditorPanel(string productId, ICatalogService catalog, IDialogFactory dialogs)
        : base(catalog.Products.Any(p => p.Id == productId) ? "Sửa món" : "Thêm món mới",
               catalog.Products.FirstOrDefault(p => p.Id == productId)?.Name ?? "Điền thông tin cơ bản của món", "Lưu món",
               catalog.Products.Any(p => p.Id == productId))
    {
        _catalog = catalog;
        _productId = productId;
        var existing = Current();
        var form = new StackPanel();

        _imageHint = new TextBlock { Text = "Chưa có ảnh", FontSize = 12, Foreground = (System.Windows.Media.Brush)FindResource("PosMuted"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var photo = new Grid { Margin = new Thickness(0, 4, 0, 12) };
        photo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(88) });
        photo.ColumnDefinitions.Add(new ColumnDefinition());
        var frame = new Grid(); frame.Children.Add(_imageHint); frame.Children.Add(_image);
        photo.Children.Add(new Border { Width = 88, Height = 88, CornerRadius = new CornerRadius(8), Background = (System.Windows.Media.Brush)FindResource("PosSubtle"), Child = frame });
        var imageButtons = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var pick = new Button { Content = "Chọn ảnh…", Padding = new Thickness(8) }; pick.Click += (_, _) => PickImage();
        var remove = new Button { Content = "Gỡ ảnh", Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 0) }; remove.Click += (_, _) => ShowImage("");
        imageButtons.Children.Add(pick); imageButtons.Children.Add(remove);
        Grid.SetColumn(imageButtons, 1); photo.Children.Add(imageButtons);
        form.Children.Add(photo);

        _name = new TextBox { Text = existing?.Name ?? "", Margin = new Thickness(0, 4, 0, 10) }; HintAssist.SetHint(_name, "Tên món");
        _category = new ComboBox { ItemsSource = _catalog.Categories.OrderBy(c => c.DisplayOrder).ToList(), DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValue = existing?.CategoryId, Margin = new Thickness(0, 4, 0, 10) }; HintAssist.SetHint(_category, "Danh mục");
        _price = new TextBox { Text = existing?.BasePrice.ToString("0") ?? "", Margin = new Thickness(0, 4, 0, 10) }; HintAssist.SetHint(_price, "Giá cơ bản (đồng)");
        TouchInput.SetNumeric(_price, true); TouchInput.SetLabel(_price, "Giá cơ bản của món");
        _active = new CheckBox { Content = "Đang bán (bỏ chọn để ẩn khỏi thực đơn)", IsChecked = existing?.IsActive ?? true, Margin = new Thickness(0, 4, 0, 6) };
        _soldOut = new CheckBox { Content = "Hết hàng tạm thời (vẫn hiện, làm mờ)", IsChecked = existing?.IsSoldOut ?? false, Margin = new Thickness(0, 4, 0, 10) };
        foreach (var field in new UIElement[] { _name, _category, _price, _active, _soldOut }) form.Children.Add(field);

        if (existing is not null)
        {
            var configure = new Button { Content = "Cấu hình size / topping / đường đá…", Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 0) };
            configure.Click += (_, _) => { if (Current() is Product p && dialogs.Create<ProductConfigurationWindow>(Owner, p).ShowDialog() == true) Toast.Show(Owner!, "Đã lưu cấu hình."); };
            var recipe = new Button { Content = "Công thức (định lượng nguyên liệu)…", Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 0) };
            recipe.Click += (_, _) => { if (Current() is Product p && dialogs.Create<RecipeWindow>(Owner, p).ShowDialog() == true) Toast.Show(Owner!, "Đã lưu công thức."); };
            form.Children.Add(configure); form.Children.Add(recipe);
        }
        else form.Children.Add(new TextBlock { Text = "Lưu món trước, rồi chọn lại món để cấu hình size, topping và công thức.", Style = (Style)FindResource("Hint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) });
        ShowImage(existing?.ImageUrl ?? "");
        Form = form;
        Loaded += (_, _) => _name.Focus();
    }

    private Product? Current() => _catalog.Products.FirstOrDefault(p => p.Id == _productId);

    protected override void Save()
    {
        if (string.IsNullOrWhiteSpace(_name.Text)) throw FieldError.Mark(_name, "Nhập tên món.");
        if (!decimal.TryParse(_price.Text, out var basePrice) || basePrice < 0) throw FieldError.Mark(_price, "Giá phải là số không âm.");
        if (_category.SelectedValue is null) throw FieldError.Mark(_category, "Chọn danh mục cho món.");
        // Start from the stored product so sizes, toppings or a recipe saved meanwhile are kept.
        var product = Current() is Product stored ? JsonSerializer.Deserialize<Product>(JsonSerializer.Serialize(stored))! : new Product();
        product.Name = _name.Text.Trim(); product.BasePrice = basePrice; product.CategoryId = _category.SelectedValue as string ?? "";
        product.IsActive = _active.IsChecked == true; product.IsSoldOut = _soldOut.IsChecked == true; product.ImageUrl = _imageUrl;
        _catalog.SaveProduct(product);
    }

    protected override void Delete()
    {
        if (Current() is not Product product || !Confirm($"Xóa vĩnh viễn món {product.Name}? Nếu chỉ tạm ngưng, hãy bỏ chọn \"Đang bán\".", "Xóa món")) return;
        try { _catalog.DeleteProduct(_productId); Close(true); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowImage(string url)
    {
        _imageUrl = url;
        _image.Source = ProductImages.Load(url);
        _imageHint.Visibility = _image.Source is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PickImage()
    {
        string imagesDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Images");
        System.IO.Directory.CreateDirectory(imagesDir);
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Chọn ảnh minh họa", Filter = "Ảnh (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg", InitialDirectory = imagesDir };
        if (dialog.ShowDialog(Owner) != true) return;
        try
        {
            // Images already in the bundled folder are referenced directly instead of duplicated.
            // Also matches a pick from the repo's Images folder, which the build copies here under the same name.
            if (System.IO.File.Exists(System.IO.Path.Combine(imagesDir, System.IO.Path.GetFileName(dialog.FileName))))
            {
                ShowImage("Images/" + System.IO.Path.GetFileName(dialog.FileName));
                return;
            }
            // Copy next to the bundled images so the product keeps its photo if the original file moves.
            string file = "custom-" + Guid.NewGuid().ToString("N")[..8] + System.IO.Path.GetExtension(dialog.FileName).ToLowerInvariant();
            System.IO.File.Copy(dialog.FileName, System.IO.Path.Combine(imagesDir, file));
            ShowImage("Images/" + file);
            if (_image.Source is null) { ShowError("Không đọc được file ảnh."); ShowImage(""); }
        }
        catch (Exception ex) { ShowError("Không thể chép ảnh: " + ex.Message); }
    }
}
