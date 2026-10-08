using System.Text.Json;
using TheCafePOS_WPF.Models;
namespace TheCafePOS_WPF.Services;

public static class OrderConfigurationService
{
    public static readonly int[] Levels = { 0, 30, 50, 70, 100 };
    public static OrderItem Clone(OrderItem item) => JsonSerializer.Deserialize<OrderItem>(JsonSerializer.Serialize(item))!;
    public static OrderItem Create(Product product, string sizeName, int sugar, int ice, int quantity,
        IEnumerable<(string Id, int Quantity)> toppings, string note, IEnumerable<Product> menu)
    {
        if (!product.IsActive || (product.IsTopping && !product.SoldSeparately)) throw new InvalidOperationException("Món không bán riêng hoặc đã ngừng bán.");
        if (quantity is < 1 or > 999) throw new InvalidOperationException("Số lượng từ 1 đến 999.");
        var size = product.IsTopping ? null : product.Sizes.FirstOrDefault(s => s.Name == sizeName)
            ?? (product.IsTopping ? null : throw new InvalidOperationException("Size không hợp lệ."));
        if (!Levels.Contains(sugar) || !Levels.Contains(ice)) throw new InvalidOperationException("Mức đường/đá không hợp lệ.");
        var selections = new List<SelectedTopping>();
        foreach (var selection in toppings)
        {
            if (product.IsTopping || selection.Quantity is < 1 or > 10 || selections.Any(t => t.ProductId == selection.Id)) throw new InvalidOperationException("Topping không hợp lệ (tối đa 10 phần mỗi loại/ly).");
            var topping = menu.FirstOrDefault(p => p.Id == selection.Id && p.IsActive && p.IsTopping && product.AllowedToppingIds.Contains(p.Id))
                ?? throw new InvalidOperationException("Topping không có sẵn cho món này.");
            selections.Add(new() { ProductId = topping.Id, Name = topping.Name, UnitPrice = topping.BasePrice, Quantity = selection.Quantity });
        }
        return new OrderItem
        {
            ProductId = product.Id, ProductName = product.Name, BasePrice = product.BasePrice,
            Size = size?.Name ?? "", SizeExtraPrice = size?.ExtraPrice ?? 0, PackagingId = size?.PackagingId ?? "",
            IsBeverage = !product.IsTopping, HasSugarOption = !product.IsTopping && product.AllowSugar,
            HasIceOption = !product.IsTopping && product.AllowIce,
            SugarPercent = product.AllowSugar ? sugar : product.DefaultSugar, IcePercent = product.AllowIce ? ice : product.DefaultIce,
            Quantity = quantity, Note = note.Trim(), SelectedToppings = selections,
            Toppings = selections.Select(t => $"{t.Quantity} × {t.Name}").ToList(),
            ToppingsPrice = selections.Sum(t => t.UnitPrice * t.Quantity)
        };
    }
}
