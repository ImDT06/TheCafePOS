using System;
using System.Collections.Generic;

namespace TheCafePOS_WPF.Models
{
    public class Order
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int DailyOrderNumber { get; set; }
        public string DailyOrderNumberFormatted => $"#{DailyOrderNumber:D2}";
        public string ShiftId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal RefundedAmount { get; set; }
        public decimal RefundableAmount => TotalAmount - RefundedAmount;
        public string RefundStatus => RefundedAmount == 0 ? "Chưa hoàn" : RefundedAmount == TotalAmount ? "Đã hoàn toàn bộ" : "Đã hoàn một phần";
        public string PaymentMethod { get; set; } = "Cash"; // Cash | VietQR
        public string PaymentStatus { get; set; } = "Completed"; // Pending | Completed | Cancelled
        public decimal CashGiven { get; set; }
        public decimal ChangeReturned { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();

        public string FormattedTotal => $"{TotalAmount:N0}đ";
    }
}
