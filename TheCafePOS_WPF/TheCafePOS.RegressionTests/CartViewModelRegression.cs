using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.ViewModels;

internal static class CartViewModelRegression
{
    public static void Run()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
        var auth = AuthService.Instance;
        var store = DataStoreService.Instance;
        auth.CreateAccount("cart-owner", "CartPass!2026", "Owner");
        auth.Login("cart-owner", "CartPass!2026");
        var category = new Category { Name = "Cart test" }; store.SaveCategory(category);
        var product = new Product { Name = "Cart latte", CategoryId = category.Id, BasePrice = 25000 };
        var soldOut = new Product { Name = "Cart cold brew", CategoryId = category.Id, BasePrice = 30000 };
        store.SaveProduct(product); store.SaveProduct(soldOut);
        store.SetProductSoldOut(soldOut.Id, true);
        OrderItem Make(int quantity) => OrderConfigurationService.Create(product, "M", 100, 100, quantity, Array.Empty<(string, int)>(), "", store.Products);

        var vm = new MainViewModel(store, store, store, store, store, store, auth, HoldOrderService.Instance, StickerPrinterService.Instance);
        var messages = new List<PosMessage>();
        var changed = new List<string?>();
        vm.MessageRaised += (_, m) => messages.Add(m);
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.LoadNextOrderNumber();
        Check(!vm.HasItems && vm.PayableTotal == 0 && vm.CartStartedAt is null, "New order starts empty with no start time");
        Check(vm.IsCartEmpty && vm.CartTitle == "Đơn mới" && vm.GrandTotalText == "0đ" && !vm.CanPay, "Empty cart shows guidance and cannot be paid");

        // Menu filtering
        vm.SelectCategory(category.Id);
        var menu = vm.FilterProducts("cart");
        Check(menu.Count == 2 && menu[0].Id == product.Id && menu[1].Id == soldOut.Id, "Category filter keeps sold-out products visible but last");
        Check(vm.FilterProducts("  LATTE ").Single().Id == product.Id, "Search is trimmed and case-insensitive");
        Check(vm.FilterProducts("không-có-món").Count == 0, "Unknown keyword returns no products");
        vm.SelectCategory(MainViewModel.AllCategories);

        // Menu grid bindings
        vm.ReloadMenu();
        Check(vm.CategoryList[0].Id == MainViewModel.AllCategories && vm.CategoryList[0].IsSelected && vm.CategoryList.Any(c => c.Id == category.Id), "Menu reload lists active categories with Tất cả selected");
        var chips = vm.CategoryList;
        vm.SelectCategory(category.Id);
        Check(ReferenceEquals(chips, vm.CategoryList) && chips.Single(c => c.IsSelected).Id == category.Id, "Selecting a category flips chips in place");
        Check(vm.MenuItems.Select(t => t.Product.Id).SequenceEqual(new[] { product.Id, soldOut.Id }) && vm.MenuCountText == "2 món", "Menu grid shows the category with sold-out items last");
        vm.SearchText = "latte";
        Check(vm.MenuItems.Single().Name == product.Name && !vm.HasNoProducts, "Typing in search refilters the grid");
        vm.SearchText = "không-có-món";
        Check(vm.HasNoProducts, "No match shows the empty state");
        vm.ResetMenuFilter();
        Check(vm.SearchText == "" && vm.SelectedCategoryId == MainViewModel.AllCategories && vm.CategoryList[0].IsSelected, "Reset clears search and returns to Tất cả");
        var tile = new MenuTile(soldOut);
        Check(tile.IsSoldOut && tile.SoldOutMenuText == "Báo có hàng lại" && tile.AutomationName.EndsWith("hết hàng"), "Sold-out tile offers restocking and announces its state");
        vm.ToggleSoldOut(soldOut);
        Check(!store.Products.Single(p => p.Id == soldOut.Id).IsSoldOut && vm.MenuItems.Any(t => t.Product.Id == soldOut.Id && !t.IsSoldOut), "Restocking refreshes the grid");
        vm.ToggleSoldOut(soldOut);
        var discounted = new MenuTile(new Product { Name = "Giảm giá", BasePrice = 20000, OriginalPrice = 25000, Sizes = new() });
        Check(discounted.MetaText == $"{25000:N0}đ" && discounted.MetaStrikethrough, "Old price alone is struck through");

