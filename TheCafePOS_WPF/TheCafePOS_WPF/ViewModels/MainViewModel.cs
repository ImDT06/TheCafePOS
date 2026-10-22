using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.ViewModels
{
    /// <summary>
    /// State and business rules of the order being rung up at the POS: cart lines, pricing adjustments,
    /// sales channel and checkout. Contains no WPF types so it can be exercised from regression tests.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        public const string AllCategories = "ALL";
        public const string DefaultServiceType = "Mang đi";
        public const int MaxLineQuantity = 999;
        private static readonly decimal[] CashNotes = { 10000m, 20000m, 50000m, 100000m, 200000m, 500000m };

        private readonly IDataStoreService _store;
        private readonly IAuthService _auth;
        private readonly IHoldOrderService _holds;
        private readonly IStickerPrinterService _stickers;

        public MainViewModel(IDataStoreService store, IAuthService auth, IHoldOrderService holds, IStickerPrinterService stickers)
        {
            _store = store;
            _auth = auth;
            _holds = holds;
            _stickers = stickers;
            CartItems.CollectionChanged += (_, _) =>
            {
                if (CartItems.Count == 0) CartStartedAt = null;
                else CartStartedAt ??= DateTime.Now;
            };
        }

        public ObservableCollection<OrderItem> CartItems { get; } = new();
        public ObservableCollection<PrintedStickerInfo> RecentStickers => _stickers.RecentPrintedStickers;

        public int CurrentDailyOrderNumber { get; private set; } = 1;
        public string CheckoutId { get; private set; } = Guid.NewGuid().ToString();
        public DateTime? CartStartedAt { get; private set; }
        public bool CheckoutInProgress { get; private set; }
        public string? CheckoutError { get; private set; }

        public string SelectedCategoryId { get; private set; } = AllCategories;
        public string ServiceType { get; private set; } = DefaultServiceType;
        public string Channel { get; private set; } = StoreSettings.Counter;
        public decimal Discount { get; private set; }
        public string DiscountReason { get; private set; } = "";
        public string CustomerPhone { get; private set; } = "";
        public int RedeemPoints { get; private set; }

        #region Derived values

        public bool HasItems => CartItems.Count > 0;
        public bool IsAppOrder => Channel != StoreSettings.Counter;
        public int TotalCups => CartItems.Sum(i => i.Quantity);
        public int HeldOrderCount => _holds.HeldOrders.Count;
        public string StoreName => _store.Settings.DisplayName;
        public Customer? Member => _store.FindCustomer(CustomerPhone);

        public decimal Subtotal => CartItems.Sum(i => i.TotalPrice);
        public (decimal Amount, string Names) Promotions => IsAppOrder ? (0m, "") : _store.PromotionsFor(CartItems, DataStoreService.CafeNow);
        public decimal PromoAmount => Promotions.Amount;
        public decimal RedeemValue => RedeemPoints * DataStoreService.PointValue;
        public decimal ChannelSurcharge => IsAppOrder && _store.Settings.Channels.FirstOrDefault(c => c.Name == Channel) is { } ch
            ? Math.Ceiling(Subtotal * ch.MarkupPercent / 100m / 1000m) * 1000m : 0;
        public decimal PayableTotal => Subtotal + ChannelSurcharge - Discount - PromoAmount - RedeemValue;

        public IReadOnlyList<string> ChannelOptions =>
            new[] { StoreSettings.Counter }.Concat(_store.Settings.Channels.Where(c => c.IsActive).Select(c => c.Name)).ToList();

        public IReadOnlyList<(string Id, string Name)> CategoryOptions =>
            new[] { (AllCategories, "Tất cả") }
                .Concat(_store.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).Select(c => (c.Id, c.Name)))
                .ToList();

        public bool ShiftOpen => _store.CurrentShift.Status == "Open";

        /// <summary>Payment error if any, otherwise a hint about the shift state; empty when checkout is possible.</summary>
        public string CheckoutNotice
        {
            get
            {
                var shift = _store.CurrentShift;
                string? shiftHint = shift.Status != "Open" ? "Chưa mở ca. Bạn có thể chọn món; hãy mở ca trước khi thanh toán."
                    : shift.CashierName != _auth.CurrentUser?.Username ? $"Ca đang thuộc về {shift.CashierName}. Đổi về đúng nhân viên để thanh toán." : null;
                return CheckoutError ?? shiftHint ?? "";
            }
        }

        #endregion

        #region Menu

        public void SelectCategory(string categoryId) => SelectedCategoryId = categoryId;

        /// <summary>Sellable products of the selected category matching the keyword; sold-out items stay visible, last.</summary>
        public IReadOnlyList<Product> FilterProducts(string? keyword)
        {
            string search = keyword?.Trim().ToLower() ?? "";
            return _store.Products.Where(p =>
            {
                bool matchCat = SelectedCategoryId == AllCategories || p.CategoryId == SelectedCategoryId || p.FeaturedCategoryIds.Contains(SelectedCategoryId);
                bool matchSearch = search.Length == 0 || p.Name.ToLower().Contains(search);
                return (!p.IsTopping || p.SoldSeparately) && p.IsActive && _store.Categories.Any(c => c.Id == p.CategoryId && c.IsActive) && matchCat && matchSearch;
            }).OrderBy(p => p.IsSoldOut).ToList();
        }

        public Product? FindActiveProduct(string productId) => _store.Products.FirstOrDefault(p => p.Id == productId && p.IsActive);

        public void ToggleSoldOut(Product product) => _store.SetProductSoldOut(product.Id, !product.IsSoldOut);

        #endregion

        #region Cart lines

        public void LoadNextOrderNumber() => CurrentDailyOrderNumber = _store.GetNextDailyOrderNumber();

        public void AddItem(OrderItem item) => CartItems.Add(item);

        /// <returns>false when the line is already at <see cref="MaxLineQuantity"/>.</returns>
        public bool IncreaseQuantity(OrderItem item)
        {
            if (item.Quantity >= MaxLineQuantity) return false;
            item.Quantity++;
            return true;
        }

        public bool DecreaseQuantity(OrderItem item)
        {
            if (item.Quantity <= 1) return false;
            item.Quantity--;
            return true;
        }

        /// <summary>Removes a line and returns an undo action, which is ignored once the cart moved on to another order.</summary>
        public Action? RemoveItem(OrderItem item)
        {
            int index = CartItems.IndexOf(item);
            if (index < 0) return null;
            string checkoutId = CheckoutId;
            CartItems.Remove(item);
            return () =>
            {
                if (CheckoutId != checkoutId || CheckoutInProgress) return;
                CartItems.Insert(Math.Min(index, CartItems.Count), item);
            };
        }

        public void ReplaceItem(OrderItem original, OrderItem edited)
        {
            int index = CartItems.IndexOf(original);
            if (index >= 0) CartItems[index] = edited;
        }

        /// <summary>Moves one unit of <paramref name="original"/> into its own line right below it.</summary>
        public void SplitItem(OrderItem original, OrderItem splitOff)
        {
            int index = CartItems.IndexOf(original);
            if (index < 0 || original.Quantity <= 1) return;
            original.Quantity--;
            CartItems.Insert(index + 1, splitOff);
        }

        public void ClearCart()
        {
            CheckoutId = Guid.NewGuid().ToString();
            CartItems.Clear();
        }

        #endregion

        #region Adjustments

        public void SetServiceType(string serviceType) => ServiceType = serviceType;

        /// <summary>App orders are priced by the platform: they drop in-store discounts, membership and dine-in.</summary>
        public void SetChannel(string? channel)
        {
            Channel = channel ?? StoreSettings.Counter;
            if (!IsAppOrder) return;
            ServiceType = DefaultServiceType;
            Discount = 0; DiscountReason = ""; CustomerPhone = ""; RedeemPoints = 0;
        }

        public bool DiscountNeedsApproval(decimal amount) => _store.DiscountNeedsApproval(Subtotal, amount);

        public string DiscountApprovalAction(decimal amount, string reason) => AuthService.DiscountApprovalAction(CheckoutId, amount, reason);

        public void ApplyDiscount(decimal amount, string reason) { Discount = amount; DiscountReason = reason; }

        public void SetCustomer(string phone, int redeemPoints) { CustomerPhone = phone; RedeemPoints = redeemPoints; }

        /// <summary>
        /// Drops discount and redeemed points once they no longer fit (empty cart or non-positive total).
        /// </summary>
        /// <returns>true when adjustments on a non-empty cart were dropped, so the cashier should be told.</returns>
        public bool DropInvalidAdjustments()
        {
            if (HasItems && PayableTotal > 0) return false;
            bool notify = HasItems && (Discount > 0 || RedeemPoints > 0);
            Discount = 0; DiscountReason = ""; RedeemPoints = 0;
            return notify;
        }

        /// <summary>Next notes a customer is likely to hand over, e.g. 47k → 50k, 60k, 100k, 200k.</summary>
        public static IReadOnlyList<decimal> SuggestCashNotes(decimal total, int count = 4) =>
            CashNotes.Select(n => Math.Ceiling(total / n) * n).Where(v => v > total).Distinct().OrderBy(v => v).Take(count).ToList();

        #endregion

        #region Checkout

        /// <returns>false when checkout cannot start; <see cref="CheckoutError"/> explains validation failures.</returns>
        public bool BeginCheckout()
        {
            if (CheckoutInProgress || !HasItems) return false;
            try { _store.ValidateCheckoutCart(CartItems, IsAppOrder ? DefaultServiceType : ServiceType); }
            catch (Exception ex) { CheckoutError = ex.Message; return false; }
            CheckoutInProgress = true;
            CheckoutError = null;
            return true;
        }

        public void EndCheckout() => CheckoutInProgress = false;

        public void SetCheckoutError(string? message) => CheckoutError = message;

        /// <summary>Persists the order. Once this returns the sale is recorded and must not be charged again.</summary>
        public Order CompleteCheckout(string paymentMethod, decimal cashGiven, bool qrApproved = false) =>
            _store.Checkout(CartItems, paymentMethod, cashGiven, CheckoutId, qrApproved, ServiceType, Discount, DiscountReason, CustomerPhone, RedeemPoints, Channel);

        /// <summary>Resets the cart and all adjustments for the next customer.</summary>
        public void StartNewOrder()
        {
            Discount = 0; DiscountReason = ""; ServiceType = DefaultServiceType; CustomerPhone = ""; RedeemPoints = 0;
            Channel = StoreSettings.Counter;
            ClearCart();
            LoadNextOrderNumber();
        }

        /// <returns>null on success, otherwise why the sticker data could not be produced.</returns>
        public string? PrintStickers(Order order)
        {
            try
            {
                _stickers.GenerateStickersForOrder(order);
                _store.Save();
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        #endregion

        #region Held orders

        public void HoldCurrentOrder(string note = "")
        {
            if (!HasItems) return;
            _holds.HoldOrder(CartItems, Subtotal, note, ServiceType);
            ClearCart();
        }

        public void RecallHeldOrder(HeldOrderInfo held)
        {
            ClearCart();
            foreach (var item in held.Items) CartItems.Add(item);
        }

        #endregion
    }
}
