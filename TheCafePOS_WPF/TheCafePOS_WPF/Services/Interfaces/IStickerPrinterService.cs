using System.Collections.Generic;
using System.Collections.ObjectModel;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services.Interfaces
{
    public interface IStickerPrinterService
    {
        ObservableCollection<PrintedStickerInfo> RecentPrintedStickers { get; }
        List<PrintedStickerInfo> GenerateStickersForOrder(Order order);
        PrintedStickerInfo ReprintSticker(PrintedStickerInfo originalSticker, string reason, string cashierName);
    }
}
