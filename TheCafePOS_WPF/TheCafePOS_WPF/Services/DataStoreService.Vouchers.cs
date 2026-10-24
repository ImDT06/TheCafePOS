using System.Text.RegularExpressions;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    public List<Voucher> Vouchers { get; private set; } = new();

    public static string NormalizeVoucherCode(string code) => (code ?? "").Trim().ToUpperInvariant();

    public Voucher? FindVoucher(string code)
    {
        string c = NormalizeVoucherCode(code);
        return c.Length == 0 ? null : Vouchers.FirstOrDefault(v => v.Code == c);
    }

    // Applied to the amount left after automatic promotions; rounded down to 1.000đ like promotions.
    public static decimal VoucherDiscount(Voucher voucher, decimal baseAmount)
    {
        if (baseAmount <= 0) return 0;
        decimal amount = voucher.Type == Voucher.Percent
            ? Math.Floor(baseAmount * voucher.Value / 100m / 1000m) * 1000m
            : voucher.Value;
        if (voucher.Type == Voucher.Percent && voucher.MaxDiscount > 0) amount = Math.Min(amount, voucher.MaxDiscount);
        return Math.Min(amount, baseAmount);
    }

    /// <summary>Discount the code gives a cart at the given time; throws with the reason the cashier reads out.</summary>
    public decimal EvaluateVoucher(string code, decimal subtotal, decimal promotionAmount, DateTime at)
    {
        var voucher = FindVoucher(code) ?? throw new InvalidOperationException("Mã voucher không tồn tại.");
        if (!voucher.IsActive) throw new InvalidOperationException("Voucher đã bị tắt.");
        if (at.Date < voucher.ValidFrom.Date) throw new InvalidOperationException($"Voucher có hiệu lực từ {voucher.ValidFrom:dd/MM/yyyy}.");
        if (at.Date > voucher.ValidTo.Date) throw new InvalidOperationException($"Voucher đã hết hạn ngày {voucher.ValidTo:dd/MM/yyyy}.");
        if (voucher.UsageLimit > 0 && voucher.UsedCount >= voucher.UsageLimit) throw new InvalidOperationException("Voucher đã hết lượt sử dụng.");
        if (subtotal < voucher.MinSubtotal) throw new InvalidOperationException($"Đơn cần tạm tính từ {voucher.MinSubtotal:N0}đ để dùng voucher này.");
        return VoucherDiscount(voucher, subtotal - promotionAmount);
    }

    public void SaveVoucher(Voucher voucher)
    {
        _auth.RequireManager();
        string code = NormalizeVoucherCode(voucher.Code);
        if (!Regex.IsMatch(code, "^[A-Z0-9_-]{3,20}$")) throw new InvalidOperationException("Mã voucher gồm 3–20 ký tự chữ, số, gạch ngang hoặc gạch dưới.");
        if (Vouchers.Any(v => v.Id != voucher.Id && v.Code == code)) throw new InvalidOperationException("Mã voucher đã tồn tại.");
        if (voucher.Type is not (Voucher.Percent or Voucher.Amount)) throw new InvalidOperationException("Loại voucher không hợp lệ.");
        if (voucher.Value != decimal.Truncate(voucher.Value) || voucher.MaxDiscount != decimal.Truncate(voucher.MaxDiscount) || voucher.MinSubtotal != decimal.Truncate(voucher.MinSubtotal))
            throw new InvalidOperationException("Giá trị voucher phải là số nguyên.");
        if (voucher.Type == Voucher.Percent && voucher.Value is < 1 or > 100) throw new InvalidOperationException("Voucher phần trăm phải từ 1 đến 100%.");
        if (voucher.Type == Voucher.Amount && voucher.Value < 1000) throw new InvalidOperationException("Voucher số tiền phải từ 1.000đ trở lên.");
        if (voucher.MaxDiscount < 0 || voucher.MinSubtotal < 0) throw new InvalidOperationException("Mức tối đa và tạm tính tối thiểu không được âm.");
        if (voucher.UsageLimit < 0) throw new InvalidOperationException("Số lượt dùng không được âm.");
        if (voucher.ValidTo.Date < voucher.ValidFrom.Date) throw new InvalidOperationException("Ngày hết hạn phải từ ngày bắt đầu trở đi.");
        var existing = Vouchers.FirstOrDefault(v => v.Id == voucher.Id);
        // Usage is counted by checkout only, so editing a voucher never resets it.
        var saved = new Voucher
        {
            Id = voucher.Id, Code = code, Description = voucher.Description.Trim(), Type = voucher.Type, Value = voucher.Value,
            MaxDiscount = voucher.Type == Voucher.Percent ? voucher.MaxDiscount : 0, MinSubtotal = voucher.MinSubtotal,
            ValidFrom = voucher.ValidFrom.Date, ValidTo = voucher.ValidTo.Date, UsageLimit = voucher.UsageLimit,
            UsedCount = existing?.UsedCount ?? 0, IsActive = voucher.IsActive
        };
        if (saved.UsageLimit > 0 && saved.UsageLimit < saved.UsedCount) throw new InvalidOperationException($"Voucher đã dùng {saved.UsedCount} lượt, giới hạn không thể nhỏ hơn.");
        Mutate(() => { if (existing is null) Vouchers.Add(saved); else Vouchers[Vouchers.IndexOf(existing)] = saved; });
    }

    public void DeleteVoucher(string id)
    {
        _auth.RequireManager();
        var voucher = Vouchers.FirstOrDefault(v => v.Id == id) ?? throw new InvalidOperationException("Voucher không tồn tại.");
        // Used codes stay on their orders; turning them off keeps the history readable.
        if (voucher.UsedCount > 0) throw new InvalidOperationException("Voucher đã được dùng, hãy tắt thay vì xóa.");
        Mutate(() => Vouchers.Remove(voucher));
    }

    /// <summary>VAT in whole đồng (half away from zero) on the amount after every reduction.</summary>
    public static decimal VatFor(decimal net, int percent, bool included)
    {
        if (percent <= 0 || net <= 0) return 0;
        return included
            ? Math.Round(net * percent / (100m + percent), 0, MidpointRounding.AwayFromZero)
            : Math.Round(net * percent / 100m, 0, MidpointRounding.AwayFromZero);
    }
}
