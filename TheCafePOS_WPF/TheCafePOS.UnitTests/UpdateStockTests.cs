using TheCafePOS_WPF.Services;

/// <summary>Lab2 sheet UpdateStock: DataStoreService.UpdateStock(packagingId, operation, quantity, reason).</summary>
[TestFixture]
public class UpdateStockTests
{
    private const string Rule = "Nhập kho và hao hụt cần số dương; kiểm kê cần số không âm; điều chỉnh cần số khác 0.";
    private const string Range = "Tồn kho sau thay đổi phải từ 0 đến 2.147.483.647.";
    private const string NoReason = "Vui lòng nhập lý do hoặc thông tin phiếu nhập/kiểm kê.";
    private const string NoItem = "Bao bì không tồn tại.";
    private const string OwnerOnly = "Chức năng chỉ dành cho chủ quán.";
    private TestHost _host = null!;
    private DataStoreService Store => _host.Store;
    private int Stock => Store.PackagingInventory.Single(p => p.Id == "pack-1").StockQuantity;

    // Precondition: owner signed in, pack-1 (Vỏ Ly Size M) holds 1200.
    [SetUp]
    public void SetUp()
    {
        _host = new TestHost();
        Assert.That(Stock, Is.EqualTo(1200));
    }

    [TearDown]
    public void TearDown() => _host.Dispose();

    [TestCase("Nhập kho", 100, 1300, TestName = "UTCID01")]
    [TestCase("Kiểm kê", 500, 500, TestName = "UTCID03")]
    [TestCase("Kiểm kê", 0, 0, TestName = "UTCID04")]
    [TestCase("Điều chỉnh", -10, 1190, TestName = "UTCID06")]
    [TestCase("Hao hụt", 1200, 0, TestName = "UTCID08")]
    [TestCase("Nhập kho", 2147482447, int.MaxValue, TestName = "UTCID10")]
    public void Valid_change_updates_stock_and_history(string operation, int quantity, int expected)
    {
        int moves = Store.StockMovements.Count;
        Store.UpdateStock("pack-1", operation, quantity, "Phiếu NCC01");
        Assert.That(Stock, Is.EqualTo(expected));
        Assert.That(Store.StockMovements.Count, Is.EqualTo(moves + 1));
        Assert.That(Store.StockMovements.Last().After, Is.EqualTo(expected));
    }

    [TestCase("pack-1", "Nhập kho", 0, "Phiếu NCC01", Rule, TestName = "UTCID02")]
    [TestCase("pack-1", "Kiểm kê", -1, "Phiếu NCC01", Rule, TestName = "UTCID05")]
    [TestCase("pack-1", "Điều chỉnh", 0, "Phiếu NCC01", Rule, TestName = "UTCID07")]
    [TestCase("pack-1", "Hao hụt", 1201, "Phiếu NCC01", Range, TestName = "UTCID09")]
    [TestCase("pack-1", "Nhập kho", 2147482448, "Phiếu NCC01", Range, TestName = "UTCID11")]
    [TestCase("pack-1", "Khác", 10, "Phiếu NCC01", Rule, TestName = "UTCID12")]
    [TestCase("pack-1", "Nhập kho", 10, "", NoReason, TestName = "UTCID13")]
    [TestCase("pack-1", "Nhập kho", 10, "   ", NoReason, TestName = "UTCID14")]
    [TestCase("khong-co", "Nhập kho", 10, "Phiếu NCC01", NoItem, TestName = "UTCID15")]
    public void Invalid_change_is_refused_and_stock_kept(string packagingId, string operation, int quantity, string reason, string message) =>
        AssertRefused(() => Store.UpdateStock(packagingId, operation, quantity, reason), message);

    [TestCase(TestName = "UTCID16")]
    public void Cashier_cannot_change_stock()
    {
        _host.Auth.CreateAccount("staff", "staff123", "Cashier");
        _host.Auth.Login("staff", "staff123");
        AssertRefused(() => Store.UpdateStock("pack-1", "Nhập kho", 10, "Phiếu NCC01"), OwnerOnly);
    }

    private void AssertRefused(TestDelegate change, string message)
    {
        int moves = Store.StockMovements.Count;
        var ex = Assert.Throws<InvalidOperationException>(change);
        Assert.That(ex!.Message, Is.EqualTo(message));
        Assert.That(Stock, Is.EqualTo(1200));
        Assert.That(Store.StockMovements.Count, Is.EqualTo(moves));
    }
}
