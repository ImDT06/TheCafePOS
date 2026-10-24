using System;
using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Services
{
    public class HeldOrderInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime HeldTime { get; set; } = DateTime.Now;
        public ObservableCollection<OrderItem> Items { get; set; } = new ObservableCollection<OrderItem>();
        public decimal TotalAmount { get; set; }
        public string Note { get; set; } = string.Empty;
        public string ServiceType { get; set; } = "Mang đi";

        public string DisplaySummary => $"Đơn giữ {HeldTime:HH:mm:ss} - {Items.Count} món ({TotalAmount:N0}đ)";
    }

    public class HoldOrderService : Interfaces.IHoldOrderService
    {
        private readonly IOrderService _orders;

        public ObservableCollection<HeldOrderInfo> HeldOrders { get; private set; } = new ObservableCollection<HeldOrderInfo>();

        public HoldOrderService(LocalDatabase db, IOrderService orders)
        {
            _orders = orders;
            HeldOrders = new ObservableCollection<HeldOrderInfo>(db.Read<System.Collections.Generic.List<HeldOrderInfo>>("held-orders") ?? new());
        }

        public void HoldOrder(ObservableCollection<OrderItem> items, decimal totalAmount, string note = "", string serviceType = "Mang đi")
        {
            if (items == null || items.Count == 0) return;

            var heldInfo = new HeldOrderInfo
            {
                HeldTime = DateTime.Now,
                TotalAmount = totalAmount,
                Note = note, ServiceType = serviceType
            };

            foreach (var item in items)
            {
                heldInfo.Items.Add(OrderConfigurationService.Clone(item));
            }

            HeldOrders.Insert(0, heldInfo);
            try { _orders.SaveWorkspace(HeldOrders, null); }
            catch { HeldOrders.Remove(heldInfo); throw; }
        }

        // Customer left without paying: drop the held cart. Same removal and persistence as recalling it.
        public bool DiscardOrder(string id) => RecallOrder(id) is not null;

        public HeldOrderInfo? RecallOrder(string id)
        {
            for (int i = 0; i < HeldOrders.Count; i++)
            {
                if (HeldOrders[i].Id == id)
                {
                    var item = HeldOrders[i];
                    HeldOrders.RemoveAt(i);
                    try { _orders.SaveWorkspace(HeldOrders, null); }
                    catch { HeldOrders.Insert(i, item); throw; }
                    return item;
                }
            }
            return null;
        }
    }
}

