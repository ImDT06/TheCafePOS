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
        return new(orders, products, orders.Sum(o => o.TotalAmount), orders.Where(o => o.PaymentMethod == "Cash").Sum(o => o.TotalAmount), orders.Where(o => o.PaymentMethod == "VietQR").Sum(o => o.TotalAmount))
        {
            Refunds = (refunds ?? Array.Empty<RefundEntry>()).Where(r => r.CreatedAt.Date >= from.Date && r.CreatedAt.Date <= to.Date && (shiftId is null || r.ShiftId == shiftId)).OrderByDescending(r => r.CreatedAt).ToList(),
            CashEntries = (cashEntries ?? Array.Empty<CashEntry>()).Where(r => r.CreatedAt.Date >= from.Date && r.CreatedAt.Date <= to.Date && (shiftId is null || r.ShiftId == shiftId)).OrderByDescending(r => r.CreatedAt).ToList()
        };
    }
    private static string Cell(string value)
    {
        if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@')) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
    public static string ToDailyCsv(SalesReport report) => "Ngày,Số đơn,Doanh thu bán hàng,Hoàn tiền,Doanh thu thuần\r\n" +
        string.Join("\r\n", report.Daily.Select(d => string.Join(",", d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            d.OrderCount.ToString(CultureInfo.InvariantCulture), d.Revenue.ToString(CultureInfo.InvariantCulture),
            d.Refunded.ToString(CultureInfo.InvariantCulture), d.NetRevenue.ToString(CultureInfo.InvariantCulture))));
    public static string ToCsv(SalesReport report)
    {
        string Row(string type, string id, DateTime at, string method, decimal amount, string shift, string orderId, string reason, string actor, string approver, string reference = "") => string.Join(",", Cell(type), Cell(id), Cell(at.ToString("yyyy-MM-dd HH:mm:ss")), Cell(method), amount.ToString(CultureInfo.InvariantCulture), Cell(shift), Cell(orderId), Cell(reason), Cell(actor), Cell(approver), Cell(reference));
        var rows = report.Orders.Select(o => Row("Bán hàng", o.Id, o.CreatedAt, o.PaymentMethod, o.TotalAmount, o.ShiftId, o.Id, "", "", ""))
            .Concat(report.Refunds.Select(r => Row("Hoàn tiền", r.Id, r.CreatedAt, r.Method, -r.Amount, r.ShiftId, r.OrderId, r.Reason, r.Operator, r.Approver, r.Reference)))
            .Concat(report.CashEntries.Select(e => Row(e.TypeLabel, e.Id, e.CreatedAt, "Cash", e.SignedAmount, e.ShiftId, "", e.Reason, e.Operator, e.Approver)));
        return "Loại,Mã phiếu,Ngày,Phương thức,Số tiền có dấu,Ca,Mã đơn,Lý do,Nhân viên,Người duyệt,Mã giao dịch ngoài\r\n" + string.Join("\r\n", rows);
    }
}
