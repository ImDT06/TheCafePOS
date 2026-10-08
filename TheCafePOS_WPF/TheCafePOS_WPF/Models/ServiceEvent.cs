namespace TheCafePOS_WPF.Models;
public class ServiceEvent
{
    public DateTimeOffset At { get; set; } = DateTimeOffset.Now;
    public string Operator { get; set; } = "";
    public string Action { get; set; } = "";
    public string Before { get; set; } = "";
    public string After { get; set; } = "";
    public string Reason { get; set; } = "";
}
public class DraftOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ServiceType { get; set; } = "Mang đi";
    public List<OrderItem> Items { get; set; } = new();
}
