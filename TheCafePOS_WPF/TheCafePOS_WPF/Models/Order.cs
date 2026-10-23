using System;
using System.Collections.Generic;

namespace TheCafePOS_WPF.Models
{
    public class Order
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ServiceType { get; set; } = "Không xác định";
        public string FulfillmentStatus { get; set; } = "Legacy";
        public DateTimeOffset? PaidAt { get; set; }
        public List<ServiceEvent> ServiceEvents { get; set; } = new();
        public string CallLabel => $"#{DailyOrderNumber:D3} · {CreatedAt:dd/MM}";
        public string ProgressLabel => $"Đã làm {Items.Sum(i => i.ReadyQuantity)}/{Items.Sum(i => i.Quantity)} · Đã giao {Items.Sum(i => i.DeliveredQuantity)}/{Items.Sum(i => i.Quantity)}";
        public int DailyOrderNumber { get; set; }
        public string DailyOrderNumberFormatted => $"#{DailyOrderNumber:D2}";
        public string ShiftId { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        // Before discount; 0 on orders saved before discounts existed.
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public string DiscountReason { get; set; } = string.Empty;
        public decimal PromotionAmount { get; set; }
        public string PromotionName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public int PointsRedeemed { get; set; }
        public int PointsEarned { get; set; }
        public int PointsRevoked { get; set; }
        public string Channel { get; set; } = "Tại quầy";
        public decimal ChannelSurcharge { get; set; }
        public string VoucherCode { get; set; } = string.Empty;
        public decimal VoucherAmount { get; set; }
        // VAT on the amount after every reduction; when VatIncluded the tax is already inside TotalAmount.
        public int VatPercent { get; set; }
        public bool VatIncluded { get; set; } = true;
        public decimal VatAmount { get; set; }
        public bool IsCancelled { get; set; }
        public decimal LoyaltyAmount => PointsRedeemed * 1000m;
        public decimal RefundedAmount { get; set; }
        public decimal RefundableAmount => TotalAmount - RefundedAmount;
        public string RefundStatus => RefundedAmount == 0 ? "Chưa hoàn" : RefundedAmount == TotalAmount ? "Đã hoàn toàn bộ" : "Đã hoàn một phần";
        public string PaymentMethod { get; set; } = "Cash"; // Cash | VietQR | Mixed | Platform
        // Mixed only: the part paid by bank transfer; the rest went into the drawer.
        public decimal TransferAmount { get; set; }
        public decimal CashPortion => PaymentMethod switch { "Cash" => TotalAmount, "Mixed" => TotalAmount - TransferAmount, _ => 0 };
        public string PaymentStatus { get; set; } = "Completed"; // Pending | Completed | Cancelled
        public decimal CashGiven { get; set; }
        public decimal ChangeReturned { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();

        public string FormattedTotal => $"{TotalAmount:N0}đ";
    }
}
