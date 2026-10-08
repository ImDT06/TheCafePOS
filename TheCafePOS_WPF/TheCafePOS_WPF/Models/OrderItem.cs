using System;
using System.Collections.Generic;
using System.Linq;

namespace TheCafePOS_WPF.Models
{
    public class OrderItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrderId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Size { get; set; } = "M"; // S, M, L
        public int SugarPercent { get; set; } = 100; // 0, 30, 50, 70, 100
        public int IcePercent { get; set; } = 100; // 0, 30, 50, 70, 100
        public string SugarChoice { get; set; } = "";
        public string IceChoice { get; set; } = "";
        public List<string> Toppings { get; set; } = new List<string>();
        public decimal ToppingsPrice { get; set; }
        public List<SelectedTopping> SelectedToppings { get; set; } = new();
        public string Note { get; set; } = "";
        public bool IsBeverage { get; set; } = true;
        public bool HasSugarOption { get; set; } = true;
        public bool HasIceOption { get; set; } = true;
        public string PackagingId { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public int ReadyQuantity { get; set; }
        public int DeliveredQuantity { get; set; }
        public decimal BasePrice { get; set; }
        public decimal SizeExtraPrice { get; set; }

        public decimal UnitPrice => BasePrice + SizeExtraPrice + ToppingsPrice;
        public decimal TotalPrice => UnitPrice * Quantity;

        public string OptionsSummary
        {
            get
            {
                var options = new List<string>();
                if (IsBeverage) options.Add($"Size {Size}");
                if (HasSugarOption && IsBeverage) options.Add(string.IsNullOrEmpty(SugarChoice) ? $"Đường {SugarPercent}%" : $"Độ ngọt: {SugarChoice}");
                if (HasIceOption && IsBeverage) options.Add(string.IsNullOrEmpty(IceChoice) ? $"Đá {IcePercent}%" : $"Lượng đá: {IceChoice}");

                if (Toppings != null && Toppings.Count > 0)
                {
                    options.Add($"+{string.Join(", ", Toppings)}");
                }

                if (!string.IsNullOrWhiteSpace(Note)) options.Add($"Ghi chú: {Note}");
                return string.Join(" | ", options);
            }
        }

        public string FormattedTotalPrice => $"{TotalPrice:N0}đ";
        public string FormattedUnitPrice => $"{UnitPrice:N0}đ";
    }
}
