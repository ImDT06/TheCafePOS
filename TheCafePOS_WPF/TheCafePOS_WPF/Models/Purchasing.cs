namespace TheCafePOS_WPF.Models;

public class Supplier
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Note { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class PurchaseLine
{
    public const string Ingredient = "Nguyên liệu";
    public const string Packaging = "Bao bì";

    public string ItemType { get; set; } = Ingredient;
    public string ItemId { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal => Quantity * UnitCost;
}

// Goods received from a supplier: raises stock and records what was paid.
public class PurchaseReceipt
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Code { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string SupplierId { get; set; } = "";
    public string SupplierName { get; set; } = "";
    public List<PurchaseLine> Lines { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Note { get; set; } = "";
    public bool PaidFromDrawer { get; set; }
    public string Operator { get; set; } = "";
    public string ItemsLabel => string.Join(", ", Lines.Select(l => $"{l.ItemName} {l.Quantity:0.##}{l.Unit}"));
}

public record LowStockItem(string Kind, string Name, decimal Stock, decimal Threshold, string Unit)
{
    public string StockLabel => $"{Stock:0.##} {Unit}";
    public bool IsOut => Stock <= 0;
}

// Stock lost outside sales: negative stock counts and recorded waste.
public record ShrinkageRow(string Kind, string Name, string Unit, decimal Quantity, decimal Value, int Events)
{
    public string QuantityLabel => $"{Quantity:0.##} {Unit}";
}
