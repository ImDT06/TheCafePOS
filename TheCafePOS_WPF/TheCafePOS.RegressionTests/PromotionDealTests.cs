using Microsoft.Data.Sqlite;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.ViewModels;
using Xunit;

/// <summary>
/// Combo promotions built from slots ("1 pizza bất kỳ + 1 nước"), date-limited promotions, the retired
/// fixed-price bundle items and the "Ưu đãi" menu section that lists what is on offer today.
/// </summary>
public sealed class PromotionDealTests : IDisposable
{
    private const string Owner = "deal-owner", OwnerPass = "DealPass!2026";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "thecafepos-tests", Guid.NewGuid().ToString("N"));
    private readonly Category _pizzas = new() { Name = "Deal pizzas" }, _drinks = new() { Name = "Deal drinks" };
    private readonly Product _pizza, _bigPizza, _tea;
    private DataStoreService Store => TestHost.Store;
    private static DateTime Today => DataStoreService.CafeNow.Date;

    public PromotionDealTests()
    {
        TestHost.Start(_dataDirectory);
        TestHost.Auth.CreateAccount(Owner, OwnerPass, "Owner");
        TestHost.Auth.Login(Owner, OwnerPass);
        Store.SaveCategory(_pizzas); Store.SaveCategory(_drinks);
        _pizza = new Product { Name = "Deal pizza", CategoryId = _pizzas.Id, BasePrice = 59000, IsRetailItem = true, Sizes = new() };
        _bigPizza = new Product { Name = "Deal big pizza", CategoryId = _pizzas.Id, BasePrice = 89000, IsRetailItem = true, Sizes = new() };
        // Default sizes: M at the base price, L +5.000đ.
        _tea = new Product { Name = "Deal tea", CategoryId = _drinks.Id, BasePrice = 39000 };
        foreach (var p in new[] { _pizza, _bigPizza, _tea }) Store.SaveProduct(p);
    }

    public void Dispose()
    {
        TestHost.Stop();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dataDirectory, true); }
        catch (IOException) { }
    }

    private OrderItem Line(Product product, int quantity = 1, string size = "M") =>
        OrderConfigurationService.Create(product, size, 100, 100, quantity, Array.Empty<(string, int)>(), "", Store.Products);

    private Promotion PizzaAndDrink(decimal price = 79000, DateTime? from = null, DateTime? to = null)
    {
        var promo = new Promotion
        {
            Type = "Combo", Name = "Combo Pizza & Nước", IsActive = true, ComboPrice = price, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 59),
            Slots = new() { new ComboSlot { CategoryIds = new() { _pizzas.Id } }, new ComboSlot { CategoryIds = new() { _drinks.Id } } },
            ValidFrom = from, ValidTo = to
        };
        Store.SavePromotion(promo);
        return promo;
    }

    private decimal Saving(params OrderItem[] cart) => Store.PromotionsFor(cart, Today.AddHours(12)).Amount;

    [Fact]
    public void Any_item_of_each_slot_completes_the_combo()
    {
        PizzaAndDrink();
        Assert.Equal(59000 + 39000 - 79000, Saving(Line(_pizza), Line(_tea)));
        Assert.Equal(0, Saving(Line(_pizza)));
    }

    [Fact]
    public void Larger_size_is_still_charged_on_top_of_the_combo_price()
    {
        PizzaAndDrink();
        // L costs 5.000đ more; the saving stays that of the listed price.
        Assert.Equal(59000 + 39000 - 79000, Saving(Line(_pizza), Line(_tea, size: "L")));
    }

    [Fact]
    public void Each_set_needs_one_unit_per_slot_and_takes_the_dearest()
    {
        PizzaAndDrink();
        Assert.Equal(89000 + 39000 - 79000, Saving(Line(_pizza), Line(_bigPizza), Line(_tea)));
        Assert.Equal((89000 + 39000 - 79000) + (59000 + 39000 - 79000), Saving(Line(_pizza), Line(_bigPizza), Line(_tea, 2)));
    }

    [Fact]
    public void Combo_price_above_the_items_gives_no_negative_discount()
    {
        PizzaAndDrink(price: 150000);
        Assert.Equal(0, Saving(Line(_pizza), Line(_tea)));
    }

    [Theory]
    [InlineData(-1, 1, true)]   // running
    [InlineData(1, 3, false)]   // not started
    [InlineData(-3, -1, false)] // ended
    [InlineData(0, 0, true)]    // single day, inclusive
    public void Date_range_limits_when_a_promotion_applies(int fromDays, int toDays, bool applies)
    {
        var promo = PizzaAndDrink(from: Today.AddDays(fromDays), to: Today.AddDays(toDays));
        Assert.Equal(applies, promo.RunsOn(Today.AddHours(12)));
        Assert.Equal(applies, Saving(Line(_pizza), Line(_tea)) > 0);
    }

    [Fact]
    public void Invalid_combos_are_rejected()
    {
        var oneSlot = new Promotion { Type = "Combo", Name = "Một suất", ComboPrice = 50000, Slots = new() { new ComboSlot { CategoryIds = new() { _pizzas.Id } } } };
        Assert.Throws<InvalidOperationException>(() => Store.SavePromotion(oneSlot));
        var emptySlot = new Promotion { Type = "Combo", Name = "Suất trống", ComboPrice = 50000, Slots = new() { new ComboSlot { CategoryIds = new() { _pizzas.Id } }, new ComboSlot() } };
        Assert.Throws<InvalidOperationException>(() => Store.SavePromotion(emptySlot));
        var backwards = new Promotion { Type = "Combo", Name = "Ngược ngày", ComboPrice = 50000, ValidFrom = Today, ValidTo = Today.AddDays(-1),
            Slots = new() { new ComboSlot { CategoryIds = new() { _pizzas.Id } }, new ComboSlot { CategoryIds = new() { _drinks.Id } } } };
        Assert.Throws<InvalidOperationException>(() => Store.SavePromotion(backwards));
    }

    [Fact]
    public void Older_product_list_combos_still_apply()
    {
        Store.SavePromotion(new Promotion { Type = "Combo", Name = "Cũ", IsActive = true, ComboPrice = 80000, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 59), ProductIds = new() { _pizza.Id, _tea.Id } });
        Assert.Equal(59000 + 39000 - 80000, Saving(Line(_pizza), Line(_tea)));
    }

    [Fact]
    public void Fixed_price_bundle_items_are_retired_but_kept()
    {
        foreach (var id in DataStoreService.BundleDealIds)
        {
            var bundle = Store.Products.SingleOrDefault(p => p.Id == id);
            Assert.NotNull(bundle);
            Assert.False(bundle!.IsActive);
        }
    }

    [Fact]
    public void Deal_section_lists_items_of_running_promotions_with_their_offer()
    {
        var vm = new MainViewModel(Store, Store, Store, Store, Store, Store, TestHost.Auth, TestHost.Holds, TestHost.Stickers, Store);
        vm.ReloadMenu();
        Assert.DoesNotContain(vm.CategoryList, c => c.Id == MainViewModel.Deals);

        PizzaAndDrink();
        vm.ReloadMenu();
        Assert.Contains(vm.CategoryList, c => c.Id == MainViewModel.Deals && c.Highlight == "featured");
        vm.SelectCategory(MainViewModel.Deals);
        Assert.Equal(new[] { _bigPizza.Id, _pizza.Id, _tea.Id }.OrderBy(x => x), vm.MenuItems.Select(t => t.Product.Id).OrderBy(x => x));
        Assert.All(vm.MenuItems, t => Assert.StartsWith("Combo Pizza & Nước · 79", t.MetaText)); // thousands separator follows the culture
    }

    [Fact]
    public void Empty_groups_are_left_out_of_the_rail()
    {
        var empty = new Category { Name = "Deal empty" };
        Store.SaveCategory(empty);
        var vm = new MainViewModel(Store, Store, Store, Store, Store, Store, TestHost.Auth, TestHost.Holds, TestHost.Stickers, Store);
        vm.ReloadMenu();
        Assert.DoesNotContain(vm.CategoryList, c => c.Id == empty.Id);
        Assert.Contains(vm.CategoryList, c => c.Id == _pizzas.Id);
    }
}
