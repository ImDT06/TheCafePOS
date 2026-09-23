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
        if (order.PaymentMethod == "Cash" && amount > ExpectedCash) throw new InvalidOperationException("Tiền dự kiến trong ngăn kéo không đủ. Ghi nhận bổ sung tiền trước khi hoàn.");
        if (order.PaymentMethod == "VietQR" && string.IsNullOrWhiteSpace(reference)) throw new InvalidOperationException("Nhập mã giao dịch chuyển khoản hoàn tiền đã thực hiện.");
        var approver = AuthService.Instance.RequireApproval(RefundAction(id, orderId, amount, reason, reference));
        var entry = new RefundEntry { Id = id, OrderId = order.Id, ShiftId = CurrentShift.Id, Amount = amount, Method = order.PaymentMethod, Reason = reason.Trim(), Reference = reference.Trim(), Operator = AuthService.Instance.CurrentUser!.Username, Approver = approver };
        Mutate(() => { order.RefundedAmount += amount; Refunds.Add(entry); });
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
        var approver = AuthService.Instance.RequireApproval(CashAction(id, type, amount, reason));
        var entry = new CashEntry { Id = id, ShiftId = CurrentShift.Id, Type = type, Amount = amount, Reason = reason.Trim(), Operator = AuthService.Instance.CurrentUser!.Username, Approver = approver };
        Mutate(() => CashEntries.Add(entry));
        return entry;
    }
}
