using System.Collections.ObjectModel;
using System.Text.Json;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public class StoreState
{
    public int MenuSchemaVersion { get; set; }
    public DraftOrder? Draft { get; set; }
    public List<RefundEntry> Refunds { get; set; } = new();
    public List<CashEntry> CashEntries { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Product> Products { get; set; } = new();
    public List<PackagingItem> Packaging { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
    public List<Shift> Shifts { get; set; } = new();
    public Shift CurrentShift { get; set; } = new() { Status = "Closed" };
    public List<StickerPrintLog> StickerLogs { get; set; } = new();
    public List<StockMovement> StockMovements { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
    public List<Promotion> Promotions { get; set; } = new();
    public List<Ingredient> Ingredients { get; set; } = new();
    public List<IngredientMovement> IngredientMovements { get; set; } = new();
    public StoreSettings Settings { get; set; } = new();
}
public partial class DataStoreService
{
    public List<RefundEntry> Refunds { get; private set; } = new();
    public List<CashEntry> CashEntries { get; private set; } = new();
    public List<Shift> Shifts { get; private set; } = new();
    public List<StockMovement> StockMovements { get; private set; } = new();
    private StoreState Snapshot() => new() { Draft = Draft, Refunds = Refunds, CashEntries = CashEntries, MenuSchemaVersion = 7, Categories = Categories, Products = Products, Packaging = PackagingInventory, Orders = CompletedOrders.ToList(), Shifts = Shifts, CurrentShift = CurrentShift, StickerLogs = StickerLogs, StockMovements = StockMovements, Customers = Customers, Promotions = Promotions, Ingredients = Ingredients, IngredientMovements = IngredientMovements, Settings = Settings };
    // Early seed data used emoji and all-caps names; show them like every other category.
    private static readonly Dictionary<string, string> LegacyCategoryNames = new()
    {
        ["🔥 HỒNG TRÀ & TRÀ ĐEN"] = "Hồng trà & trà đen", ["🧋 TRÀ SỮA ĐẶC BIỆT"] = "Trà sữa đặc biệt", ["🍊 TRÀ TRÁI CÂY"] = "Trà trái cây",
        ["🧊 MACHIATO & KEM"] = "Machiato & kem", ["🍡 TOPPING THÊM"] = "Topping thêm"
    };

    private void Restore(StoreState state)
    {
        Draft = state.Draft;
        Categories = state.Categories; Products = state.Products; PackagingInventory = state.Packaging;
        foreach (var c in Categories) if (LegacyCategoryNames.TryGetValue(c.Name, out var plain)) c.Name = plain;
        CompletedOrders = new ObservableCollection<Order>(state.Orders); Shifts = state.Shifts;
        CurrentShift = state.CurrentShift; StickerLogs = state.StickerLogs;
        StockMovements = state.StockMovements;
        Refunds = state.Refunds; CashEntries = state.CashEntries;
        Customers = state.Customers ?? new(); Promotions = state.Promotions ?? new();
        Ingredients = state.Ingredients ?? new(); IngredientMovements = state.IngredientMovements ?? new();
        Settings = state.Settings ?? new();
    }
    private void LoadState()
    {
        var state = _db.Read<StoreState>("store");
        if (state is not null) Restore(state);
        if (state is null || state.MenuSchemaVersion < 1)
        {
            var toppingIds = Products.Where(p => p.CategoryId == "cat-5").Select(p => p.Id).ToList();
            foreach (var product in Products)
            {
                if (toppingIds.Contains(product.Id))
                {
                    product.IsTopping = true; product.SoldSeparately = false;
                    product.AllowSugar = product.AllowIce = false; product.Sizes.Clear();
                }
                else product.AllowedToppingIds = new List<string>(toppingIds);
            }
        }
        if (state is null || state.MenuSchemaVersion < 2)
        {
            AddAmericanoMenu();
        }
        if (state is null || state.MenuSchemaVersion < 3)
        {
            ImportCoffeeHouseMenu();
        }
        if (state is null || state.MenuSchemaVersion < 4)
        {
            ImportCoffeeHouseOptions();
            Save();
        }
        if (state is null || state.MenuSchemaVersion < 6)
        {
            AddFeaturedMenu();
            Save();
        }
        if (state is null || state.MenuSchemaVersion < 7)
        {
            AddHomepageMenu();
            Save();
        }
        NormalizeProductImagePaths();
    }
    private void AddAmericanoMenu()
    {
        var category = Categories.FirstOrDefault(c => c.Name.Trim().Equals("Cà phê", StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            category = new Category { Name = "Cà phê", DisplayOrder = Categories.Select(c => c.DisplayOrder).DefaultIfEmpty(0).Max() + 1 };
            Categories.Add(category);
        }
        foreach (var (name, hot, image) in new[] {
            ("Americano Classic", false, "americano-classic.png"),
            ("Americano Nóng", true, "americano-nong.png") })
        {
            // Preserve products the shop has already configured; run only once per database.
            if (Products.Any(p => p.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            Products.Add(new Product {
                CategoryId = category.Id, Name = name, BasePrice = 55000,
                ImageUrl = "Assets/Products/" + image, ColorHex = "#6F4935",
                AllowIce = !hot, DefaultIce = hot ? 0 : 100,
                Sizes = new() { new() { Name = "M", ExtraPrice = 0, PackagingId = "pack-1" } }
            });
        }
    }
    public void Save() => _db.Write("store", Snapshot());
    private void Mutate(Action action)
    {
        var before = JsonSerializer.Deserialize<StoreState>(JsonSerializer.Serialize(Snapshot()))!;
        try { action(); Save(); }
        catch { Restore(before); throw; }
    }
    public void OpenShift(decimal initialCash)
    {
        _auth.RequireSignedIn();
        if (CurrentShift.Status == "Open") throw new InvalidOperationException("Ca hiện tại chưa đóng.");
        if (initialCash < 0) throw new InvalidOperationException("Tiền đầu ca không được âm.");
        Mutate(() => CurrentShift = new Shift { CashierName = _auth.CurrentUser!.Username, InitialCash = initialCash });
    }
    public decimal ExpectedCash => CurrentShift.InitialCash + CompletedOrders.Where(o => o.ShiftId == CurrentShift.Id && o.PaymentMethod == "Cash" && o.PaymentStatus == "Completed").Sum(o => o.TotalAmount)
        + CashEntries.Where(e => e.ShiftId == CurrentShift.Id).Sum(e => e.SignedAmount)
        - Refunds.Where(r => r.ShiftId == CurrentShift.Id && r.Method == "Cash").Sum(r => r.Amount);
    public void RequireOpenShift()
    {
        _auth.RequireSignedIn();
        if (_auth.CurrentUser is null || CurrentShift.Status != "Open") throw new InvalidOperationException("Vui lòng mở ca trước khi thanh toán.");
        if (CurrentShift.CashierName != _auth.CurrentUser.Username) throw new InvalidOperationException($"Ca đang thuộc về {CurrentShift.CashierName}. Hãy đăng nhập đúng nhân viên.");
    }
    public void CloseShift(decimal actualCash)
    {
        RequireOpenShift();
        if (actualCash < 0) throw new InvalidOperationException("Tiền thực tế không được âm.");
        Mutate(() => { CurrentShift.ActualCash = actualCash; CurrentShift.ExpectedCash = ExpectedCash; CurrentShift.EndTime = DateTime.Now; CurrentShift.Status = "Closed"; Shifts.Add(CurrentShift); });
    }
    public Order Checkout(IEnumerable<OrderItem> cart, string method, decimal cash, string checkoutId, bool qrApproved = false, string serviceType = "Mang đi", decimal discount = 0, string discountReason = "", string customerPhone = "", int redeemPoints = 0, string channel = StoreSettings.Counter)
    {
        RequireOpenShift();
        var existing = CompletedOrders.FirstOrDefault(o => o.Id == checkoutId);
        if (existing is not null) return existing;
        var items = JsonSerializer.Deserialize<List<OrderItem>>(JsonSerializer.Serialize(cart.ToList()))!;
        ValidateCheckoutCart(items, channel == StoreSettings.Counter ? serviceType : "Mang đi");
        decimal subtotal = items.Sum(i => i.TotalPrice);
        var customer = string.IsNullOrWhiteSpace(customerPhone) ? null : FindCustomer(customerPhone) ?? throw new InvalidOperationException("Không tìm thấy khách hàng thành viên.");
        if (redeemPoints < 0 || redeemPoints > 0 && (customer is null || customer.Points < redeemPoints)) throw new InvalidOperationException("Khách không đủ điểm để đổi.");
        // Delivery-app orders: listed at the channel price, paid out by the platform, no in-store promotions.
        bool app = channel != StoreSettings.Counter;
        var appChannel = app ? Settings.Channels.FirstOrDefault(c => c.Name == channel && c.IsActive) ?? throw new InvalidOperationException("Kênh bán không hợp lệ hoặc đang tắt.") : null;
        if (app && (discount > 0 || redeemPoints > 0 || method != "Platform")) throw new InvalidOperationException("Đơn từ app giao hàng không áp dụng giảm giá/đổi điểm và được ghi nhận là thanh toán qua nền tảng.");
        if (!app && method == "Platform") throw new InvalidOperationException("Chỉ đơn từ app giao hàng mới ghi nhận thanh toán qua nền tảng.");
        if (app) serviceType = "Mang đi";
        var promos = app ? (Amount: 0m, Names: "") : PromotionsFor(items, CafeNow);
        decimal promoAmount = promos.Amount, loyaltyAmount = redeemPoints * PointValue;
        if (discount < 0 || discount != decimal.Truncate(discount) || discount + promoAmount + loyaltyAmount >= subtotal) throw new InvalidOperationException("Số tiền giảm phải là số nguyên, không âm và nhỏ hơn tạm tính.");
        if (discount > 0 && string.IsNullOrWhiteSpace(discountReason)) throw new InvalidOperationException("Nhập lý do giảm giá.");
        if (DiscountNeedsApproval(subtotal, discount)) _auth.RequireApproval(AuthService.DiscountApprovalAction(checkoutId, discount, discountReason));
        decimal surcharge = appChannel is null ? 0 : Math.Ceiling(subtotal * appChannel.MarkupPercent / 100m / 1000m) * 1000m;
        decimal total = subtotal + surcharge - discount - promoAmount - loyaltyAmount;
        if (total <= 0) throw new InvalidOperationException("Tổng tiền phải lớn hơn 0.");
        if (method is not ("Cash" or "VietQR" or "Platform")) throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");
        if (method == "Cash" && !Core.CashAmount.IsValid(cash)) throw new InvalidOperationException("Tiền khách đưa phải là số nguyên không âm, tối đa 999.999.999.999đ.");
        if (method == "Cash" && cash < total) throw new InvalidOperationException($"Tiền khách đưa còn thiếu {total - cash:N0}đ.");
        if (method == "VietQR" && (!qrApproved || !_auth.HasPaymentApproval(checkoutId, total))) throw new InvalidOperationException("Cần quản lý xác nhận đã nhận chuyển khoản.");
        if (serviceType is not ("Mang đi" or "Tại chỗ")) throw new InvalidOperationException("Chọn loại đơn hợp lệ.");
        var order = new Order { ServiceType = serviceType, FulfillmentStatus = "Chờ làm", PaidAt = DateTimeOffset.Now, CreatedAt = CafeNow, Id = checkoutId, ShiftId = CurrentShift.Id, DailyOrderNumber = GetNextDailyOrderNumber(), Items = items, SubtotalAmount = subtotal, DiscountAmount = discount, DiscountReason = discountReason.Trim(), PromotionAmount = promoAmount, PromotionName = promos.Names, CustomerPhone = customer?.Phone ?? "", PointsRedeemed = redeemPoints, Channel = channel, ChannelSurcharge = surcharge, TotalAmount = total, PaymentMethod = method, CashGiven = method == "Cash" ? cash : 0, ChangeReturned = method == "Cash" ? cash - total : 0 };
        foreach (var item in items) { item.OrderId = order.Id; item.ReadyQuantity = item.DeliveredQuantity = 0; }
        Mutate(() =>
        {
            var stockBefore = PackagingInventory.ToDictionary(p => p.Id, p => p.StockQuantity);
            // Dine-in drinks are served in reusable glasses, so only take-away consumes cups, lids and straws.
            if (serviceType == "Mang đi") DeductPackagingForOrder(order);
            if (PackagingInventory.Any(p => p.StockQuantity < 0)) throw new InvalidOperationException("Không đủ bao bì để hoàn tất đơn.");
            foreach (var packaging in PackagingInventory.Where(p => p.StockQuantity != stockBefore[p.Id]))
                RecordStockMovement(packaging, stockBefore[packaging.Id], "Bán hàng", $"Đơn #{order.DailyOrderNumber:D2}", order.Id);
            ApplyLoyalty(order);
            DeductIngredientsForOrder(order);
            CompletedOrders.Add(order);
            if (Draft?.Id == checkoutId) Draft = null;
        });
        return order;
    }
    public void SaveProduct(Product product)
    {
        _auth.RequireManager();
        if (string.IsNullOrWhiteSpace(product.Name) || product.BasePrice <= 0 || product.BasePrice != decimal.Truncate(product.BasePrice) || !Categories.Any(c => c.Id == product.CategoryId)) throw new InvalidOperationException("Nhập tên món, giá nguyên dương và danh mục hợp lệ.");
        if (!OrderConfigurationService.Levels.Contains(product.DefaultSugar) || !OrderConfigurationService.Levels.Contains(product.DefaultIce)) throw new InvalidOperationException("Mức đường/đá mặc định không hợp lệ.");
        if ((product.AllowSugar && product.SugarChoices != null && !product.SugarChoices.Contains(product.DefaultSugarChoice)) ||
            (product.AllowIce && product.IceChoices != null && !product.IceChoices.Contains(product.DefaultIceChoice)))
            throw new InvalidOperationException("Lựa chọn đường/đá mặc định phải nằm trong tùy chọn của món.");
        if (product.IsTopping && product.IsRetailItem) throw new InvalidOperationException("Chọn một loại: topping hoặc hàng bán lẻ.");
        if (product.IsBeverage && (product.Sizes.Count == 0 || product.Sizes.Select(s => s.Name).Distinct().Count() != product.Sizes.Count || product.Sizes.Any(s => string.IsNullOrWhiteSpace(s.Name) || s.ExtraPrice < 0 || s.ExtraPrice != decimal.Truncate(s.ExtraPrice) || !PackagingInventory.Any(p => p.Id == s.PackagingId)))) throw new InvalidOperationException("Cần ít nhất một size, phụ thu nguyên không âm và bao bì hợp lệ.");
        if (product.AllowedToppingIds.Any(id => id == product.Id || !Products.Any(p => p.Id == id && p.IsTopping))) throw new InvalidOperationException("Danh sách topping không hợp lệ.");
        Mutate(() => { Products.RemoveAll(p => p.Id == product.Id); Products.Add(product); });
    }
    public void SaveCategory(Category category)
    {
        _auth.RequireManager();
        if (string.IsNullOrWhiteSpace(category.Name)) throw new InvalidOperationException("Tên danh mục không được trống.");
        Mutate(() => { Categories.RemoveAll(c => c.Id == category.Id); Categories.Add(category); });
    }

    public void UpdateStock(string packagingId, string operation, int quantity, string reason)
    {
        _auth.RequireManager();
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Vui lòng nhập lý do hoặc thông tin phiếu nhập/kiểm kê.");
        var item = PackagingInventory.FirstOrDefault(p => p.Id == packagingId)
            ?? throw new InvalidOperationException("Bao bì không tồn tại.");
        long target = operation switch
        {
            "Nhập kho" when quantity > 0 => (long)item.StockQuantity + quantity,
            "Kiểm kê" when quantity >= 0 => quantity,
            "Điều chỉnh" when quantity != 0 => (long)item.StockQuantity + quantity,
            _ => throw new InvalidOperationException("Nhập kho cần số dương; kiểm kê cần số không âm; điều chỉnh cần số khác 0.")
        };
        if (target < 0 || target > int.MaxValue) throw new InvalidOperationException("Tồn kho sau thay đổi phải từ 0 đến 2.147.483.647.");
        Mutate(() =>
        {
            int before = item.StockQuantity;
            item.StockQuantity = (int)target;
            RecordStockMovement(item, before, operation, reason.Trim());
        });
    }

    private void RecordStockMovement(PackagingItem item, int before, string type, string reason, string orderId = "")
    {
        StockMovements.Add(new StockMovement
        {
            PackagingId = item.Id, PackagingName = item.Name, Before = before, After = item.StockQuantity,
            Type = type, Reason = reason, OrderId = orderId, Operator = _auth.CurrentUser?.Username ?? ""
        });
    }
}

