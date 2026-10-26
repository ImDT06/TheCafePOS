using System.Text.RegularExpressions;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    public const decimal SpendPerPoint = 10_000;   // 1 điểm cho mỗi 10.000đ thực thu
    public const decimal PointValue = 1_000;       // 1 điểm đổi được 1.000đ
    public List<Customer> Customers { get; private set; } = new();
    public List<Promotion> Promotions { get; private set; } = new();
    public StoreSettings Settings { get; private set; } = new();

    public static string NormalizePhone(string phone)
    {
        string digits = Regex.Replace(phone ?? "", "[^0-9]", "");
        if (digits.StartsWith("84") && digits.Length == 11) digits = "0" + digits[2..];
        return Regex.IsMatch(digits, "^0[0-9]{9}$") ? digits : throw new InvalidOperationException("Số điện thoại phải gồm 10 chữ số, bắt đầu bằng 0.");
    }
    public Customer? FindCustomer(string phone)
    {
        try { var p = NormalizePhone(phone); return Customers.FirstOrDefault(c => c.Phone == p); }
        catch (InvalidOperationException) { return null; }
    }
    public Customer RegisterCustomer(string phone, string name)
    {
        _auth.RequireSignedIn();
        var p = NormalizePhone(phone);
        if (Customers.Any(c => c.Phone == p)) throw new InvalidOperationException("Số điện thoại đã là thành viên.");
        var customer = new Customer { Phone = p, Name = string.IsNullOrWhiteSpace(name) ? "Khách " + p[^4..] : name.Trim() };
        Mutate(() => Customers.Add(customer));
        return customer;
    }

    // The phone is the member key, so changing it moves the purchase history to the new number as well.
    public void UpdateCustomer(string phone, string name, string? newPhone = null)
    {
        _auth.RequireManager();
        var customer = FindCustomer(phone) ?? throw new InvalidOperationException("Không tìm thấy khách hàng.");
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Nhập tên khách hàng.");
        string target = string.IsNullOrWhiteSpace(newPhone) ? customer.Phone : NormalizePhone(newPhone);
        if (target != customer.Phone && Customers.Any(c => c.Phone == target)) throw new InvalidOperationException("Số điện thoại mới đã là thành viên khác.");
        string old = customer.Phone;
        Mutate(() =>
        {
            customer.Name = name.Trim();
            if (target == old) return;
            customer.Phone = target;
            foreach (var order in CompletedOrders.Where(o => o.CustomerPhone == old)) order.CustomerPhone = target;
        });
    }

    // Removes the member profile and its points; paid orders keep their phone for reports and refunds still work.
    public void DeleteCustomer(string phone)
    {
        _auth.RequireManager();
        var customer = FindCustomer(phone) ?? throw new InvalidOperationException("Không tìm thấy khách hàng.");
        Mutate(() => Customers.Remove(customer));
    }
    public List<Order> CustomerOrders(string phone) => CompletedOrders.Where(o => o.CustomerPhone == phone).OrderByDescending(o => o.CreatedAt).ToList();

    public bool DiscountNeedsApproval(decimal subtotal, decimal discount) =>
        discount > 0 && !_auth.IsManager && discount > Math.Floor(subtotal * Settings.CashierDiscountLimitPercent / 100m);
    public void SaveSettings(StoreSettings settings)
    {
        _auth.RequireManager();
        if (settings.CashierDiscountLimitPercent is < 0 or > 100) throw new InvalidOperationException("Hạn mức giảm giá phải từ 0 đến 100%.");
        if (settings.AutoLockMinutes is < 0 or > 60) throw new InvalidOperationException("Thời gian tự khóa phải từ 0 đến 60 phút.");
        if (settings.VatPercent is < 0 or > 10) throw new InvalidOperationException("Thuế VAT phải từ 0 đến 10%.");
        if ((settings.ReceiptFooter ?? "").Length > 200) throw new InvalidOperationException("Lời chào cuối hóa đơn tối đa 200 ký tự.");
        if (settings.Channels.Any(c => string.IsNullOrWhiteSpace(c.Name) || c.Name == StoreSettings.Counter || c.MarkupPercent is < 0 or > 100)) throw new InvalidOperationException("Kênh bán cần tên hợp lệ và phụ thu từ 0 đến 100%.");
        if (settings.Channels.GroupBy(c => c.Name.Trim(), StringComparer.CurrentCultureIgnoreCase).Any(g => g.Count() > 1)) throw new InvalidOperationException("Tên kênh bán bị trùng.");
        Mutate(() => Settings = settings);
    }

    public void DeletePromotion(string id)
    {
        _auth.RequireManager();
        Mutate(() => Promotions.RemoveAll(p => p.Id == id));
    }

    public Promotion? ActivePromotion(DateTime at) => Promotions.FirstOrDefault(p => p.AppliesAt(at) && p.Type == "Giờ vàng");
    // Rounded down to 1.000đ so the cashier never handles odd change.
    public static decimal PromotionDiscount(Promotion? promo, decimal subtotal) =>
        promo is null ? 0 : Math.Floor(subtotal * promo.Percent / 100m / 1000m) * 1000m;

    // Every active promotion is applied; the combined discount is capped by Checkout's total validation.
    public (decimal Amount, string Names) PromotionsFor(IEnumerable<OrderItem> cart, DateTime at)
    {
        var items = cart.ToList();
        decimal subtotal = items.Sum(i => i.TotalPrice), total = 0;
        var names = new List<string>();
        foreach (var promo in Promotions.Where(p => p.AppliesAt(at)))
        {
            decimal amount = promo.Type switch
            {
                "Giờ vàng" => PromotionDiscount(promo, subtotal),
                "Mua X tặng Y" => BuyXGetY(promo, items),
                "Combo" => Combo(promo, items),
                _ => 0
            };
            if (amount <= 0) continue;
            total += amount; names.Add(promo.Name);
        }
        return (total, string.Join(", ", names));
    }

    // Units sorted from most to least expensive; in each group of X+Y the Y cheapest are free.
    private decimal BuyXGetY(Promotion promo, List<OrderItem> items)
    {
        var units = items.Where(i => promo.ProductIds.Count == 0 ? i.IsBeverage : promo.ProductIds.Contains(i.ProductId))
            .SelectMany(i => Enumerable.Repeat(i.UnitPrice, i.Quantity)).OrderByDescending(p => p).ToList();
        int group = promo.BuyQuantity + promo.FreeQuantity;
        decimal free = 0;
        // Only complete groups count: "mua 2 tặng 1" needs 3 eligible units.
        for (int start = 0; start + group <= units.Count; start += group) free += units.Skip(start + promo.BuyQuantity).Take(promo.FreeQuantity).Sum();
        return free;
    }

    // As many complete sets as the cart holds; each set costs ComboPrice instead of its items' prices.
    private decimal Combo(Promotion promo, List<OrderItem> items)
    {
        if (promo.Slots.Count > 0) return SlotCombo(promo, items);
        if (promo.ProductIds.Count == 0) return 0;
        var pools = promo.ProductIds.Distinct().ToDictionary(id => id, id => items.Where(i => i.ProductId == id).SelectMany(i => Enumerable.Repeat(i.UnitPrice, i.Quantity)).OrderByDescending(p => p).ToList());
        int sets = pools.Values.Min(p => p.Count);
        decimal saving = 0;
        for (int s = 0; s < sets; s++) saving += Math.Max(0, pools.Values.Sum(p => p[s]) - promo.ComboPrice);
        return saving;
    }

    // Each set takes one unit per slot; the combo price replaces the items' base prices, so a larger size or
    // toppings are still charged on top. Scarcest slot fills first, with the dearest eligible unit, so a unit
    // that fits two slots is not used up by the wrong one.
    private decimal SlotCombo(Promotion promo, List<OrderItem> items)
    {
        if (promo.Slots.Any(s => s.CategoryIds.Count == 0 && s.ProductIds.Count == 0)) return 0;
        var units = items.SelectMany(i => Enumerable.Repeat(i, i.Quantity))
            .Select(i => (Product: Products.FirstOrDefault(p => p.Id == i.ProductId), Unit: i.UnitPrice))
            .Where(u => u.Product is not null)
            // Listed price: base plus the default size, as on the menu tile.
            .Select(u => (Product: u.Product!, Value: Math.Min(u.Product!.BasePrice + (u.Product.IsBeverage ? u.Product.Sizes.FirstOrDefault(s => s.Name == u.Product.DefaultSize)?.ExtraPrice ?? 0 : 0), u.Unit)))
            .OrderByDescending(u => u.Value).ToList();
        var used = new bool[units.Count];
        decimal saving = 0;
        while (true)
        {
            var picks = new List<int>();
            foreach (var slot in promo.Slots.OrderBy(s => units.Where((u, i) => !used[i] && s.Accepts(u.Product)).Count()))
            {
                // Units are sorted dearest first, so the first free match is the best one.
                int pick = Enumerable.Range(0, units.Count).FirstOrDefault(i => !used[i] && !picks.Contains(i) && slot.Accepts(units[i].Product), -1);
                if (pick < 0) return saving;
                picks.Add(pick);
            }
            foreach (int i in picks) used[i] = true;
            saving += Math.Max(0, picks.Sum(i => units[i].Value) - promo.ComboPrice);
        }
    }

    public void SavePromotion(Promotion promo)
    {
        _auth.RequireManager();
        if (string.IsNullOrWhiteSpace(promo.Name)) throw new InvalidOperationException("Nhập tên chương trình.");
        if (promo.Type == "Giờ vàng" && promo.Percent is <= 0 or >= 100) throw new InvalidOperationException("Mức giảm phải từ 1 đến 99%.");
        if (promo.Type == "Mua X tặng Y" && (promo.BuyQuantity < 1 || promo.FreeQuantity < 1)) throw new InvalidOperationException("Số lượng mua và tặng phải từ 1 trở lên.");
        if (promo.Type == "Combo" && promo.ComboPrice <= 0) throw new InvalidOperationException("Giá combo phải lớn hơn 0.");
        if (promo.Type == "Combo" && promo.Slots.Count == 0 && promo.ProductIds.Distinct().Count() < 2) throw new InvalidOperationException("Combo cần ít nhất 2 suất.");
        if (promo.Type == "Combo" && promo.Slots.Count is > 0 and < 2) throw new InvalidOperationException("Combo cần ít nhất 2 suất.");
        if (promo.Slots.Any(s => s.CategoryIds.Count == 0 && s.ProductIds.Count == 0)) throw new InvalidOperationException("Mỗi suất cần ít nhất một danh mục hoặc món.");
        if (promo.ValidFrom is { } from && promo.ValidTo is { } to && to.Date < from.Date) throw new InvalidOperationException("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        if (promo.Type is not ("Giờ vàng" or "Mua X tặng Y" or "Combo")) throw new InvalidOperationException("Loại khuyến mãi không hợp lệ.");
        if (promo.End <= promo.Start) throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.");
        Mutate(() =>
        {
            int i = Promotions.FindIndex(p => p.Id == promo.Id);
            if (i < 0) Promotions.Add(promo); else Promotions[i] = promo;
        });
    }

    // Refunds take back earned points pro rata; a full refund also gives back the points spent on the order.
    private void RevokeLoyalty(Order order, decimal refunded)
    {
        var customer = Customers.FirstOrDefault(c => c.Phone == order.CustomerPhone);
        if (customer is null || order.TotalAmount <= 0) return;
        int shouldRevoke = (int)Math.Ceiling(order.PointsEarned * order.RefundedAmount / order.TotalAmount);
        int delta = Math.Min(shouldRevoke, order.PointsEarned) - order.PointsRevoked;
        bool full = order.RefundedAmount >= order.TotalAmount;
        customer.Points = Math.Max(0, customer.Points - delta + (full ? order.PointsRedeemed : 0));
        customer.TotalSpent = Math.Max(0, customer.TotalSpent - refunded);
        order.PointsRevoked += delta;
    }

    // Called inside Checkout's Mutate so points and the order are saved together.
    private void ApplyLoyalty(Order order)
    {
        if (string.IsNullOrEmpty(order.CustomerPhone)) return;
        var customer = Customers.Single(c => c.Phone == order.CustomerPhone);
        order.PointsEarned = (int)Math.Floor(order.TotalAmount / SpendPerPoint);
        customer.Points += order.PointsEarned - order.PointsRedeemed;
        customer.TotalSpent += order.TotalAmount;
        customer.VisitCount++;
        customer.LastVisit = order.CreatedAt;
    }
}
