using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TheCafePOS_WPF.Core;
using Promotion = TheCafePOS_WPF.Models.Promotion;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;
using TheCafePOS_WPF.ViewModels;
using TheCafePOS_WPF.Views;
using TheCafePOS_WPF.Views.Dialogs;

namespace TheCafePOS_WPF
{
    public partial class MainWindow : Window
    {
        private readonly IShiftService _shifts;
        private readonly IAuthService _auth;
        private readonly MainViewModel _vm;
        private readonly IDialogFactory _dialogs;
        private readonly IVietQRService _bank;
        private readonly System.Windows.Threading.DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(20) };
        private readonly System.Windows.Threading.DispatcherTimer _wallClock = new() { Interval = TimeSpan.FromSeconds(1) };
        private static readonly System.Globalization.CultureInfo Vietnamese = new("vi-VN");

        private void UpdateWallClock()
        {
            var now = DateTime.Now;
            TxtClockTime.Text = now.ToString("HH:mm:ss", Vietnamese);
            TxtClockDate.Text = Vietnamese.TextInfo.ToTitleCase(now.ToString("dddd, dd/MM/yyyy", Vietnamese));
        }

        public MainWindow(MainViewModel vm, IShiftService shifts, IAuthService auth, IDialogFactory dialogs, IVietQRService bank)
        {
            _vm = vm;
            _dialogs = dialogs;
            _bank = bank;
            _shifts = shifts;
            _auth = auth;
            InitializeComponent();
            DataContext = _vm;
            _vm.MessageRaised += (_, m) => Toast.Show(this, m.Text, m.IsError, m.Action is { } run ? (m.ActionLabel ?? "", run) : null);
            // Keeps the "started N minutes ago" text current.
            _clock.Tick += (_, _) => _vm.Refresh();
            _clock.Start();
            UpdateWallClock();
            _wallClock.Tick += (_, _) => UpdateWallClock();
            _wallClock.Start();
            Closed += (_, _) => { _clock.Stop(); _wallClock.Stop(); };
            LvRecentStickers.ItemsSource = _vm.RecentStickers;

            Loaded += MainWindow_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            LoadStoreSettings();
            Closing += (_, e) =>
            {
                if (!_vm.HasItems) return;
                try { _vm.HoldCurrentOrder("Tự lưu khi đóng ứng dụng"); }
                catch (Exception ex) { e.Cancel = true; MessageBox.Show(this, $"Không thể lưu giỏ hàng: {ex.Message}"); }
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.LoadNextOrderNumber();
            _vm.ReloadMenu();

            RefreshSession();
        }

        #region Menu (POS-01)

        public static readonly DependencyProperty TileWidthProperty =
            DependencyProperty.Register(nameof(TileWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(170d));

        /// <summary>Width of one menu tile, so the grid fills the available width in whole columns.</summary>
        public double TileWidth
        {
            get => (double)GetValue(TileWidthProperty);
            set => SetValue(TileWidthProperty, value);
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { DataContext: CategoryOption category } button) return;
            _vm.SelectCategory(category.Id);
            PnlCategories.UpdateLayout();
            button.BringIntoView();
        }

        private void PreviousCategories_Click(object sender, RoutedEventArgs e) =>
            CategoryScroller.ScrollToHorizontalOffset(CategoryScroller.HorizontalOffset - CategoryScroller.ViewportWidth * 0.8);

        private void NextCategories_Click(object sender, RoutedEventArgs e) =>
            CategoryScroller.ScrollToHorizontalOffset(CategoryScroller.HorizontalOffset + CategoryScroller.ViewportWidth * 0.8);

        private void CategoryScroller_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            CategoryScroller.ScrollToHorizontalOffset(CategoryScroller.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        private void CategoryScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (PreviousCategories == null || NextCategories == null) return;
            PreviousCategories.IsEnabled = CategoryScroller.HorizontalOffset > 0.5;
            NextCategories.IsEnabled = CategoryScroller.HorizontalOffset < CategoryScroller.ScrollableWidth - 0.5;
            var visibility = CategoryScroller.ScrollableWidth > 0.5 ? Visibility.Visible : Visibility.Hidden;
            PreviousCategories.Visibility = NextCategories.Visibility = visibility;
        }

        private void ProductTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: MenuTile tile }) return;
            if (tile.IsSoldOut) Toast.Show(this, $"{tile.Name} đang hết hàng. Chuột phải để báo có hàng lại.", true);
            else OpenProductOptionModal(tile.Product);
        }

        private void ToggleSoldOut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: MenuTile tile }) _vm.ToggleSoldOut(tile.Product);
        }

        private double ProductCardWidth()
        {
            double available = Math.Max(180, (ProductScroller?.ActualWidth ?? 420) - 20);
            int columns = Math.Max(1, (int)(available / 184));
            return Math.Max(150, available / columns - 14);
        }

        private void ProductScroller_SizeChanged(object sender, SizeChangedEventArgs e) => TileWidth = ProductCardWidth();

        private void ResetProductFilter_Click(object sender, RoutedEventArgs e)
        {
            _vm.ResetMenuFilter();
            TxtSearch.Focus();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Clear();
            TxtSearch.Focus();
        }

        private void OpenProductOptionModal(Product p)
        {
            var modal = _dialogs.Create<OptionCustomModal>(this, p, _vm.CurrentDailyOrderNumber);
            if (modal.ShowDialog() == true && modal.ResultItem != null)
            {
                _vm.AddItem(modal.ResultItem);
            }
        }

        #endregion

        #region Cart

        private void BtnClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;

            if (MessageBox.Show(this, "Xóa toàn bộ món trong giỏ chưa thanh toán?", "Xóa giỏ", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
                _vm.ClearCart();
        }

        #endregion

        #region Checkout Actions (POS-02, POS-04, POS-06, POS-09)

        private void BtnPayCash_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.BeginCheckout()) return;
            try
            {
                if (_vm.IsAppOrder) { CompletePayment("Platform"); return; }
                if (CashPaymentModal.Show(this, _vm.PayableTotal) is not decimal cash) return;
                CompletePayment("Cash", cash);
            }
            catch (Exception ex) { _vm.SetCheckoutError(ex.Message); }
            finally { _vm.EndCheckout(); }
        }

        private void BtnPayVietQR_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.BeginCheckout()) return;
            try
            {
                _bank.ValidateSettings();
                var qrModal = _dialogs.Create<VietQRModal>(this, _vm.PayableTotal, _vm.CurrentDailyOrderNumber, _vm.CheckoutId);
                if (qrModal.ShowDialog() == true) CompletePayment("VietQR", qrApproved: true);
            }
            catch (Exception ex) { _vm.SetCheckoutError(ex.Message); }
            finally { _vm.EndCheckout(); }
        }

        private void CompletePayment(string paymentMethod, decimal cashGiven = 0, bool qrApproved = false)
        {
            Order order;
            try { order = _vm.CompleteCheckout(paymentMethod, cashGiven, qrApproved); }
            catch (Exception ex) { _vm.SetCheckoutError(ex.Message); return; }

            try
            {
                _vm.StartNewOrder();
                if (_vm.PrintStickers(order) is { } stickerError) Toast.Show(this, $"Đơn đã lưu, nhưng chưa tạo được dữ liệu tem: {stickerError}", true);
                OrderCompleteModal.Show(this, order, _vm.FindCustomer(order.CustomerPhone));
                TxtSearch.Focus();
            }
            catch (Exception ex) { _vm.SetCheckoutError($"Đơn đã lưu. Không thanh toán lại. {ex.Message}"); }
        }

        #endregion

        #region Operational Actions (POS-05, POS-08, POS-10)

        // Cashier shortcuts: F2 search, F4 cash, F5 VietQR, F8 hold, F9 held orders.
        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Button? target = e.Key switch { System.Windows.Input.Key.F4 => BtnCash, System.Windows.Input.Key.F5 => BtnQr, System.Windows.Input.Key.F8 => BtnHold, System.Windows.Input.Key.F9 => BtnHeldOrders, System.Windows.Input.Key.F6 => BtnQueue, _ => null };
            if (e.Key == System.Windows.Input.Key.F2) { TxtSearch.Focus(); TxtSearch.SelectAll(); e.Handled = true; }
            else if (target is { IsEnabled: true }) { target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
        }

        private void BtnDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;
            var modal = new DiscountModal();
            if (!modal.Show(this, _vm.Subtotal, _vm.Discount, _vm.DiscountReason)) return;
            if (_vm.DiscountNeedsApproval(modal.Amount) && _dialogs.Create<PinApprovalModal>(this, _vm.DiscountApprovalAction(modal.Amount, modal.Reason)).ShowDialog() != true) return;
            _vm.ApplyDiscount(modal.Amount, modal.Reason);
        }

        private void BtnCustomer_Click(object sender, RoutedEventArgs e)
        {
            var modal = _dialogs.Create<CustomerModal>(null);
            if (!modal.Show(this, _vm.PayableTotal + _vm.RedeemValue, _vm.CustomerPhone, _vm.RedeemPoints)) return;
            _vm.SetCustomer(modal.Phone, modal.RedeemPoints);
        }

        private void LoadStoreSettings()
        {
            _vm.ReloadSettings();
            Title = _vm.StoreName + " — POS";
            TxtBrand.Text = _vm.StoreName;
        }

        private void BtnQueue_Click(object sender, RoutedEventArgs e) => _dialogs.Create<OrderQueueWindow>(this).Show();

        private void BtnHoldOrder_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;
            try { _vm.HoldCurrentOrder(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không thể giữ đơn"); return; }

            Toast.Show(this, "Đã tạm giữ đơn thành công!");
        }

        private void BtnHeldOrders_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.HasItems) { Toast.Show(this, "Hãy giữ hoặc hoàn tất giỏ hàng hiện tại trước khi gọi đơn khác.", true); return; }
            if (_vm.HeldOrderCount == 0)
            {
                Toast.Show(this, "Hiện không có đơn nào đang giữ!");
                return;
            }

            var modal = _dialogs.Create<HeldOrdersModal>(this);
            if (modal.ShowDialog() == true && modal.SelectedHeldOrder != null)
            {
                _vm.RecallHeldOrder(modal.SelectedHeldOrder);
            }
            _vm.Refresh(); // the held-order count changes after a recall or discard
        }

        private void BtnReprintSticker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PrintedStickerInfo sticker)
            {
                var modal = _dialogs.Create<ReprintStickerModal>(this, sticker);
                modal.ShowDialog();
            }
        }

        private void BtnPackaging_Click(object sender, RoutedEventArgs e)
        {
            var modal = _dialogs.Create<PackagingStatusModal>(this);
            modal.ShowDialog();
        }

        private void BtnBlindDrop_Click(object sender, RoutedEventArgs e)
        {
            try { _shifts.RequireOpenShift(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
            if (_vm.HasItems) { Toast.Show(this, "Hãy giữ hoặc hoàn tất đơn trước khi chốt ca.", true); return; }
            decimal expectedCash = _shifts.ExpectedCash;
            var modal = _dialogs.Create<BlindDropModal>(this, expectedCash);
            if (modal.ShowDialog() == true)
            {
                Toast.Show(this, "Đã hoàn tất chốt ca mù!");
                RefreshSession();
            }
        }

        #endregion
    }
}


