using System.Text.Json;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

internal static class CheckoutFlowRegression
{
    public static void Run()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
        void Reject(Action action, string message)
        {
            try { action(); } catch (InvalidOperationException) { Console.WriteLine("PASS " + message); return; }
            throw new Exception("Expected rejection: " + message);
        }
        var auth = TestHost.Auth;
        var store = TestHost.Store;
        auth.CreateAccount("checkout-owner", "CheckoutPass!2026", "Owner");
        auth.Login("checkout-owner", "CheckoutPass!2026");
        var category = new Category { Name = "Checkout test" }; store.SaveCategory(category);
        var product = new Product { Name = "Checkout coffee", CategoryId = category.Id, BasePrice = 25000 };
        store.SaveProduct(product);
        OrderItem Make(int quantity) => OrderConfigurationService.Create(product, "M", 100, 100, quantity, Array.Empty<(string, int)>(), "", store.Products);
        var items = new[] { Make(2), Make(2) };
        Reject(() => store.ValidateCheckoutCart(items, "Mang đi"), "Payment preview rejects a closed shift");
        store.OpenShift(0);
        foreach (var id in new[] { "pack-1", "pack-3", "pack-5" }) store.UpdateStock(id, "Kiểm kê", 10, "Checkout test");
        string Disk() => JsonSerializer.Serialize(TestHost.Database.Read<StoreState>("store"));
        string Stock() => JsonSerializer.Serialize(store.PackagingInventory);
        var before = Disk(); var stock = Stock();
        store.ValidateCheckoutCart(items, "Mang đi");
        Check(Disk() == before && Stock() == stock && store.CompletedOrders.Count == 0, "Payment preview never writes orders or deducts stock");
        Reject(() => store.ValidateCheckoutCart(Array.Empty<OrderItem>(), "Mang đi"), "Payment preview rejects an empty cart");
        store.UpdateStock("pack-1", "Kiểm kê", 3, "Checkout test");
        before = Disk(); stock = Stock();
        Reject(() => store.ValidateCheckoutCart(items, "Mang đi"), "Preview aggregates packaging across multiple cart rows");
        Reject(() => store.Checkout(items, "Cash", 100000, "no-stock"), "Final checkout also rejects aggregated packaging shortage");
        Check(Disk() == before && Stock() == stock && store.CompletedOrders.Count == 0, "Rejected checkout preserves all inventory and persisted data");
        store.ValidateCheckoutCart(items, "Tại chỗ");
        Check(Disk() == before && Stock() == stock, "Dine-in preview does not require disposable cups");
        store.UpdateStock("pack-1", "Kiểm kê", 10, "Checkout test");
        store.SetProductSoldOut(product.Id, true);
        Reject(() => store.ValidateCheckoutCart(items, "Mang đi"), "Preview rejects sold-out products before collecting payment");
        store.SetProductSoldOut(product.Id, false);
        product.IsActive = false; store.SaveProduct(product);
        Reject(() => store.ValidateCheckoutCart(items, "Mang đi"), "Preview rejects inactive products already in the cart");
        product.IsActive = true; store.SaveProduct(product);
        before = Disk(); stock = Stock();
        foreach (var cash in new[] { -1m, 0m, 99999m, 100000.5m, 1000000000000m })
            Reject(() => store.Checkout(items, "Cash", cash, "invalid-cash"), "Checkout rejects invalid cash " + cash);
        Check(Disk() == before && Stock() == stock, "Invalid cash leaves orders and all inventory unchanged");
        var order = store.Checkout(items, "Cash", 100000, "paid-once");
        Check(order.TotalAmount == 100000 && order.ChangeReturned == 0, "Exact cash for a multiple-row cart returns zero change");
        Check(new[] { "pack-1", "pack-3", "pack-5" }.All(id => store.PackagingInventory.Single(p => p.Id == id).StockQuantity == 6), "Checkout deducts the correct cups lids and straws across rows");
        before = Disk(); stock = Stock();
        store.Checkout(items, "Cash", 100000, "paid-once");
        Check(store.CompletedOrders.Count == 1 && Disk() == before && Stock() == stock, "Repeated confirmation cannot create a second payment or stock movement");
    }
}
