using System.Collections.Generic;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.ViewModels
{
    /// <summary>A product as shown on the POS menu grid.</summary>
    public sealed class MenuTile
    {
        public MenuTile(Product product)
        {
            Product = product;
            var meta = new List<string>();
            if (product.OriginalPrice > product.BasePrice) meta.Add($"{product.OriginalPrice:N0}đ");
            if (product.IsBeverage && product.Sizes.Count > 1) meta.Add($"Size {product.DefaultSize}");
            MetaText = string.Join("  ", meta);
            // Strike through only when the line is just the old price.
            MetaStrikethrough = product.OriginalPrice > product.BasePrice && meta.Count == 1;
        }

        public Product Product { get; }
        public string Name => Product.Name;
        public string ImageUrl => Product.ImageUrl;
        public string FormattedPrice => Product.FormattedPrice;
        public bool IsSoldOut => Product.IsSoldOut;
        public string MetaText { get; }
        public bool HasMeta => MetaText.Length > 0;
        public bool MetaStrikethrough { get; }
        public string SoldOutMenuText => IsSoldOut ? "Báo có hàng lại" : "Đánh dấu hết hàng";
        public string AutomationName => IsSoldOut ? Name + " hết hàng" : Name + " " + FormattedPrice;
    }

    /// <summary>An entry of the category rail beside the menu grid.</summary>
    public sealed class CategoryOption : ObservableObject
    {
        private bool _isSelected;

        public CategoryOption(string id, string name, string highlight = "", bool startsGroup = false, string imageUrl = "")
        {
            Id = id; Name = name; Highlight = highlight; StartsGroup = startsGroup; ImageUrl = imageUrl;
        }

        public string Id { get; }
        public string Name { get; }
        /// <summary>"best" for best sellers, "featured" for promotion sections, empty for a regular group.</summary>
        public string Highlight { get; }
        /// <summary>First regular group after the highlights; the rail draws a divider above it.</summary>
        public bool StartsGroup { get; }
        /// <summary>Photo of the group's best seller (or first item with a photo); empty for highlights.</summary>
        public string ImageUrl { get; }
        public bool HasImage => ImageUrl.Length > 0;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
