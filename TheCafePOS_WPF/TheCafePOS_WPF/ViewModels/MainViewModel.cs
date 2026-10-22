using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.ViewModels
{
    /// <summary>A notification for the cashier; the view decides how to show it (toast, snackbar).</summary>
    public record PosMessage(string Text, bool IsError = false, string? ActionLabel = null, Action? Action = null);

    /// <summary>
    /// State and business rules of the order being rung up at the POS: cart lines, pricing adjustments,
    /// sales channel and checkout. Contains no WPF controls so it can be exercised from regression tests.
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        public const string AllCategories = "ALL";
        public const string DefaultServiceType = "Mang đi";
        public const string DineIn = "Tại chỗ";
        public const int MaxLineQuantity = 999;
        private static readonly decimal[] CashNotes = { 10000m, 20000m, 50000m, 100000m, 200000m, 500000m };

        private readonly ICatalogService _catalog;
        private readonly IOrderService _orders;
        private readonly IShiftService _shifts;
        private readonly ICustomerService _customers;
        private readonly IPromotionService _promotions;
        private readonly IStoreSettingsService _settings;
        private readonly IAuthService _auth;
        private readonly IHoldOrderService _holds;
        private readonly IStickerPrinterService _stickers;
        private IReadOnlyList<string> _channelOptions = Array.Empty<string>();
        private string _channel = StoreSettings.Counter;

        public MainViewModel(ICatalogService catalog, IOrderService orders, IShiftService shifts, ICustomerService customers,
            IPromotionService promotions, IStoreSettingsService settings, IAuthService auth, IHoldOrderService holds, IStickerPrinterService stickers)
        {
            _catalog = catalog;
            _orders = orders;
            _shifts = shifts;
            _customers = customers;
            _promotions = promotions;
            _settings = settings;
            _auth = auth;
            _holds = holds;
            _stickers = stickers;
            ReloadSettings();
            CartItems.CollectionChanged += (_, _) =>
            {
                if (CartItems.Count == 0) CartStartedAt = null;
                else CartStartedAt ??= DateTime.Now;
                Changed();
            };
            IncreaseQuantityCommand = new RelayCommand(p => { if (p is OrderItem item) IncreaseQuantity(item); });
            DecreaseQuantityCommand = new RelayCommand(p => { if (p is OrderItem item) DecreaseQuantity(item); });
            RemoveItemCommand = new RelayCommand(p => { if (p is OrderItem item) RemoveItemWithUndo(item); });
            SetServiceTypeCommand = new RelayCommand(p => { if (p is string type) SetServiceType(type); });
        }

        /// <summary>Raised when the cashier should be told something, e.g. a removed line that can be undone.</summary>
        public event EventHandler<PosMessage>? MessageRaised;

        public ObservableCollection<OrderItem> CartItems { get; } = new();
        public ObservableCollection<PrintedStickerInfo> RecentStickers => _stickers.RecentPrintedStickers;

        public ICommand IncreaseQuantityCommand { get; }
        public ICommand DecreaseQuantityCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand SetServiceTypeCommand { get; }

        public int CurrentDailyOrderNumber { get; private set; } = 1;
        public string CheckoutId { get; private set; } = Guid.NewGuid().ToString();
        public DateTime? CartStartedAt { get; private set; }
        public bool CheckoutInProgress { get; private set; }
        public string? CheckoutError { get; private set; }

        public string SelectedCategoryId { get; private set; } = AllCategories;
        public string ServiceType { get; private set; } = DefaultServiceType;
        public decimal Discount { get; private set; }
        public string DiscountReason { get; private set; } = "";
        public string CustomerPhone { get; private set; } = "";
        public int RedeemPoints { get; private set; }

        /// <summary>Bound two-way to the channel picker; see <see cref="SetChannel"/>.</summary>
        public string Channel
        {
            get => _channel;
            set => SetChannel(value);
        }

        #region Derived values

        public bool HasItems => CartItems.Count > 0;
        public bool IsCartEmpty => !HasItems;
        public bool IsAppOrder => Channel != StoreSettings.Counter;
        public bool IsCounterOrder => !IsAppOrder;
        public bool IsTakeAway => ServiceType == DefaultServiceType;
        public bool IsDineIn => ServiceType == DineIn;
        public bool CanEditCart => !CheckoutInProgress;
        public bool CanPay => HasItems && !CheckoutInProgress;
        public int TotalCups => CartItems.Sum(i => i.Quantity);
        public int HeldOrderCount => _holds.HeldOrders.Count;
        public string StoreName => _settings.Settings.DisplayName;
        public Customer? Member => _customers.FindCustomer(CustomerPhone);
        public Customer? FindCustomer(string phone) => _customers.FindCustomer(phone);

        public decimal Subtotal => CartItems.Sum(i => i.TotalPrice);
        public (decimal Amount, string Names) Promotions => IsAppOrder ? (0m, "") : _promotions.PromotionsFor(CartItems, DataStoreService.CafeNow);
        public decimal PromoAmount => Promotions.Amount;
        public decimal RedeemValue => RedeemPoints * DataStoreService.PointValue;
        public decimal ChannelSurcharge => IsAppOrder && _settings.Settings.Channels.FirstOrDefault(c => c.Name == Channel) is { } ch
            ? Math.Ceiling(Subtotal * ch.MarkupPercent / 100m / 1000m) * 1000m : 0;
        public decimal PayableTotal => Subtotal + ChannelSurcharge - Discount - PromoAmount - RedeemValue;

        /// <summary>Same instance until <see cref="ReloadSettings"/>, so a bound picker keeps its selection.</summary>
        public IReadOnlyList<string> ChannelOptions => _channelOptions;

        public IReadOnlyList<(string Id, string Name)> CategoryOptions =>
            new[] { (AllCategories, "Tất cả") }
                .Concat(_catalog.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).Select(c => (c.Id, c.Name)))
                .ToList();

        public bool ShiftOpen => _shifts.CurrentShift.Status == "Open";
        public bool ShowOpenShiftHint => !ShiftOpen;

        /// <summary>Payment error if any, otherwise a hint about the shift state; empty when checkout is possible.</summary>
        public string CheckoutNotice
        {
            get
            {
                var shift = _shifts.CurrentShift;
                string? shiftHint = shift.Status != "Open" ? "Chưa mở ca. Bạn có thể chọn món; hãy mở ca trước khi thanh toán."
                    : shift.CashierName != _auth.CurrentUser?.Username ? $"Ca đang thuộc về {shift.CashierName}. Đổi về đúng nhân viên để thanh toán." : null;
                return CheckoutError ?? shiftHint ?? "";
            }
        }
        public bool HasCheckoutNotice => CheckoutNotice.Length > 0;

        #endregion

        #region Display text

        public string CartTitle => CartStartedAt is null ? "Đơn mới" : $"Đơn #{CurrentDailyOrderNumber:D2}";
        public string CartTimeText
        {
            get
            {
                // The order "exists" only once the first item is added; until then show guidance instead of a time.
                if (CartStartedAt is not DateTime started) return "Chọn món bên trái để bắt đầu";
                int minutes = (int)(DateTime.Now - started).TotalMinutes;
                return $"Bắt đầu {started:HH:mm} ({(minutes < 1 ? "vừa xong" : $"{minutes} phút trước")})";
            }
        }
        public string HeldOrdersText => $"Đơn giữ: {HeldOrderCount} (F9)";
        public string ItemCountText => $"Tổng cộng ({TotalCups} món)";
        public string GrandTotalText => $"{PayableTotal:N0}đ";
        public string CashButtonText => IsAppOrder ? $"Ghi nhận đơn {Channel} (F4)" : "Tiền mặt (F4)";
        public string CustomerButtonText => Member is null ? "Thành viên" : "Đổi khách";
        public string DiscountButtonText => Discount > 0 ? "Sửa giảm giá" : "Giảm giá";

        public string CustomerLine => Member is { } m ? $"{m.Name}: {m.Points} điểm, hạng {m.Tier}" : "";
        public bool ShowCustomerLine => Member is not null;
        public string SubtotalLine => $"Tạm tính {Subtotal:N0}đ";
        // Subtotal is only worth a line when something changes it.
        public bool ShowSubtotalLine => Subtotal != PayableTotal;
        public string ChannelLine => $"Giá kênh {Channel}: +{ChannelSurcharge:N0}đ (khách trả qua ứng dụng)";
        public string PromoLine
        {
            get
            {
                var (amount, names) = Promotions;
                return $"Khuyến mãi ({names}): −{amount:N0}đ";
            }
        }
        public bool ShowPromoLine => PromoAmount > 0;
        public string LoyaltyLine => $"Đổi {RedeemPoints} điểm −{RedeemValue:N0}đ";
        public bool ShowLoyaltyLine => RedeemPoints > 0;
        public string DiscountLine => $"Giảm giá −{Discount:N0}đ ({DiscountReason})";
        public bool ShowDiscountLine => Discount > 0;

        #endregion

        #region Notifications

        /// <summary>
        /// Re-evaluates the order after any change: drops adjustments that no longer fit, then refreshes every binding.
        /// </summary>
        private void Changed(bool clearCheckoutError = true)
        {
            if (clearCheckoutError) CheckoutError = null;
            if (DropInvalidAdjustments()) Raise(new PosMessage("Đã bỏ giảm giá / đổi điểm vì vượt quá tạm tính.", true));
            OnPropertyChanged(string.Empty);
        }

        /// <summary>For state that changes outside this view model: shift, signed-in user, held orders, clock.</summary>
        public void Refresh() => OnPropertyChanged(string.Empty);

        private void Raise(PosMessage message) => MessageRaised?.Invoke(this, message);

        #endregion

        #region Menu

        public void ReloadSettings()
        {
            _channelOptions = new[] { StoreSettings.Counter }.Concat(_settings.Settings.Channels.Where(c => c.IsActive).Select(c => c.Name)).ToList();
            if (!_channelOptions.Contains(_channel)) _channel = StoreSettings.Counter;
            Changed();
        }

        public ObservableCollection<MenuTile> MenuItems { get; } = new();

        /// <summary>Rebuilt only by <see cref="ReloadMenu"/>; selection changes flip <see cref="CategoryOption.IsSelected"/> in place.</summary>
        public IReadOnlyList<CategoryOption> CategoryList { get; private set; } = Array.Empty<CategoryOption>();

        public string MenuCountText => $"{MenuItems.Count} món";
        public bool HasNoProducts => MenuItems.Count == 0;

        /// <summary>Bound to the search box; every keystroke refilters the menu.</summary>
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value ?? "")) RefreshMenu(); }
        }
        private string _searchText = "";

        /// <summary>Reloads categories from the store (after catalog edits) and shows the whole menu.</summary>
        public void ReloadMenu()
        {
            CategoryList = CategoryOptions.Select(c => new CategoryOption(c.Id, c.Name)).ToList();
            OnPropertyChanged(nameof(CategoryList));
            SelectCategory(AllCategories);
        }

        public void SelectCategory(string categoryId)
        {
            SelectedCategoryId = categoryId;
            foreach (var option in CategoryList) option.IsSelected = option.Id == categoryId;
            RefreshMenu();
        }

        public void ResetMenuFilter()
        {
            _searchText = "";
            OnPropertyChanged(nameof(SearchText));
            SelectCategory(AllCategories);
        }

        public void RefreshMenu()
        {
            MenuItems.Clear();
            foreach (var product in FilterProducts(SearchText)) MenuItems.Add(new MenuTile(product));
            OnPropertyChanged(nameof(MenuCountText));
            OnPropertyChanged(nameof(HasNoProducts));
        }

        /// <summary>Sellable products of the selected category matching the keyword; sold-out items stay visible, last.</summary>
        public IReadOnlyList<Product> FilterProducts(string? keyword)
        {
            string search = keyword?.Trim().ToLower() ?? "";
            return _catalog.Products.Where(p =>
            {
                bool matchCat = SelectedCategoryId == AllCategories || p.CategoryId == SelectedCategoryId || p.FeaturedCategoryIds.Contains(SelectedCategoryId);
                bool matchSearch = search.Length == 0 || p.Name.ToLower().Contains(search);
                return (!p.IsTopping || p.SoldSeparately) && p.IsActive && _catalog.Categories.Any(c => c.Id == p.CategoryId && c.IsActive) && matchCat && matchSearch;
            }).OrderBy(p => p.IsSoldOut).ToList();
        }

        public Product? FindActiveProduct(string productId) => _catalog.Products.FirstOrDefault(p => p.Id == productId && p.IsActive);

        public void ToggleSoldOut(Product product)
        {
            try
            {
                _catalog.SetProductSoldOut(product.Id, !product.IsSoldOut);
                RefreshMenu();
            }
            catch (Exception ex) { Raise(new PosMessage(ex.Message, true)); }
        }

        #endregion

        #region Cart lines

        public void LoadNextOrderNumber()
        {
            CurrentDailyOrderNumber = _orders.GetNextDailyOrderNumber();
            Changed();
        }

        public void AddItem(OrderItem item) => CartItems.Add(item);

        /// <returns>false when the line is already at <see cref="MaxLineQuantity"/>; the cashier is told why.</returns>
        public bool IncreaseQuantity(OrderItem item)
        {
            if (item.Quantity >= MaxLineQuantity)
            {
                Raise(new PosMessage($"Tối đa {MaxLineQuantity} sản phẩm trên một dòng.", true));
                return false;
            }
            item.Quantity++;
            Changed();
            return true;
        }

        public bool DecreaseQuantity(OrderItem item)
        {
            if (item.Quantity <= 1) return false;
            item.Quantity--;
            Changed();
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

        /// <summary>Removes immediately and offers undo instead of asking for confirmation.</summary>
        public void RemoveItemWithUndo(OrderItem item)
        {
            if (RemoveItem(item) is { } undo)
                Raise(new PosMessage($"Đã xóa {item.Quantity}× {item.ProductName}", ActionLabel: "Hoàn tác", Action: undo));
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
            // Insert before decrementing so the subtotal never dips and drops a discount that still fits.
            CartItems.Insert(index + 1, splitOff);
            original.Quantity--;
            Changed();
        }

        public void ClearCart()
        {
            CheckoutId = Guid.NewGuid().ToString();
            CartItems.Clear();
        }

        #endregion

        #region Adjustments

        public void SetServiceType(string serviceType)
        {
            ServiceType = serviceType;
            Changed();
        }

        /// <summary>App orders are priced by the platform: they drop in-store discounts, membership and dine-in.</summary>
        public void SetChannel(string? channel)
        {
            _channel = channel ?? StoreSettings.Counter;
            if (IsAppOrder)
            {
                ServiceType = DefaultServiceType;
                Discount = 0; DiscountReason = ""; CustomerPhone = ""; RedeemPoints = 0;
            }
            Changed();
        }

        public bool DiscountNeedsApproval(decimal amount) => _settings.DiscountNeedsApproval(Subtotal, amount);

        public string DiscountApprovalAction(decimal amount, string reason) => AuthService.DiscountApprovalAction(CheckoutId, amount, reason);

        public void ApplyDiscount(decimal amount, string reason)
        {
            Discount = amount; DiscountReason = reason;
            Changed();
        }

        public void SetCustomer(string phone, int redeemPoints)
        {
            CustomerPhone = phone; RedeemPoints = redeemPoints;
            Changed();
        }

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
            try { _orders.ValidateCheckoutCart(CartItems, IsAppOrder ? DefaultServiceType : ServiceType); }
            catch (Exception ex) { SetCheckoutError(ex.Message); return false; }
            CheckoutInProgress = true;
            Changed();
            return true;
        }

        /// <summary>Unlocks the cart; a payment error from the attempt stays visible.</summary>
        public void EndCheckout()
        {
            CheckoutInProgress = false;
            Changed(clearCheckoutError: false);
        }

        public void SetCheckoutError(string? message)
        {
            CheckoutError = message;
            OnPropertyChanged(string.Empty);
        }

        /// <summary>Persists the order. Once this returns the sale is recorded and must not be charged again.</summary>
        public Order CompleteCheckout(string paymentMethod, decimal cashGiven, bool qrApproved = false) =>
            _orders.Checkout(CartItems, paymentMethod, cashGiven, CheckoutId, qrApproved, ServiceType, Discount, DiscountReason, CustomerPhone, RedeemPoints, _channel);

        /// <summary>Resets the cart and all adjustments for the next customer.</summary>
        public void StartNewOrder()
        {
            Discount = 0; DiscountReason = ""; ServiceType = DefaultServiceType; CustomerPhone = ""; RedeemPoints = 0;
            _channel = StoreSettings.Counter;
            ClearCart();
            LoadNextOrderNumber();
        }

        /// <returns>null on success, otherwise why the sticker data could not be produced.</returns>
        public string? PrintStickers(Order order)
        {
            try
            {
                _stickers.GenerateStickersForOrder(order);
                _orders.Save();
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
