using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

internal static class ReportDashboardRegression
{
    public static void Run()
    {
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        var day = new DateTime(2026, 10, 1);
        var orders = new[] {
            new Order { CreatedAt = day, ShiftId = "a", TotalAmount = 55000 },
            new Order { CreatedAt = day.AddHours(23), ShiftId = "a", TotalAmount = 110000, PaymentMethod = "VietQR" },
            new Order { CreatedAt = day, ShiftId = "b", TotalAmount = 999000 },
            new Order { CreatedAt = day, ShiftId = "a", TotalAmount = 999000, PaymentStatus = "Pending" }
        };
        var refunds = new[] {
            new RefundEntry { CreatedAt = day.AddDays(1), ShiftId = "a", Amount = 55000, Method = "Cash" },
            new RefundEntry { CreatedAt = day.AddDays(1), ShiftId = "b", Amount = 999000 }
        };
        var report = ReportService.Build(orders, day, day.AddDays(1), "a", refunds);
        Check(report.Daily.Count == 2 && report.Daily[0].OrderCount == 2, "Daily aggregation respects shift and completed status");
        Check(report.AverageOrderValue == 82500, "Average order value uses gross completed sales");
        Check(report.Daily[1].OrderCount == 0 && report.Daily[1].NetRevenue == -55000, "Refund-only day retains negative net revenue");
        Check(report.Daily.Sum(d => d.NetRevenue) == report.NetRevenue && report.NetRevenue == 110000, "Daily totals reconcile with summary");
        Check(ReportService.ToDailyCsv(report).Contains("2026-10-02,0,0,55000,-55000"), "Daily CSV preserves signed values and refund date");
        var empty = ReportService.Build(orders, day.AddDays(3), day.AddDays(4), refunds: refunds);
        Check(empty.AverageOrderValue == 0 && empty.Daily.Count == 0, "Empty period has safe average and empty chart");
        var singleDay = ReportService.Build(orders, day, day, "a", refunds);
        Check(singleDay.Revenue == 165000 && singleDay.Refunded == 0, "Date boundaries include late sales and exclude later refunds");
    }
}
