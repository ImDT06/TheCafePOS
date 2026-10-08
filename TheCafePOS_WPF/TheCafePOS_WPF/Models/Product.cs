using System;

namespace TheCafePOS_WPF.Models
{
    public class Product
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#3498DB";
        public bool IsActive { get; set; } = true;
        public bool IsTopping { get; set; }
        public bool IsRetailItem { get; set; }
        public bool IsBeverage => !IsTopping && !IsRetailItem;
        public string DefaultSize { get; set; } = "M";
        public bool SoldSeparately { get; set; } = true;
        public bool AllowSugar { get; set; } = true;
        public bool AllowIce { get; set; } = true;
        public int DefaultSugar { get; set; } = 100;
        public int DefaultIce { get; set; } = 100;
        // Null retains legacy percentage choices; empty means no source option.
        public List<string>? SugarChoices { get; set; }
        public List<string>? IceChoices { get; set; }
        public string DefaultSugarChoice { get; set; } = "";
        public string DefaultIceChoice { get; set; } = "";
        public List<string> AllowedToppingIds { get; set; } = new();
        public List<ProductSize> Sizes { get; set; } = new()
        {
            new() { Name = "M", PackagingId = "pack-1" },
            new() { Name = "L", ExtraPrice = 5000, PackagingId = "pack-2" },
            new() { Name = "XL", ExtraPrice = 10000, PackagingId = "pack-xl" }
        };

        public string FormattedPrice => $"{BasePrice + (IsBeverage ? Sizes.FirstOrDefault(s => s.Name == DefaultSize)?.ExtraPrice ?? 0 : 0):N0}đ";
    }
}

namespace TheCafePOS_WPF.Models
{
    public class ProductSize
    {
        public string Name { get; set; } = "M";
        public decimal ExtraPrice { get; set; }
        public string PackagingId { get; set; } = "pack-1";
    }
    public class SelectedTopping
    {
        public string ProductId { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; } = 1;
    }
}
