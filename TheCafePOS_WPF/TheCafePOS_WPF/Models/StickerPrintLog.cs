using System;

namespace TheCafePOS_WPF.Models
{
    public class StickerPrintLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrderId { get; set; } = string.Empty;
        public int DailyOrderNumber { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string OptionsSummary { get; set; } = string.Empty;
        public int StickerIndex { get; set; }
        public int TotalStickers { get; set; }
        public string Reason { get; set; } = "InLầnĐầu"; // InLầnĐầu | InLại_RáchLy | InLại_TemMờ | InLại_ĐổNước
        public DateTime PrintedAt { get; set; } = DateTime.Now;
        public string PrintedBy { get; set; } = "ThuNgân";
    }
}
