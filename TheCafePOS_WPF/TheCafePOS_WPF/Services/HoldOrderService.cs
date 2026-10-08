using System;
using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services
{
    public class HeldOrderInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime HeldTime { get; set; } = DateTime.Now;
        public ObservableCollection<OrderItem> Items { get; set; } = new ObservableCollection<OrderItem>();
        public decimal TotalAmount { get; set; }
        public string Note { get; set; } = string.Empty;

        public string DisplaySummary => $"Đơn giữ {HeldTime:HH:mm:ss} - {Items.Count} món ({TotalAmount:N0}đ)";
    }

    public class HoldOrderService
    {
        private static HoldOrderService? _instance;
        public static HoldOrderService Instance => _instance ??= new HoldOrderService();

        public ObservableCollection<HeldOrderInfo> HeldOrders { get; private set; } = new ObservableCollection<HeldOrderInfo>();

        private HoldOrderService()
        {
            HeldOrders = new ObservableCollection<HeldOrderInfo>(LocalDatabase.Instance.Read<System.Collections.Generic.List<HeldOrderInfo>>("held-orders") ?? new());
        }

        public void HoldOrder(ObservableCollection<OrderItem> items, decimal totalAmount, string note = "")
        {
            if (items == null || items.Count == 0) return;

            var heldInfo = new HeldOrderInfo
            {
                HeldTime = DateTime.Now,
                TotalAmount = totalAmount,
                Note = note
            };

            foreach (var item in items)
            {
                heldInfo.Items.Add(OrderConfigurationService.Clone(item));
            }

            HeldOrders.Insert(0, heldInfo);
            try { LocalDatabase.Instance.Write("held-orders", HeldOrders); }
            catch { HeldOrders.Remove(heldInfo); throw; }
        }

        public HeldOrderInfo? RecallOrder(string id)
        {
            for (int i = 0; i < HeldOrders.Count; i++)
            {
                if (HeldOrders[i].Id == id)
                {
                    var item = HeldOrders[i];
                    HeldOrders.RemoveAt(i);
                    try { LocalDatabase.Instance.Write("held-orders", HeldOrders); }
                    catch { HeldOrders.Insert(i, item); throw; }
                    return item;
                }
            }
            return null;
        }
    }
}

