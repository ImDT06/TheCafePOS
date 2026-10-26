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
        private readonly IBackupService _backup;
        private readonly IPurchasingService _purchasing;
        private readonly System.Windows.Threading.DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(20) };
        private readonly System.Windows.Threading.DispatcherTimer _wallClock = new() { Interval = TimeSpan.FromSeconds(1) };
        private static readonly System.Globalization.CultureInfo Vietnamese = new("vi-VN");

        private void UpdateWallClock()
        {
            var now = DateTime.Now;
            TxtClockTime.Text = now.ToString("HH:mm:ss", Vietnamese);
            TxtClockDate.Text = Vietnamese.TextInfo.ToTitleCase(now.ToString("dddd, dd/MM/yyyy", Vietnamese));
        }

        public MainWindow(MainViewModel vm, IShiftService shifts, IAuthService auth, IDialogFactory dialogs, IVietQRService bank, IBackupService backup, IPurchasingService purchasing)
        {
            _purchasing = purchasing;
            _backup = backup;
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
            if (_auth.IsManager && _purchasing.LowStockItems() is { Count: > 0 } low)
                Toast.Show(this, $"{low.Count} mặt hàng chạm ngưỡng tồn kho: {string.Join(", ", low.Take(3).Select(i => i.Name))}{(low.Count > 3 ? "…" : "")}", true);
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

        private void ProductTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: MenuTile tile }) return;
            if (tile.IsSoldOut) { Toast.Show(this, $"{tile.Name} đang hết hàng. Chuột phải để báo có hàng lại.", true); return; }
            // Drinks with several sizes need a choice, so they open the options dialog instead of quick-adding.
            if (tile.Product.IsBeverage && tile.Product.Sizes.Count > 1) { OpenProductOptionModal(tile.Product); return; }
            OrderItem line;
            try { line = _vm.QuickAdd(tile.Product); }
            catch (InvalidOperationException) { OpenProductOptionModal(tile.Product); return; }
            Toast.Show(this, $"Đã thêm {line.Quantity} × {tile.Name}", false, ("Tùy chỉnh", () => CustomizeLine(line)));
        }

        // The options button sits inside the tile; mark the click handled so the tile does not also quick-add.
        private void CustomizeTile_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is not FrameworkElement { DataContext: MenuTile tile }) return;
            if (tile.IsSoldOut) { Toast.Show(this, $"{tile.Name} đang hết hàng.", true); return; }
            OpenProductOptionModal(tile.Product);
        }

        // From the "added" toast: a merged line customises only the cup just added.
        private void CustomizeLine(OrderItem line)
        {
            if (!_vm.CartItems.Contains(line) || !_vm.CanEditCart) return;
            EditCartItem(new Button { Tag = line }, split: line.Quantity > 1);
        }

        private void ToggleSoldOut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: MenuTile tile }) return;
            bool wasSoldOut = tile.Product.IsSoldOut;
            _vm.ToggleSoldOut(tile.Product);
            if (tile.Product.IsSoldOut != wasSoldOut) Toast.Show(this, wasSoldOut ? $"{tile.Product.Name} đã bán lại." : $"Đã báo {tile.Product.Name} hết hàng.");
        }

        // Size only matters for drinks, where one product comes in several cups.
        private static string LineName(OrderItem item) => item.IsBeverage && item.Size.Length > 0 ? $"{item.ProductName} ({item.Size})" : item.ProductName;

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
                Toast.Show(this, $"Đã thêm {modal.ResultItem.Quantity} × {LineName(modal.ResultItem)}");
            }
        }

        #endregion

        #region Cart

        private void BtnClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;

            if (MessageBox.Show(this, "Xóa toàn bộ món trong giỏ chưa thanh toán?", "Xóa giỏ", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            _vm.ClearCart();
            Toast.Show(this, "Đã xóa giỏ hàng.");
        }

        #endregion

        #region Checkout Actions (POS-02, POS-04, POS-06, POS-09)

        private void BtnPayCash_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.BeginCheckout()) return;
            try
            {
                if (_vm.IsAppOrder) { CompletePayment("Platform"); return; }
                if (CashPaymentModal.Show(this, _vm.PayableTotal) is not { } payment) return;
                if (payment.Transfer == 0) { CompletePayment("Cash", payment.Cash); return; }
                // Mixed payment: the cash is in hand, now confirm the transfer for the rest.
                _bank.ValidateSettings();
                var qrModal = _dialogs.Create<VietQRModal>(this, payment.Transfer, _vm.CurrentDailyOrderNumber, _vm.CheckoutId);
                if (qrModal.ShowDialog() == true) CompletePayment("Mixed", payment.Cash, qrApproved: true, transferAmount: payment.Transfer);
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

        private void CompletePayment(string paymentMethod, decimal cashGiven = 0, bool qrApproved = false, decimal transferAmount = 0)
        {
            Order order;
            var lowBefore = _purchasing.LowStockItems().Select(i => (i.Kind, i.Name)).ToHashSet();
            try { order = _vm.CompleteCheckout(paymentMethod, cashGiven, qrApproved, transferAmount); }
            catch (Exception ex) { _vm.SetCheckoutError(ex.Message); return; }

            try
            {
                _vm.StartNewOrder();
                if (_vm.PrintStickers(order) is { } stickerError) Toast.Show(this, $"Đơn đã lưu, nhưng chưa tạo được dữ liệu tem: {stickerError}", true);
                OrderCompleteModal.Show(this, order, _vm.FindCustomer(order.CustomerPhone), () => _dialogs.Create<ReceiptWindow>(this, order, false).ShowDialog());
                // Only items this sale pushed under their threshold, so the cashier is not nagged on every order.
                var newlyLow = _purchasing.LowStockItems().Where(i => !lowBefore.Contains((i.Kind, i.Name))).ToList();
                if (newlyLow.Count > 0) Toast.Show(this, $"Sắp hết hàng: {string.Join(", ", newlyLow.Select(i => $"{i.Name} ({i.StockLabel})"))}", true);
                TxtSearch.Focus();
            }
            catch (Exception ex) { _vm.SetCheckoutError($"Đơn đã lưu. Không thanh toán lại. {ex.Message}"); }
        }

        #endregion

        #region Operational Actions (POS-05, POS-08, POS-10)

        // Cashier shortcuts: F2 search, F4 cash, F5 VietQR, F8 hold, F9 held orders, Ctrl+1..9 category chips in order.
        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Button? target = e.Key switch { System.Windows.Input.Key.F4 => BtnCash, System.Windows.Input.Key.F5 => BtnQr, System.Windows.Input.Key.F8 => BtnHold, System.Windows.Input.Key.F9 => BtnHeldOrders, System.Windows.Input.Key.F6 => BtnQueue, _ => null };
            int chip = e.Key is >= System.Windows.Input.Key.D1 and <= System.Windows.Input.Key.D9 ? e.Key - System.Windows.Input.Key.D1
                : e.Key is >= System.Windows.Input.Key.NumPad1 and <= System.Windows.Input.Key.NumPad9 ? e.Key - System.Windows.Input.Key.NumPad1 : -1;
            if (chip >= 0 && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                if (chip < _vm.CategoryList.Count) _vm.SelectCategory(_vm.CategoryList[chip].Id);
                e.Handled = true;
            }
            else if (e.Key == System.Windows.Input.Key.F2) { TxtSearch.Focus(); TxtSearch.SelectAll(); e.Handled = true; }
            else if (target is { IsEnabled: true }) { target.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
        }

        private void BtnDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;
            var modal = new DiscountModal();
            if (!modal.Show(this, _vm.Subtotal, _vm.Discount, _vm.DiscountReason)) return;
            if (_vm.DiscountNeedsApproval(modal.Amount) && _dialogs.Create<PinApprovalModal>(this, _vm.DiscountApprovalAction(modal.Amount, modal.Reason)).ShowDialog() != true) return;
            _vm.ApplyDiscount(modal.Amount, modal.Reason);
            Toast.Show(this, modal.Amount > 0 ? $"Đã giảm {modal.Amount:N0}đ ({modal.Reason.Trim()})." : "Đã bỏ giảm giá.");
        }

        private void BtnVoucher_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;
            if (!VoucherModal.Show(this, _vm.VoucherCode, _vm.ApplyVoucher)) return;
            Toast.Show(this, _vm.VoucherCode.Length > 0 ? $"Đã áp dụng voucher {_vm.VoucherCode}." : "Đã bỏ voucher.");
        }

        private void BtnSplitBill_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.CartItems.Count < 2) { Toast.Show(this, "Cần ít nhất 2 dòng món để tách hóa đơn. Dùng \"Tách 1 sản phẩm\" để tách dòng nhiều ly.", true); return; }
            if (SplitBillModal.Show(this, _vm.CartItems.ToList()) is not { } moving) return;
            try
            {
                if (_vm.SplitBill(moving) is { } error) { Toast.Show(this, error, true); return; }
            }
            catch (Exception ex) { Toast.Show(this, "Không thể tách hóa đơn: " + ex.Message, true); return; }
            Toast.Show(this, $"Đã tách {moving.Count} món sang đơn giữ (F9 để gọi lại).");
        }

        private void BtnCustomer_Click(object sender, RoutedEventArgs e)
        {
            var modal = _dialogs.Create<CustomerModal>(null);
            if (!modal.Show(this, _vm.PayableTotal + _vm.RedeemValue, _vm.CustomerPhone, _vm.RedeemPoints)) return;
            _vm.SetCustomer(modal.Phone, modal.RedeemPoints);
            if (string.IsNullOrEmpty(modal.Phone)) { Toast.Show(this, "Đã bỏ khách hàng khỏi đơn."); return; }
            string name = _vm.FindCustomer(modal.Phone)?.Name ?? modal.Phone;
            Toast.Show(this, modal.RedeemPoints > 0 ? $"Đã gắn khách {name}, đổi {modal.RedeemPoints} điểm." : $"Đã gắn khách {name}.");
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
                Toast.Show(this, $"Đã gọi lại đơn giữ ({modal.SelectedHeldOrder.Items.Sum(i => i.Quantity)} món).");
            }
            _vm.Refresh(); // the held-order count changes after a recall or discard
        }

        private void BtnReprintSticker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PrintedStickerInfo sticker)
            {
                var modal = _dialogs.Create<ReprintStickerModal>(this, new[] { sticker });
                modal.ShowDialog();
            }
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
                // The shift's sales are final now: copy them off the machine without holding up the till.
                _ = System.Threading.Tasks.Task.Run(_backup.BackupAfterShiftQuietlyAsync);
                RefreshSession();
            }
        }

        #endregion
    }
}


