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

    /// <summary>A category chip above the menu grid.</summary>
    public sealed class CategoryOption : ObservableObject
    {
        private bool _isSelected;

        public CategoryOption(string id, string name) { Id = id; Name = name; }

        public string Id { get; }
        public string Name { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