        // Cart lines
        var line = Make(2);
        vm.AddItem(line);
        Check(vm.Subtotal == 50000 && vm.TotalCups == 2 && vm.CartStartedAt is not null, "Adding a line updates subtotal, cup count and start time");
        Check(changed.Contains(string.Empty), "Cart changes notify every binding");
        Check(vm.CanPay && vm.CartTitle == $"Đơn #{vm.CurrentDailyOrderNumber:D2}" && vm.ItemCountText == "Tổng cộng (2 món)" && vm.GrandTotalText == $"{50000:N0}đ", "Display text follows the cart");
        vm.IncreaseQuantityCommand.Execute(line);
        vm.DecreaseQuantityCommand.Execute(line);
        Check(line.Quantity == 2, "Quantity commands change the bound line");
        Check(vm.IncreaseQuantity(line) && vm.Subtotal == 75000, "Increasing quantity reprices the line");
        line.Quantity = MainViewModel.MaxLineQuantity;
        messages.Clear();
        Check(!vm.IncreaseQuantity(line) && line.Quantity == MainViewModel.MaxLineQuantity, "Quantity is capped per line");
        Check(messages.Single().IsError, "The cap is reported to the cashier");
        line.Quantity = 1;
        Check(!vm.DecreaseQuantity(line) && line.Quantity == 1, "Quantity never drops below one");
        line.Quantity = 2;

        var second = Make(1);
        vm.AddItem(second);
        var undo = vm.RemoveItem(line);
        Check(undo is not null && vm.CartItems.Count == 1, "Removing a line returns an undo action");
        undo!();
        Check(vm.CartItems.IndexOf(line) == 0 && vm.CartItems.Count == 2, "Undo restores the line at its original position");
        undo = vm.RemoveItem(line);
        string oldCheckout = vm.CheckoutId;
        vm.ClearCart();
        undo!();
        Check(vm.CartItems.Count == 0 && vm.CheckoutId != oldCheckout && vm.CartStartedAt is null, "Undo is ignored once the cart was cleared for a new order");

        var splitSource = Make(3);
        vm.AddItem(splitSource);
        vm.SplitItem(splitSource, Make(1));
        Check(vm.CartItems.Count == 2 && splitSource.Quantity == 2 && vm.TotalCups == 3, "Splitting moves one unit into its own line");
        messages.Clear();
        vm.RemoveItemCommand.Execute(splitSource);
        var removed = messages.Single();
        Check(vm.CartItems.Count == 1 && removed.ActionLabel == "Hoàn tác" && removed.Action is not null, "Remove command offers undo through a message");
        removed.Action!();
        Check(vm.CartItems.IndexOf(splitSource) == 0, "Undo from the message restores the line");
        vm.ClearCart();

        // Channels and adjustments
        vm.AddItem(Make(2));
        vm.SetCustomer("0900000000", 0);
        vm.ApplyDiscount(5000, "Khách quen");
        vm.SetServiceTypeCommand.Execute(MainViewModel.DineIn);
        Check(vm.IsDineIn && !vm.IsTakeAway, "Service type command selects dine-in");
        var options = vm.ChannelOptions;
        vm.Channel = "GrabFood";
        Check(ReferenceEquals(options, vm.ChannelOptions), "Channel options keep their instance so the bound picker keeps its selection");
        Check(vm.CashButtonText.Contains("GrabFood") && !vm.IsCounterOrder, "Cash button records app orders for the channel");
        Check(vm.IsAppOrder && vm.ChannelSurcharge == 13000, "App channel adds its markup rounded up to 1.000đ");
        Check(vm.Discount == 0 && vm.CustomerPhone == "" && vm.ServiceType == MainViewModel.DefaultServiceType && vm.PromoAmount == 0, "App orders drop in-store discount, membership, dine-in and promotions");
        Check(vm.PayableTotal == 63000, "App order total is subtotal plus channel surcharge");
        vm.SetChannel(null);
        Check(!vm.IsAppOrder && vm.ChannelSurcharge == 0, "Unknown channel falls back to the counter");

