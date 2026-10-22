using System;
using System.Collections.ObjectModel;
using System.Linq;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IDataStoreService _dataStore;
        private readonly IStickerPrinterService _stickerPrinter;
        private readonly IHoldOrderService _holds;

        private int _currentDailyOrderNumber;
        private string _supabaseStatusText = "Dữ liệu tại máy (SQLite)";
        private decimal _cashGiven;
        private string _cashGivenInput = string.Empty;

        public ObservableCollection<OrderItem> CartItems { get; } = new ObservableCollection<OrderItem>();
        public ObservableCollection<PrintedStickerInfo> RecentStickers => _stickerPrinter.RecentPrintedStickers;

        public string SupabaseStatusText
        {
            get => _supabaseStatusText;
            set => SetProperty(ref _supabaseStatusText, value);
        }

        public string NextDailyOrderNumberText => $"Số thứ tự tiếp theo: #{_currentDailyOrderNumber:D2}";
        public string CartTitleText => $"ĐƠN HÀNG #{_currentDailyOrderNumber:D2}";
        public string CartTimeText => $" ({DateTime.Now:HH:mm})";
        public string HeldOrdersCountText => $"Đơn giữ ({_holds.HeldOrders.Count})";

        public decimal GrandTotal => CartItems.Sum(i => i.TotalPrice);
        public string GrandTotalFormatted => $"{GrandTotal:N0}đ";

        public string CashGivenInput
        {
            get => _cashGivenInput;
            set
            {
                if (SetProperty(ref _cashGivenInput, value))
                {
                    decimal.TryParse(value, out _cashGiven);
                    OnPropertyChanged(nameof(ChangeReturnedFormatted));
                    OnPropertyChanged(nameof(ChangeReturnedColor));
                }
            }
        }

        public decimal ChangeReturned => Math.Max(0, _cashGiven - GrandTotal);

        public string ChangeReturnedFormatted
        {
            get
            {
                if (_cashGiven == 0) return "0đ";
                decimal diff = _cashGiven - GrandTotal;
                return diff >= 0 ? $"{diff:N0}đ" : $"Thiếu {Math.Abs(diff):N0}đ";
            }
        }

        public string ChangeReturnedColor => (_cashGiven >= GrandTotal) ? "#27AE60" : "#E74C3C";

        public MainViewModel(IDataStoreService dataStore, IStickerPrinterService stickerPrinter, IHoldOrderService holds)
        {
            _dataStore = dataStore;
            _stickerPrinter = stickerPrinter;
            _holds = holds;

            _currentDailyOrderNumber = _dataStore.GetNextDailyOrderNumber();
            UpdateHeaderState();
        }

        public void RefreshCartState()
        {
            OnPropertyChanged(nameof(GrandTotal));
            OnPropertyChanged(nameof(GrandTotalFormatted));
            OnPropertyChanged(nameof(ChangeReturnedFormatted));
            OnPropertyChanged(nameof(ChangeReturnedColor));
        }

        public void UpdateHeaderState()
        {
            OnPropertyChanged(nameof(NextDailyOrderNumberText));
            OnPropertyChanged(nameof(CartTitleText));
            OnPropertyChanged(nameof(CartTimeText));
            OnPropertyChanged(nameof(HeldOrdersCountText));
        }

        public void CompleteOrder(string paymentMethod, decimal cashGiven = 0)
        {
            var order = _dataStore.Checkout(CartItems, paymentMethod, cashGiven, Guid.NewGuid().ToString());

            _stickerPrinter.GenerateStickersForOrder(order);
            _dataStore.Save();

            CartItems.Clear();
            CashGivenInput = string.Empty;
            _currentDailyOrderNumber = _dataStore.GetNextDailyOrderNumber();

            UpdateHeaderState();
            RefreshCartState();
        }
    }
}
