using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    // Shared by the payment preview and final checkout; never changes inventory.
    public void ValidateCheckoutCart(IEnumerable<OrderItem> cart, string serviceType)
    {
        RequireOpenShift();
        var items = cart.ToList();
        if (items.Count == 0 || items.Any(i => i.Quantity <= 0 || i.UnitPrice < 0)) throw new InvalidOperationException("Đơn hàng không hợp lệ.");
        if (items.Any(i => !Products.Any(p => p.Id == i.ProductId && p.IsActive && Categories.Any(c => c.Id == p.CategoryId && c.IsActive)))) throw new InvalidOperationException("Đơn có món đã ngừng bán. Vui lòng cập nhật giỏ hàng.");
        foreach (var item in items)
        {
            var product = Products.Single(p => p.Id == item.ProductId);
            if (item.Quantity > 999 || product.IsTopping && !product.SoldSeparately || item.IsBeverage != product.IsBeverage)
                throw new InvalidOperationException("Loại món hoặc số lượng đã thay đổi. Vui lòng chọn lại món.");
            if (product.IsBeverage && !product.Sizes.Any(s => s.Name == item.Size)) throw new InvalidOperationException("Size đã ngừng bán. Vui lòng sửa món.");
            if (product.IsBeverage &&
                ((product.AllowSugar && product.SugarChoices != null && !product.SugarChoices.Contains(item.SugarChoice)) ||
                 (product.AllowIce && product.IceChoices != null && !product.IceChoices.Contains(item.IceChoice))))
                throw new InvalidOperationException("Tùy chọn đường/đá đã thay đổi. Vui lòng sửa món trước khi thanh toán.");
            if (item.SelectedToppings.Any(t => !product.AllowedToppingIds.Contains(t.ProductId) || !Products.Any(p => p.Id == t.ProductId && p.IsTopping && p.IsActive && Categories.Any(c => c.Id == p.CategoryId && c.IsActive))))
                throw new InvalidOperationException("Topping đã ngừng bán hoặc không còn áp dụng. Vui lòng sửa món.");
        }
        if (items.Any(i => Products.Single(p => p.Id == i.ProductId).IsSoldOut)) throw new InvalidOperationException("Đơn có món đang hết hàng. Vui lòng bỏ món đó khỏi giỏ.");
        if (serviceType is not ("Mang đi" or "Tại chỗ")) throw new InvalidOperationException("Chọn loại đơn hợp lệ.");
        if (serviceType == "Mang đi")
            foreach (var (id, needed) in PackagingRequired(items))
            {
                var packaging = PackagingInventory.FirstOrDefault(p => p.Id == id)
                    ?? throw new InvalidOperationException("Chưa cấu hình bao bì: " + id);
                if (packaging.StockQuantity < needed)
                    throw new InvalidOperationException($"Không đủ {packaging.Name}: cần {needed}, còn {packaging.StockQuantity}. Giảm số lượng hoặc nhập thêm kho.");
            }
    }

    private static Dictionary<string, int> PackagingRequired(IEnumerable<OrderItem> items)
    {
        var required = new Dictionary<string, int>();
        foreach (var item in items.Where(i => i.IsBeverage))
        {
            string cupId = string.IsNullOrEmpty(item.PackagingId)
                ? item.Size switch { "M" => "pack-1", "L" => "pack-2", "XL" => "pack-xl", _ => throw new InvalidOperationException("Size không hợp lệ.") }
                : item.PackagingId;
            foreach (var id in new[] { cupId, "pack-3", "pack-5" })
                required[id] = checked(required.GetValueOrDefault(id) + item.Quantity);
        }
        return required;
    }
}
