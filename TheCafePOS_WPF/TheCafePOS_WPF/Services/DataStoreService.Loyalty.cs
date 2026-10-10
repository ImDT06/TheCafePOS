using System.Text.RegularExpressions;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    public const decimal SpendPerPoint = 10_000;   // 1 điểm cho mỗi 10.000đ thực thu
    public const decimal PointValue = 1_000;       // 1 điểm đổi được 1.000đ
    public List<Customer> Customers { get; private set; } = new();
    public List<Promotion> Promotions { get; private set; } = new();

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
        AuthService.Instance.RequireSignedIn();
        var p = NormalizePhone(phone);
        if (Customers.Any(c => c.Phone == p)) throw new InvalidOperationException("Số điện thoại đã là thành viên.");
        var customer = new Customer { Phone = p, Name = string.IsNullOrWhiteSpace(name) ? "Khách " + p[^4..] : name.Trim() };
        Mutate(() => Customers.Add(customer));
        return customer;
    }

    public Promotion? ActivePromotion(DateTime at) => Promotions.FirstOrDefault(p => p.AppliesAt(at));
    // Rounded down to 1.000đ so the cashier never handles odd change.
    public static decimal PromotionDiscount(Promotion? promo, decimal subtotal) =>
        promo is null ? 0 : Math.Floor(subtotal * promo.Percent / 100m / 1000m) * 1000m;

    public void SavePromotion(Promotion promo)
    {
        AuthService.Instance.RequireManager();
        if (string.IsNullOrWhiteSpace(promo.Name)) throw new InvalidOperationException("Nhập tên chương trình.");
        if (promo.Percent is <= 0 or >= 100) throw new InvalidOperationException("Mức giảm phải từ 1 đến 99%.");
        if (promo.End <= promo.Start) throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.");
        Mutate(() =>
        {
            int i = Promotions.FindIndex(p => p.Id == promo.Id);
            if (i < 0) Promotions.Add(promo); else Promotions[i] = promo;
        });
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
