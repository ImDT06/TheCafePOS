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
}
public partial class DataStoreService
{
    public List<RefundEntry> Refunds { get; private set; } = new();
    public List<CashEntry> CashEntries { get; private set; } = new();
    public List<Shift> Shifts { get; private set; } = new();
    public List<StockMovement> StockMovements { get; private set; } = new();
    private StoreState Snapshot() => new() { Draft = Draft, Refunds = Refunds, CashEntries = CashEntries, MenuSchemaVersion = 4, Categories = Categories, Products = Products, Packaging = PackagingInventory, Orders = CompletedOrders.ToList(), Shifts = Shifts, CurrentShift = CurrentShift, StickerLogs = StickerLogs, StockMovements = StockMovements, Customers = Customers, Promotions = Promotions };
    private void Restore(StoreState state)
    {
        Draft = state.Draft;
        Categories = state.Categories; Products = state.Products; PackagingInventory = state.Packaging;
        CompletedOrders = new ObservableCollection<Order>(state.Orders); Shifts = state.Shifts;
        CurrentShift = state.CurrentShift; StickerLogs = state.StickerLogs;
        StockMovements = state.StockMovements;
        Refunds = state.Refunds; CashEntries = state.CashEntries;
        Customers = state.Customers ?? new(); Promotions = state.Promotions ?? new();
    }
    private void LoadState()
    {
        var state = LocalDatabase.Instance.Read<StoreState>("store");
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
    public void Save() => LocalDatabase.Instance.Write("store", Snapshot());
    private void Mutate(Action action)
    {
        var before = JsonSerializer.Deserialize<StoreState>(JsonSerializer.Serialize(Snapshot()))!;
        try { action(); Save(); }
        catch { Restore(before); throw; }
    }
    public void OpenShift(decimal initialCash)
    {
        AuthService.Instance.RequireSignedIn();
        if (CurrentShift.Status == "Open") throw new InvalidOperationException("Ca hiện tại chưa đóng.");
        if (initialCash < 0) throw new InvalidOperationException("Tiền đầu ca không được âm.");
        Mutate(() => CurrentShift = new Shift { CashierName = AuthService.Instance.CurrentUser!.Username, InitialCash = initialCash });
    }
    public decimal ExpectedCash => CurrentShift.InitialCash + CompletedOrders.Where(o => o.ShiftId == CurrentShift.Id && o.PaymentMethod == "Cash" && o.PaymentStatus == "Completed").Sum(o => o.TotalAmount)
        + CashEntries.Where(e => e.ShiftId == CurrentShift.Id).Sum(e => e.SignedAmount)
        - Refunds.Where(r => r.ShiftId == CurrentShift.Id && r.Method == "Cash").Sum(r => r.Amount);
    public void RequireOpenShift()
    {
        AuthService.Instance.RequireSignedIn();
        if (AuthService.Instance.CurrentUser is null || CurrentShift.Status != "Open") throw new InvalidOperationException("Vui lòng mở ca trước khi thanh toán.");
        if (CurrentShift.CashierName != AuthService.Instance.CurrentUser.Username) throw new InvalidOperationException($"Ca đang thuộc về {CurrentShift.CashierName}. Hãy đăng nhập đúng nhân viên.");
    }
    public void CloseShift(decimal actualCash)
    {
        RequireOpenShift();
        if (actualCash < 0) throw new InvalidOperationException("Tiền thực tế không được âm.");
        Mutate(() => { CurrentShift.ActualCash = actualCash; CurrentShift.ExpectedCash = ExpectedCash; CurrentShift.EndTime = DateTime.Now; CurrentShift.Status = "Closed"; Shifts.Add(CurrentShift); });
    }
    public Order Checkout(IEnumerable<OrderItem> cart, string method, decimal cash, string checkoutId, bool qrApproved = false, string serviceType = "Mang đi", decimal discount = 0, string discountReason = "", string customerPhone = "", int redeemPoints = 0)
    {
        RequireOpenShift();
        var existing = CompletedOrders.FirstOrDefault(o => o.Id == checkoutId);
        if (existing is not null) return existing;
        var items = JsonSerializer.Deserialize<List<OrderItem>>(JsonSerializer.Serialize(cart.ToList()))!;
        if (items.Count == 0 || items.Any(i => i.Quantity <= 0 || i.UnitPrice < 0)) throw new InvalidOperationException("Đơn hàng không hợp lệ.");
        if (items.Any(i => !Products.Any(p => p.Id == i.ProductId && p.IsActive && Categories.Any(c => c.Id == p.CategoryId && c.IsActive)))) throw new InvalidOperationException("Đơn có món đã ngừng bán. Vui lòng cập nhật giỏ hàng.");
        foreach (var item in items)
        {
            var product = Products.Single(p => p.Id == item.ProductId);
            if (item.Quantity > 999 || product.IsTopping && !product.SoldSeparately || item.IsBeverage != product.IsBeverage)
                throw new InvalidOperationException("Loại món hoặc số lượng đã thay đổi. Vui lòng chọn lại món.");
            if (product.IsBeverage && !product.Sizes.Any(s => s.Name == item.Size)) throw new InvalidOperationException("Size đã ngừng bán. Vui lòng sửa món.");
            if (product.IsBeverage &&
                ((product.AllowSugar && product.SugarChoices != null && !product.SugarChoices.Contains(item.SugarChoice)) ||
                 (product.AllowIce && product.IceChoices != null && !product.IceChoices.Contains(item.IceChoice))))
                throw new InvalidOperationException("Tùy chọn đường/đá đã thay đổi. Vui lòng sửa món trước khi thanh toán.");
            if (item.SelectedToppings.Any(t => !product.AllowedToppingIds.Contains(t.ProductId) || !Products.Any(p => p.Id == t.ProductId && p.IsTopping && p.IsActive && Categories.Any(c => c.Id == p.CategoryId && c.IsActive))))
                throw new InvalidOperationException("Topping đã ngừng bán hoặc không còn áp dụng. Vui lòng sửa món.");
        }
        if (items.Any(i => Products.Single(p => p.Id == i.ProductId).IsSoldOut)) throw new InvalidOperationException("Đơn có món đang hết hàng. Vui lòng bỏ món đó khỏi giỏ.");
        decimal subtotal = items.Sum(i => i.TotalPrice);
        var customer = string.IsNullOrWhiteSpace(customerPhone) ? null : FindCustomer(customerPhone) ?? throw new InvalidOperationException("Không tìm thấy khách hàng thành viên.");
        if (redeemPoints < 0 || redeemPoints > 0 && (customer is null || customer.Points < redeemPoints)) throw new InvalidOperationException("Khách không đủ điểm để đổi.");
        var promo = ActivePromotion(CafeNow);
        decimal promoAmount = PromotionDiscount(promo, subtotal), loyaltyAmount = redeemPoints * PointValue;
        if (discount < 0 || discount != decimal.Truncate(discount) || discount + promoAmount + loyaltyAmount >= subtotal) throw new InvalidOperationException("Số tiền giảm phải là số nguyên, không âm và nhỏ hơn tạm tính.");
        if (discount > 0 && string.IsNullOrWhiteSpace(discountReason)) throw new InvalidOperationException("Nhập lý do giảm giá.");
        if (discount > 0) AuthService.Instance.RequireApproval(AuthService.DiscountApprovalAction(checkoutId, discount, discountReason));
        decimal total = subtotal - discount - promoAmount - loyaltyAmount;
        if (total <= 0) throw new InvalidOperationException("Tổng tiền phải lớn hơn 0.");
        if (method is not ("Cash" or "VietQR")) throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");
        if (method == "Cash" && cash < total) throw new InvalidOperationException("Số tiền khách đưa chưa đủ hoặc không hợp lệ.");
        if (method == "VietQR" && (!qrApproved || !AuthService.Instance.HasPaymentApproval(checkoutId, total))) throw new InvalidOperationException("Cần quản lý xác nhận đã nhận chuyển khoản.");
        if (serviceType is not ("Mang đi" or "Tại chỗ")) throw new InvalidOperationException("Chọn loại đơn hợp lệ.");
        var order = new Order { ServiceType = serviceType, FulfillmentStatus = "Chờ làm", PaidAt = DateTimeOffset.Now, CreatedAt = CafeNow, Id = checkoutId, ShiftId = CurrentShift.Id, DailyOrderNumber = GetNextDailyOrderNumber(), Items = items, SubtotalAmount = subtotal, DiscountAmount = discount, DiscountReason = discountReason.Trim(), PromotionAmount = promoAmount, PromotionName = promoAmount > 0 ? promo!.Name : "", CustomerPhone = customer?.Phone ?? "", PointsRedeemed = redeemPoints, TotalAmount = total, PaymentMethod = method, CashGiven = method == "Cash" ? cash : 0, ChangeReturned = method == "Cash" ? cash - total : 0 };
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
            CompletedOrders.Add(order);
            if (Draft?.Id == checkoutId) Draft = null;
        });
        return order;
    }
    public void SaveProduct(Product product)
    {
        AuthService.Instance.RequireManager();
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
        AuthService.Instance.RequireManager();
        if (string.IsNullOrWhiteSpace(category.Name)) throw new InvalidOperationException("Tên danh mục không được trống.");
        Mutate(() => { Categories.RemoveAll(c => c.Id == category.Id); Categories.Add(category); });
    }

    public void UpdateStock(string packagingId, string operation, int quantity, string reason)
    {
        AuthService.Instance.RequireManager();
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
            Type = type, Reason = reason, OrderId = orderId, Operator = AuthService.Instance.CurrentUser?.Username ?? ""
        });
    }
}

