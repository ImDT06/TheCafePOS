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
using TheCafePOS_WPF.Views.Dialogs;

namespace TheCafePOS_WPF
{
    public partial class MainWindow : Window
    {
        private readonly IDataStoreService _store;
        private readonly IAuthService _auth;
        private readonly MainViewModel _vm;
        private readonly System.Windows.Threading.DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(20) };
        private readonly System.Windows.Threading.DispatcherTimer _wallClock = new() { Interval = TimeSpan.FromSeconds(1) };
        private static readonly System.Globalization.CultureInfo Vietnamese = new("vi-VN");

        private void UpdateWallClock()
        {
            var now = DateTime.Now;
            TxtClockTime.Text = now.ToString("HH:mm:ss", Vietnamese);
            TxtClockDate.Text = Vietnamese.TextInfo.ToTitleCase(now.ToString("dddd, dd/MM/yyyy", Vietnamese));
        }

        public MainWindow(MainViewModel vm, IDataStoreService store, IAuthService auth)
        {
            _vm = vm;
            _store = store;
            _auth = auth;
            InitializeComponent();
            DataContext = _vm;
            LbCartItems.ItemsSource = _vm.CartItems;
            _vm.CartItems.CollectionChanged += (_, _) => UpdateOrderHeaderInfo();
            _clock.Tick += (_, _) => UpdateOrderHeaderInfo();
            _clock.Start();
            UpdateWallClock();
            _wallClock.Tick += (_, _) => UpdateWallClock();
            _wallClock.Start();
            Closed += (_, _) => { _clock.Stop(); _wallClock.Stop(); };
            LvRecentStickers.ItemsSource = _vm.RecentStickers;

            Loaded += MainWindow_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            LoadChannels();
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
            UpdateOrderHeaderInfo();
            RenderCategories();
            RenderProducts();
            UpdateCartTotal();

            RefreshSession();
        }

        private void UpdateOrderHeaderInfo()
        {
            // The order "exists" only once the first item is added; until then show guidance instead of a time.
            if (_vm.CartStartedAt is DateTime started)
            {
                int minutes = (int)(DateTime.Now - started).TotalMinutes;
                TxtCartTitle.Text = $"Đơn #{_vm.CurrentDailyOrderNumber:D2}";
                TxtCartTime.Text = $"Bắt đầu {started:HH:mm} ({(minutes < 1 ? "vừa xong" : $"{minutes} phút trước")})";
            }
            else
            {
                TxtCartTitle.Text = "Đơn mới";
                TxtCartTime.Text = "Chọn món bên trái để bắt đầu";
            }
            BtnHeldOrders.Content = IconContent.Of(MaterialDesignThemes.Wpf.PackIconKind.ClipboardClockOutline, $"Đơn giữ: {_vm.HeldOrderCount} (F9)");
        }

        #region Category & Product Rendering (POS-01)

