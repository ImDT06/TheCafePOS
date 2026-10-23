namespace TheCafePOS_WPF.Services.Interfaces
{
    /// <summary>
    /// The whole shop data store. Prefer depending on the focused interface a class actually uses;
    /// this aggregate exists for the composition root and the data store itself.
    /// </summary>
    public interface IDataStoreService : ICatalogService, IInventoryService, IShiftService, IFinanceService,
        ICustomerService, IPromotionService, IStoreSettingsService, IOrderService, IVoucherService, IPurchasingService
    {
    }
}
