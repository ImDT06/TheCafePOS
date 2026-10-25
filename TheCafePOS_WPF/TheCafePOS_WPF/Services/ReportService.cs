using System.Globalization;
using TheCafePOS_WPF.Models;
namespace TheCafePOS_WPF.Services;
public record ProductSales(string ProductName, int Quantity, decimal Revenue);
public record DailySales(DateTime Date, int OrderCount, decimal Revenue, decimal Refunded)
{
    public decimal NetRevenue => Revenue - Refunded;
}
public record SalesReport(List<Order> Orders, List<ProductSales> Products, decimal Revenue, decimal Cash, decimal Transfer)
{
    public List<RefundEntry> Refunds { get; init; } = new();
    public List<CashEntry> CashEntries { get; init; } = new();
    public decimal Refunded => Refunds.Sum(r => r.Amount);
    public decimal NetRevenue => Revenue - Refunded;
    public decimal CashRefunded => Refunds.Where(r => r.Method == "Cash").Sum(r => r.Amount);
    public decimal TransferRefunded => Refunds.Where(r => r.Method == "VietQR").Sum(r => r.Amount);
    public decimal CashIn => CashEntries.Where(e => e.Type == "In").Sum(e => e.Amount);
    public decimal CashOut => CashEntries.Where(e => e.Type == "Out").Sum(e => e.Amount);
    public decimal AverageOrderValue => Orders.Count == 0 ? 0 : Revenue / Orders.Count;
    public List<DailySales> Daily
    {
        get
        {
            var sales = Orders.GroupBy(o => o.CreatedAt.Date).ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(o => o.TotalAmount)));
            var refunds = Refunds.GroupBy(r => r.CreatedAt.Date).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
            return sales.Keys.Union(refunds.Keys).OrderBy(d => d).Select(d =>
                new DailySales(d, sales.GetValueOrDefault(d).Count, sales.GetValueOrDefault(d).Total, refunds.GetValueOrDefault(d))).ToList();
        }
    }
}
public static class ReportService
{
    public static SalesReport Build(IEnumerable<Order> source, DateTime from, DateTime to, string? shiftId = null, IEnumerable<RefundEntry>? refunds = null, IEnumerable<CashEntry>? cashEntries = null)
    {
        if (from.Date > to.Date) throw new InvalidOperationException("Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        var orders = source.Where(o => o.PaymentStatus == "Completed" && o.CreatedAt.Date >= from.Date && o.CreatedAt.Date <= to.Date && (shiftId is null || o.ShiftId == shiftId)).OrderByDescending(o => o.CreatedAt).ToList();
        var products = orders.SelectMany(o => o.Items).GroupBy(i => new { i.ProductId, i.ProductName }).Select(g => new ProductSales(g.Key.ProductName, g.Sum(i => i.Quantity), g.Sum(i => i.TotalPrice))).OrderByDescending(p => p.Revenue).ToList();
        return new(orders, products, orders.Sum(o => o.TotalAmount), orders.Sum(o => o.CashPortion), orders.Sum(o => o.PaymentMethod == "VietQR" ? o.TotalAmount : o.TransferAmount))
        {
            Refunds = (refunds ?? Array.Empty<RefundEntry>()).Where(r => r.CreatedAt.Date >= from.Date && r.CreatedAt.Date <= to.Date && (shiftId is null || r.ShiftId == shiftId)).OrderByDescending(r => r.CreatedAt).ToList(),
            CashEntries = (cashEntries ?? Array.Empty<CashEntry>()).Where(r => r.CreatedAt.Date >= from.Date && r.CreatedAt.Date <= to.Date && (shiftId is null || r.ShiftId == shiftId)).OrderByDescending(r => r.CreatedAt).ToList()
        };
    }
    /// <summary>Sheets of the Excel export: summary, daily totals, orders, lines, products, refunds and cash entries.</summary>
    public static List<(string Name, IReadOnlyList<string> Header, IEnumerable<object?[]> Rows)> ToWorkbook(SalesReport report, string storeName, DateTime? from, DateTime? to)
    {
        static string Pay(string m) => m switch { "Cash" => "Tiền mặt", "VietQR" => "Chuyển khoản", "Platform" => "Nền tảng", "Mixed" => "Kết hợp", _ => m };
        return new()
        {
            ("Tổng quan", new[] { "Chỉ số", "Giá trị" }, new object?[][]
            {
                new object?[] { "Cửa hàng", storeName },
                new object?[] { "Từ ngày", from }, new object?[] { "Đến ngày", to },
                new object?[] { "Số đơn", report.Orders.Count }, new object?[] { "Doanh thu bán hàng", report.Revenue },
                new object?[] { "Hoàn tiền", report.Refunded }, new object?[] { "Doanh thu thuần", report.NetRevenue },
                new object?[] { "Tiền mặt", report.Cash }, new object?[] { "Chuyển khoản", report.Transfer },
                new object?[] { "Giảm giá thủ công", report.Orders.Sum(o => o.DiscountAmount) }, new object?[] { "Khuyến mãi", report.Orders.Sum(o => o.PromotionAmount) },
                new object?[] { "Đổi điểm", report.Orders.Sum(o => o.LoyaltyAmount) }, new object?[] { "Voucher", report.Orders.Sum(o => o.VoucherAmount) },
                new object?[] { "Thuế VAT", report.Orders.Sum(o => o.VatAmount) }, new object?[] { "Giá trị TB/đơn", Math.Round(report.AverageOrderValue) }
            }),
            ("Theo ngày", new[] { "Ngày", "Số đơn", "Doanh thu bán hàng", "Hoàn tiền", "Doanh thu thuần" },
                report.Daily.Select(d => new object?[] { d.Date, d.OrderCount, d.Revenue, d.Refunded, d.NetRevenue })),
            ("Đơn hàng", new[] { "Mã đơn", "Thời gian", "Kênh", "Loại đơn", "Thanh toán", "Tạm tính", "Phụ thu kênh", "Khuyến mãi", "Đổi điểm", "Giảm giá", "Voucher", "Thuế VAT", "Thực thu", "Đã hoàn", "Khách", "Trạng thái", "Ca", "Mã hệ thống" },
                report.Orders.Select(o => new object?[] { o.DailyOrderNumberFormatted, o.CreatedAt, o.Channel, o.ServiceType, Pay(o.PaymentMethod), o.SubtotalAmount, o.ChannelSurcharge, o.PromotionAmount, o.LoyaltyAmount, o.DiscountAmount, o.VoucherAmount, o.VatAmount, o.TotalAmount, o.RefundedAmount, o.CustomerPhone, o.IsCancelled ? "Đã hủy" : o.RefundStatus, o.ShiftId, o.Id })),
            ("Chi tiết món", new[] { "Mã đơn", "Món", "Size", "Số lượng", "Thành tiền" },
                report.Orders.SelectMany(o => o.Items.Select(i => new object?[] { o.DailyOrderNumberFormatted, i.ProductName, i.Size, i.Quantity, i.TotalPrice }))),
            ("Món bán", new[] { "Món", "Số lượng", "Doanh thu" }, report.Products.Select(p => new object?[] { p.ProductName, p.Quantity, p.Revenue })),
            ("Hoàn tiền", new[] { "Thời gian", "Mã phiếu", "Mã đơn", "Số tiền", "Phương thức", "Lý do", "Mã GD", "Nhân viên", "Người duyệt", "Ca" },
                report.Refunds.Select(r => new object?[] { r.CreatedAt, r.Id, r.OrderId, r.Amount, Pay(r.Method), r.Reason, r.Reference, r.Operator, r.Approver, r.ShiftId })),
            ("Thu chi", new[] { "Thời gian", "Loại", "Số tiền", "Lý do", "Nhân viên", "Người duyệt", "Ca" },
                report.CashEntries.Select(c => new object?[] { c.CreatedAt, c.Type == "In" ? "Thu" : "Chi", c.Amount, c.Reason, c.Operator, c.Approver, c.ShiftId }))
        };
    }
}
