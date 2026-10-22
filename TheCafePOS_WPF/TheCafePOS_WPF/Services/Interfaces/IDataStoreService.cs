using System.Collections.Generic;
using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services.Interfaces
{
    public interface IDataStoreService
    {
        List<Category> Categories { get; }
        List<Product> Products { get; }
        List<PackagingItem> PackagingInventory { get; }
        ObservableCollection<Order> CompletedOrders { get; }
        List<StickerPrintLog> StickerLogs { get; }
        StoreSettings Settings { get; }
        Shift CurrentShift { get; }
        decimal ExpectedCash { get; }

        int GetNextDailyOrderNumber();
        void DeductPackagingForOrder(Order order);
        void SetProductSoldOut(string id, bool soldOut);
        Customer? FindCustomer(string phone);
        (decimal Amount, string Names) PromotionsFor(IEnumerable<OrderItem> cart, DateTime at);
        bool DiscountNeedsApproval(decimal subtotal, decimal discount);

        void ValidateCheckoutCart(IEnumerable<OrderItem> cart, string serviceType);
        Order Checkout(IEnumerable<OrderItem> cart, string method, decimal cash, string checkoutId, bool qrApproved = false, string serviceType = "Mang đi", decimal discount = 0, string discountReason = "", string customerPhone = "", int redeemPoints = 0, string channel = StoreSettings.Counter);

        void OpenShift(decimal initialCash);
        void RequireOpenShift();
        void Save();
    }
}
