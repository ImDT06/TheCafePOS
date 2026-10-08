using System.Text.Json;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    private void ImportCoffeeHouseOptions()
    {
        using var stream = typeof(DataStoreService).Assembly.GetManifestResourceStream("TheCafePOS_WPF.Assets.Menu.thecoffeehouse.json")!;
        var catalog = JsonSerializer.Deserialize<StoreState>(stream)!;
        foreach (var source in catalog.Products)
        {
            var product = Products.FirstOrDefault(p => p.Id == source.Id)
                ?? Products.FirstOrDefault(p => p.Name.Trim().Equals(source.Name, StringComparison.OrdinalIgnoreCase));
            if (product is null) continue;
            product.SugarChoices = source.SugarChoices;
            product.IceChoices = source.IceChoices;
            product.DefaultSugarChoice = source.DefaultSugarChoice;
            product.DefaultIceChoice = source.DefaultIceChoice;
            product.AllowSugar = product.IsBeverage && source.AllowSugar;
            product.AllowIce = product.IsBeverage && source.AllowIce;
            // v3 imported sizes/prices from the same snapshot; retain shop edits and packaging.
        }
    }
    private void NormalizeProductImagePaths()
    {
        using var stream = typeof(DataStoreService).Assembly.GetManifestResourceStream("TheCafePOS_WPF.Assets.Menu.image-renames.json")
            ?? throw new InvalidOperationException("Không tìm thấy bảng đổi tên ảnh.");
        var paths = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
        var changes = Products.Where(p => paths.ContainsKey(p.ImageUrl.Replace('\\', '/'))).ToList();
        if (changes.Count == 0) return;
        Mutate(() =>
        {
            foreach (var product in changes) product.ImageUrl = paths[product.ImageUrl.Replace('\\', '/')];
        });
    }

    private void ImportCoffeeHouseMenu()
    {
        using var stream = typeof(DataStoreService).Assembly.GetManifestResourceStream("TheCafePOS_WPF.Assets.Menu.thecoffeehouse.json")
            ?? throw new InvalidOperationException("Không tìm thấy dữ liệu thực đơn The Coffee House.");
        var catalog = JsonSerializer.Deserialize<StoreState>(stream)
            ?? throw new InvalidOperationException("Thực đơn không hợp lệ.");
        // Keep historical products and order snapshots; hide only the original demo menu.
        string[] demoNames = { "Hồng Trà Truyền Thống", "Hồng Trà Tắc Xí Muội", "Hồng Trà Sữa Thượng Hạng",
            "Trà Sữa Ngô Gia (Đặc Biệt)", "Trà Sữa Oolong Nướng", "Trà Sữa Trân Châu Hoàng Gia", "Trà Sữa Matcha Uji",
            "Trà Đào Cam Sả", "Trà Vải Lài Mới", "Trà Mãng Cầu Tươi", "Trà Chanh Dây Thạch Dừa",
            "Hồng Trà Macchiato Cheese", "Oolong Macchiato Muối Biển", "Trân Châu Đen", "Trân Châu Hoàng Kim",
            "Thạch Trái Cây", "Panna Cotta Trắng", "Kem Cheese Béo" };
        for (int i = 0; i < demoNames.Length; i++)
        {
            var demo = Products.FirstOrDefault(p => p.Id == $"p-{i + 1}" && p.Name == demoNames[i]);
            if (demo != null) demo.IsActive = false;
        }
        foreach (var category in catalog.Categories)
        {
            Categories.RemoveAll(c => c.Id == category.Id);
            Categories.Add(category);
        }
        var idMap = new Dictionary<string, string>();
        foreach (var product in catalog.Products)
        {
            var existing = Products.FirstOrDefault(p => p.Id == product.Id)
                ?? Products.FirstOrDefault(p => p.Name.Trim().Equals(product.Name, StringComparison.OrdinalIgnoreCase));
            idMap[product.Id] = existing?.Id ?? product.Id;
            product.Id = existing?.Id ?? product.Id;
        }
        foreach (var product in catalog.Products)
        {
            product.AllowedToppingIds = product.AllowedToppingIds.Select(id => idMap[id]).ToList();
            Products.RemoveAll(p => p.Id == product.Id);
            Products.Add(product);
        }
        foreach (var category in Categories.Where(c => c.Id.StartsWith("cat-") || c.Name == "Cà phê"))
            if (!Products.Any(p => p.IsActive && p.CategoryId == category.Id)) category.IsActive = false;
    }
}
