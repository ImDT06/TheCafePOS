namespace TheCafePOS_WPF.Models;

public class SalesChannel
{
    public string Name { get; set; } = "";
    // Delivery apps take a commission, so shops usually list a higher price there.
    public int MarkupPercent { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StoreSettings
{
    public const string Counter = "Tại quầy";
    public string ShopName { get; set; } = "";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string OpeningHours { get; set; } = "";
    public string DisplayName => string.IsNullOrWhiteSpace(ShopName) ? "TheCafePOS" : ShopName;
    // Cashiers may give up to this % of the subtotal without a manager; managers are never limited.
    public int CashierDiscountLimitPercent { get; set; }
    // 0 turns VAT off. Included: menu prices already contain the tax, receipts only show it.
    public int VatPercent { get; set; }
    public bool PricesIncludeVat { get; set; } = true;
    public string ReceiptFooter { get; set; } = "Cảm ơn quý khách, hẹn gặp lại!";
    public List<SalesChannel> Channels { get; set; } = new()
    {
        new() { Name = "GrabFood", MarkupPercent = 25 },
        new() { Name = "ShopeeFood", MarkupPercent = 20 },
        new() { Name = "Be", MarkupPercent = 20, IsActive = false }
    };
}