        private void RenderCategories()
        {
            PnlCategories.Children.Clear();

            foreach (var cat in _vm.CategoryOptions)
            {
                bool isSelected = cat.Id == _vm.SelectedCategoryId;
                var btn = new Button
                {
                    Content = cat.Name,
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = isSelected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F6F3")),
                    Foreground = isSelected ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#183D34")),
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8C8BF")),
                    BorderThickness = new Thickness(1),
                    Tag = cat.Id,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                btn.Click += CategoryButton_Click;
                PnlCategories.Children.Add(btn);
            }
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string catId)
            {
                _vm.SelectCategory(catId);
                RenderCategories();
                RenderProducts();
                PnlCategories.UpdateLayout();
                PnlCategories.Children.OfType<Button>().FirstOrDefault(b => Equals(b.Tag, catId))?.BringIntoView();
            }
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

        private void RenderProducts()
        {
            if (PnlProducts == null) return;
            PnlProducts.Children.Clear();
            // Sold-out items stay visible (dimmed, last) so the cashier can tell the customer right away.
            var products = _vm.FilterProducts(TxtSearch?.Text);
            if (TxtProductCount != null) TxtProductCount.Text = $"{products.Count} món";
            if (PnlNoProducts != null) PnlNoProducts.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var p in products)
            {
                Brush Res(string key) => (Brush)FindResource(key);
                var productImage = LoadProductImage(p.ImageUrl);
                // The Border is the sizing host that ProductScroller_SizeChanged resizes; the tile look lives in the ProductTile style.
                var card = new Border { Width = ProductCardWidth(), Height = productImage != null ? 226 : 128 };
                var grid = new Grid();
                if (productImage != null)
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(122) });
                    grid.Children.Add(new Border { Background = Res("PosSubtle"), CornerRadius = new CornerRadius(11, 11, 0, 0), Child = new Image { Source = productImage, Stretch = Stretch.Uniform, Margin = new Thickness(10) } });
                }
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var txtName = new TextBlock { Text = p.Name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Res("PosInk"), TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 42, Margin = new Thickness(12, 10, 12, 0) };
                Grid.SetRow(txtName, grid.RowDefinitions.Count - 2);

                var bottomRow = new Grid { Margin = new Thickness(12, 6, 10, 10) };
                Grid.SetRow(bottomRow, grid.RowDefinitions.Count - 1);
                bottomRow.ColumnDefinitions.Add(new ColumnDefinition());
                bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var price = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                price.Children.Add(new TextBlock { Text = p.FormattedPrice, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Res("PosAccent") });
                var meta = new List<string>();
                if (p.OriginalPrice > p.BasePrice) meta.Add($"{p.OriginalPrice:N0}đ");
                if (p.IsBeverage && p.Sizes.Count > 1) meta.Add($"Size {p.DefaultSize}");
                if (meta.Count > 0)
                    price.Children.Add(new TextBlock { Text = string.Join("  ", meta), FontSize = 12, Foreground = Res("PosMuted"), TextDecorations = p.OriginalPrice > p.BasePrice && meta.Count == 1 ? TextDecorations.Strikethrough : null });
                if (p.IsSoldOut) price.Children.Add(new TextBlock { Text = "Hết hàng", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Res("PosDanger") });
                bottomRow.Children.Add(price);

                var btnAdd = new Border
                {
                    Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = p.IsSoldOut ? Res("PosBorder") : Res("PosAccent"), VerticalAlignment = VerticalAlignment.Bottom,
                    Child = new MaterialDesignThemes.Wpf.PackIcon { Kind = MaterialDesignThemes.Wpf.PackIconKind.Plus, Width = 20, Height = 20, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                };
                Grid.SetColumn(btnAdd, 1);
                bottomRow.Children.Add(btnAdd);

                grid.Children.Add(txtName);
                grid.Children.Add(bottomRow);
                card.Child = grid;

                var productButton = new Button { Content = card, Style = (Style)FindResource("ProductTile"), ToolTip = p.Name, Opacity = p.IsSoldOut ? 0.6 : 1 };
                System.Windows.Automation.AutomationProperties.SetName(productButton, p.Name + " " + p.FormattedPrice);
                productButton.Click += (s, e) =>
                {
                    if (p.IsSoldOut) Toast.Show(this, $"{p.Name} đang hết hàng. Chuột phải để báo có hàng lại.", true);
                    else OpenProductOptionModal(p);
                };
                var toggleSoldOut = new MenuItem { Header = p.IsSoldOut ? "Báo có hàng lại" : "Đánh dấu hết hàng" };
                toggleSoldOut.Click += (s, e) =>
                {
                    try { _vm.ToggleSoldOut(p); RenderProducts(); }
                    catch (Exception ex) { Toast.Show(this, ex.Message, true); }
                };
                productButton.ContextMenu = new ContextMenu { Items = { toggleSoldOut } };
                if (p.IsSoldOut) System.Windows.Automation.AutomationProperties.SetName(productButton, p.Name + " hết hàng");

                PnlProducts.Children.Add(productButton);
            }
        }

        internal static System.Windows.Media.Imaging.BitmapImage? LoadProductImage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, path));
                if (!System.IO.File.Exists(fullPath)) return null;
                using var stream = System.IO.File.OpenRead(fullPath);
                var image = new System.Windows.Media.Imaging.BitmapImage();
                image.BeginInit();
                image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 400;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or System.IO.FileFormatException)
            {
                return null; // A missing or invalid photo must not prevent sales.
            }
        }

        private double ProductCardWidth()
        {
            double available = Math.Max(180, (ProductScroller?.ActualWidth ?? 420) - 20);
            int columns = Math.Max(1, (int)(available / 184));
            return Math.Max(150, available / columns - 14);
        }

        private void ProductScroller_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (PnlProducts == null) return;
            foreach (var button in PnlProducts.Children.OfType<Button>())
                if (button.Content is Border card) card.Width = ProductCardWidth();
        }

        private void ResetProductFilter_Click(object sender, RoutedEventArgs e)
        {
            _vm.SelectCategory(MainViewModel.AllCategories);
            TxtSearch.Clear();
            RenderCategories();
            RenderProducts();
            TxtSearch.Focus();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            RenderProducts();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            TxtSearch.Focus();
            RenderProducts();
        }

        private void OpenProductOptionModal(Product p)
        {
            var modal = new OptionCustomModal(p, _vm.CurrentDailyOrderNumber) { Owner = this };
            if (modal.ShowDialog() == true && modal.ResultItem != null)
            {
                _vm.AddItem(modal.ResultItem);
                UpdateCartTotal();
            }
        }

        #endregion

        #region Cart & Calculations

        private void UpdateCartTotal(bool clearPaymentError = true)
        {
            if (clearPaymentError) _vm.SetCheckoutError(null);
            if (_vm.DropInvalidAdjustments()) Toast.Show(this, "Đã bỏ giảm giá / đổi điểm vì vượt quá tạm tính.", true);
            decimal total = _vm.PayableTotal;
            decimal subtotal = _vm.Subtotal;
            bool appOrder = _vm.IsAppOrder;

            if (PnlEmptyCart != null)
            {
                PnlEmptyCart.Visibility = _vm.HasItems ? Visibility.Collapsed : Visibility.Visible;
            }

            TxtItemCountSummary.Text = $"Tổng cộng ({_vm.TotalCups} món)";
            BtnHold.IsEnabled = BtnQr.IsEnabled = BtnCash.IsEnabled = _vm.HasItems && !_vm.CheckoutInProgress;
            LbCartItems.IsEnabled = PnlCheckout.IsEnabled = BtnClearCart.IsEnabled = !_vm.CheckoutInProgress;
            // Progressive disclosure: payment controls appear only once there is something to pay for.
            var hasItems = _vm.HasItems ? Visibility.Visible : Visibility.Collapsed;
            BtnClearCart.Visibility = PnlCheckout.Visibility = PnlPayButtons.Visibility = hasItems;
            var promos = _vm.Promotions;
            TxtPromoLine.Text = $"Khuyến mãi ({promos.Names}): −{promos.Amount:N0}đ";
            TxtChannelLine.Text = $"Giá kênh {_vm.Channel}: +{_vm.ChannelSurcharge:N0}đ (khách trả qua ứng dụng)";
            TxtChannelLine.Visibility = appOrder ? Visibility.Visible : Visibility.Collapsed;
            BtnDiscount.Visibility = BtnCustomer.Visibility = appOrder ? Visibility.Collapsed : Visibility.Visible;
            ServiceTypeRow.IsEnabled = !appOrder;
            BtnQr.Visibility = appOrder ? Visibility.Collapsed : Visibility.Visible;
            BtnCash.Content = appOrder ? IconContent.Of(MaterialDesignThemes.Wpf.PackIconKind.Moped, $"Ghi nhận đơn {_vm.Channel} (F4)") : IconContent.Of(MaterialDesignThemes.Wpf.PackIconKind.Cash, "Tiền mặt (F4)");
            TxtPromoLine.Visibility = promos.Amount > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtLoyaltyLine.Text = $"Đổi {_vm.RedeemPoints} điểm −{_vm.RedeemValue:N0}đ";
            TxtLoyaltyLine.Visibility = _vm.RedeemPoints > 0 ? Visibility.Visible : Visibility.Collapsed;
            var member = _vm.Member;
            TxtCustomerLine.Text = member is null ? "" : $"{member.Name}: {member.Points} điểm, hạng {member.Tier}";
            TxtCustomerLine.Visibility = member is null ? Visibility.Collapsed : Visibility.Visible;
            // Subtotal is only worth a line when something changes it.
            TxtSubtotalLine.Visibility = subtotal != total ? Visibility.Visible : Visibility.Collapsed;
            BtnCustomer.Content = IconContent.Of(MaterialDesignThemes.Wpf.PackIconKind.AccountOutline, member is null ? "Thành viên" : "Đổi khách");
            TxtSubtotalLine.Text = $"Tạm tính {subtotal:N0}đ";
            TxtDiscountLine.Text = $"Giảm giá −{_vm.Discount:N0}đ ({_vm.DiscountReason})";
            TxtDiscountLine.Visibility = _vm.Discount > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnDiscount.Content = IconContent.Of(MaterialDesignThemes.Wpf.PackIconKind.TagOutline, _vm.Discount > 0 ? "Sửa giảm giá" : "Giảm giá");
            BtnDiscount.IsEnabled = _vm.HasItems;
            UpdateServiceTypeButtons();
            UpdateCashSuggestions(total);
            TxtGrandTotal.Text = $"{total:N0}đ";
            Core.TouchInput.SetDue(TxtCashGiven, total);

            CalculateChange();
            UpdateCashShortcutButtons();
            RefreshCheckoutNotice();
        }

        private void BtnMinusItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderItem item)
            {
                if (_vm.DecreaseQuantity(item))
                {
                    LbCartItems.Items.Refresh();
                    UpdateCartTotal();
                }
            }
        }

        private void BtnPlusItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderItem item)
            {
                if (!_vm.IncreaseQuantity(item)) { Toast.Show(this, $"Tối đa {MainViewModel.MaxLineQuantity} sản phẩm trên một dòng.", true); return; }
                LbCartItems.Items.Refresh();
                UpdateCartTotal();
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderItem item)
            {
                // Remove immediately, offer undo instead of a confirmation dialog.
                if (_vm.RemoveItem(item) is not { } undo) return;
                UpdateCartTotal();
                Toast.Show(this, $"Đã xóa {item.Quantity}× {item.ProductName}", action: ("Hoàn tác", () =>
                {
                    undo();
                    UpdateCartTotal();
                }));
            }
        }

        private void BtnClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;

            if (MessageBox.Show(this, "Xóa toàn bộ món trong giỏ chưa thanh toán?", "Xóa giỏ", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                _vm.ClearCart(); TxtCashGiven.Clear(); UpdateCartTotal();
            }
        }

        #endregion

        #region Cash Shortcuts (POS-07)

        private void BtnCashShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagVal && decimal.TryParse(tagVal, out decimal val))
            {
                decimal total = _vm.PayableTotal;
                decimal cashGiven = (val == 0) ? total : val;
                TxtCashGiven.Text = cashGiven.ToString("F0");
                UpdateCashShortcutButtons();
            }
        }

        private void TxtCashGiven_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCashShortcutButtons();
            CalculateChange();
        }

        private void UpdateCashShortcutButtons()
        {
            if (GridCashShortcuts == null) return;

            decimal total = _vm.PayableTotal;
            decimal.TryParse(TxtCashGiven.Text, out decimal cashGiven);

            var normalBg = Brushes.White;
            var normalFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#183D34"));
            var normalBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B8C8BF"));

            var activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B"));
            var activeFg = Brushes.White;
            var activeBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B"));

            foreach (var child in GridCashShortcuts.Children)
            {
                if (child is Button btn && btn.Tag is string tagVal && decimal.TryParse(tagVal, out decimal val))
                {
                    bool isSelected = false;
                    if (!string.IsNullOrWhiteSpace(TxtCashGiven.Text))
                    {
                        if (val == 0 && total > 0 && cashGiven == total)
                        {
                            isSelected = true;
                        }
                        else if (val > 0 && cashGiven == val)
                        {
                            isSelected = true;
                        }
                    }

                    if (isSelected)
                    {
                        btn.Background = activeBg;
                        btn.Foreground = activeFg;
                        btn.BorderBrush = activeBorder;
                        btn.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        btn.Background = normalBg;
                        btn.Foreground = normalFg;
                        btn.BorderBrush = normalBorder;
                        btn.FontWeight = FontWeights.Normal;
                    }
                }
            }
        }

        private void CalculateChange()
        {
            decimal total = _vm.PayableTotal;
            if (decimal.TryParse(TxtCashGiven.Text, out decimal cashGiven))
            {
                decimal change = cashGiven - total;
                if (change >= 0)
                {
                    TxtChangeReturned.Text = $"{change:N0}đ";
                    TxtChangeReturned.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B"));
                }
                else
                {
                    TxtChangeReturned.Text = $"Thiếu {Math.Abs(change):N0}đ";
                    TxtChangeReturned.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B42318"));
                }
            }
            else
            {
                TxtChangeReturned.Text = "0đ";
                TxtChangeReturned.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B"));
            }
        }

        #endregion

        #region Checkout Actions (POS-02, POS-04, POS-06, POS-09)

        private async void BtnPayCash_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginCheckout()) return;
            try
            {
                if (_vm.IsAppOrder) { await ProcessOrderPaymentAsync("Platform"); return; }
                if (CashPaymentModal.Show(this, _vm.PayableTotal) is not decimal cash) return;
                TxtCashGiven.Text = cash.ToString("0");
                await ProcessOrderPaymentAsync("Cash");
            }
            catch (Exception ex) { ShowCheckoutError(ex.Message); }
            finally { _vm.EndCheckout(); UpdateCartTotal(false); }
        }

        private async void BtnPayVietQR_Click(object sender, RoutedEventArgs e)
        {
            if (!BeginCheckout()) return;
            try
            {
                VietQRService.ValidateSettings();
                decimal total = _vm.PayableTotal;
                var qrModal = new VietQRModal(total, _vm.CurrentDailyOrderNumber, _vm.CheckoutId) { Owner = this };
                if (qrModal.ShowDialog() == true) await ProcessOrderPaymentAsync("VietQR", true);
            }
            catch (Exception ex) { ShowCheckoutError(ex.Message); }
            finally { _vm.EndCheckout(); UpdateCartTotal(false); }
        }

        private bool BeginCheckout()
        {
            if (_vm.BeginCheckout()) { UpdateCartTotal(); return true; }
            RefreshCheckoutNotice();
            return false;
        }

        private void ShowCheckoutError(string message)
        {
            _vm.SetCheckoutError(message);
            RefreshCheckoutNotice();
        }

        private void RefreshCheckoutNotice()
        {
            TxtCheckoutNotice.Text = _vm.CheckoutNotice;
            CheckoutNotice.Visibility = string.IsNullOrEmpty(TxtCheckoutNotice.Text) ? Visibility.Collapsed : Visibility.Visible;
            BtnOpenShiftHint.Visibility = _vm.ShiftOpen ? Visibility.Collapsed : Visibility.Visible;
        }

        private Task ProcessOrderPaymentAsync(string paymentMethod, bool qrApproved = false)
        {
            Order order;
            try
            {
                decimal cashGiven = 0;
                if (paymentMethod == "Cash" && !CashAmount.TryParse(TxtCashGiven.Text, out cashGiven))
                    throw new InvalidOperationException("Vui lòng nhập số tiền khách đưa hợp lệ.");
                order = _vm.CompleteCheckout(paymentMethod, cashGiven, qrApproved);
            }
            catch (Exception ex) { ShowCheckoutError(ex.Message); return Task.CompletedTask; }

            try
            {
                _vm.StartNewOrder();
                ChannelPicker.SelectedIndex = 0;
                TxtCashGiven.Clear();
                UpdateOrderHeaderInfo(); UpdateCartTotal();
                if (_vm.PrintStickers(order) is { } stickerError) Toast.Show(this, $"Đơn đã lưu, nhưng chưa tạo được dữ liệu tem: {stickerError}", true);
                OrderCompleteModal.Show(this, order);
                TxtSearch.Focus();
            }
            catch (Exception ex) { ShowCheckoutError($"Đơn đã lưu. Không thanh toán lại. {ex.Message}"); }
            return Task.CompletedTask;
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

        private void ServiceType_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string type }) { _vm.SetServiceType(type); UpdateServiceTypeButtons(); }
        }

        private void UpdateServiceTypeButtons()
        {
            foreach (var b in new[] { BtnTakeAway, BtnDineIn })
            {
                bool active = (string)b.Tag == _vm.ServiceType;
                b.Background = active ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B")) : Brushes.White;
                b.Foreground = active ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#183D34"));
                b.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        // Suggest the next notes a customer is likely to hand over (e.g. 47k → 50k, 100k, 200k, 500k).
        private void UpdateCashSuggestions(decimal total)
        {
            var suggestions = MainViewModel.SuggestCashNotes(total);
            var buttons = GridCashShortcuts.Children.OfType<Button>().Where(b => (string)b.Tag != "0").ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].Visibility = i < suggestions.Count ? Visibility.Visible : Visibility.Hidden;
                if (i >= suggestions.Count) continue;
                buttons[i].Tag = suggestions[i].ToString("0");
                buttons[i].Content = suggestions[i] >= 1000 ? $"{suggestions[i] / 1000:0}k" : suggestions[i].ToString("N0");
            }
        }

        private void BtnDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;
            var modal = new DiscountModal();
            if (!modal.Show(this, _vm.Subtotal, _vm.Discount, _vm.DiscountReason)) return;
            if (_vm.DiscountNeedsApproval(modal.Amount) && new PinApprovalModal(_vm.DiscountApprovalAction(modal.Amount, modal.Reason)) { Owner = this }.ShowDialog() != true) return;
            _vm.ApplyDiscount(modal.Amount, modal.Reason);
            UpdateCartTotal();
        }

        private void BtnCustomer_Click(object sender, RoutedEventArgs e)
        {
            var modal = new CustomerModal();
            if (!modal.Show(this, _vm.PayableTotal + _vm.RedeemValue, _vm.CustomerPhone, _vm.RedeemPoints)) return;
            _vm.SetCustomer(modal.Phone, modal.RedeemPoints);
            UpdateCartTotal();
        }

        private void LoadChannels()
        {
            ChannelPicker.ItemsSource = _vm.ChannelOptions;
            ChannelPicker.SelectedIndex = 0;
            Title = _vm.StoreName + " — POS";
            TxtBrand.Text = _vm.StoreName;
        }

        private void ChannelPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _vm.SetChannel(ChannelPicker.SelectedItem as string);
            if (IsLoaded) UpdateCartTotal();
        }

        private void BtnQueue_Click(object sender, RoutedEventArgs e) => new OrderQueueWindow { Owner = this }.Show();

        private void BtnHoldOrder_Click(object sender, RoutedEventArgs e)
        {
            if (!_vm.HasItems) return;
            try { _vm.HoldCurrentOrder(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không thể giữ đơn"); return; }

            TxtCashGiven.Clear();
            UpdateCartTotal();
            UpdateOrderHeaderInfo();

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

            var modal = new HeldOrdersModal() { Owner = this };
            if (modal.ShowDialog() == true && modal.SelectedHeldOrder != null)
            {
                TxtCashGiven.Clear();
                _vm.RecallHeldOrder(modal.SelectedHeldOrder);
                UpdateCartTotal();
            }
            UpdateOrderHeaderInfo(); // also refreshes the held-order count after a discard
        }

        private void BtnReprintSticker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PrintedStickerInfo sticker)
            {
                var modal = new ReprintStickerModal(sticker) { Owner = this };
                modal.ShowDialog();
            }
        }

        private void BtnPackaging_Click(object sender, RoutedEventArgs e)
        {
            var modal = new PackagingStatusModal() { Owner = this };
            modal.ShowDialog();
        }

        private void BtnBlindDrop_Click(object sender, RoutedEventArgs e)
        {
            try { _store.RequireOpenShift(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
            if (_vm.HasItems) { Toast.Show(this, "Hãy giữ hoặc hoàn tất đơn trước khi chốt ca.", true); return; }
            decimal expectedCash = _store.ExpectedCash;
            var modal = new BlindDropModal(expectedCash) { Owner = this };
            if (modal.ShowDialog() == true)
            {
                Toast.Show(this, "Đã hoàn tất chốt ca mù!");
                RefreshSession();
            }
        }

        #endregion
    }
}


