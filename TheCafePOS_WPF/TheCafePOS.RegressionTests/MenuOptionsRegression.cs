using System.Reflection;
using System.Text.Json;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

internal static class MenuOptionsRegression
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
        void Reject(Action action, string message) { try { action(); } catch (InvalidOperationException) { Check(true, message); return; } throw new Exception(message); }
        var store = DataStoreService.Instance;
        Product Find(string name) => store.Products.Single(p => p.Name == name);
        OrderItem Make(Product p, string? sugar = null, string? ice = null) => OrderConfigurationService.Create(p, p.DefaultSize, 100, 100, 1, Array.Empty<(string, int)>(), "", store.Products, sugar, ice);
        var classic = Find("Americano Classic");
        var hot = Find("Americano Nóng");
        var espresso = Find("Espresso Đá");
        var tea = Find("Trà Đào Cam Sả - Đá");
        Check(classic.Sizes.Select(s => s.Name).SequenceEqual(new[] { "S", "M", "L" }) && hot.Sizes.Single().Name == "M", "Source sizes differ for iced and hot Americano");
        Check(classic.SugarChoices!.SequenceEqual(new[] { "Bình thường", "Không ngọt", "Ít ngọt", "Thêm ngọt" }), "Exact source sweetness labels");
        Check(espresso.IceChoices!.SequenceEqual(new[] { "Bình thường", "Ít đá" }), "Espresso does not offer separate ice");
        Check(!hot.AllowIce && hot.IceChoices!.Count == 0, "Hot drink has no ice group");
        Check(!tea.SugarChoices!.Contains("Không ngọt"), "Tea cannot select unsupported no-sugar option");
        Check(Find("Bạc Xỉu Caramel Muối").SugarChoices!.SequenceEqual(new[] { "Bình thường" }), "Recipe with fixed sweetness has one choice");
        Check(store.Products.Where(p => p.IsActive && p.IsBeverage).All(p =>
            (!p.AllowSugar || p.SugarChoices!.Contains(p.DefaultSugarChoice)) &&
            (!p.AllowIce || p.IceChoices!.Contains(p.DefaultIceChoice))), "Every imported default belongs to that product");
        var item = Make(classic, "Thêm ngọt", "Đá riêng");
        Check(item.OptionsSummary.Contains("Thêm ngọt") && item.OptionsSummary.Contains("Đá riêng") && !item.OptionsSummary.Contains('%'), "Summary preserves named recipe options without inventing percentages");
        Check(OrderConfigurationService.Clone(item).IceChoice == "Đá riêng", "Cloning preserves separate ice");
        Reject(() => Make(espresso, null, "Đá riêng"), "Reject unsupported ice in service");
        Reject(() => Make(tea, "Không ngọt"), "Reject unsupported sweetness in service");
        var legacy = new OrderItem { SugarPercent = 30, IcePercent = 70 };
        Check(legacy.OptionsSummary.Contains("30%") && legacy.OptionsSummary.Contains("70%"), "Historical percentage orders remain readable");
        var auth = AuthService.Instance;
        auth.CreateAccount("options-owner", "OptionsTest!2026", "Owner"); auth.Login("options-owner", "OptionsTest!2026"); store.OpenShift(0);
        var invalid = OrderConfigurationService.Clone(item); invalid.IceChoice = "Không đá";
        Reject(() => store.Checkout(new[] { invalid }, "Cash", 100000, "invalid-options"), "Checkout rejects tampered recipe option");
        var order = store.Checkout(new[] { item }, "Cash", 100000, "named-options");
        StickerPrinterService.Instance.GenerateStickersForOrder(order);
        Check(store.StickerLogs.Any(l => l.OrderId == order.Id && l.OptionsSummary.Contains("Đá riêng")), "Printed sticker preserves separate ice");
        Check(LocalDatabase.Instance.Read<StoreState>("store")!.Orders.Single(o => o.Id == order.Id).Items[0].SugarChoice == "Thêm ngọt", "Named selections persisted in order snapshot");
        var state = LocalDatabase.Instance.Read<StoreState>("store")!;
        state.MenuSchemaVersion = 3;
        var saved = state.Products.Single(p => p.Id == classic.Id);
        saved.BasePrice = 60000; saved.ImageUrl = "Images/custom.png"; saved.SugarChoices = null; saved.IceChoices = null;
        var history = JsonSerializer.Serialize(state.Orders);
        LocalDatabase.Instance.Write("store", state);
        var reload = typeof(DataStoreService).GetMethod("LoadState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        reload.Invoke(store, null);
        Check(Find("Americano Classic").BasePrice == 60000 && Find("Americano Classic").ImageUrl == "Images/custom.png", "v4 migration preserves shop prices and images");
        Check(Find("Americano Classic").IceChoices!.Contains("Đá riêng"), "v3 database receives recipe options");
        Check(JsonSerializer.Serialize(store.CompletedOrders.ToList()) == history, "Migration leaves completed orders untouched");
        Find("Americano Classic").DefaultIceChoice = "Ít đá"; store.Save(); reload.Invoke(store, null);
        Check(Find("Americano Classic").DefaultIceChoice == "Ít đá", "Reload preserves manager's chosen default");
        Check(LocalDatabase.Instance.Read<StoreState>("store")!.MenuSchemaVersion >= 4, "Options migration persisted alongside later menu migrations");
        var additions = new[] { "Shan Nhài Cam Đào", "Shan Xoài Chanh Leo", "Frappe Xôi Xoài Dừa Non", "Shan Xanh Quýt Cam", "Shan Xanh Vải Hibiscus" };
        state = LocalDatabase.Instance.Read<StoreState>("store")!;
        state.MenuSchemaVersion = 6;
        state.Products.RemoveAll(p => additions.Contains(p.Name));
        state.Products.Single(p => p.Name == "Hạt Sen").ImageUrl = "";
        LocalDatabase.Instance.Write("store", state);
        reload.Invoke(store, null);
        Check(additions.All(name => Find(name).FeaturedCategoryIds.Contains(DataStoreService.NewCategoryId)), "v6 databases receive all five homepage drinks in the featured section");
        Check(Find("Shan Nhài Cam Đào").BasePrice == 79000 && Find("Shan Xoài Chanh Leo").BasePrice == 79000 && Find("Frappe Xôi Xoài Dừa Non").BasePrice == 89000, "New single-size drink prices match homepage");
        Check(new[] { "Shan Xanh Quýt Cam", "Shan Xanh Vải Hibiscus" }.All(name => Find(name).DefaultSize == "M" && Find(name).BasePrice + Find(name).Sizes.Single(s => s.Name == "M").ExtraPrice == 65000), "Shan default M prices match 65000 shown on homepage");
        Check(Find("Hạt Sen").ImageUrl == "Images/hat-sen.png" && Find("Americano Classic").ImageUrl == "Images/custom.png", "Image migration fills missing toppings while preserving custom images");
        Check(JsonSerializer.Serialize(store.CompletedOrders.ToList()) == history && Find("Americano Classic").BasePrice == 60000, "Homepage migration preserves order history and manager prices");
        reload.Invoke(store, null);
        Check(additions.All(name => store.Products.Count(p => p.Name == name) == 1), "Homepage migration does not duplicate drinks after reload");
        Find("Combo Pizza & Nước").ImageUrl = "Images/pizza-tomyum-hai-san.png";
        Find("Combo Pasta & Nước").ImageUrl = "Images/wafu-pasta-ca-bao-trung-onsen-xot-mentaiko.png";
        store.Save(); reload.Invoke(store, null);
        Check(Find("Combo Pizza & Nước").ImageUrl == "Images/combo-pizza-nuoc.png" && Find("Combo Pasta & Nước").ImageUrl == "Images/combo-pasta-nuoc.png", "Existing deals receive actual combo photos");
        Check(Find("Pizza Tomyum Hải Sản").ImageUrl == "Images/pizza-tomyum-hai-san.png", "Fixing combo images does not replace standalone dish photos");
        Find("Combo Pizza & Nước").ImageUrl = "Images/shop-combo.png";
        store.Save(); reload.Invoke(store, null);
        Check(Find("Combo Pizza & Nước").ImageUrl == "Images/shop-combo.png", "Deal image correction preserves manager custom photos");
        var recoveredToppings = new[] { "Shot Espresso", "Hạt Nổ Củ Năng", "Foam Phô Mai", "Hạt Nổ Yến Mạch" };
        foreach (var name in recoveredToppings) Find(name).ImageUrl = "";
        store.Save(); reload.Invoke(store, null);
        Check(recoveredToppings.All(name => !string.IsNullOrWhiteSpace(Find(name).ImageUrl)), "Latest databases receive missing official topping detail images");
        Find("Shot Espresso").ImageUrl = "Images/shop-espresso.png";
        store.Save(); reload.Invoke(store, null);
        Check(Find("Shot Espresso").ImageUrl == "Images/shop-espresso.png", "Topping image fill preserves manager custom images");
        Check(string.IsNullOrEmpty(Find("Shot Espresso SOE").ImageUrl), "Missing SOE photo is not replaced with a different espresso product");
        Console.WriteLine($"Completed {checks} menu option checks.");
    }
}
