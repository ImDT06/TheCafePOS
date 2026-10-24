using System.Text.Json;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    private void AddHomepageMenu()
    {
        using var stream = typeof(DataStoreService).Assembly.GetManifestResourceStream("TheCafePOS_WPF.Assets.Menu.thecoffeehouse.json")!;
        var catalog = JsonSerializer.Deserialize<StoreState>(stream)!;
        foreach (var category in catalog.Categories)
            if (Categories.All(c => c.Id != category.Id)) Categories.Add(category);
        Product? Existing(Product source) => Products.FirstOrDefault(p => p.Id == source.Id)
            ?? Products.FirstOrDefault(p => p.Name.Trim().Equals(source.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        var ids = catalog.Products.ToDictionary(p => p.Id, p => Existing(p)?.Id ?? p.Id);
        foreach (var source in catalog.Products)
        {
            var existing = Existing(source);
            if (existing is null)
            {
                source.AllowedToppingIds = source.AllowedToppingIds.Select(id => ids.GetValueOrDefault(id, id)).ToList();
                Products.Add(source);
            }
            else if (string.IsNullOrWhiteSpace(existing.ImageUrl) && !string.IsNullOrWhiteSpace(source.ImageUrl))
                existing.ImageUrl = source.ImageUrl;
        }
    }

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
        var dealImages = new[]
        {
            (Id: "deal-combo-pizza-nuoc", Old: "Images/pizza-tomyum-hai-san.png", Image: "Images/combo-pizza-nuoc.png"),
            (Id: "deal-combo-pasta-nuoc", Old: "Images/wafu-pasta-ca-bao-trung-onsen-xot-mentaiko.png", Image: "Images/combo-pasta-nuoc.png")
        };
        var deals = dealImages.Select(fix => (Product: Products.FirstOrDefault(p => p.Id == fix.Id && p.ImageUrl.Replace('\\', '/') == fix.Old), fix.Image))
            .Where(fix => fix.Product is not null).ToList();
        using var catalogStream = typeof(DataStoreService).Assembly.GetManifestResourceStream("TheCafePOS_WPF.Assets.Menu.thecoffeehouse.json")!;
        var catalog = JsonSerializer.Deserialize<StoreState>(catalogStream)!;
        var toppingImages = catalog.Products.Where(p => p.IsTopping && !string.IsNullOrWhiteSpace(p.ImageUrl))
            .Select(source => (Product: Products.FirstOrDefault(p => p.IsTopping && p.Id == source.Id)
                ?? Products.FirstOrDefault(p => p.IsTopping && p.Name.Trim().Equals(source.Name.Trim(), StringComparison.OrdinalIgnoreCase)), source.ImageUrl))
            .Where(fix => fix.Product is not null && string.IsNullOrWhiteSpace(fix.Product.ImageUrl)).ToList();
        if (changes.Count == 0 && deals.Count == 0 && toppingImages.Count == 0) return;
        Mutate(() =>
        {
            foreach (var product in changes) product.ImageUrl = paths[product.ImageUrl.Replace('\\', '/')];
            foreach (var fix in deals) fix.Product!.ImageUrl = fix.Image;
            foreach (var fix in toppingImages) fix.Product!.ImageUrl = fix.ImageUrl;
        });
    }

    public const string NewCategoryId = "tch-cat-92", DealCategoryId = "tch-cat-deals", SeasonalComboCategoryId = "tch-cat-7171";

    // Featured sections mirrored from order.thecoffeehouse.com (checked 2026-10-12). Idempotent: keeps manager edits.
    private void AddFeaturedMenu()
    {
        void Category(string id, string name, int order)
        {
            if (Categories.All(c => c.Id != id)) Categories.Add(new Category { Id = id, Name = name, IsActive = true, DisplayOrder = order });
        }
        Category(SeasonalComboCategoryId, "Combo 20/10", -3);
        Category(NewCategoryId, "Món Mới Phải Thử", -2);
        Category(DealCategoryId, "Ưu đãi riêng bạn", -1);
        // The web renamed "Butter Croissant Sữa Đặc" to "Butter Croissant Mama"; follow it unless a manager renamed it.
        if (Products.FirstOrDefault(p => p.Id == "tch-656952a89375030019b6da2c" && p.Name == "Butter Croissant Sữa Đặc") is { } mama) mama.Name = "Butter Croissant Mama";
        foreach (var id in new[] { "tch-699eafedbde92e0012ac3304", "tch-67e13816c6fcd7001a826690", "tch-656952a89375030019b6da2c" })
            if (Products.FirstOrDefault(p => p.Id == id) is { } featured && !featured.FeaturedCategoryIds.Contains(NewCategoryId))
                featured.FeaturedCategoryIds.Add(NewCategoryId);
        void Bundle(string id, string category, string name, decimal price, decimal original, string image)
        {
            if (Products.Any(p => p.Id == id)) return;
            Products.Add(new Product { Id = id, CategoryId = category, Name = name, BasePrice = price, OriginalPrice = original, ImageUrl = image, IsRetailItem = true, Sizes = new(), AllowSugar = false, AllowIce = false, IsActive = true });
        }
        Bundle("tch-6ac3f014e0a2d40012d588a9", SeasonalComboCategoryId, "Combo Sắc Xinh - Matcha Latte Tây Bắc (M)", 119000, 0, "Images/combo-sac-xinh-matcha-latte-tay-bac.png");
        Bundle("deal-combo-pizza-nuoc", DealCategoryId, "Combo Pizza & Nước", 79000, 98000, "Images/combo-pizza-nuoc.png");
        Bundle("deal-combo-pasta-nuoc", DealCategoryId, "Combo Pasta & Nước", 95000, 108000, "Images/combo-pasta-nuoc.png");
    }

    // v8: deals are combo promotions now; the old fixed-price bundle items skipped cup, sticker and recipe
    // deductions for their drink. Hidden so past orders and reports keep their names.
    public static readonly string[] BundleDealIds = { "tch-6ac3f014e0a2d40012d588a9", "deal-combo-pizza-nuoc", "deal-combo-pasta-nuoc" };
    private void HideBundleDeals()
    {
        foreach (var product in Products.Where(p => BundleDealIds.Contains(p.Id))) product.IsActive = false;
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
