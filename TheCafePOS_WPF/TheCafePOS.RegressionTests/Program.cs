using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

if (args.Length < 2) throw new ArgumentException("Usage: <isolated data directory> <create|reload>");
Environment.SetEnvironmentVariable("THECAFEPOS_DATA_DIR", Path.GetFullPath(args[0]));
if (args[1] == "americano") { AmericanoRegression.Run(); return; }
if (args[1] == "menu-options") { MenuOptionsRegression.Run(); return; }
if (args[1] == "report-dashboard") { ReportDashboardRegression.Run(); return; }
if (args[1] is "finance" or "finance-reload") { FinanceRegression.Run(args[1] == "finance-reload"); return; }
if (args[1] is "session" or "session-reload") { SessionRegression.Run(args[1] == "session-reload"); return; }
int checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
void Reject(Action action, string message)
{
    try { action(); } catch (InvalidOperationException) { checks++; Console.WriteLine("PASS " + message); return; }
    throw new Exception("Expected rejection: " + message);
}

var store = DataStoreService.Instance;
var auth = AuthService.Instance;
if (args[1] == "reload")
{
    Check(!auth.NeedsSetup, "Accounts survive process restart");
    auth.Login("owner", "OwnerPass!2026");
    Check(store.CompletedOrders.Count == 3, "Completed orders survive process restart");
    Check(store.Shifts.Count == 2 && store.CurrentShift.Status == "Closed", "Shift history survives process restart");
    Check(store.Products.Any(p => p.Name == "Regression drink"), "Menu survives process restart");
    Check(HoldOrderService.Instance.HeldOrders.Count == 1, "Held order survives process restart");
    var held = HoldOrderService.Instance.RecallOrder(HoldOrderService.Instance.HeldOrders[0].Id);
    Check(held?.Items.Count == 1 && HoldOrderService.Instance.HeldOrders.Count == 0, "Recall restores held cart");
    Check(LocalDatabase.Instance.Read<List<HeldOrderInfo>>("held-orders")!.Count == 0, "Recall removal persisted");
    Check(VietQRService.Settings.AccountNo == "1234567890", "Bank configuration survives restart");
    Check(store.StockMovements.Any(m => m.Type == "Nhập kho" && m.Reason == "Receipt regression"), "Stock history survives restart");
    Check(store.PackagingInventory.Single(p => p.Id == "pack-5").StockQuantity == 22, "Counted and adjusted stock survives restart");
    Console.WriteLine($"Completed {checks} restart checks.");
    return;
}

