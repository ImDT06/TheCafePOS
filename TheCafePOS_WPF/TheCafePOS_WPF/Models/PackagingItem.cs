namespace TheCafePOS_WPF.Models
{
    public class PackagingItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public string Unit { get; set; } = "Cái";
        public int WarningThreshold { get; set; } = 50;
        public decimal UnitCost { get; set; }

        public bool IsLowStock => StockQuantity <= WarningThreshold;
    }
}
