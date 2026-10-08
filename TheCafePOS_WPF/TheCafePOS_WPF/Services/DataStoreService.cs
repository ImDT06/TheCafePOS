using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Services
{
    public partial class DataStoreService : IDataStoreService
    {
        private static DataStoreService? _instance;
        public static DataStoreService Instance => _instance ??= new DataStoreService();

        public List<Category> Categories { get; private set; } = new List<Category>();
        public List<Product> Products { get; private set; } = new List<Product>();
        public List<PackagingItem> PackagingInventory { get; private set; } = new List<PackagingItem>();
        public ObservableCollection<Order> CompletedOrders { get; private set; } = new ObservableCollection<Order>();
        public List<StickerPrintLog> StickerLogs { get; private set; } = new List<StickerPrintLog>();

        public Shift CurrentShift { get; private set; }

        private DataStoreService()
        {
            CurrentShift = new Shift
            {
                CashierName = "Thu Ngân CA 1",
                StartTime = DateTime.Now,
                InitialCash = 0,
                Status = "Closed"
            };

            SeedInitialData();
            LoadState();
        }

        public int GetNextDailyOrderNumber()
        {
            return CompletedOrders.Where(o => o.CreatedAt.Date == DateTime.Today).Select(o => o.DailyOrderNumber).DefaultIfEmpty(0).Max() + 1;
        }

        private void SeedInitialData()
        {
            // Categories
            Categories = new List<Category>
            {
                new Category { Id = "cat-1", Name = "🔥 HỒNG TRÀ & TRÀ ĐEN", DisplayOrder = 1 },
                new Category { Id = "cat-2", Name = "🧋 TRÀ SỮA ĐẶC BIỆT", DisplayOrder = 2 },
                new Category { Id = "cat-3", Name = "🍊 TRÀ TRÁI CÂY", DisplayOrder = 3 },
                new Category { Id = "cat-4", Name = "🧊 MACHIATO & KEM", DisplayOrder = 4 },
                new Category { Id = "cat-5", Name = "🍡 TOPPING THÊM", DisplayOrder = 5 }
            };

            // Products
            Products = new List<Product>
            {
                new Product { Id = "p-1", CategoryId = "cat-1", Name = "Hồng Trà Truyền Thống", BasePrice = 18000, ColorHex = "#D35400" },
                new Product { Id = "p-2", CategoryId = "cat-1", Name = "Hồng Trà Tắc Xí Muội", BasePrice = 22000, ColorHex = "#E67E22" },
                new Product { Id = "p-3", CategoryId = "cat-1", Name = "Hồng Trà Sữa Thượng Hạng", BasePrice = 25000, ColorHex = "#C0392B" },

                new Product { Id = "p-4", CategoryId = "cat-2", Name = "Trà Sữa Ngô Gia (Đặc Biệt)", BasePrice = 28000, ColorHex = "#8E44AD" },
                new Product { Id = "p-5", CategoryId = "cat-2", Name = "Trà Sữa Oolong Nướng", BasePrice = 30000, ColorHex = "#9B59B6" },
                new Product { Id = "p-6", CategoryId = "cat-2", Name = "Trà Sữa Trân Châu Hoàng Gia", BasePrice = 32000, ColorHex = "#6C5CE7" },
                new Product { Id = "p-7", CategoryId = "cat-2", Name = "Trà Sữa Matcha Uji", BasePrice = 35000, ColorHex = "#27AE60" },

                new Product { Id = "p-8", CategoryId = "cat-3", Name = "Trà Đào Cam Sả", BasePrice = 29000, ColorHex = "#F39C12" },
                new Product { Id = "p-9", CategoryId = "cat-3", Name = "Trà Vải Lài Mới", BasePrice = 28000, ColorHex = "#16A085" },
                new Product { Id = "p-10", CategoryId = "cat-3", Name = "Trà Mãng Cầu Tươi", BasePrice = 32000, ColorHex = "#2ECC71" },
                new Product { Id = "p-11", CategoryId = "cat-3", Name = "Trà Chanh Dây Thạch Dừa", BasePrice = 25000, ColorHex = "#F1C40F" },

                new Product { Id = "p-12", CategoryId = "cat-4", Name = "Hồng Trà Macchiato Cheese", BasePrice = 32000, ColorHex = "#E74C3C" },
                new Product { Id = "p-13", CategoryId = "cat-4", Name = "Oolong Macchiato Muối Biển", BasePrice = 35000, ColorHex = "#3498DB" },

                new Product { Id = "p-14", CategoryId = "cat-5", Name = "Trân Châu Đen", BasePrice = 5000, ColorHex = "#34495E" },
                new Product { Id = "p-15", CategoryId = "cat-5", Name = "Trân Châu Hoàng Kim", BasePrice = 6000, ColorHex = "#F39C12" },
                new Product { Id = "p-16", CategoryId = "cat-5", Name = "Thạch Trái Cây", BasePrice = 5000, ColorHex = "#1ABC9C" },
                new Product { Id = "p-17", CategoryId = "cat-5", Name = "Panna Cotta Trắng", BasePrice = 8000, ColorHex = "#BDC3C7" },
                new Product { Id = "p-18", CategoryId = "cat-5", Name = "Kem Cheese Béo", BasePrice = 10000, ColorHex = "#F1C40F" }
            };

            // Packaging Inventory (POS-09)
            PackagingInventory = new List<PackagingItem>
            {
                new PackagingItem { Id = "pack-1", Name = "Vỏ Ly Size M (500ml)", StockQuantity = 1200, Unit = "Cái", WarningThreshold = 100 },
                new PackagingItem { Id = "pack-2", Name = "Vỏ Ly Size L (700ml)", StockQuantity = 850, Unit = "Cái", WarningThreshold = 100 },
                new PackagingItem { Id = "pack-xl", Name = "Vỏ Ly Size XL", StockQuantity = 500, Unit = "Cái", WarningThreshold = 50 },
                new PackagingItem { Id = "pack-3", Name = "Nắp Ly Ép Màng", StockQuantity = 2500, Unit = "Cái", WarningThreshold = 200 },
                new PackagingItem { Id = "pack-4", Name = "Túi Đôi Mang Đi", StockQuantity = 600, Unit = "Cái", WarningThreshold = 50 },
                new PackagingItem { Id = "pack-5", Name = "Ống Hút Phi 12", StockQuantity = 1800, Unit = "Cái", WarningThreshold = 150 }
            };
        }

        public void DeductPackagingForOrder(Order order)
        {
            foreach (var item in order.Items.Where(i => i.IsBeverage))
            {
                string cupId = string.IsNullOrEmpty(item.PackagingId)
                    ? item.Size switch { "M" => "pack-1", "L" => "pack-2", "XL" => "pack-xl", _ => throw new InvalidOperationException("Size không hợp lệ.") }
                    : item.PackagingId;
                foreach (var id in new[] { cupId, "pack-3", "pack-5" })
                {
                    var packaging = PackagingInventory.FirstOrDefault(p => p.Id == id)
                        ?? throw new InvalidOperationException("Chưa cấu hình bao bì: " + id);
                    packaging.StockQuantity = checked(packaging.StockQuantity - item.Quantity);
                }
            }
        }
    }
}
