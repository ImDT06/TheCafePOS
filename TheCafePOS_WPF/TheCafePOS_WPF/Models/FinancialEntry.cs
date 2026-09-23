namespace TheCafePOS_WPF.Models;

public class RefundEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; } = "";
    public string ShiftId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash";
    public string Reason { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Approver { get; set; } = "";
}
public class CashEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ShiftId { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string Type { get; set; } = "In";
    public decimal Amount { get; set; }
    public decimal SignedAmount => Type == "In" ? Amount : -Amount;
    public string TypeLabel => Type == "In" ? "Thu vào" : "Chi ra";
    public string Reason { get; set; } = "";
    public string Operator { get; set; } = "";
    public string Approver { get; set; } = "";
}
