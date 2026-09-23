using System;

namespace TheCafePOS_WPF.Models
{
    public class Shift
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CashierName { get; set; } = "Thu Ngân 01";
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime? EndTime { get; set; }
        public decimal InitialCash { get; set; } = 500000;
        public decimal ExpectedCash { get; set; }
        public decimal ActualCash { get; set; }
        public decimal Discrepancy => ActualCash - ExpectedCash;
        public string Status { get; set; } = "Open"; // Open, Closed
    }
}
