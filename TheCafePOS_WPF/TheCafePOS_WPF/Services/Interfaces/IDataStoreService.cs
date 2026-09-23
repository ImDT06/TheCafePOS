using System.Collections.Generic;
using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services.Interfaces
{
    public interface IDataStoreService
    {
        List<Category> Categories { get; }
        List<Product> Products { get; }
        List<PackagingItem> PackagingInventory { get; }
        ObservableCollection<Order> CompletedOrders { get; }
        List<StickerPrintLog> StickerLogs { get; }
        Shift CurrentShift { get; }

        int GetNextDailyOrderNumber();
        void DeductPackagingForOrder(Order order);
    }
}
