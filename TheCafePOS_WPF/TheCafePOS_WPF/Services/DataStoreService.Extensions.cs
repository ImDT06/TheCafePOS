using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    // Checkout falls back to these ids (cup by size, lid, straw); deleting them would block every sale.
    private static readonly string[] CorePackagingIds = { "pack-1", "pack-2", "pack-xl", "pack-3", "pack-5" };

    public void DeleteProduct(string id)
    {
        AuthService.Instance.RequireManager();
        if (!Products.Any(p => p.Id == id)) throw new InvalidOperationException("Món không tồn tại.");
        // Paid orders keep their own item snapshot, so history and reports are unaffected.
        Mutate(() =>
        {
            Products.RemoveAll(p => p.Id == id);
            foreach (var product in Products) product.AllowedToppingIds.Remove(id);
        });
    }
    // Any signed-in staff may flag sold-out so the counter can react without a manager.
    public void SetProductSoldOut(string id, bool soldOut)
    {
        var product = Products.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Món không tồn tại.");
        if (product.IsSoldOut != soldOut) Mutate(() => product.IsSoldOut = soldOut);
    }
    public void DeleteCategory(string id, bool deleteProducts = false)
    {
        AuthService.Instance.RequireManager();
        if (!Categories.Any(c => c.Id == id)) throw new InvalidOperationException("Danh mục không tồn tại.");
        if (!deleteProducts && Products.Any(p => p.CategoryId == id)) throw new InvalidOperationException("Không thể xóa danh mục đang chứa món. Chuyển hoặc xóa món trước.");
        Mutate(() =>
        {
            var removed = Products.Where(p => p.CategoryId == id).Select(p => p.Id).ToHashSet();
            Products.RemoveAll(p => removed.Contains(p.Id));
            foreach (var product in Products) product.AllowedToppingIds.RemoveAll(removed.Contains);
            Categories.RemoveAll(c => c.Id == id);
        });
    }

    public void SavePackagingItem(PackagingItem item)
    {
        AuthService.Instance.RequireManager();
        string name = item.Name.Trim(), unit = item.Unit.Trim();
        if (name.Length == 0 || unit.Length == 0) throw new InvalidOperationException("Tên và đơn vị tính không được trống.");
        if (item.WarningThreshold < 0) throw new InvalidOperationException("Ngưỡng cảnh báo phải là số nguyên không âm.");
        if (PackagingInventory.Any(p => p.Id != item.Id && p.Name.Trim().Equals(name, StringComparison.CurrentCultureIgnoreCase))) throw new InvalidOperationException("Tên bao bì đã tồn tại.");
        var existing = PackagingInventory.FirstOrDefault(p => p.Id == item.Id);
        // Stock only changes through UpdateStock so every change keeps a movement record.
        var saved = new PackagingItem { Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString() : item.Id, Name = name, Unit = unit, WarningThreshold = item.WarningThreshold, StockQuantity = existing?.StockQuantity ?? 0 };
        Mutate(() =>
        {
            int index = existing is null ? -1 : PackagingInventory.IndexOf(existing);
            if (index < 0) PackagingInventory.Add(saved); else PackagingInventory[index] = saved;
        });
    }
    public void DeletePackagingItem(string id)
    {
        AuthService.Instance.RequireManager();
        var item = PackagingInventory.FirstOrDefault(p => p.Id == id) ?? throw new InvalidOperationException("Bao bì không tồn tại.");
        if (CorePackagingIds.Contains(id)) throw new InvalidOperationException("Không thể xóa bao bì mặc định (ly M/L/XL, nắp, ống hút) vì hệ thống dùng khi bán hàng.");
        if (Products.Any(p => p.Sizes.Any(s => s.PackagingId == id))) throw new InvalidOperationException("Không thể xóa bao bì đang được gán cho size của món.");
        if (HoldOrderService.Instance.HeldOrders.Any(h => h.Items.Any(i => i.PackagingId == id)) || Draft?.Items.Any(i => i.PackagingId == id) == true)
            throw new InvalidOperationException("Bao bì đang được dùng trong giỏ hàng hoặc đơn giữ.");
        if (item.StockQuantity != 0) throw new InvalidOperationException("Kiểm kê tồn về 0 trước khi xóa để giữ lịch sử kho.");
        Mutate(() => PackagingInventory.Remove(item));
    }
}
