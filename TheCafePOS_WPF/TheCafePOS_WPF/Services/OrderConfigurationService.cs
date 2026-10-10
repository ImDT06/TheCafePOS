using System.Text.Json;
using TheCafePOS_WPF.Models;
namespace TheCafePOS_WPF.Services;

public static class OrderConfigurationService
{
    public static readonly int[] Levels = { 0, 30, 50, 70, 100 };
    public static OrderItem Clone(OrderItem item) => JsonSerializer.Deserialize<OrderItem>(JsonSerializer.Serialize(item))!;
    public static string? ResolveChoice(List<string>? choices, string? current, string? fallback)
    {
        if (choices is null || choices.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(current) && choices.Contains(current)) return current;
        if (!string.IsNullOrWhiteSpace(fallback) && choices.Contains(fallback)) return fallback;
        return choices.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
    }
    public static OrderItem Create(Product product, string sizeName, int sugar, int ice, int quantity,
        IEnumerable<(string Id, int Quantity)> toppings, string note, IEnumerable<Product> menu,
        string? sugarChoice = null, string? iceChoice = null)
    {
        if (!product.IsActive || (product.IsTopping && !product.SoldSeparately)) throw new InvalidOperationException("Món không bán riêng hoặc đã ngừng bán.");
        if (quantity is < 1 or > 999) throw new InvalidOperationException("Số lượng từ 1 đến 999.");
        var size = !product.IsBeverage ? null : product.Sizes.FirstOrDefault(s => s.Name == sizeName)
            ?? (!product.IsBeverage ? null : throw new InvalidOperationException("Size không hợp lệ."));
        if (!Levels.Contains(sugar) || !Levels.Contains(ice)) throw new InvalidOperationException("Mức đường/đá không hợp lệ.");
        string Resolve(bool enabled, List<string>? choices, string fallback, string? selected)
        {
            if (!product.IsBeverage || !enabled || choices is null) return "";
            var value = ResolveChoice(choices, selected, fallback);
            if (string.IsNullOrWhiteSpace(value) || !choices.Contains(value))
                throw new InvalidOperationException("Tùy chọn đường/đá không áp dụng cho món này.");
            return value;
        }
        var selectedSugar = Resolve(product.AllowSugar, product.SugarChoices, product.DefaultSugarChoice, sugarChoice);
        var selectedIce = Resolve(product.AllowIce, product.IceChoices, product.DefaultIceChoice, iceChoice);
        var selections = new List<SelectedTopping>();
        foreach (var selection in toppings)
        {
            if (!product.IsBeverage || selection.Quantity is < 1 or > 10 || selections.Any(t => t.ProductId == selection.Id)) throw new InvalidOperationException("Topping không hợp lệ (tối đa 10 phần mỗi loại/ly).");
            var topping = menu.FirstOrDefault(p => p.Id == selection.Id && p.IsActive && p.IsTopping && product.AllowedToppingIds.Contains(p.Id))
                ?? throw new InvalidOperationException("Topping không có sẵn cho món này.");
            selections.Add(new() { ProductId = topping.Id, Name = topping.Name, UnitPrice = topping.BasePrice, Quantity = selection.Quantity });
        }
        return new OrderItem
        {
            ProductId = product.Id, ProductName = product.Name, BasePrice = product.BasePrice,
            Size = size?.Name ?? "", SizeExtraPrice = size?.ExtraPrice ?? 0, PackagingId = size?.PackagingId ?? "",
            IsBeverage = product.IsBeverage, HasSugarOption = product.IsBeverage && product.AllowSugar,
            HasIceOption = product.IsBeverage && product.AllowIce,
            SugarPercent = product.AllowSugar ? sugar : product.DefaultSugar, IcePercent = product.AllowIce ? ice : product.DefaultIce,
            SugarChoice = selectedSugar, IceChoice = selectedIce,
            Quantity = quantity, Note = note.Trim(), SelectedToppings = selections,
            Toppings = selections.Select(t => $"{t.Quantity} × {t.Name}").ToList(),
            ToppingsPrice = selections.Sum(t => t.UnitPrice * t.Quantity)
        };
    }
}
