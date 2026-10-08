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
        Check(LocalDatabase.Instance.Read<StoreState>("store")!.MenuSchemaVersion == 4, "Options migration persisted once");
        Console.WriteLine($"Completed {checks} menu option checks.");
    }
}
