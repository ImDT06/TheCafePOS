using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public partial class DataStoreService
{
    public List<Ingredient> Ingredients { get; private set; } = new();
    public List<IngredientMovement> IngredientMovements { get; private set; } = new();

    // Recipes are written for size M; other sizes scale like common cafe cup volumes.
    public static decimal SizeFactor(string size) => size switch { "S" => 0.8m, "L" => 1.3m, "XL" => 1.6m, _ => 1m };

    public void SaveIngredient(Ingredient item)
    {
        _auth.RequireManager();
        string name = item.Name.Trim(), unit = item.Unit.Trim();
        if (name.Length == 0 || unit.Length == 0) throw new InvalidOperationException("Nhập tên và đơn vị nguyên liệu.");
        if (item.WarningThreshold < 0) throw new InvalidOperationException("Ngưỡng cảnh báo không được âm.");
        if (Ingredients.Any(i => i.Id != item.Id && i.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))) throw new InvalidOperationException("Tên nguyên liệu đã tồn tại.");
        var existing = Ingredients.FirstOrDefault(i => i.Id == item.Id);
        // Stock only changes through receive / count so every change has a movement record.
        var saved = new Ingredient { Id = item.Id, Name = name, Unit = unit, WarningThreshold = item.WarningThreshold, Stock = existing?.Stock ?? 0 };
        Mutate(() => { if (existing is null) Ingredients.Add(saved); else Ingredients[Ingredients.IndexOf(existing)] = saved; });
    }

    public void DeleteIngredient(string id)
    {
        _auth.RequireManager();
        var item = Ingredients.FirstOrDefault(i => i.Id == id) ?? throw new InvalidOperationException("Nguyên liệu không tồn tại.");
        var users = Products.Where(p => p.Recipe.Any(r => r.IngredientId == id)).Select(p => p.Name).ToList();
        if (users.Count > 0) throw new InvalidOperationException("Nguyên liệu đang dùng trong công thức: " + string.Join(", ", users.Take(5)) + (users.Count > 5 ? "…" : ""));
        Mutate(() => Ingredients.Remove(item));
    }

    // type: "Nhập kho" adds quantity; "Kiểm kê" sets the counted stock.
    public void AdjustIngredient(string id, string type, decimal quantity, string reason)
    {
        _auth.RequireManager();
        var item = Ingredients.FirstOrDefault(i => i.Id == id) ?? throw new InvalidOperationException("Nguyên liệu không tồn tại.");
        if (type == "Nhập kho" && quantity <= 0) throw new InvalidOperationException("Số lượng nhập phải lớn hơn 0.");
        if (type == "Kiểm kê" && quantity < 0) throw new InvalidOperationException("Tồn kiểm kê không được âm.");
        if (type is not ("Nhập kho" or "Kiểm kê")) throw new InvalidOperationException("Thao tác kho không hợp lệ.");
        Mutate(() =>
        {
            decimal change = type == "Nhập kho" ? quantity : quantity - item.Stock;
            item.Stock += change;
            Log(item, type, change, reason);
            RefreshStockSoldOut();
        });
    }

    public void SaveRecipe(string productId, List<RecipeLine> lines)
    {
        _auth.RequireManager();
        var product = Products.FirstOrDefault(p => p.Id == productId) ?? throw new InvalidOperationException("Món không tồn tại.");
        if (lines.Any(l => l.Amount <= 0 || !Ingredients.Any(i => i.Id == l.IngredientId))) throw new InvalidOperationException("Mỗi dòng công thức cần nguyên liệu hợp lệ và định lượng > 0.");
        if (lines.GroupBy(l => l.IngredientId).Any(g => g.Count() > 1)) throw new InvalidOperationException("Mỗi nguyên liệu chỉ khai báo một lần.");
        Mutate(() => { product.Recipe = lines.Select(l => new RecipeLine { IngredientId = l.IngredientId, Amount = l.Amount }).ToList(); RefreshStockSoldOut(); });
    }

    // Runs inside Checkout's Mutate: drink recipe × size × quantity, plus each topping's own recipe.
    private void DeductIngredientsForOrder(Order order)
    {
        if (Ingredients.Count == 0) return;
        foreach (var item in order.Items)
        {
            var product = Products.FirstOrDefault(p => p.Id == item.ProductId);
            if (product is null) continue;
            decimal factor = product.IsBeverage ? SizeFactor(item.Size) : 1m;
            Consume(product, factor * item.Quantity, order);
            foreach (var topping in item.SelectedToppings)
                if (Products.FirstOrDefault(p => p.Id == topping.ProductId) is { } t) Consume(t, topping.Quantity * item.Quantity, order);
        }
        RefreshStockSoldOut();
    }

    private void Consume(Product product, decimal multiplier, Order order)
    {
        foreach (var line in product.Recipe)
        {
            if (Ingredients.FirstOrDefault(i => i.Id == line.IngredientId) is not { } ing) continue;
            decimal used = Math.Round(line.Amount * multiplier, 2);
            ing.Stock -= used;
            Log(ing, "Bán hàng", -used, $"Đơn #{order.DailyOrderNumber:D2} · {product.Name}");
        }
    }

    // Products whose recipe needs an ingredient at or below zero are flagged sold-out automatically;
    // they come back once restocked, but a manual sold-out flag is never cleared here.
    private void RefreshStockSoldOut()
    {
        foreach (var product in Products.Where(p => p.Recipe.Count > 0))
        {
            bool missing = product.Recipe.Any(r => Ingredients.FirstOrDefault(i => i.Id == r.IngredientId) is { Stock: <= 0 });
            if (missing && !product.IsSoldOut) { product.IsSoldOut = true; product.SoldOutByStock = true; }
            else if (!missing && product.SoldOutByStock) { product.IsSoldOut = false; product.SoldOutByStock = false; }
        }
    }

    private void Log(Ingredient ing, string type, decimal change, string reason) =>
        IngredientMovements.Add(new IngredientMovement { IngredientId = ing.Id, IngredientName = ing.Name, Type = type, Change = change, After = ing.Stock, Reason = reason.Trim(), Operator = _auth.CurrentUser?.Username ?? "" });
}