        vm.ApplyDiscount(4000, "Khách quen");
        Check(vm.ShowDiscountLine && vm.ShowSubtotalLine && vm.DiscountButtonText == "Sửa giảm giá", "Discount shows its line and the subtotal");
        messages.Clear();
        vm.ApplyDiscount(vm.Subtotal + 1000, "Quá mức");
        Check(vm.Discount == 0 && vm.DiscountReason == "" && messages.Single().IsError, "Discount larger than the total is dropped and reported");
        Check(!vm.DropInvalidAdjustments(), "Nothing is reported when there is nothing to drop");

        Check(MainViewModel.SuggestCashNotes(47000).SequenceEqual(new[] { 50000m, 60000m, 100000m, 200000m }), "Cash suggestions for 47k are 50k, 60k, 100k, 200k");
        Check(MainViewModel.SuggestCashNotes(50000).SequenceEqual(new[] { 60000m, 100000m, 200000m, 500000m }), "Cash suggestions skip the exact amount");

        // Checkout
        Check(vm.CheckoutNotice.Contains("Chưa mở ca") && !vm.ShiftOpen, "Notice explains a closed shift");
        Check(!vm.BeginCheckout() && vm.CheckoutError is not null && !vm.CheckoutInProgress, "Checkout cannot start without an open shift");
        Check(vm.HasCheckoutNotice && vm.CheckoutNotice == vm.CheckoutError && vm.ShowOpenShiftHint, "Payment error replaces the shift hint");
        vm.SetServiceType(MainViewModel.DefaultServiceType);
        Check(vm.CheckoutError is null, "Editing the order clears the previous payment error");
        store.OpenShift(0);
        foreach (var id in new[] { "pack-1", "pack-3", "pack-5" }) store.UpdateStock(id, "Kiểm kê", 10, "Cart test");
        Check(vm.BeginCheckout() && vm.CheckoutInProgress && vm.CheckoutError is null, "Checkout starts once the shift is open");
        Check(!vm.BeginCheckout(), "A second checkout cannot start while one is in progress");
        var lockedUndo = vm.RemoveItem(vm.CartItems[0]);
        lockedUndo!();
        Check(vm.CartItems.Count == 0, "Undo is ignored while a checkout is in progress");
        vm.SetCheckoutError("Lỗi thử");
        vm.EndCheckout();
        Check(vm.CheckoutError == "Lỗi thử" && vm.CanEditCart, "Ending checkout unlocks the cart but keeps its error visible");
        vm.AddItem(Make(2));
        Check(vm.BeginCheckout(), "Checkout restarts after the previous attempt ended");
        int orderNumber = vm.CurrentDailyOrderNumber;
        string checkoutId = vm.CheckoutId;
        decimal payable = vm.PayableTotal;
        var order = vm.CompleteCheckout("Cash", payable);
        Check(order.TotalAmount == payable && store.CompletedOrders.Any(o => o.Id == order.Id), "Completing checkout records the order at the payable total");
        vm.StartNewOrder();
        vm.EndCheckout();
        Check(!vm.HasItems && vm.CheckoutId != checkoutId && vm.CurrentDailyOrderNumber == orderNumber + 1 && vm.Channel == StoreSettings.Counter, "Next order gets a new id, number and default channel");
        Check(vm.PrintStickers(order) is null, "Stickers are generated for the completed order");

        // Held orders
        int held = vm.HeldOrderCount;
        vm.AddItem(Make(1));
        string beforeHold = vm.CheckoutId;
        vm.HoldCurrentOrder();
        Check(!vm.HasItems && vm.HeldOrderCount == held + 1 && vm.CheckoutId != beforeHold, "Holding an order empties the cart under a new id");
        var heldOrder = HoldOrderService.Instance.HeldOrders.Last();
        vm.RecallHeldOrder(heldOrder);
        Check(vm.TotalCups == 1 && vm.Subtotal == 25000, "Recalling a held order restores its lines");
    }
}
