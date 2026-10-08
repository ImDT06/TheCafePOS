using System.Reflection;
using TheCafePOS_WPF.Services;

internal static class AmericanoRegression
{
    public static void Run()
    {
        void Check(bool valid, string message)
        {
            if (!valid) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var store = DataStoreService.Instance;
        Check(store.Products.Count(p => p.IsActive && !p.IsTopping) == 79 && store.Products.Count(p => p.IsActive && p.IsTopping) == 22, "Complete public menu and topping catalog imported");
        Check(store.Categories.Count(c => c.IsActive) == 14, "Menu grouped into fourteen categories without featured duplicates");
        Check(store.Products.Where(p => p.IsActive && !p.IsTopping).All(p => File.Exists(Path.Combine(AppContext.BaseDirectory, p.ImageUrl))), "Every sale product has a packaged image");
        Check(store.Products.Where(p => p.IsActive && p.IsBeverage).All(p => p.Sizes.Any(s => s.Name == p.DefaultSize) && p.Sizes.All(s => s.ExtraPrice >= 0)), "Every beverage has valid default size and nonnegative adjustments");
        Check(store.Products.Where(p => p.IsActive).All(p => p.AllowedToppingIds.All(id => store.Products.Any(t => t.Id == id && t.IsActive && t.IsTopping))), "Imported toppings resolve after existing ID remapping");
        var classic = store.Products.Single(p => p.Name == "Americano Classic");
        var hot = store.Products.Single(p => p.Name == "Americano Nóng");
        Check(classic.BasePrice + classic.Sizes.Single(s => s.Name == classic.DefaultSize).ExtraPrice == 55000 && hot.BasePrice == 55000, "Both Americanos cost 55000");
        Check(classic.Sizes.Count == 3 && hot.Sizes.Count == 1 && classic.BasePrice == 49000 && classic.Sizes.Single(s => s.Name == "L").ExtraPrice == 16000, "Official S/M/L prices and hot single size");
        Check(!hot.AllowIce && hot.DefaultIce == 0 && classic.AllowIce, "Hot coffee has no ice option");
        var item = OrderConfigurationService.Create(hot, "M", 100, 100, 2, Array.Empty<(string, int)>(), "", store.Products);
        Check(item.TotalPrice == 110000 && !item.HasIceOption && item.IcePercent == 0, "Two hot coffees total 110000 with no ice");
        var retail = store.Products.Single(p => p.Name == "Pizza Tomyum Hải Sản");
        var food = OrderConfigurationService.Create(retail, "", 100, 100, 2, Array.Empty<(string, int)>(), "", store.Products);
        Check(food.TotalPrice == 118000 && !food.IsBeverage && !food.HasIceOption && food.Size == "", "Food has no drink size or ice options");
        AuthService.Instance.CreateAccount("catalog-owner", "CatalogTest!2026", "Owner");
        AuthService.Instance.Login("catalog-owner", "CatalogTest!2026");
        store.OpenShift(0);
        var stockBefore = store.PackagingInventory.ToDictionary(p => p.Id, p => p.StockQuantity);
        var foodOrder = store.Checkout(new[] { food }, "Cash", 118000, "food-test");
        Check(store.PackagingInventory.All(p => p.StockQuantity == stockBefore[p.Id]), "Food checkout never deducts drink packaging");
        StickerPrinterService.Instance.GenerateStickersForOrder(foodOrder);
        Check(!store.StickerLogs.Any(s => s.OrderId == foodOrder.Id), "Food checkout never generates beverage labels");

        var state = LocalDatabase.Instance.Read<StoreState>("store")!;
        state.MenuSchemaVersion = 1;
        state.Products.RemoveAll(p => p.Id == hot.Id);
        state.Products.Single(p => p.Id == classic.Id).BasePrice = 60000;
        LocalDatabase.Instance.Write("store", state);
        var reload = typeof(DataStoreService).GetMethod("LoadState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        reload.Invoke(store, null);
        Check(store.Products.Single(p => p.Id == classic.Id).BasePrice == 49000, "Explicit catalog upgrade applies official price and retains product ID");
        Check(store.Products.Count(p => p.Name == "Americano Nóng") == 1, "Version 1 database receives missing hot coffee");
        int count = store.Products.Count;
        store.Products.Single(p => p.Name == "Americano Nóng").IsActive = false;
        store.Save();
        reload.Invoke(store, null);
        Check(store.Products.Count == count && !store.Products.Single(p => p.Name == "Americano Nóng").IsActive, "Reload neither duplicates nor reactivates coffee");
        Check(LocalDatabase.Instance.Read<StoreState>("store")!.MenuSchemaVersion == 4, "Menu upgrade persisted");
    }
}
