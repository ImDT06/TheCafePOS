using Microsoft.Data.Sqlite;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.ViewModels;
using Xunit;

/// <summary>
/// VAT, vouchers, mixed payment, bill splitting, receipts, purchasing, stock alerts and shrinkage.
/// Boundary values are listed as theory rows so each partition shows up as its own test case.
/// </summary>
public sealed class PosFeatureTests : IDisposable
{
    private const string Owner = "feature-owner", OwnerPass = "FeaturePass!2026";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "thecafepos-tests", Guid.NewGuid().ToString("N"));
    private Product _product = null!;

    private DataStoreService Store => TestHost.Store;
    private AuthService Auth => TestHost.Auth;
    private static DateTime Today => DataStoreService.CafeNow.Date;

    public PosFeatureTests()
    {
        TestHost.Start(_dataDirectory);
        Auth.CreateAccount(Owner, OwnerPass, "Owner");
        Auth.Login(Owner, OwnerPass);
        var category = new Category { Name = "Feature test" };
        Store.SaveCategory(category);
        _product = new Product { Name = "Feature coffee", CategoryId = category.Id, BasePrice = 50000 };
        Store.SaveProduct(_product);
        Store.OpenShift(100000);
    }

    public void Dispose()
    {
        TestHost.Stop();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dataDirectory, true); }
        catch (IOException) { }
    }

    private OrderItem Item(int quantity = 1) => OrderConfigurationService.Create(_product, "M", 100, 100, quantity, Array.Empty<(string, int)>(), "", Store.Products);
    private OrderItem[] Cart(int cups) => new[] { Item(cups) };
    private void Approve(string action) => Auth.Approve(Owner, OwnerPass, action);
    private void Restart() { TestHost.Start(_dataDirectory); Auth.Login(Owner, OwnerPass); }

    private void SetVat(int percent, bool included)
    {
        var s = Store.Settings;
        Store.SaveSettings(new StoreSettings { ShopName = "Feature Cafe", Address = "1 Test St", Phone = "0281234567", CashierDiscountLimitPercent = s.CashierDiscountLimitPercent, Channels = s.Channels, VatPercent = percent, PricesIncludeVat = included, ReceiptFooter = "Hẹn gặp lại" });
    }

    private Voucher SaveVoucher(string code, string type, decimal value, decimal min = 0, int limit = 0, decimal max = 0, DateTime? from = null, DateTime? to = null, bool active = true)
    {
        var v = new Voucher { Code = code, Type = type, Value = value, MinSubtotal = min, UsageLimit = limit, MaxDiscount = max, ValidFrom = from ?? Today, ValidTo = to ?? Today.AddDays(7), IsActive = active };
        Store.SaveVoucher(v);
        return Store.Vouchers.Single(x => x.Id == v.Id);
    }

    private Ingredient AddIngredient(string name, decimal stock, decimal threshold)
    {
        var ing = new Ingredient { Name = name, Unit = "g", WarningThreshold = threshold };
        Store.SaveIngredient(ing);
        if (stock > 0) Store.AdjustIngredient(ing.Id, "Nhập kho", stock, "Tồn đầu");
        return Store.Ingredients.Single(i => i.Id == ing.Id);
    }

    #region VAT

    [Theory]
    [InlineData(100000, 8, false, 8000)]
    [InlineData(100000, 8, true, 7407)]   // 100.000 × 8/108 = 7.407,4
    [InlineData(54000, 10, true, 4909)]
    [InlineData(12345, 8, false, 988)]    // 987,6 rounds up
    [InlineData(5, 10, false, 1)]         // 0,5 rounds away from zero
    [InlineData(4, 10, false, 0)]         // 0,4 rounds down
    [InlineData(0, 8, false, 0)]
    [InlineData(100000, 0, false, 0)]
    public void Vat_is_rounded_to_whole_dong(decimal net, int percent, bool included, decimal expected) =>
        Assert.Equal(expected, DataStoreService.VatFor(net, percent, included));

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Vat_setting_accepts_zero_to_ten_percent(int percent, bool accepted)
    {
        if (accepted) { SetVat(percent, true); Assert.Equal(percent, Store.Settings.VatPercent); }
        else Assert.Throws<InvalidOperationException>(() => SetVat(percent, true));
    }

    [Fact]
    public void Vat_on_top_is_added_to_the_total_and_collected()
    {
        SetVat(8, included: false);
        Assert.Throws<InvalidOperationException>(() => Store.Checkout(Cart(2), "Cash", 100000, "vat-short", serviceType: "Tại chỗ"));
        var order = Store.Checkout(Cart(2), "Cash", 108000, "vat-ok", serviceType: "Tại chỗ");
        Assert.Equal(8000, order.VatAmount);
        Assert.Equal(108000, order.TotalAmount);
        Assert.False(order.VatIncluded);
        Assert.Equal(0, order.ChangeReturned);
    }

    [Fact]
    public void Vat_included_is_shown_but_does_not_change_the_total()
    {
        SetVat(8, included: true);
        var order = Store.Checkout(Cart(2), "Cash", 100000, "vat-in", serviceType: "Tại chỗ");
        Assert.Equal(100000, order.TotalAmount);
        Assert.Equal(7407, order.VatAmount);
    }

    #endregion

    #region Vouchers

    [Theory]
    [InlineData(Voucher.Amount, 20000, 0, 50000, 20000)]
    [InlineData(Voucher.Amount, 60000, 0, 50000, 50000)]     // never more than the amount left
    [InlineData(Voucher.Percent, 10, 0, 55000, 5000)]        // 5.500 rounds down to 1.000đ
    [InlineData(Voucher.Percent, 50, 20000, 100000, 20000)]  // capped by MaxDiscount
    [InlineData(Voucher.Percent, 50, 0, 100000, 50000)]      // 0 = no cap
    [InlineData(Voucher.Percent, 100, 0, 30000, 30000)]
    public void Voucher_discount_by_type(string type, decimal value, decimal max, decimal baseAmount, decimal expected) =>
        Assert.Equal(expected, DataStoreService.VoucherDiscount(new Voucher { Type = type, Value = value, MaxDiscount = max }, baseAmount));

    [Theory]
    [InlineData("AB", Voucher.Amount, 10000)]         // code too short
    [InlineData("HELLO WORLD", Voucher.Amount, 10000)] // space not allowed
    [InlineData("ABCDEFGHIJKLMNOPQRSTU", Voucher.Amount, 10000)] // 21 characters
    [InlineData("PCT0", Voucher.Percent, 0)]
    [InlineData("PCT101", Voucher.Percent, 101)]
    [InlineData("AMT999", Voucher.Amount, 999)]
    [InlineData("FRACTION", Voucher.Amount, 1500.5)]
    [InlineData("BADTYPE", "Khác", 10000)]
    public void Invalid_vouchers_are_refused(string code, string type, decimal value) =>
        Assert.Throws<InvalidOperationException>(() => Store.SaveVoucher(new Voucher { Code = code, Type = type, Value = value }));

    [Fact]
    public void Voucher_codes_are_unique_case_insensitively_and_dates_must_be_ordered()
    {
        SaveVoucher("giam10", Voucher.Amount, 10000);
        Assert.NotNull(Store.FindVoucher("GIAM10"));
        Assert.Throws<InvalidOperationException>(() => SaveVoucher("GIAM10", Voucher.Amount, 5000));
        Assert.Throws<InvalidOperationException>(() => SaveVoucher("BACKWARD", Voucher.Amount, 5000, from: Today, to: Today.AddDays(-1)));
        SaveVoucher("ONEDAY", Voucher.Amount, 5000, from: Today, to: Today); // same day is a valid one-day voucher
    }

    [Theory]
    [InlineData(99999, false)]
    [InlineData(100000, true)]
    public void Voucher_minimum_subtotal_boundary(decimal subtotal, bool accepted)
    {
        SaveVoucher("MIN100", Voucher.Amount, 10000, min: 100000);
        if (accepted) Assert.Equal(10000, Store.EvaluateVoucher("MIN100", subtotal, 0, Today));
        else Assert.Throws<InvalidOperationException>(() => Store.EvaluateVoucher("MIN100", subtotal, 0, Today));
    }

    [Theory]
    [InlineData(-1, true)]   // day before ValidFrom
    [InlineData(0, false)]   // ValidFrom
    [InlineData(2, false)]   // ValidTo
    [InlineData(3, true)]    // day after ValidTo
    public void Voucher_validity_window_is_inclusive(int dayOffset, bool rejected)
    {
        SaveVoucher("WINDOW", Voucher.Amount, 10000, from: Today, to: Today.AddDays(2));
        void Run() => Store.EvaluateVoucher("WINDOW", 50000, 0, Today.AddDays(dayOffset));
        if (rejected) Assert.Throws<InvalidOperationException>(Run); else Run();
    }

    [Fact]
    public void Unknown_or_disabled_vouchers_are_refused()
    {
        SaveVoucher("OFF", Voucher.Amount, 10000, active: false);
        Assert.Throws<InvalidOperationException>(() => Store.EvaluateVoucher("NOPE", 50000, 0, Today));
        Assert.Throws<InvalidOperationException>(() => Store.EvaluateVoucher("OFF", 50000, 0, Today));
    }

    [Fact]
    public void Voucher_usage_limit_is_counted_at_checkout_and_returned_on_cancel()
    {
        var v = SaveVoucher("ONCE", Voucher.Amount, 10000, limit: 1);
        var order = Store.Checkout(Cart(1), "Cash", 50000, "once-1", serviceType: "Tại chỗ", voucherCode: "once");
        Assert.Equal(10000, order.VoucherAmount);
        Assert.Equal(40000, order.TotalAmount);
        Assert.Equal("ONCE", order.VoucherCode);
        Assert.Equal(1, Store.FindVoucher("ONCE")!.UsedCount);
        Assert.Throws<InvalidOperationException>(() => Store.Checkout(Cart(1), "Cash", 50000, "once-2", serviceType: "Tại chỗ", voucherCode: "ONCE"));
        Assert.Throws<InvalidOperationException>(() => Store.DeleteVoucher(v.Id));

        Approve(DataStoreService.RefundAction("cancel-once", order.Id, order.TotalAmount, DataStoreService.CancelReason("Khách đổi ý"), ""));
        Store.CancelOrder("cancel-once", order.Id, "Khách đổi ý", "");
        Assert.Equal(0, Store.FindVoucher("ONCE")!.UsedCount);
        Store.Checkout(Cart(1), "Cash", 50000, "once-3", serviceType: "Tại chỗ", voucherCode: "ONCE");
    }

    [Fact]
    public void Voucher_applies_after_promotions_and_cannot_zero_the_total()
    {
        SaveVoucher("HALF", Voucher.Percent, 50);
        Store.SavePromotion(new Promotion { Name = "Giờ vàng test", Type = "Giờ vàng", Percent = 20, IsActive = true, Start = TimeSpan.Zero, End = new TimeSpan(23, 59, 59) });
        var order = Store.Checkout(Cart(2), "Cash", 100000, "promo-voucher", serviceType: "Tại chỗ", voucherCode: "HALF");
        Assert.Equal(20000, order.PromotionAmount);
        Assert.Equal(40000, order.VoucherAmount); // 50% of 80.000 left after the promotion
        Assert.Equal(40000, order.TotalAmount);

        SaveVoucher("ALL", Voucher.Amount, 100000);
        Assert.Throws<InvalidOperationException>(() => Store.Checkout(Cart(2), "Cash", 100000, "zero", serviceType: "Tại chỗ", voucherCode: "ALL"));
        Assert.Throws<InvalidOperationException>(() => Store.Checkout(Cart(2), "Cash", 100000, "stack", serviceType: "Tại chỗ", discount: 40000, discountReason: "Khách quen", voucherCode: "HALF"));
    }

    [Fact]
    public void Delivery_app_orders_cannot_use_vouchers()
    {
        SaveVoucher("APP", Voucher.Amount, 10000);
        Assert.Throws<InvalidOperationException>(() => Store.Checkout(Cart(1), "Platform", 0, "app-voucher", channel: "GrabFood", voucherCode: "APP"));
    }

    #endregion

    #region Mixed payment

    [Fact]
    public void Mixed_payment_keeps_cash_in_the_drawer_and_transfer_out_of_it()
    {
        decimal drawer = Store.ExpectedCash;
        Approve(AuthService.PaymentApprovalAction("mixed", 30000));
        var order = Store.Checkout(Cart(2), "Mixed", 80000, "mixed", qrApproved: true, serviceType: "Tại chỗ", transferAmount: 30000);
        Assert.Equal(100000, order.TotalAmount);
        Assert.Equal(30000, order.TransferAmount);
        Assert.Equal(70000, order.CashPortion);
        Assert.Equal(10000, order.ChangeReturned);
        Assert.Equal(drawer + 70000, Store.ExpectedCash);

        var report = ReportService.Build(Store.CompletedOrders, Today, Today);
        Assert.Equal(70000, report.Cash);
        Assert.Equal(30000, report.Transfer);
    }

    [Theory]
    [InlineData(0, 100000)]       // no transfer part: use plain cash
    [InlineData(100000, 0)]       // whole total by transfer: use VietQR
    [InlineData(-1000, 101000)]
    [InlineData(30000, 69999)]    // cash 1đ short of its part
    public void Mixed_payment_rejects_invalid_splits(decimal transfer, decimal cash)
    {
        Approve(AuthService.PaymentApprovalAction("bad-mixed", transfer));
        Assert.Throws<InvalidOperationException>(() => Store.Checkout(Cart(2), "Mixed", cash, "bad-mixed", qrApproved: true, serviceType: "Tại chỗ", transferAmount: transfer));
        Assert.Empty(Store.CompletedOrders);
    }

    [Fact]
    public void Mixed_payment_needs_the_transfer_confirmed_for_that_amount()
    {
        Approve(AuthService.PaymentApprovalAction("mixed-approval", 20000));
        Assert.Throws<InvalidOperationException>(() => Store.Checkout(Cart(2), "Mixed", 70000, "mixed-approval", qrApproved: true, serviceType: "Tại chỗ", transferAmount: 30000));
        Assert.Throws<InvalidOperationException>(() => Store.Checkout(Cart(2), "Cash", 100000, "cash-with-transfer", serviceType: "Tại chỗ", transferAmount: 30000));
    }

    [Fact]
    public void Mixed_refunds_give_cash_back_first_and_need_a_reference_for_the_rest()
    {
        Approve(AuthService.PaymentApprovalAction("mixed-refund", 30000));
        var order = Store.Checkout(Cart(2), "Mixed", 70000, "mixed-refund", qrApproved: true, serviceType: "Tại chỗ", transferAmount: 30000);
        decimal drawer = Store.ExpectedCash;

        Approve(DataStoreService.RefundAction("r1", order.Id, 50000, "Món lỗi", ""));
        var first = Store.RefundOrder("r1", order.Id, 50000, "Món lỗi", "");
        Assert.Equal(50000, first.DrawerAmount);
        Assert.Equal(drawer - 50000, Store.ExpectedCash);

        // 20.000 cash is left, so 30.000 needs 10.000 by transfer and therefore a reference.
        Approve(DataStoreService.RefundAction("r2", order.Id, 30000, "Món lỗi", ""));
        Assert.Throws<InvalidOperationException>(() => Store.RefundOrder("r2", order.Id, 30000, "Món lỗi", ""));
        Approve(DataStoreService.RefundAction("r3", order.Id, 30000, "Món lỗi", "FT123"));
        var second = Store.RefundOrder("r3", order.Id, 30000, "Món lỗi", "FT123");
        Assert.Equal(20000, second.DrawerAmount);
        Assert.Equal(drawer - 70000, Store.ExpectedCash);
    }

    #endregion

    #region Split bill

    [Fact]
    public void Split_bill_moves_chosen_lines_to_a_new_held_order()
    {
        var vm = new MainViewModel(Store, Store, Store, Store, Store, Store, Auth, TestHost.Holds, TestHost.Stickers, Store);
        var a = Item(1); var b = Item(2); var c = Item(3);
        vm.AddItem(a); vm.AddItem(b); vm.AddItem(c);
        int held = TestHost.Holds.HeldOrders.Count;

        Assert.NotNull(vm.SplitBill(Array.Empty<OrderItem>()));
        Assert.NotNull(vm.SplitBill(new[] { a, b, c }));
        Assert.Equal(3, vm.CartItems.Count);

        Assert.Null(vm.SplitBill(new[] { b, c }));
        Assert.Equal(new[] { a }, vm.CartItems);
        Assert.Equal(50000, vm.Subtotal);
        Assert.Equal(held + 1, TestHost.Holds.HeldOrders.Count);
        var split = TestHost.Holds.HeldOrders[0];
        Assert.Equal(5, split.Items.Sum(i => i.Quantity));
        Assert.Equal(250000, split.TotalAmount);
        Assert.StartsWith("Tách từ đơn", split.Note);
    }

    [Fact]
    public void View_model_voucher_and_vat_match_checkout()
    {
        SetVat(10, included: false);
        SaveVoucher("VM10", Voucher.Amount, 10000, min: 60000);
        var vm = new MainViewModel(Store, Store, Store, Store, Store, Store, Auth, TestHost.Holds, TestHost.Stickers, Store);
        var line = Item(1);
        vm.AddItem(line);
        Assert.NotNull(vm.ApplyVoucher("VM10"));           // 50.000 is under the minimum
        vm.IncreaseQuantity(line);
        Assert.Null(vm.ApplyVoucher("vm10"));
        Assert.Equal(10000, vm.VoucherAmount);
        Assert.Equal(9000, vm.VatAmount);                  // 10% of 90.000
        Assert.Equal(99000, vm.PayableTotal);
        vm.DecreaseQuantity(line);                         // back under the minimum: voucher dropped
        Assert.Equal("", vm.VoucherCode);
        Assert.Equal(55000, vm.PayableTotal);
    }

    [Fact]
    public void Quick_add_uses_defaults_and_stacks_on_the_same_default_line()
    {
        var vm = new MainViewModel(Store, Store, Store, Store, Store, Store, Auth, TestHost.Holds, TestHost.Stickers, Store);
        var first = vm.QuickAdd(_product);
        Assert.Same(first, vm.QuickAdd(_product));
        Assert.Single(vm.CartItems);
        Assert.Equal(2, first.Quantity);
        Assert.Equal(_product.DefaultSize, first.Size);

        // A customised line (note, toppings, other options) is never merged into.
        first.Note = "Đá để riêng";
        var second = vm.QuickAdd(_product);
        Assert.NotSame(first, second);
        Assert.Equal(2, vm.CartItems.Count);
        Assert.Equal(100000 + 50000, vm.Subtotal);
    }

    [Fact]
    public void Quick_add_refuses_products_that_cannot_be_sold()
    {
        var vm = new MainViewModel(Store, Store, Store, Store, Store, Store, Auth, TestHost.Holds, TestHost.Stickers, Store);
        _product.IsActive = false;
        Store.SaveProduct(_product);
        Assert.Throws<InvalidOperationException>(() => vm.QuickAdd(_product));
        Assert.Empty(vm.CartItems);
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("Đá để riêng", "Đá để riêng", "")]
    [InlineData("Không ống hút, ghi tên An", "Không ống hút", "ghi tên An")]
    [InlineData("Pha đậm, Đá để riêng", "Đá để riêng|Pha đậm", "")]
    public void Quick_notes_round_trip_through_the_saved_note(string note, string chips, string freeText)
    {
        var (picked, rest) = TheCafePOS_WPF.Views.Dialogs.OptionCustomModal.SplitNote(note);
        Assert.Equal(chips.Split('|', StringSplitOptions.RemoveEmptyEntries).OrderBy(c => c), picked.OrderBy(c => c));
        Assert.Equal(freeText, rest);
        // Saving again lists chips in their on-screen order, then the free text.
        string saved = TheCafePOS_WPF.Views.Dialogs.OptionCustomModal.JoinNote(picked, rest);
        Assert.Equal(TheCafePOS_WPF.Views.Dialogs.OptionCustomModal.SplitNote(saved), (picked, rest), new NoteComparer());
    }

    private sealed class NoteComparer : IEqualityComparer<(HashSet<string> Picked, string FreeText)>
    {
        public bool Equals((HashSet<string> Picked, string FreeText) a, (HashSet<string> Picked, string FreeText) b) => a.Picked.SetEquals(b.Picked) && a.FreeText == b.FreeText;
        public int GetHashCode((HashSet<string> Picked, string FreeText) obj) => obj.FreeText.GetHashCode();
    }

    #endregion

    #region Receipt

    [Fact]
    public void Receipt_fits_80mm_paper_and_lists_every_adjustment()
    {
        SetVat(8, included: true);
        SaveVoucher("RECEIPT", Voucher.Amount, 5000);
        var member = Store.RegisterCustomer("0901234567", "Khách Hóa Đơn Có Tên Rất Dài Để Kiểm Tra Cắt Dòng");
        Approve(AuthService.PaymentApprovalAction("receipt", 20000));
        var order = Store.Checkout(new[] { Item(1), Item(2) }, "Mixed", 200000, "receipt", qrApproved: true, serviceType: "Tại chỗ", customerPhone: member.Phone, voucherCode: "RECEIPT", transferAmount: 20000);
        var lines = ReceiptService.Lines(order, Store.Settings, Store.FindCustomer(member.Phone));
        string text = string.Join("\n", lines);

        Assert.All(lines, l => Assert.True(l.Length <= ReceiptService.Width, $"Line too wide ({l.Length}): {l}"));
        Assert.Contains("FEATURE CAFE", text);
        Assert.Contains($"#{order.DailyOrderNumber:D2}", text);
        Assert.Contains("Voucher RECEIPT", text);
        Assert.Contains("Đã gồm VAT 8%", text);
        Assert.Contains($"{order.TotalAmount:N0}đ", text);
        Assert.Contains("Chuyển khoản", text);
        Assert.Contains("*******567", text);
        Assert.DoesNotContain("0901234567", text);
        Assert.Contains("Hẹn gặp lại", text);
        Assert.Contains("(IN LẠI)", ReceiptService.Text(order, Store.Settings, reprint: true));
    }

    #endregion

    #region Purchasing

    [Fact]
    public void Purchase_receipt_raises_stock_records_cost_and_is_idempotent()
    {
        var milk = AddIngredient("Sữa tươi", 0, 500);
        var supplier = new Supplier { Name = "NCC Sữa", Phone = "0912345678" };
        Store.SaveSupplier(supplier);
        int cupsBefore = Store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity;
        var lines = new List<PurchaseLine>
        {
            new() { ItemType = PurchaseLine.Ingredient, ItemId = milk.Id, Quantity = 2000, UnitCost = 40 },
            new() { ItemType = PurchaseLine.Packaging, ItemId = "pack-1", Quantity = 100, UnitCost = 800 }
        };
        var receipt = Store.CreatePurchaseReceipt("pn-1", supplier.Id, lines, "HĐ 001", paidFromDrawer: false);

        Assert.Equal(160000, receipt.TotalAmount);
        Assert.Matches(@"^PN-\d{6}-01$", receipt.Code);
        Assert.Equal(2000, Store.Ingredients.Single(i => i.Id == milk.Id).Stock);
        Assert.Equal(40, Store.Ingredients.Single(i => i.Id == milk.Id).UnitCost);
        Assert.Equal(cupsBefore + 100, Store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity);
        Assert.Contains(Store.IngredientMovements, m => m.Type == DataStoreService.PurchaseMovement && m.Change == 2000);
        Assert.Contains(Store.StockMovements, m => m.Type == DataStoreService.PurchaseMovement && m.Change == 100);

        Assert.Same(receipt, Store.CreatePurchaseReceipt("pn-1", supplier.Id, lines, "HĐ 001", false));
        Assert.Single(Store.PurchaseReceipts);
        Assert.Matches(@"-02$", Store.CreatePurchaseReceipt("pn-2", supplier.Id, lines.Take(1).ToList(), "", false).Code);
        Assert.Throws<InvalidOperationException>(() => Store.DeleteSupplier(supplier.Id));
    }

    [Fact]
    public void Purchase_paid_from_the_drawer_records_cash_out()
    {
        var beans = AddIngredient("Cà phê hạt", 0, 0);
        var supplier = new Supplier { Name = "NCC Cà phê" };
        Store.SaveSupplier(supplier);
        var line = new List<PurchaseLine> { new() { ItemType = PurchaseLine.Ingredient, ItemId = beans.Id, Quantity = 1000, UnitCost = 30 } };
        decimal drawer = Store.ExpectedCash; // 100.000 opening cash

        var receipt = Store.CreatePurchaseReceipt("drawer-1", supplier.Id, line, "", paidFromDrawer: true);
        Assert.Equal(drawer - 30000, Store.ExpectedCash);
        Assert.Contains(Store.CashEntries, e => e.Type == "Out" && e.Amount == 30000 && e.Reason.Contains(receipt.Code));

        var tooBig = new List<PurchaseLine> { new() { ItemType = PurchaseLine.Ingredient, ItemId = beans.Id, Quantity = 1000, UnitCost = 71 } };
        Assert.Throws<InvalidOperationException>(() => Store.CreatePurchaseReceipt("drawer-2", supplier.Id, tooBig, "", paidFromDrawer: true));
    }

    public static IEnumerable<object[]> InvalidPurchaseLines()
    {
        yield return new object[] { PurchaseLine.Ingredient, 0m, 10m };       // zero quantity
        yield return new object[] { PurchaseLine.Ingredient, -5m, 10m };
        yield return new object[] { PurchaseLine.Ingredient, 10m, -1m };      // negative cost
        yield return new object[] { PurchaseLine.Ingredient, 10m, 10.5m };    // fractional đồng
        yield return new object[] { PurchaseLine.Packaging, 2.5m, 100m };     // half a cup
        yield return new object[] { "Khác", 10m, 10m };
    }

    [Theory]
    [MemberData(nameof(InvalidPurchaseLines))]
    public void Invalid_purchase_lines_are_refused_without_changing_stock(string type, decimal quantity, decimal cost)
    {
        var ing = AddIngredient("Đường", 100, 0);
        var supplier = new Supplier { Name = "NCC Đường" };
        Store.SaveSupplier(supplier);
        string id = type == PurchaseLine.Packaging ? "pack-1" : ing.Id;
        int cups = Store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity;
        Assert.Throws<InvalidOperationException>(() => Store.CreatePurchaseReceipt("bad", supplier.Id, new() { new() { ItemType = type, ItemId = id, Quantity = quantity, UnitCost = cost } }, "", false));
        Assert.Equal(100, Store.Ingredients.Single(i => i.Id == ing.Id).Stock);
        Assert.Equal(cups, Store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity);
        Assert.Empty(Store.PurchaseReceipts);
    }

    [Fact]
    public void Purchase_needs_an_active_supplier_lines_and_a_manager()
    {
        var ing = AddIngredient("Trà", 0, 0);
        var supplier = new Supplier { Name = "NCC Trà" };
        Store.SaveSupplier(supplier);
        var line = new PurchaseLine { ItemType = PurchaseLine.Ingredient, ItemId = ing.Id, Quantity = 1, UnitCost = 1 };
        Assert.Throws<InvalidOperationException>(() => Store.CreatePurchaseReceipt("empty", supplier.Id, new(), "", false));
        Assert.Throws<InvalidOperationException>(() => Store.CreatePurchaseReceipt("dup", supplier.Id, new() { line, line }, "", false));
        Assert.Throws<InvalidOperationException>(() => Store.SaveSupplier(new Supplier { Name = "ncc trà" }));
        Assert.Throws<InvalidOperationException>(() => Store.SaveSupplier(new Supplier { Name = " " }));
        Assert.Throws<InvalidOperationException>(() => Store.SaveSupplier(new Supplier { Name = "Bad phone", Phone = "12345" }));

        supplier.IsActive = false;
        Store.SaveSupplier(supplier);
        Assert.Throws<InvalidOperationException>(() => Store.CreatePurchaseReceipt("inactive", supplier.Id, new() { line }, "", false));

        Auth.CreateAccount("feature-cashier", "CashierPass!2026", "Cashier");
        Auth.Login("feature-cashier", "CashierPass!2026");
        Assert.Throws<InvalidOperationException>(() => Store.SaveSupplier(new Supplier { Name = "Cashier supplier" }));
    }

    [Fact]
    public void Vouchers_suppliers_and_receipts_survive_a_restart()
    {
        SaveVoucher("KEEP", Voucher.Percent, 15, max: 30000);
        var ing = AddIngredient("Bột cacao", 0, 0);
        var supplier = new Supplier { Name = "NCC Cacao" };
        Store.SaveSupplier(supplier);
        Store.CreatePurchaseReceipt("keep", supplier.Id, new() { new() { ItemType = PurchaseLine.Ingredient, ItemId = ing.Id, Quantity = 500, UnitCost = 120 } }, "", false);
        SetVat(8, included: false);

        Restart();
        Assert.Equal(30000, Store.FindVoucher("KEEP")!.MaxDiscount);
        Assert.Equal("NCC Cacao", Store.Suppliers.Single().Name);
        Assert.Equal(60000, Store.PurchaseReceipts.Single().TotalAmount);
        Assert.Equal(120, Store.Ingredients.Single(i => i.Id == ing.Id).UnitCost);
        Assert.Equal(8, Store.Settings.VatPercent);
        Assert.False(Store.Settings.PricesIncludeVat);
    }

    #endregion

    #region Low stock and shrinkage

    [Theory]
    [InlineData(101, false)]
    [InlineData(100, true)]   // at the threshold counts as low
    [InlineData(0, true)]
    public void Low_stock_alert_at_or_below_threshold(decimal stock, bool low)
    {
        var ing = AddIngredient("Siro", 200, 100);
        if (stock < 200) Store.RecordIngredientWaste(ing.Id, 200 - stock, "Hỏng");
        Assert.Equal(low, Store.LowStockItems().Any(i => i.Name == "Siro"));
        if (stock == 0) Assert.True(Store.LowStockItems().First().IsOut);
    }

    [Fact]
    public void Waste_needs_a_positive_amount_within_stock_and_a_reason()
    {
        var ing = AddIngredient("Kem", 100, 0);
        Assert.Throws<InvalidOperationException>(() => Store.RecordIngredientWaste(ing.Id, 0, "Hỏng"));
        Assert.Throws<InvalidOperationException>(() => Store.RecordIngredientWaste(ing.Id, 100.01m, "Hỏng"));
        Assert.Throws<InvalidOperationException>(() => Store.RecordIngredientWaste(ing.Id, 10, " "));
        Store.RecordIngredientWaste(ing.Id, 100, "Hết hạn");
        Assert.Equal(0, Store.Ingredients.Single(i => i.Id == ing.Id).Stock);
        Assert.Throws<InvalidOperationException>(() => Store.UpdateStock("pack-1", DataStoreService.WasteMovement, int.MaxValue, "Rơi vỡ"));
    }

    [Fact]
    public void Shrinkage_report_sums_losses_valued_at_purchase_cost()
    {
        var tea = AddIngredient("Trà đen", 0, 0);
        var supplier = new Supplier { Name = "NCC Trà đen" };
        Store.SaveSupplier(supplier);
        Store.CreatePurchaseReceipt("tea", supplier.Id, new()
        {
            new() { ItemType = PurchaseLine.Ingredient, ItemId = tea.Id, Quantity = 1000, UnitCost = 50 },
            new() { ItemType = PurchaseLine.Packaging, ItemId = "pack-3", Quantity = 10, UnitCost = 500 }
        }, "", false);
        Store.AdjustIngredient(tea.Id, "Kiểm kê", 980, "Đếm cuối ngày");   // 20 g missing
        Store.RecordIngredientWaste(tea.Id, 50, "Đổ");                     // 50 g spilled
        Store.AdjustIngredient(tea.Id, "Nhập kho", 100, "Bổ sung");        // gains never count
        Store.UpdateStock("pack-3", DataStoreService.WasteMovement, 4, "Rách");

        var rows = Store.ShrinkageReport(Today, Today);
        var teaRow = rows.Single(r => r.Name == "Trà đen");
        Assert.Equal(70, teaRow.Quantity);
        Assert.Equal(3500, teaRow.Value);
        Assert.Equal(2, teaRow.Events);
        var lidRow = rows.Single(r => r.Kind == PurchaseLine.Packaging);
        Assert.Equal(4, lidRow.Quantity);
        Assert.Equal(2000, lidRow.Value);
        Assert.Equal(teaRow, rows[0]); // highest value first

        Assert.Empty(Store.ShrinkageReport(Today.AddDays(1), Today.AddDays(2)));
    }

    #endregion
}
