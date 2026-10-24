namespace TheCafePOS_WPF.Models;

public class Ingredient
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "g";
    public decimal Stock { get; set; }
    public decimal WarningThreshold { get; set; }
    // Cost of one unit from the latest purchase receipt; values stock losses.
    public decimal UnitCost { get; set; }
    public bool IsLow => Stock <= WarningThreshold;
    public string StockLabel => $"{Stock:0.##} {Unit}";
}

// Amount of one ingredient for a size-M serving (or one unit of a non-drink / topping).
public class RecipeLine
{
    public string IngredientId { get; set; } = "";
    public decimal Amount { get; set; }
}

public class IngredientMovement
{
    public DateTime At { get; set; } = DateTime.Now;
    public string IngredientId { get; set; } = "";
    public string IngredientName { get; set; } = "";
    public string Type { get; set; } = "";   // Bán hàng | Nhập kho | Nhập hàng | Kiểm kê | Hao hụt | Hủy đơn
    public decimal Change { get; set; }
    public decimal After { get; set; }
    public string Reason { get; set; } = "";
    public string Operator { get; set; } = "";
}
