namespace TheCafePOS_WPF.Models;
public class StockMovement
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string PackagingId { get; set; } = "";
    public string PackagingName { get; set; } = "";
    public string Type { get; set; } = "";
    public int Before { get; set; }
    public int After { get; set; }
    public int Change => After - Before;
    public string Reason { get; set; } = "";
    public string Operator { get; set; } = "";
    public string OrderId { get; set; } = "";
}
