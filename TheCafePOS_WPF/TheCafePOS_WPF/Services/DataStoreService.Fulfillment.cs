using TheCafePOS_WPF.Models;
namespace TheCafePOS_WPF.Services;
public partial class DataStoreService
{
    public DraftOrder? Draft { get; private set; }
    public void SaveDraft(DraftOrder? draft) => Mutate(() => Draft = draft);
    public void SaveWorkspace(IEnumerable<HeldOrderInfo> held, DraftOrder? draft)
    {
        var before = Draft;
        try { Draft = draft; LocalDatabase.Instance.WritePair("store", Snapshot(), "held-orders", held.ToList()); }
        catch { Draft = before; throw; }
    }
    public void UpdateFulfillment(string orderId, string action, string? lineId = null, string reason = "")
    {
        AuthService.Instance.RequireSignedIn();
        var order = CompletedOrders.Single(o => o.Id == orderId);
        if (order.FulfillmentStatus == "Legacy") throw new InvalidOperationException("Đơn lịch sử không theo dõi phục vụ.");
        if (action is "reopen" or "stop")
        {
            AuthService.Instance.RequireManager();
            if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Cần nhập lý do.");
        }
        else if (order.FulfillmentStatus is "Đã giao" or "Dừng phục vụ") throw new InvalidOperationException("Đơn đã kết thúc; cần quản lý mở lại.");
        var line = lineId == null ? null : order.Items.Single(i => i.Id == lineId);
        string Describe() => order.FulfillmentStatus + " | " + string.Join(";", order.Items.Select(i => $"{i.Id}:{i.ReadyQuantity}/{i.DeliveredQuantity}"));
        var before = Describe();
        Mutate(() =>
        {
            switch (action)
            {
                case "start": order.FulfillmentStatus = "Đang làm"; break;
                case "ready" when line != null && line.ReadyQuantity < line.Quantity: line.ReadyQuantity++; break;
                case "deliver" when line != null && line.DeliveredQuantity < line.ReadyQuantity: line.DeliveredQuantity++; break;
                case "undo" when line != null && line.ReadyQuantity > line.DeliveredQuantity: line.ReadyQuantity--; break;
                case "all-ready": foreach (var item in order.Items) item.ReadyQuantity = item.Quantity; break;
                case "all-delivered" when order.Items.All(i => i.ReadyQuantity == i.Quantity): foreach (var item in order.Items) item.DeliveredQuantity = item.Quantity; break;
                case "stop": order.FulfillmentStatus = "Dừng phục vụ"; break;
                case "reopen":
                    if (order.FulfillmentStatus is not ("Đã giao" or "Dừng phục vụ")) throw new InvalidOperationException("Chỉ mở lại đơn đã kết thúc.");
                    foreach (var item in order.Items) item.DeliveredQuantity = 0;
                    order.FulfillmentStatus = "Đang làm"; break;
                default: throw new InvalidOperationException("Thao tác không hợp lệ hoặc chưa làm đủ món.");
            }
            if (action != "stop") order.FulfillmentStatus = order.Items.All(i => i.DeliveredQuantity == i.Quantity) ? "Đã giao"
                : order.Items.All(i => i.ReadyQuantity == i.Quantity) ? "Chờ giao" : "Đang làm";
            order.ServiceEvents.Add(new() { Operator = AuthService.Instance.CurrentUser!.Username, Action = action, Before = before, After = Describe(), Reason = reason.Trim() });
        });
    }
}
