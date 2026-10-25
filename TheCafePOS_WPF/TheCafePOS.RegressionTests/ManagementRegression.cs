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
        var store = TestHost.Store;
        var auth = TestHost.Auth;
        auth.CreateAccount("mgmt-owner", "OwnerPass!2026", "Owner");
        auth.Login("mgmt-owner", "OwnerPass!2026");

        // Featured sections mirrored from The Coffee House
        Check(store.Categories.Any(c => c.Id == DataStoreService.NewCategoryId) && store.Categories.Any(c => c.Id == DataStoreService.DealCategoryId), "Featured menu sections are seeded");
        Check(store.Products.Single(p => p.Id == "deal-combo-pizza-nuoc") is { BasePrice: 79000, OriginalPrice: 98000 }, "Deal combo keeps its struck-through original price");
        Check(store.Products.Any(p => p.FeaturedCategoryIds.Contains(DataStoreService.NewCategoryId) && p.CategoryId != DataStoreService.NewCategoryId), "New items also stay in their own category");

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
        auth.Approve("mgmt-owner", "OwnerPass!2026", DataStoreService.RefundAction("refund-loyalty", second.Id, 40000, "Khách trả món", ""));
        store.RefundOrder("refund-loyalty", second.Id, 40000, "Khách trả món", "");
        Check(member.Points == 5, "Full refund revokes earned points and returns redeemed points");
        store.SavePromotion(new Promotion { Id = store.Promotions[0].Id, Name = "Test giờ vàng", IsActive = false, Percent = 10, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 59) });
        store.CloseShift(store.CompletedOrders.Where(o => o.ShiftId == store.CurrentShift.Id && o.PaymentMethod == "Cash").Sum(o => o.TotalAmount));

        // Member edit / delete
        var other = store.RegisterCustomer("0987654321", "Minh");
        Reject(() => store.UpdateCustomer(member.Phone, "Lan", other.Phone), "Cannot move a member onto another member's phone");
        int lanOrders = store.CustomerOrders(member.Phone).Count;
        store.UpdateCustomer(member.Phone, "Lan Anh", "0911222333");
        Check(member.Phone == "0911222333" && member.Name == "Lan Anh" && store.FindCustomer("0912345678") is null, "Member phone and name can be changed");
        Check(store.CustomerOrders("0911222333").Count == lanOrders && store.CustomerOrders("0912345678").Count == 0, "Changing phone moves purchase history");
        store.DeleteCustomer(other.Phone);
        Check(store.FindCustomer("0987654321") is null, "Member can be deleted");
        Reject(() => store.DeleteCustomer("0987654321"), "Deleting a missing member is rejected");

        // Ingredient inventory by recipe
        var beans = new Ingredient { Name = "Mgmt beans", Unit = "g", WarningThreshold = 5 };
        store.SaveIngredient(beans);
        store.SaveRecipe(loyaltyDrink.Id, new() { new RecipeLine { IngredientId = beans.Id, Amount = 10 } });
        store.AdjustIngredient(beans.Id, "Nhập kho", 25, "Mgmt receipt");
        Reject(() => store.DeleteIngredient(beans.Id), "Ingredient used in a recipe cannot be deleted");
        store.OpenShift(0);
        var twoUnits = OrderConfigurationService.Clone(line); twoUnits.Quantity = 2;
        store.Checkout(new[] { twoUnits }, "Cash", 100000, "recipe-1");
        Check(store.Ingredients.Single(i => i.Id == beans.Id).Stock == 5 && !store.Products.Single(p => p.Id == loyaltyDrink.Id).IsSoldOut, "Sale deducts recipe quantity");
        store.Checkout(new[] { line }, "Cash", 50000, "recipe-2");
        Check(store.Products.Single(p => p.Id == loyaltyDrink.Id) is { IsSoldOut: true, SoldOutByStock: true }, "Empty ingredient marks product sold out");
        Reject(() => store.Checkout(new[] { line }, "Cash", 50000, "recipe-3"), "Sold-out product cannot be sold");
        store.AdjustIngredient(beans.Id, "Nhập kho", 20, "Mgmt receipt 2");
        Check(!store.Products.Single(p => p.Id == loyaltyDrink.Id).IsSoldOut && store.Ingredients.Single(i => i.Id == beans.Id).Stock == 15, "Restock reopens product automatically");
        store.CloseShift(store.CompletedOrders.Where(o => o.ShiftId == store.CurrentShift.Id && o.PaymentMethod == "Cash").Sum(o => o.TotalAmount));

        // Channels, promotions, cancellation, settings
        store.SaveSettings(new StoreSettings { ShopName = "Mgmt Cafe", CashierDiscountLimitPercent = 10 });
        Check(store.Settings.Channels.Any(c => c.Name == "GrabFood" && c.MarkupPercent == 25), "Default delivery channels exist");
        store.OpenShift(0);
        Reject(() => store.Checkout(new[] { line }, "Cash", 100000, "app-cash", channel: "GrabFood"), "App orders must be recorded as platform payments");
        var appOrder = store.Checkout(new[] { line }, "Platform", 0, "app-1", channel: "GrabFood");
        Check(appOrder.ChannelSurcharge == 13000 && appOrder.TotalAmount == 63000 && appOrder.ServiceType == "Mang đi", "Channel markup is added and rounded up to 1.000đ");
        var cheap = new Product { Name = "Mgmt cheap", CategoryId = promoCategory.Id, BasePrice = 20000, IsRetailItem = true, Sizes = new() };
        store.SaveProduct(cheap);
        var cheapLine = OrderConfigurationService.Create(cheap, "", 100, 100, 1, Array.Empty<(string, int)>(), "", store.Products);
        var bxgy = new Promotion { Type = "Mua X tặng Y", Name = "Mua 2 tặng 1", IsActive = true, BuyQuantity = 2, FreeQuantity = 1, ProductIds = new() { loyaltyDrink.Id, cheap.Id }, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 59) };
        store.SavePromotion(bxgy);
        var bxgyCart = new[] { twoUnits, cheapLine };
        Check(store.PromotionsFor(bxgyCart, DataStoreService.CafeNow).Amount == 20000, "Buy 2 get 1 frees the cheapest unit");
        Check(store.PromotionsFor(new[] { twoUnits }, DataStoreService.CafeNow).Amount == 0, "Buy 2 get 1 needs a complete group");
        store.DeletePromotion(bxgy.Id);
        store.SavePromotion(new Promotion { Type = "Combo", Name = "Combo bánh", IsActive = true, ProductIds = new() { loyaltyDrink.Id, cheap.Id }, ComboPrice = 60000, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 59) });
        Check(store.PromotionsFor(new[] { line, cheapLine }, DataStoreService.CafeNow).Amount == 10000, "Combo charges the fixed combo price");
        store.DeletePromotion(store.Promotions.Single(p => p.Type == "Combo").Id);
        decimal beansBefore = store.Ingredients.Single(i => i.Id == beans.Id).Stock;
        var toCancel = store.Checkout(new[] { line }, "Cash", 50000, "cancel-1");
        auth.Approve("mgmt-owner", "OwnerPass!2026", DataStoreService.RefundAction("cancel-refund", toCancel.Id, toCancel.TotalAmount, DataStoreService.CancelReason("Khách đổi ý"), ""));
        store.CancelOrder("cancel-refund", toCancel.Id, "Khách đổi ý", "");
        Check(toCancel.IsCancelled && toCancel.RefundedAmount == toCancel.TotalAmount && store.Ingredients.Single(i => i.Id == beans.Id).Stock == beansBefore, "Cancelling refunds in full and returns ingredients");
        var report = ReportService.Build(store.CompletedOrders, DateTime.Today, DateTime.Today, store.CurrentShift.Id, store.Refunds, store.CashEntries);
        Check(report.Orders.Contains(toCancel) && report.Refunds.Count(r => r.OrderId == toCancel.Id) == 1 && report.Refunds.Single(r => r.OrderId == toCancel.Id).Amount == toCancel.TotalAmount, "Cancelled order stays in sales once and is netted by exactly one full refund");
        string xlsx = Path.Combine(Path.GetTempPath(), $"mgmt-{Guid.NewGuid():N}.xlsx");
        XlsxWriter.Write(xlsx, new (string, IReadOnlyList<string>, IEnumerable<object?[]>)[] { ("Test", new[] { "A", "B" }, new[] { new object?[] { "x & y", 12000m } }) });
        using (var zip = System.IO.Compression.ZipFile.OpenRead(xlsx)) Check(zip.GetEntry("xl/worksheets/sheet1.xml") != null && zip.GetEntry("xl/workbook.xml") != null, "Excel export writes a valid workbook package");
        store.CloseShift(store.CompletedOrders.Where(o => o.ShiftId == store.CurrentShift.Id && o.PaymentMethod == "Cash").Sum(o => o.RefundableAmount));

        // Staff accounts
        auth.CreateAccount("mgmt-cashier", "CashierPass!2026", "Cashier");
        Reject(() => auth.CreateAccount("mgmt-manager", "ManagerPass!2026", "Manager"), "Owner cannot create a manager account");
        Reject(() => auth.DeleteAccount("mgmt-owner"), "Cannot delete own owner account");
        // Data saved before the Quản lý role was dropped.
        var legacy = TestHost.Database.Read<AuthState>("auth")!;
        legacy.Accounts.Single(a => a.Username == "mgmt-cashier").Role = "Manager";
        TestHost.Database.Write("auth", legacy);
        Check(TestHost.NewAuth().Accounts.Single(a => a.Username == "mgmt-cashier").Role == "Cashier", "Legacy manager account loads as staff");
        legacy.Accounts.Single(a => a.Username == "mgmt-cashier").Role = "Cashier";
        TestHost.Database.Write("auth", legacy);
        auth.Login("mgmt-cashier", "CashierPass!2026");
        Check(!store.DiscountNeedsApproval(100000, 10000) && store.DiscountNeedsApproval(100000, 10001), "Cashier discount above the limit needs approval");
        store.OpenShift(0);
        auth.Login("mgmt-owner", "OwnerPass!2026");
        Reject(() => auth.DeleteAccount("mgmt-cashier"), "Cannot delete staff holding an open shift");
        auth.Login("mgmt-cashier", "CashierPass!2026");
        store.CloseShift(0);
        auth.Login("mgmt-owner", "OwnerPass!2026");
        Reject(() => auth.DeleteAccount("mgmt-cashier"), "Staff who worked a shift cannot be deleted");
        Reject(() => auth.CreateAccount("mgmt-cashier", "OtherPass!2026", "Cashier"), "A kept account's username cannot be reused");
        auth.SetAccountActive("mgmt-cashier", false);
        Reject(() => auth.Login("mgmt-cashier", "CashierPass!2026"), "Locked account cannot log in");
        Check(store.Shifts.Any(s => s.CashierName == "mgmt-cashier"), "Locking keeps the staff member's shift history");
        auth.CreateAccount("mgmt-unused", "UnusedPass!2026", "Cashier");
        auth.DeleteAccount("mgmt-unused");
        Check(auth.Accounts.All(a => a.Username != "mgmt-unused"), "Unused staff account can be deleted");
        Reject(() => auth.Login("mgmt-unused", "UnusedPass!2026"), "Deleted account cannot log in");
    }
}
