using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services.Interfaces;
namespace TheCafePOS_WPF.Views.Dialogs;

// Add or edit one menu category in the management side panel; an empty id adds a new one.
public sealed class CategoryEditorPanel : EditorPanel
{
    private readonly ICatalogService _catalog;
    private readonly string _categoryId;
    private readonly TextBox _name;
    private readonly CheckBox _active;
    private readonly int _count;

    public CategoryEditorPanel(string categoryId, ICatalogService catalog)
        : base(catalog.Categories.Any(c => c.Id == categoryId) ? "Sửa danh mục" : "Thêm danh mục",
               catalog.Categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "Nhóm món mới trên màn bán hàng", "Lưu danh mục",
               catalog.Categories.Any(c => c.Id == categoryId))
    {
        _catalog = catalog;
        _categoryId = categoryId;
        var existing = Current();
        _count = existing is null ? 0 : catalog.Products.Count(p => p.CategoryId == categoryId);
        var form = new StackPanel();
        _name = new TextBox { Text = existing?.Name ?? "", Margin = new Thickness(0, 4, 0, 10) }; HintAssist.SetHint(_name, "Tên danh mục");
        _active = new CheckBox { Content = "Đang hiển thị trên màn bán hàng", IsChecked = existing?.IsActive ?? true, Margin = new Thickness(0, 4, 0, 10) };
        form.Children.Add(_name); form.Children.Add(_active);
        form.Children.Add(new TextBlock
        {
            Text = existing is not null ? $"Danh mục đang có {_count} món. Bỏ \"Đang hiển thị\" để tạm ẩn cả nhóm mà không xóa món." : "Sắp nhóm giúp nhân viên tìm món nhanh hơn. Danh mục mới được xếp cuối hàng chip.",
            Style = (Style)FindResource("Hint"), TextWrapping = TextWrapping.Wrap
        });
        Form = form;
        Loaded += (_, _) => _name.Focus();
    }

    private Category? Current() => _catalog.Categories.FirstOrDefault(c => c.Id == _categoryId);

    protected override void Save()
    {
        if (string.IsNullOrWhiteSpace(_name.Text)) throw FieldError.Mark(_name, "Nhập tên danh mục.");
        // Copy the stored category so fields not shown here are kept.
        var category = Current() is Category stored
            ? JsonSerializer.Deserialize<Category>(JsonSerializer.Serialize(stored))!
            : new Category { Id = Guid.NewGuid().ToString(), DisplayOrder = _catalog.Categories.Count + 1 };
        category.Name = _name.Text.Trim(); category.IsActive = _active.IsChecked == true;
        _catalog.SaveCategory(category);
    }

    protected override void Delete()
    {
        if (Current() is not Category category) return;
        string message = _count == 0 ? $"Xóa danh mục {category.Name}?" : $"Danh mục {category.Name} đang có {_count} món. Xóa luôn cả {_count} món này?\nLịch sử đơn và báo cáo vẫn được giữ. Nếu chỉ muốn tạm ẩn, hãy bỏ chọn \"Đang hiển thị\".";
        if (!Confirm(message, "Xóa danh mục")) return;
        try { _catalog.DeleteCategory(_categoryId, deleteProducts: true); Close(true); }
        catch (Exception ex) { ShowError(ex.Message); }
    }
}
