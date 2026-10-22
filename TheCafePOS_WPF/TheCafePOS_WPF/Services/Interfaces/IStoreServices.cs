using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services.Interfaces
{
    // Focused views of the shop data. DataStoreService implements all of them over one persisted snapshot,
    // so callers depend only on the slice they use and the storage can be split later without touching them.

    /// <summary>Menu: categories and products.</summary>
    public interface ICatalogService
    {
        List<Category> Categories { get; }
        List<Product> Products { get; }

        void SaveCategory(Category category);
        void DeleteCategory(string id, bool deleteProducts = false);
        void SaveProduct(Product product);
        void DeleteProduct(string id);
        void SetProductSoldOut(string id, bool soldOut);
    }

    /// <summary>Packaging stock and ingredient recipes.</summary>
    public interface IInventoryService
    {
        List<PackagingItem> PackagingInventory { get; }
        List<StockMovement> StockMovements { get; }
        List<Ingredient> Ingredients { get; }
        List<IngredientMovement> IngredientMovements { get; }

        void SavePackagingItem(PackagingItem item);
        void DeletePackagingItem(string id);
        void UpdateStock(string packagingId, string operation, int quantity, string reason);
        void DeductPackagingForOrder(Order order);

        void SaveIngredient(Ingredient item);
        void DeleteIngredient(string id);
        void AdjustIngredient(string id, string type, decimal quantity, string reason);
        void SaveRecipe(string productId, List<RecipeLine> lines);
    }

    /// <summary>Cashier shifts and the cash expected in the drawer.</summary>
    public interface IShiftService
    {
        Shift CurrentShift { get; }
        List<Shift> Shifts { get; }
        decimal ExpectedCash { get; }

        void OpenShift(decimal initialCash);
        void CloseShift(decimal actualCash);
        void RequireOpenShift();
    }

    /// <summary>Refunds, cancellations and petty cash movements.</summary>
    public interface IFinanceService
    {
        List<RefundEntry> Refunds { get; }
        List<CashEntry> CashEntries { get; }

        RefundEntry RefundOrder(string id, string orderId, decimal amount, string reason, string reference);
        RefundEntry CancelOrder(string refundId, string orderId, string reason, string reference);
        CashEntry RecordCash(string id, string type, decimal amount, string reason);
    }

    /// <summary>Loyalty members.</summary>
    public interface ICustomerService
    {
        List<Customer> Customers { get; }

        Customer? FindCustomer(string phone);
        Customer RegisterCustomer(string phone, string name);
        void UpdateCustomer(string phone, string name, string? newPhone = null);
        void DeleteCustomer(string phone);
        List<Order> CustomerOrders(string phone);
    }

    /// <summary>Promotions and the discount they give a cart.</summary>
    public interface IPromotionService
    {
        List<Promotion> Promotions { get; }

        (decimal Amount, string Names) PromotionsFor(IEnumerable<OrderItem> cart, DateTime at);
        Promotion? ActivePromotion(DateTime at);
        void SavePromotion(Promotion promo);
        void DeletePromotion(string id);
    }

    /// <summary>Shop profile, sales channels and cashier limits.</summary>
    public interface IStoreSettingsService
    {
        StoreSettings Settings { get; }

        void SaveSettings(StoreSettings settings);
        bool DiscountNeedsApproval(decimal subtotal, decimal discount);
    }

    /// <summary>Selling: order numbers, checkout, fulfilment and the saved workspace.</summary>
    public interface IOrderService
    {
        ObservableCollection<Order> CompletedOrders { get; }
        List<StickerPrintLog> StickerLogs { get; }
        DraftOrder? Draft { get; }

        int GetNextDailyOrderNumber();
        void ValidateCheckoutCart(IEnumerable<OrderItem> cart, string serviceType);
        Order Checkout(IEnumerable<OrderItem> cart, string method, decimal cash, string checkoutId, bool qrApproved = false, string serviceType = "Mang đi", decimal discount = 0, string discountReason = "", string customerPhone = "", int redeemPoints = 0, string channel = StoreSettings.Counter);
        void UpdateFulfillment(string orderId, string action, string? lineId = null, string reason = "");
        void SaveDraft(DraftOrder? draft);
        void SaveWorkspace(IEnumerable<HeldOrderInfo> held, DraftOrder? draft);
        void Save();
    }
}
