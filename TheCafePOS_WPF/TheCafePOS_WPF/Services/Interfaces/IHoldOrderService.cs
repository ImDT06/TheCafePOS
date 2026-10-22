using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services.Interfaces
{
    public interface IHoldOrderService
    {
        ObservableCollection<HeldOrderInfo> HeldOrders { get; }

        void HoldOrder(ObservableCollection<OrderItem> items, decimal totalAmount, string note = "", string serviceType = "Mang đi");
        HeldOrderInfo? RecallOrder(string id);
        bool DiscardOrder(string id);
    }
}
