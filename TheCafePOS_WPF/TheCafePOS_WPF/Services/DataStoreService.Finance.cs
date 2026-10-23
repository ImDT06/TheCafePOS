using TheCafePOS_WPF.Models;
namespace TheCafePOS_WPF.Services;
public partial class DataStoreService
{
    public static string RefundAction(string id, string orderId, decimal amount, string reason, string reference)
        => $"Hoàn tiền {amount:N0}đ | Đơn {orderId} | {reason.Trim()} | {reference.Trim()} | Phiếu {id}";
    public static string CashAction(string id, string type, decimal amount, string reason)
        => $"{(type == "In" ? "Thu" : "Chi")} tiền mặt {amount:N0}đ | {reason.Trim()} | Phiếu {id}";
    private static void ValidateMoney(decimal amount, string reason)
    {
        if (amount <= 0 || amount != decimal.Truncate(amount)) throw new InvalidOperationException("Số tiền phải là số nguyên dương (đồng).");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 300) throw new InvalidOperationException("Nhập lý do từ 1 đến 300 ký tự.");
    }
    public RefundEntry RefundOrder(string id, string orderId, decimal amount, string reason, string reference)
    {
        RequireOpenShift();
        var previous = Refunds.FirstOrDefault(r => r.Id == id);
        if (previous != null)
        {
            if (previous.OrderId != orderId || previous.Amount != amount || previous.Reason != reason.Trim() || previous.Reference != reference.Trim()) throw new InvalidOperationException("Mã phiếu hoàn đã được dùng cho nội dung khác.");
            return previous;
        }
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Mã phiếu không hợp lệ.");
        ValidateMoney(amount, reason);
        var order = CompletedOrders.FirstOrDefault(o => o.Id == orderId && o.PaymentStatus == "Completed") ?? throw new InvalidOperationException("Không tìm thấy đơn đã thanh toán.");
        if (amount > order.RefundableAmount) throw new InvalidOperationException("Số tiền vượt phần còn được hoàn của đơn.");
        // Mixed orders hand back cash first, up to the cash still unrefunded; the rest goes back by transfer.
        decimal cashBack = order.PaymentMethod switch
        {
            "Cash" => amount,
            "Mixed" => Math.Max(0, Math.Min(amount, order.CashPortion - Refunds.Where(r => r.OrderId == order.Id).Sum(r => r.DrawerAmount))),
            _ => 0
        };
        if (cashBack > ExpectedCash) throw new InvalidOperationException("Tiền dự kiến trong ngăn kéo không đủ. Ghi nhận bổ sung tiền trước khi hoàn.");
        if (order.PaymentMethod is "VietQR" or "Mixed" && amount > cashBack && string.IsNullOrWhiteSpace(reference)) throw new InvalidOperationException("Nhập mã giao dịch chuyển khoản hoàn tiền đã thực hiện.");
        var approver = _auth.RequireApproval(RefundAction(id, orderId, amount, reason, reference));
        var entry = new RefundEntry { Id = id, OrderId = order.Id, ShiftId = CurrentShift.Id, Amount = amount, Method = order.PaymentMethod, CashAmount = order.PaymentMethod == "Mixed" ? cashBack : 0, Reason = reason.Trim(), Reference = reference.Trim(), Operator = _auth.CurrentUser!.Username, Approver = approver };
        Mutate(() => { order.RefundedAmount += amount; Refunds.Add(entry); RevokeLoyalty(order, amount); });
        return entry;
    }
    public static string CancelReason(string reason) => "Hủy đơn: " + reason.Trim();

    // Cancelling = full refund (same approval as a refund) + stop service + put unused stock back.
    // Only orders the bar has not started, otherwise the cups and ingredients are really gone.
    public RefundEntry CancelOrder(string refundId, string orderId, string reason, string reference)
    {
        var order = CompletedOrders.FirstOrDefault(o => o.Id == orderId) ?? throw new InvalidOperationException("Không tìm thấy đơn.");
        if (order.FulfillmentStatus != "Chờ làm") throw new InvalidOperationException("Chỉ hủy được đơn chưa bắt đầu pha chế. Đơn đã làm hãy dùng hoàn tiền.");
        if (order.RefundedAmount > 0) throw new InvalidOperationException("Đơn đã hoàn một phần, không thể hủy.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Nhập lý do hủy đơn.");
        var entry = RefundOrder(refundId, orderId, order.TotalAmount, CancelReason(reason), reference);
        Mutate(() =>
        {
            foreach (var move in StockMovements.Where(m => m.OrderId == order.Id && m.Type == "Bán hàng").ToList())
                if (PackagingInventory.FirstOrDefault(p => p.Id == move.PackagingId) is { } pack)
                {
                    int before = pack.StockQuantity;
                    pack.StockQuantity -= move.Change;
                    RecordStockMovement(pack, before, "Hủy đơn", $"Hủy đơn #{order.DailyOrderNumber:D2}", order.Id);
                }
            string tag = $"Đơn #{order.DailyOrderNumber:D2}";
            foreach (var move in IngredientMovements.Where(m => m.Type == "Bán hàng" && m.Reason.StartsWith(tag + " ") && m.At.Date == order.CreatedAt.Date).ToList())
                if (Ingredients.FirstOrDefault(i => i.Id == move.IngredientId) is { } ing)
                {
                    ing.Stock -= move.Change;
                    IngredientMovements.Add(new IngredientMovement { IngredientId = ing.Id, IngredientName = ing.Name, Type = "Hủy đơn", Change = -move.Change, After = ing.Stock, Reason = tag, Operator = _auth.CurrentUser?.Username ?? "" });
                }
            // The customer did not get the order, so the voucher can be used again.
            if (FindVoucher(order.VoucherCode) is { UsedCount: > 0 } voucher) voucher.UsedCount--;
            order.FulfillmentStatus = "Dừng phục vụ";
            // Payment stays "Completed": the full refund entry already nets the revenue to zero in reports.
            order.IsCancelled = true;
        });
        return entry;
    }
    public CashEntry RecordCash(string id, string type, decimal amount, string reason)
    {
        RequireOpenShift();
        var previous = CashEntries.FirstOrDefault(e => e.Id == id);
        if (previous != null)
        {
            if (previous.Type != type || previous.Amount != amount || previous.Reason != reason.Trim()) throw new InvalidOperationException("Mã phiếu thu/chi đã được dùng cho nội dung khác.");
            return previous;
        }
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Mã phiếu không hợp lệ.");
        ValidateMoney(amount, reason);
        if (type is not ("In" or "Out")) throw new InvalidOperationException("Loại thu/chi không hợp lệ.");
        if (type == "Out" && amount > ExpectedCash) throw new InvalidOperationException("Số tiền chi vượt tiền dự kiến trong ngăn kéo.");
        var approver = _auth.RequireApproval(CashAction(id, type, amount, reason));
        var entry = new CashEntry { Id = id, ShiftId = CurrentShift.Id, Type = type, Amount = amount, Reason = reason.Trim(), Operator = _auth.CurrentUser!.Username, Approver = approver };
        Mutate(() => CashEntries.Add(entry));
        return entry;
    }
}
