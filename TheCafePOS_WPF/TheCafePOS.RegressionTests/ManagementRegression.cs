using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

internal static class ManagementRegression
{
    public static void Run()
    {
        void Check(bool valid, string message)
        {
            if (!valid) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        void Reject(Action action, string message)
        {
            try { action(); } catch (InvalidOperationException) { Console.WriteLine("PASS " + message); return; }
            throw new Exception("Expected rejection: " + message);
        }
        var store = DataStoreService.Instance;
        var auth = AuthService.Instance;
        auth.CreateAccount("mgmt-owner", "OwnerPass!2026", "Owner");
        auth.Login("mgmt-owner", "OwnerPass!2026");

        // Products and categories
        var category = new Category { Name = "Mgmt category" };
        store.SaveCategory(category);
        var topping = new Product { Name = "Mgmt topping", CategoryId = category.Id, BasePrice = 5000, IsTopping = true, SoldSeparately = false, Sizes = new() };
        store.SaveProduct(topping);
        var drink = new Product { Name = "Mgmt drink", CategoryId = category.Id, BasePrice = 30000, AllowedToppingIds = new() { topping.Id } };
        store.SaveProduct(drink);
        Reject(() => store.DeleteCategory(category.Id), "Category with products cannot be deleted");
        store.DeleteProduct(topping.Id);
        Check(store.Products.All(p => p.Id != topping.Id) && !store.Products.Single(p => p.Id == drink.Id).AllowedToppingIds.Contains(topping.Id), "Deleting a topping removes it from drinks");
        store.SaveProduct(store.Products.Single(p => p.Id == drink.Id));
        Check(true, "Drink stays savable after its topping is deleted");
        store.DeleteProduct(drink.Id);
        store.DeleteCategory(category.Id);
        Check(store.Categories.All(c => c.Id != category.Id), "Empty category can be deleted");

        var cascade = new Category { Name = "Mgmt cascade" };
        store.SaveCategory(cascade);
        var cascadeDrink = new Product { Name = "Mgmt cascade drink", CategoryId = cascade.Id, BasePrice = 25000 };
        store.SaveProduct(cascadeDrink);
        store.DeleteCategory(cascade.Id, deleteProducts: true);
        Check(store.Categories.All(c => c.Id != cascade.Id) && store.Products.All(p => p.Id != cascadeDrink.Id), "Category can be deleted together with its products");

        // Packaging catalog
        Reject(() => store.DeletePackagingItem("pack-3"), "Core lid packaging cannot be deleted");
        Reject(() => store.SavePackagingItem(new PackagingItem { Id = "x", Name = " ", Unit = "Cái" }), "Packaging needs a name");
        Reject(() => store.SavePackagingItem(new PackagingItem { Id = "x", Name = "Vỏ Ly Size M (500ml)", Unit = "Cái" }), "Packaging name must be unique");
        store.SavePackagingItem(new PackagingItem { Id = "pack-test", Name = "Mgmt cup", Unit = "Cái", WarningThreshold = 5, StockQuantity = 999 });
        Check(store.PackagingInventory.Single(p => p.Id == "pack-test").StockQuantity == 0, "New packaging starts at zero stock");
        store.UpdateStock("pack-test", "Nhập kho", 10, "Mgmt receipt");
        store.SavePackagingItem(new PackagingItem { Id = "pack-test", Name = "Mgmt cup renamed", Unit = "Cái", WarningThreshold = 3 });
        var renamed = store.PackagingInventory.Single(p => p.Id == "pack-test");
        Check(renamed.Name == "Mgmt cup renamed" && renamed.StockQuantity == 10, "Editing packaging keeps its stock");
        Reject(() => store.DeletePackagingItem("pack-test"), "Packaging with stock cannot be deleted");
        store.UpdateStock("pack-test", "Kiểm kê", 0, "Mgmt count");
        store.DeletePackagingItem("pack-test");
        Check(store.PackagingInventory.All(p => p.Id != "pack-test"), "Zero-stock custom packaging can be deleted");

        // Loyalty and time-window promotion
        Reject(() => store.RegisterCustomer("12345", ""), "Reject invalid member phone");
        var member = store.RegisterCustomer("+84 912 345 678", "Lan");
        Check(member.Phone == "0912345678" && store.FindCustomer("0912-345-678") == member, "Phone numbers are normalized for lookup");
        var promoCategory = new Category { Name = "Mgmt loyalty" };
        store.SaveCategory(promoCategory);
        var loyaltyDrink = new Product { Name = "Mgmt loyalty food", CategoryId = promoCategory.Id, BasePrice = 50000, IsRetailItem = true, Sizes = new() };
        store.SaveProduct(loyaltyDrink);
        var line = OrderConfigurationService.Create(loyaltyDrink, "", 100, 100, 1, Array.Empty<(string, int)>(), "", store.Products);
        store.OpenShift(0);
        var first = store.Checkout(new[] { line }, "Cash", 50000, "loyalty-1", customerPhone: member.Phone);
        Check(first.PointsEarned == 5 && member.Points == 5 && member.VisitCount == 1, "Member earns 1 point per 10k paid");
        Reject(() => store.Checkout(new[] { line }, "Cash", 50000, "loyalty-over", customerPhone: member.Phone, redeemPoints: 6), "Cannot redeem more points than owned");
        var now = DataStoreService.CafeNow;
        store.SavePromotion(new Promotion { Name = "Test giờ vàng", IsActive = true, Percent = 10, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 59) });
        var second = store.Checkout(new[] { line }, "Cash", 50000, "loyalty-2", customerPhone: member.Phone, redeemPoints: 5);
        Check(second.PromotionAmount == 5000 && second.LoyaltyAmount == 5000 && second.TotalAmount == 40000, "Promotion and redeemed points both reduce the total");
        Check(member.Points == 4, "Redeemed points are deducted and new points earned on the net total");
        store.SavePromotion(new Promotion { Id = store.Promotions[0].Id, Name = "Test giờ vàng", IsActive = false, Percent = 10, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 59) });
        store.CloseShift(store.CompletedOrders.Where(o => o.ShiftId == store.CurrentShift.Id && o.PaymentMethod == "Cash").Sum(o => o.TotalAmount));

        // Staff accounts
        auth.CreateAccount("mgmt-cashier", "CashierPass!2026", "Cashier");
        auth.UpdateAccountRole("mgmt-cashier", "Manager");
        Check(auth.Accounts.Single(a => a.Username == "mgmt-cashier").Role == "Manager", "Owner can promote cashier to manager");
        Reject(() => auth.DeleteAccount("mgmt-owner"), "Cannot delete own owner account");
        auth.UpdateAccountRole("mgmt-cashier", "Cashier");
        auth.Login("mgmt-cashier", "CashierPass!2026");
        store.OpenShift(0);
        auth.Login("mgmt-owner", "OwnerPass!2026");
        Reject(() => auth.DeleteAccount("mgmt-cashier"), "Cannot delete staff holding an open shift");
        auth.Login("mgmt-cashier", "CashierPass!2026");
        store.CloseShift(0);
        auth.Login("mgmt-owner", "OwnerPass!2026");
        auth.DeleteAccount("mgmt-cashier");
        Check(auth.Accounts.All(a => a.Username != "mgmt-cashier"), "Staff account can be deleted after shift closes");
        Reject(() => auth.Login("mgmt-cashier", "CashierPass!2026"), "Deleted account cannot log in");
    }
}
