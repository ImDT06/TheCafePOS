namespace TheCafePOS_WPF.Models;

// A code the customer hands over; unlike promotions it is applied only when entered.
public class Voucher
{
    public const string Percent = "Phần trăm";
    public const string Amount = "Số tiền";

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Code { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = Amount;
    // Percent: 1–100; Amount: đồng off.
    public decimal Value { get; set; }
    // Percent vouchers only; 0 = no cap.
    public decimal MaxDiscount { get; set; }
    public decimal MinSubtotal { get; set; }
    public DateTime ValidFrom { get; set; } = DateTime.Today;
    public DateTime ValidTo { get; set; } = DateTime.Today.AddMonths(1);
    // 0 = unlimited.
    public int UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public bool IsActive { get; set; } = true;

    public string ValueLabel => Type == Percent ? $"{Value:0}%" + (MaxDiscount > 0 ? $" (tối đa {MaxDiscount:N0}đ)" : "") : $"{Value:N0}đ";
    public string UsageLabel => UsageLimit == 0 ? $"{UsedCount} lượt" : $"{UsedCount}/{UsageLimit} lượt";
    public string PeriodLabel => $"{ValidFrom:dd/MM/yyyy} – {ValidTo:dd/MM/yyyy}";
}
