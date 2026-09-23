using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Services
{
    public class PrintedStickerInfo
    {
        public string OrderId { get; set; } = string.Empty;
        public int DailyOrderNumber { get; set; }
        public string DailyOrderFormatted => $"#{DailyOrderNumber:D2}";
        public string ProductName { get; set; } = string.Empty;
        public string OptionsSummary { get; set; } = string.Empty;
        public int Index { get; set; }
        public int Total { get; set; }
        public string IndexFormatted => $"Ly {Index}/{Total}";
        public string FormattedPrice { get; set; } = string.Empty;
        public DateTime PrintTime { get; set; } = DateTime.Now;
        public string Reason { get; set; } = "InLầnĐầu";
    }

    public class StickerPrinterService : IStickerPrinterService
    {
        private static StickerPrinterService? _instance;
        public static StickerPrinterService Instance => _instance ??= new StickerPrinterService();

        public ObservableCollection<PrintedStickerInfo> RecentPrintedStickers { get; private set; } = new ObservableCollection<PrintedStickerInfo>();

        private StickerPrinterService()
        {
            foreach (var log in DataStoreService.Instance.StickerLogs.OrderByDescending(l => l.PrintedAt).Take(200))
                RecentPrintedStickers.Add(new PrintedStickerInfo { OrderId = log.OrderId, DailyOrderNumber = log.DailyOrderNumber, ProductName = log.ProductName, OptionsSummary = log.OptionsSummary, Index = log.StickerIndex, Total = log.TotalStickers, PrintTime = log.PrintedAt, Reason = log.Reason });
        }

        public List<PrintedStickerInfo> GenerateStickersForOrder(Order order)
        {
            var stickers = new List<PrintedStickerInfo>();

            int totalCups = order.Items.Where(i => i.IsBeverage).Sum(i => i.Quantity);
            int cupIndex = 0;
            foreach (var item in order.Items.Where(i => i.IsBeverage))
            {
                for (int i = 1; i <= item.Quantity; i++)
                {
                    cupIndex++;
                    var sticker = new PrintedStickerInfo
                    {
                        OrderId = order.Id,
                        DailyOrderNumber = order.DailyOrderNumber,
                        ProductName = item.ProductName,
                        OptionsSummary = item.OptionsSummary,
                        Index = cupIndex,
                        Total = totalCups,
                        FormattedPrice = item.FormattedUnitPrice,
                        PrintTime = DateTime.Now,
                        Reason = "InLầnĐầu"
                    };

                    stickers.Add(sticker);
                    RecentPrintedStickers.Insert(0, sticker);

                    DataStoreService.Instance.StickerLogs.Add(new StickerPrintLog
                    {
                        OrderId = order.Id,
                        DailyOrderNumber = order.DailyOrderNumber,
                        ProductName = item.ProductName,
                        OptionsSummary = item.OptionsSummary,
                        StickerIndex = cupIndex,
                        TotalStickers = totalCups,
                        Reason = "InLầnĐầu",
                        PrintedAt = DateTime.Now
                    });
                }
            }

            return stickers;
        }

        public PrintedStickerInfo ReprintSticker(PrintedStickerInfo originalSticker, string reason, string cashierName)
        {
            var newSticker = new PrintedStickerInfo
            {
                OrderId = originalSticker.OrderId,
                DailyOrderNumber = originalSticker.DailyOrderNumber,
                ProductName = originalSticker.ProductName,
                OptionsSummary = originalSticker.OptionsSummary,
                Index = originalSticker.Index,
                Total = originalSticker.Total,
                FormattedPrice = originalSticker.FormattedPrice,
                PrintTime = DateTime.Now,
                Reason = $"IN LẠI: {reason}"
            };

            RecentPrintedStickers.Insert(0, newSticker);

            DataStoreService.Instance.StickerLogs.Add(new StickerPrintLog
            {
                OrderId = originalSticker.OrderId,
                DailyOrderNumber = originalSticker.DailyOrderNumber,
                ProductName = originalSticker.ProductName,
                OptionsSummary = originalSticker.OptionsSummary,
                StickerIndex = originalSticker.Index,
                TotalStickers = originalSticker.Total,
                Reason = $"Reprint ({reason})",
                PrintedAt = DateTime.Now,
                PrintedBy = cashierName
            });

            return newSticker;
        }
    }
}

