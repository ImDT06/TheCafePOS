using System.Text;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

/// <summary>
/// The customer's receipt as fixed-width text lines, sized for 80 mm thermal paper (42 columns).
/// Pure formatting, so the same text is printed, saved and checked by tests.
/// </summary>
public static class ReceiptService
{
    public const int Width = 42;

    public static string PaymentLabel(Order order) => order.PaymentMethod switch
    {
        "Cash" => "Tiền mặt",
        "VietQR" => "Chuyển khoản VietQR",
        "Mixed" => "Tiền mặt + chuyển khoản",
        _ => "Qua " + order.Channel
    };

    public static IReadOnlyList<string> Lines(Order order, StoreSettings shop, Customer? member = null, bool reprint = false)
    {
        var lines = new List<string>();
        void Center(string text) { foreach (var part in Wrap(text, Width)) lines.Add(part.PadLeft((Width + part.Length) / 2)); }
        void Row(string left, string right) => lines.Add(Fit(left, Width - right.Length - 1) + " " + right);
        void Rule(char c = '-') => lines.Add(new string(c, Width));
        static string Money(decimal value) => $"{value:N0}";

        Center(shop.DisplayName.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(shop.Address)) Center(shop.Address.Trim());
        if (!string.IsNullOrWhiteSpace(shop.Phone)) Center("ĐT: " + shop.Phone.Trim());
        Rule('=');
        Center(reprint ? "HÓA ĐƠN BÁN HÀNG (IN LẠI)" : "HÓA ĐƠN BÁN HÀNG");
        Row($"Số: #{order.DailyOrderNumber:D2}", $"{order.CreatedAt:dd/MM/yyyy HH:mm}");
        Row("Loại đơn:", order.Channel == StoreSettings.Counter ? order.ServiceType : $"{order.Channel} · {order.ServiceType}");
        Rule();
        foreach (var item in order.Items)
        {
            foreach (var part in Wrap(item.ProductName + (item.IsBeverage && !string.IsNullOrWhiteSpace(item.Size) ? $" ({item.Size})" : ""), Width)) lines.Add(part);
            var options = new[] { item.SugarChoice, item.IceChoice }.Where(s => !string.IsNullOrWhiteSpace(s)).Concat(item.Toppings.Select(t => "+" + t)).ToList();
            if (options.Count > 0) foreach (var part in Wrap(string.Join(", ", options), Width - 2)) lines.Add("  " + part);
            Row($"  {item.Quantity} x {Money(item.UnitPrice)}", Money(item.TotalPrice));
        }
        Rule();
        decimal subtotal = order.SubtotalAmount > 0 ? order.SubtotalAmount : order.Items.Sum(i => i.TotalPrice);
        Row("Tạm tính", Money(subtotal));
        if (order.ChannelSurcharge > 0) Row($"Giá kênh {order.Channel}", "+" + Money(order.ChannelSurcharge));
        if (order.PromotionAmount > 0) Row("KM " + order.PromotionName, "-" + Money(order.PromotionAmount));
        if (order.VoucherAmount > 0) Row("Voucher " + order.VoucherCode, "-" + Money(order.VoucherAmount));
        if (order.LoyaltyAmount > 0) Row($"Đổi {order.PointsRedeemed} điểm", "-" + Money(order.LoyaltyAmount));
        if (order.DiscountAmount > 0) Row("Giảm giá", "-" + Money(order.DiscountAmount));
        if (order.VatPercent > 0)
            Row(order.VatIncluded ? $"Đã gồm VAT {order.VatPercent}%" : $"Thuế VAT {order.VatPercent}%", (order.VatIncluded ? "" : "+") + Money(order.VatAmount));
        Rule('=');
        Row("TỔNG CỘNG", Money(order.TotalAmount) + "đ");
        Row("Thanh toán", PaymentLabel(order));
        if (order.PaymentMethod == "Mixed")
        {
            Row("  Chuyển khoản", Money(order.TransferAmount));
            Row("  Tiền mặt", Money(order.CashPortion));
        }
        if (order.PaymentMethod is "Cash" or "Mixed")
        {
            Row("  Khách đưa", Money(order.CashGiven));
            Row("  Tiền thối", Money(order.ChangeReturned));
        }
        if (order.RefundedAmount > 0) Row("Đã hoàn tiền", "-" + Money(order.RefundedAmount));
        if (member is not null || order.PointsEarned > 0)
        {
            Rule();
            // A long name is shortened, never the phone the customer uses to check the receipt is theirs.
            if (member is not null) Row($"Thành viên: {member.Name}", $"({MaskPhone(member.Phone)})");
            Row("Điểm cộng đơn này", $"+{order.PointsEarned}");
            if (member is not null) Row("Điểm hiện có", member.Points.ToString());
        }
        Rule();
        if (!string.IsNullOrWhiteSpace(shop.ReceiptFooter)) Center(shop.ReceiptFooter.Trim());
        Center("Mã đơn: " + order.Id[..Math.Min(8, order.Id.Length)].ToUpperInvariant());
        return lines;
    }

    public static string Text(Order order, StoreSettings shop, Customer? member = null, bool reprint = false) =>
        string.Join(Environment.NewLine, Lines(order, shop, member, reprint));

    // Only the last 3 digits stay readable on paper the customer may leave behind.
    public static string MaskPhone(string phone) => phone.Length <= 3 ? phone : new string('*', phone.Length - 3) + phone[^3..];

    private static string Fit(string text, int width) => width < 2 ? "" : text.Length <= width ? text.PadRight(width) : text[..(width - 1)] + "…";

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > width) { if (line.Length > 0) { yield return line.ToString(); line.Clear(); } yield return w[..width]; w = w[width..]; }
            if (line.Length > 0 && line.Length + 1 + w.Length > width) { yield return line.ToString(); line.Clear(); }
            if (line.Length > 0) line.Append(' ');
            line.Append(w);
        }
        if (line.Length > 0) yield return line.ToString();
    }
}
