using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    public const string PurchaseMovement = "Nhập hàng";
    public const string WasteMovement = "Hao hụt";
    private static readonly string[] PackagingLossTypes = { "Kiểm kê", "Điều chỉnh", WasteMovement };
    private static readonly string[] IngredientLossTypes = { "Kiểm kê", WasteMovement };

    public List<Supplier> Suppliers { get; private set; } = new();
    public List<PurchaseReceipt> PurchaseReceipts { get; private set; } = new();

    public void SaveSupplier(Supplier supplier)
    {
        _auth.RequireManager();
        string name = supplier.Name.Trim();
        if (name.Length == 0) throw new InvalidOperationException("Nhập tên nhà cung cấp.");
        if (Suppliers.Any(s => s.Id != supplier.Id && s.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))) throw new InvalidOperationException("Tên nhà cung cấp đã tồn tại.");
        string phone = supplier.Phone.Trim();
        if (phone.Length > 0) phone = NormalizePhone(phone);
        var saved = new Supplier { Id = supplier.Id, Name = name, Phone = phone, Address = supplier.Address.Trim(), Note = supplier.Note.Trim(), IsActive = supplier.IsActive };
        Mutate(() =>
        {
            int i = Suppliers.FindIndex(s => s.Id == saved.Id);
            if (i < 0) Suppliers.Add(saved); else Suppliers[i] = saved;
        });
    }

    public void DeleteSupplier(string id)
    {
        _auth.RequireManager();
        var supplier = Suppliers.FirstOrDefault(s => s.Id == id) ?? throw new InvalidOperationException("Nhà cung cấp không tồn tại.");
        if (PurchaseReceipts.Any(r => r.SupplierId == id)) throw new InvalidOperationException("Nhà cung cấp đã có phiếu nhập, hãy ngừng giao dịch thay vì xóa.");
        Mutate(() => Suppliers.Remove(supplier));
    }

    /// <summary>
    /// Receives goods: raises stock, remembers the unit cost and, when paid from the drawer, records the cash out.
    /// Retrying with the same id returns the receipt already saved.
    /// </summary>
    public PurchaseReceipt CreatePurchaseReceipt(string id, string supplierId, List<PurchaseLine> lines, string note, bool paidFromDrawer)
    {
        _auth.RequireManager();
        if (PurchaseReceipts.FirstOrDefault(r => r.Id == id) is { } previous) return previous;
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Mã phiếu không hợp lệ.");
        var supplier = Suppliers.FirstOrDefault(s => s.Id == supplierId && s.IsActive) ?? throw new InvalidOperationException("Chọn nhà cung cấp đang giao dịch.");
        if (lines is null || lines.Count == 0) throw new InvalidOperationException("Phiếu nhập cần ít nhất một mặt hàng.");
        if (lines.GroupBy(l => (l.ItemType, l.ItemId)).Any(g => g.Count() > 1)) throw new InvalidOperationException("Mỗi mặt hàng chỉ nhập một dòng trên phiếu.");
        var saved = new List<PurchaseLine>();
        foreach (var line in lines)
        {
            if (line.Quantity <= 0) throw new InvalidOperationException("Số lượng nhập phải lớn hơn 0.");
            if (line.UnitCost < 0 || line.UnitCost != decimal.Truncate(line.UnitCost)) throw new InvalidOperationException("Đơn giá phải là số nguyên không âm (đồng).");
            if (line.ItemType == PurchaseLine.Ingredient)
            {
                var ing = Ingredients.FirstOrDefault(i => i.Id == line.ItemId) ?? throw new InvalidOperationException("Nguyên liệu không tồn tại.");
                saved.Add(new PurchaseLine { ItemType = line.ItemType, ItemId = ing.Id, ItemName = ing.Name, Unit = ing.Unit, Quantity = line.Quantity, UnitCost = line.UnitCost });
            }
            else if (line.ItemType == PurchaseLine.Packaging)
            {
                var pack = PackagingInventory.FirstOrDefault(p => p.Id == line.ItemId) ?? throw new InvalidOperationException("Bao bì không tồn tại.");
                if (line.Quantity != decimal.Truncate(line.Quantity)) throw new InvalidOperationException("Bao bì nhập theo số nguyên.");
                if (pack.StockQuantity + line.Quantity > int.MaxValue) throw new InvalidOperationException("Tồn kho bao bì vượt giới hạn.");
                saved.Add(new PurchaseLine { ItemType = line.ItemType, ItemId = pack.Id, ItemName = pack.Name, Unit = pack.Unit, Quantity = line.Quantity, UnitCost = line.UnitCost });
            }
            else throw new InvalidOperationException("Loại mặt hàng không hợp lệ.");
        }
        decimal total = Math.Round(saved.Sum(l => l.LineTotal), 0, MidpointRounding.AwayFromZero);
        if (paidFromDrawer)
        {
            if (CurrentShift.Status != "Open") throw new InvalidOperationException("Cần có ca đang mở để chi tiền từ ngăn kéo.");
            if (total <= 0) throw new InvalidOperationException("Phiếu không có giá trị, không cần chi tiền.");
            if (total > ExpectedCash) throw new InvalidOperationException("Tiền dự kiến trong ngăn kéo không đủ để thanh toán phiếu nhập.");
        }
        var today = CafeNow.Date;
        var receipt = new PurchaseReceipt
        {
            Id = id, Code = $"PN-{today:yyMMdd}-{PurchaseReceipts.Count(r => r.CreatedAt.Date == today) + 1:D2}", CreatedAt = CafeNow,
            SupplierId = supplier.Id, SupplierName = supplier.Name, Lines = saved, TotalAmount = total, Note = note.Trim(),
            PaidFromDrawer = paidFromDrawer, Operator = _auth.CurrentUser!.Username
        };
        Mutate(() =>
        {
            string reason = $"{receipt.Code} · {supplier.Name}";
            foreach (var line in saved)
            {
                if (line.ItemType == PurchaseLine.Ingredient)
                {
                    var ing = Ingredients.First(i => i.Id == line.ItemId);
                    ing.Stock += line.Quantity;
                    ing.UnitCost = line.UnitCost;
                    Log(ing, PurchaseMovement, line.Quantity, reason);
                }
                else
                {
                    var pack = PackagingInventory.First(p => p.Id == line.ItemId);
                    int before = pack.StockQuantity;
                    pack.StockQuantity += (int)line.Quantity;
                    pack.UnitCost = line.UnitCost;
                    RecordStockMovement(pack, before, PurchaseMovement, reason);
                }
            }
            if (paidFromDrawer)
                CashEntries.Add(new CashEntry { ShiftId = CurrentShift.Id, Type = "Out", Amount = total, Reason = "Thanh toán " + reason, Operator = receipt.Operator, Approver = receipt.Operator });
            PurchaseReceipts.Add(receipt);
            RefreshStockSoldOut();
        });
        return receipt;
    }

    /// <summary>Spoiled, spilled or expired stock taken out with a reason, so it shows up as shrinkage.</summary>
    public void RecordIngredientWaste(string id, decimal quantity, string reason)
    {
        _auth.RequireManager();
        var item = Ingredients.FirstOrDefault(i => i.Id == id) ?? throw new InvalidOperationException("Nguyên liệu không tồn tại.");
        if (quantity <= 0) throw new InvalidOperationException("Số lượng hao hụt phải lớn hơn 0.");
        if (quantity > item.Stock) throw new InvalidOperationException($"Hao hụt vượt tồn hiện có ({item.StockLabel}).");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Nhập lý do hao hụt (hỏng, đổ, hết hạn…).");
        Mutate(() =>
        {
            item.Stock -= quantity;
            Log(item, WasteMovement, -quantity, reason);
            RefreshStockSoldOut();
        });
    }

    /// <summary>Ingredients and packaging at or below their warning threshold, empty ones first.</summary>
    public List<LowStockItem> LowStockItems() =>
        Ingredients.Where(i => i.IsLow).Select(i => new LowStockItem(PurchaseLine.Ingredient, i.Name, i.Stock, i.WarningThreshold, i.Unit))
            .Concat(PackagingInventory.Where(p => p.IsLowStock).Select(p => new LowStockItem(PurchaseLine.Packaging, p.Name, p.StockQuantity, p.WarningThreshold, p.Unit)))
            .OrderBy(i => !i.IsOut).ThenBy(i => i.Threshold == 0 ? 0 : i.Stock / i.Threshold).ThenBy(i => i.Name).ToList();

    /// <summary>Stock lost between two dates (inclusive): negative counts, adjustments and waste, valued at the latest unit cost.</summary>
    public List<ShrinkageRow> ShrinkageReport(DateTime from, DateTime to)
    {
        var rows = IngredientMovements
            .Where(m => m.Change < 0 && IngredientLossTypes.Contains(m.Type) && m.At.Date >= from.Date && m.At.Date <= to.Date)
            .GroupBy(m => m.IngredientId)
            .Select(g =>
            {
                var ing = Ingredients.FirstOrDefault(i => i.Id == g.Key);
                decimal qty = -g.Sum(m => m.Change);
                return new ShrinkageRow(PurchaseLine.Ingredient, ing?.Name ?? g.First().IngredientName, ing?.Unit ?? "", qty, Math.Round(qty * (ing?.UnitCost ?? 0), 0, MidpointRounding.AwayFromZero), g.Count());
            })
            .Concat(StockMovements
                .Where(m => m.Change < 0 && PackagingLossTypes.Contains(m.Type) && m.CreatedAt.Date >= from.Date && m.CreatedAt.Date <= to.Date)
                .GroupBy(m => m.PackagingId)
                .Select(g =>
                {
                    var pack = PackagingInventory.FirstOrDefault(p => p.Id == g.Key);
                    decimal qty = -g.Sum(m => m.Change);
                    return new ShrinkageRow(PurchaseLine.Packaging, pack?.Name ?? g.First().PackagingName, pack?.Unit ?? "Cái", qty, qty * (pack?.UnitCost ?? 0), g.Count());
                }));
        return rows.OrderByDescending(r => r.Value).ThenByDescending(r => r.Quantity).ToList();
    }
}
