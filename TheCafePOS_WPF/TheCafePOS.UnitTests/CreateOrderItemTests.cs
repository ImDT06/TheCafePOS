using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;

/// <summary>Lab2 sheet CreateOrderItem: OrderConfigurationService.Create(product, size, sugar, ice, quantity, toppings, note, menu).</summary>
[TestFixture]
public class CreateOrderItemTests
{
    private const string BadQuantity = "Số lượng từ 1 đến 999.";
    private const string BadTopping = "Topping không hợp lệ (tối đa 10 phần mỗi loại/ly).";
    private const string NotOffered = "Topping không có sẵn cho món này.";
    private const string BadSize = "Size không hợp lệ.";
    private const string BadLevel = "Mức đường/đá không hợp lệ.";
    private const string NotSold = "Món không bán riêng hoặc đã ngừng bán.";

    private Product _americano = null!;
    private List<Product> _menu = null!;

    // Precondition: Americano Classic 49.000đ, S +0 / M +6.000 / L +16.000; Shot Espresso 10.000đ and
    // Sữa Đặc 5.000đ are allowed toppings; Trân châu trắng is a topping the drink does not offer.
    [SetUp]
    public void SetUp()
    {
        var shot = new Product { Id = "shot", Name = "Shot Espresso", BasePrice = 10000, IsTopping = true, SoldSeparately = false };
        var milk = new Product { Id = "sua", Name = "Sữa Đặc", BasePrice = 5000, IsTopping = true, SoldSeparately = false };
        var pearl = new Product { Id = "tran", Name = "Trân châu trắng", BasePrice = 5000, IsTopping = true, SoldSeparately = false };
        _americano = new Product
        {
            Id = "americano", Name = "Americano Classic", BasePrice = 49000, AllowedToppingIds = { "shot", "sua" },
            Sizes = new() { new() { Name = "S", ExtraPrice = 0 }, new() { Name = "M", ExtraPrice = 6000 }, new() { Name = "L", ExtraPrice = 16000 } }
        };
        _menu = new() { _americano, shot, milk, pearl };
    }

    // "shot*1 sua*1" → [("shot", 1), ("sua", 1)]
    private static (string, int)[] Toppings(string spec) =>
        spec.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Split('*')).Select(p => (p[0], int.Parse(p[1]))).ToArray();

    private OrderItem Create(string size, int sugar, int quantity, string toppings, string note = "") =>
        OrderConfigurationService.Create(_americano, size, sugar, 100, quantity, Toppings(toppings), note, _menu);

    [TestCase("M", 1, "", 55000, 55000, TestName = "UTCID01")]
    [TestCase("L", 2, "shot*1", 75000, 150000, TestName = "UTCID02")]
    [TestCase("M", 1, "shot*1 sua*1", 70000, 70000, TestName = "UTCID03")]
    [TestCase("M", 999, "", 55000, 54945000, TestName = "UTCID04")]
    [TestCase("M", 1, "shot*10", 155000, 155000, TestName = "UTCID07")]
    public void Item_is_priced_from_size_and_toppings(string size, int quantity, string toppings, decimal unit, decimal total)
    {
        var item = Create(size, 50, quantity, toppings);
        Assert.That(item.UnitPrice, Is.EqualTo(unit));
        Assert.That(item.TotalPrice, Is.EqualTo(total));
    }

    [TestCase("M", 50, 0, "", BadQuantity, TestName = "UTCID05")]
    [TestCase("M", 50, 1000, "", BadQuantity, TestName = "UTCID06")]
    [TestCase("M", 50, 1, "shot*11", BadTopping, TestName = "UTCID08")]
    [TestCase("M", 50, 1, "shot*0", BadTopping, TestName = "UTCID09")]
    [TestCase("M", 50, 1, "shot*1 shot*1", BadTopping, TestName = "UTCID10")]
    [TestCase("M", 50, 1, "tran*1", NotOffered, TestName = "UTCID11")]
    [TestCase("XL", 50, 1, "", BadSize, TestName = "UTCID12")]
    [TestCase("M", 40, 1, "", BadLevel, TestName = "UTCID13")]
    public void Invalid_choice_is_refused(string size, int sugar, int quantity, string toppings, string message)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Create(size, sugar, quantity, toppings));
        Assert.That(ex!.Message, Is.EqualTo(message));
    }

    [TestCase(TestName = "UTCID14")]
    public void Discontinued_item_cannot_be_ordered()
    {
        _americano.IsActive = false;
        var ex = Assert.Throws<InvalidOperationException>(() => Create("M", 50, 1, ""));
        Assert.That(ex!.Message, Is.EqualTo(NotSold));
    }

    [TestCase(TestName = "UTCID15")]
    public void Note_is_trimmed()
    {
        var item = Create("M", 50, 1, "", "  Ít đá  ");
        Assert.That(item.Note, Is.EqualTo("Ít đá"));
        Assert.That(item.UnitPrice, Is.EqualTo(55000));
    }
}