Check(auth.NeedsSetup, "Fresh isolated database");
Reject(() => auth.CreateAccount("owner", "short", "Owner"), "Reject short password");
auth.CreateAccount("owner", "OwnerPass!2026", "Owner");
auth.Login("owner", "OwnerPass!2026");
Check(auth.IsManager && auth.CurrentUser?.Role == "Owner", "First account is owner");
auth.CreateAccount("cashier", "CashierPass!2026", "Cashier");
Reject(() => auth.CreateAccount("CASHIER", "CashierPass!2026", "Cashier"), "Reject duplicate case-insensitive username");
Check(auth.Accounts.All(a => a.PasswordHash != "OwnerPass!2026" && a.Salt.Length > 0), "Passwords are salted hashes");
auth.Login("cashier", "CashierPass!2026");
Reject(() => auth.RequireManager(), "Cashier cannot access management");
Reject(() => store.SaveProduct(new Product()), "Cashier cannot edit menu");
Reject(() => store.UpdateStock("pack-1", "Nhập kho", 10, "Unauthorized"), "Cashier cannot update stock");
Reject(() => auth.CreateAccount("manager", "ManagerPass!2026", "Manager"), "Cashier cannot create staff");
Reject(() => auth.Approve("cashier", "CashierPass!2026", "test"), "Cashier cannot approve protected operation");
for (int i = 0; i < 5; i++) Reject(() => auth.Login("cashier", "bad"), "Reject incorrect password");
Reject(() => auth.Login("cashier", "CashierPass!2026"), "Repeated attempts are temporarily locked");
auth.Login("owner", "OwnerPass!2026");
var category = new Category { Name = "Regression category" };
store.SaveCategory(category);
var product = new Product { Name = "Regression drink", CategoryId = category.Id, BasePrice = 25000 };
store.SaveProduct(product);
Reject(() => store.SaveProduct(new Product { Name = "Invalid", CategoryId = category.Id, BasePrice = -1 }), "Reject invalid menu price");
var cart = new ObservableCollection<OrderItem> { new() { ProductId = product.Id, ProductName = product.Name, BasePrice = 25000, Quantity = 2, Size = "M" } };
Reject(() => store.Checkout(cart, "Cash", 50000, "closed"), "Reject checkout without open shift");
Reject(() => store.OpenShift(-1), "Reject negative opening cash");
store.OpenShift(100000);
Reject(() => store.OpenShift(100000), "Cannot open overlapping shift");
Reject(() => store.Checkout(cart, "Cash", 0, "zero"), "Reject zero cash");
Reject(() => store.Checkout(cart, "Cash", -100, "negative"), "Reject negative cash");
Reject(() => store.Checkout(cart, "Cash", 49000, "insufficient"), "Reject insufficient cash");
Reject(() => store.Checkout(cart, "VietQR", 0, "unconfirmed", true), "Boolean alone cannot authorize QR payment");
int stockBefore = store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity;
var order = store.Checkout(cart, "Cash", 60000, "cash-order");
Check(order.ChangeReturned == 10000 && order.TotalAmount == 50000, "Correct total and change");
Check(order.ShiftId == store.CurrentShift.Id && order.Items.All(i => i.OrderId == order.Id), "Order and line references are assigned");
Check(store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity == stockBefore - 2, "Packaging deducted once");
store.Checkout(cart, "Cash", 60000, "cash-order");
Check(store.CompletedOrders.Count == 1 && store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity == stockBefore - 2, "Duplicate checkout is idempotent");
Check(store.StockMovements.Count(m => m.OrderId == "cash-order") == 3, "Duplicate checkout does not duplicate stock history");
cart[0].Quantity = 3;
Check(order.Items[0].Quantity == 2, "Completed order is independent of cart edits");
cart[0].Quantity = 999; cart[0].Size = "XL";
Reject(() => store.Checkout(cart, "Cash", 9999999999, "stock-failure"), "Reject insufficient packaging");
Check(store.CompletedOrders.Count == 1 && store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity == stockBefore - 2, "Failed checkout rolls back stock and orders");
Check(LocalDatabase.Instance.Read<StoreState>("store")!.Orders.Count == 1, "Failed checkout leaves persisted state unchanged");
Check(store.StockMovements.All(m => m.OrderId != "stock-failure"), "Failed sale leaves no stock movement");
cart[0].Quantity = 1; cart[0].Size = "M";
auth.Approve("owner", "OwnerPass!2026", AuthService.PaymentApprovalAction("qr-order", 25000));
store.Checkout(cart, "VietQR", 0, "qr-order", true);
Check(store.ExpectedCash == 150000, "QR revenue excluded from drawer cash");
Reject(() => store.CloseShift(-1), "Reject negative closing cash");
store.CloseShift(149000);
Check(store.CurrentShift.Discrepancy == -1000, "Closing discrepancy calculated");
Reject(() => store.Checkout(cart, "Cash", 25000, "after-close"), "Closed shift blocks payment");
store.OpenShift(20000);
Check(store.ExpectedCash == 20000, "New shift excludes previous sales");
cart[0].Size = "XL";
int xlBefore = store.PackagingInventory.Single(p => p.Id == "pack-xl").StockQuantity;
store.Checkout(cart, "Cash", 25000, "second-shift");
Check(store.PackagingInventory.Single(p => p.Id == "pack-xl").StockQuantity == xlBefore - 1, "XL uses XL packaging");
Check(store.ExpectedCash == 45000, "New shift totals only its own cash orders");
var secondShiftId = store.CurrentShift.Id;
store.CloseShift(45000);
HoldOrderService.Instance.HoldOrder(cart, 25000, "restart test");
VietQRService.SaveSettings(new BankSettings { BankId = "MB", AccountNo = "1234567890", AccountName = "TEST ACCOUNT" });
var report = ReportService.Build(store.CompletedOrders, DateTime.Today, DateTime.Today);
Check(report.Orders.Count == 3 && report.Revenue == 100000 && report.Cash == 75000 && report.Transfer == 25000, "Report payment totals reconcile");
Check(report.Products.Single().Quantity == 4, "Product report sums quantities");
Check(ReportService.Build(store.CompletedOrders, DateTime.Today, DateTime.Today, secondShiftId).Revenue == 25000, "Report shift filter");
Check(ReportService.Build(store.CompletedOrders, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1)).Revenue == 0, "Report date filter");
Reject(() => ReportService.Build(store.CompletedOrders, DateTime.Today, DateTime.Today.AddDays(-1)), "Reject reversed date range");
Check(ReportService.ToCsv(report).Contains("cash-order"), "CSV includes persisted order reference");
var saved = LocalDatabase.Instance.Read<StoreState>("store")!;
Check(saved.Orders.Count == 3 && saved.Shifts.Count == 2 && saved.CurrentShift.Status == "Closed", "SQLite snapshot contains all completed transactions");
int initialStraws = store.PackagingInventory.Single(p => p.Id == "pack-5").StockQuantity;
int initialMovements = store.StockMovements.Count;
Reject(() => store.UpdateStock("missing", "Nhập kho", 10, "test"), "Reject unknown packaging");
Reject(() => store.UpdateStock("pack-5", "Nhập kho", 0, "test"), "Reject zero receipt");
Reject(() => store.UpdateStock("pack-5", "Nhập kho", -1, "test"), "Reject negative receipt");
Reject(() => store.UpdateStock("pack-5", "Kiểm kê", -1, "test"), "Reject negative count");
Reject(() => store.UpdateStock("pack-5", "Điều chỉnh", 0, "test"), "Reject zero adjustment");
Reject(() => store.UpdateStock("pack-5", "Unknown", 1, "test"), "Reject unknown stock operation");
Reject(() => store.UpdateStock("pack-5", "Nhập kho", 1, "  "), "Stock change requires reason");
Reject(() => store.UpdateStock("pack-5", "Điều chỉnh", -initialStraws - 1, "test"), "Stock cannot become negative");
Reject(() => store.UpdateStock("pack-5", "Nhập kho", int.MaxValue, "test"), "Stock cannot overflow integer range");
Check(store.StockMovements.Count == initialMovements && store.PackagingInventory.Single(p => p.Id == "pack-5").StockQuantity == initialStraws, "Invalid operations leave stock and history unchanged");
store.UpdateStock("pack-5", "Nhập kho", 50, "Receipt regression");
var movement = store.StockMovements.Last();
Check(movement.Before == initialStraws && movement.After == initialStraws + 50 && movement.Change == 50 && movement.Operator == "owner", "Receipt records before, after and operator");
store.UpdateStock("pack-5", "Kiểm kê", 0, "Empty stock count");
Check(store.PackagingInventory.Single(p => p.Id == "pack-5").StockQuantity == 0, "Stock count accepts zero");
store.UpdateStock("pack-5", "Kiểm kê", 20, "Physical count");
store.UpdateStock("pack-5", "Điều chỉnh", -3, "Damaged stock");
store.UpdateStock("pack-5", "Điều chỉnh", 5, "Correction");
Check(store.PackagingInventory.Single(p => p.Id == "pack-5").StockQuantity == 22 && store.StockMovements.Last().Change == 5, "Positive and negative adjustments apply correctly");
var inventorySnapshot = LocalDatabase.Instance.Read<StoreState>("store")!;
Check(inventorySnapshot.StockMovements.Count == initialMovements + 5 && inventorySnapshot.Packaging.Single(p => p.Id == "pack-5").StockQuantity == 22, "Stock and audit saved together");
Check(System.Text.Json.JsonSerializer.Deserialize<StoreState>("{}")!.StockMovements.Count == 0, "Older snapshots default to empty stock history");
Check(store.Products.Where(p => p.CategoryId == "cat-5").All(p => p.IsTopping && !p.SoldSeparately), "Seed topping migration separates extras from drinks");
var extraProduct = new Product { Id = "test-extra", Name = "Pearls", IsTopping = true, SoldSeparately = true, BasePrice = 5000 };
var drinkProduct = new Product { Id = "test-drink", Name = "Milk tea", BasePrice = 25000, AllowSugar = false, DefaultSugar = 50, AllowedToppingIds = new() { extraProduct.Id }, Sizes = new() { new() { Name = "L", ExtraPrice = 7000, PackagingId = "pack-2" } } };
var configured = OrderConfigurationService.Create(drinkProduct, "L", 100, 30, 2, new[] { (extraProduct.Id, 2) }, "  Less sweet  ", new[] { extraProduct });
Check(configured.UnitPrice == 42000 && configured.TotalPrice == 84000, "Size and topping quantities charged per cup");
Check(configured.SugarPercent == 50 && !configured.HasSugarOption && configured.IcePercent == 30 && configured.Note == "Less sweet", "Disabled sugar uses recipe default; note retained");
Check(configured.PackagingId == "pack-2" && configured.SelectedToppings.Single().Quantity == 2, "Cup mapping and topping snapshot preserved");
Reject(() => OrderConfigurationService.Create(drinkProduct, "M", 50, 30, 1, Array.Empty<(string, int)>(), "", new[] { extraProduct }), "Unsupported product size rejected");
Reject(() => OrderConfigurationService.Create(drinkProduct, "L", 50, 30, 0, Array.Empty<(string, int)>(), "", new[] { extraProduct }), "Zero configured quantity rejected");
Reject(() => OrderConfigurationService.Create(drinkProduct, "L", 50, 30, 1, new[] { (extraProduct.Id, 11) }, "", new[] { extraProduct }), "Topping portion limit enforced");
Reject(() => OrderConfigurationService.Create(drinkProduct, "L", 50, 30, 1, new[] { ("unavailable", 1) }, "", new[] { extraProduct }), "Unavailable topping rejected");
var independent = OrderConfigurationService.Clone(configured);
independent.Quantity = 1; independent.SelectedToppings[0].Quantity = 1;
Check(configured.Quantity == 2 && configured.SelectedToppings[0].Quantity == 2, "Editing a split draft cannot mutate original line");
var standalone = OrderConfigurationService.Create(extraProduct, "", 100, 100, 3, Array.Empty<(string, int)>(), "", new[] { extraProduct });
var stockSnapshot = store.PackagingInventory.ToDictionary(p => p.Id, p => p.StockQuantity);
store.DeductPackagingForOrder(new Order { Items = new() { standalone } });
Check(!standalone.IsBeverage && store.PackagingInventory.All(p => p.StockQuantity == stockSnapshot[p.Id]), "Standalone toppings consume no drink packaging");
var heldCount = HoldOrderService.Instance.HeldOrders.Count;
HoldOrderService.Instance.HoldOrder(new ObservableCollection<OrderItem> { configured }, configured.TotalPrice);
var customHeld = HoldOrderService.Instance.HeldOrders.First();
Check(customHeld.Items.Single().Note == configured.Note && customHeld.Items.Single().SelectedToppings.Single().Quantity == 2 && customHeld.Items.Single().PackagingId == "pack-2", "Held order preserves all customization fields");
HoldOrderService.Instance.RecallOrder(customHeld.Id);
Check(HoldOrderService.Instance.HeldOrders.Count == heldCount, "Custom held test leaves original held orders intact");
var thirdCup = OrderConfigurationService.Clone(configured); thirdCup.Quantity = 1;
var labels = StickerPrinterService.Instance.GenerateStickersForOrder(new Order { Items = new() { configured, thirdCup, standalone } });
Check(labels.Count == 3 && labels.Select(s => s.Index).SequenceEqual(new[] { 1, 2, 3 }) && labels.All(s => s.Total == 3), "Labels numbered across entire drink order excluding standalone toppings");
Console.WriteLine($"Completed {checks} regression checks.");

