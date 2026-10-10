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
using TheCafePOS_WPF.Views.Dialogs;

namespace TheCafePOS_WPF
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<OrderItem> _cartItems = new ObservableCollection<OrderItem>();
        private string _selectedCategoryId = "ALL";
        private int _currentDailyOrderNumber = 1;
        private string _checkoutId = Guid.NewGuid().ToString();
        private string _serviceType = "Mang đi";
        private decimal _discount;
        private string _discountReason = "";
        private decimal Subtotal => _cartItems.Sum(i => i.TotalPrice);
        private string _customerPhone = "";
        private int _redeemPoints;
        private Promotion? ActivePromo => DataStoreService.Instance.ActivePromotion(DataStoreService.CafeNow);
        private decimal PromoAmount => DataStoreService.PromotionDiscount(ActivePromo, Subtotal);
        private decimal PayableTotal => Subtotal - _discount - PromoAmount - _redeemPoints * DataStoreService.PointValue;

        public MainWindow()
        {
            InitializeComponent();
            LbCartItems.ItemsSource = _cartItems;
            LvRecentStickers.ItemsSource = StickerPrinterService.Instance.RecentPrintedStickers;

            Loaded += MainWindow_Loaded;
            PreviewKeyDown += MainWindow_PreviewKeyDown;
            Closing += (_, e) =>
            {
                if (_cartItems.Count == 0) return;
                try { HoldOrderService.Instance.HoldOrder(_cartItems, Subtotal, "Tự lưu khi đóng ứng dụng", _serviceType); }
                catch (Exception ex) { e.Cancel = true; MessageBox.Show(this, $"Không thể lưu giỏ hàng: {ex.Message}"); }
            };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _currentDailyOrderNumber = DataStoreService.Instance.GetNextDailyOrderNumber();
            UpdateOrderHeaderInfo();
            RenderCategories();
            RenderProducts();
            UpdateCartTotal();

            RefreshSession();
        }

        private void UpdateOrderHeaderInfo()
        {
            TxtNextOrderNumber.Text = $"Đơn mới #{_currentDailyOrderNumber:D2}";
            TxtCartTitle.Text = $"Đơn hàng #{_currentDailyOrderNumber:D2}";
            TxtCartTime.Text = $"Tạo lúc {DateTime.Now:HH:mm} · Chưa thanh toán";
            BtnHeldOrders.Content = $"Đơn giữ ({HoldOrderService.Instance.HeldOrders.Count})";
        }

        #region Category & Product Rendering (POS-01)

        private void RenderCategories()
        {
            PnlCategories.Children.Clear();

            var categories = new List<(string Id, string Name)> { ("ALL", "Tất cả") };
            categories.AddRange(DataStoreService.Instance.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).Select(c => (c.Id, c.Name)));

            foreach (var cat in categories)
            {
                bool isSelected = cat.Id == _selectedCategoryId;
                var btn = new Button
                {
                    Content = cat.Name,
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = isSelected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                    Foreground = isSelected ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155")),
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")),
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
                _selectedCategoryId = catId;
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
            string searchKeyword = TxtSearch?.Text?.Trim().ToLower() ?? "";

            var allProducts = DataStoreService.Instance.Products;
            var filtered = allProducts.Where(p =>
            {
                bool matchCat = _selectedCategoryId == "ALL" || p.CategoryId == _selectedCategoryId;
                bool matchSearch = string.IsNullOrEmpty(searchKeyword) || p.Name.ToLower().Contains(searchKeyword);
                return (!p.IsTopping || p.SoldSeparately) && p.IsActive && DataStoreService.Instance.Categories.Any(c => c.Id == p.CategoryId && c.IsActive) && matchCat && matchSearch;
            });

            // Sold-out items stay visible (dimmed, last) so the cashier can tell the customer right away.
            var products = filtered.OrderBy(p => p.IsSoldOut).ToList();
            if (TxtProductCount != null) TxtProductCount.Text = $"{products.Count} món";
            if (PnlNoProducts != null) PnlNoProducts.Visibility = products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var p in products)
            {
                var card = new Border
                {
                    Width = ProductCardWidth(),
                    Height = 142,
                    Margin = new Thickness(0),
                    CornerRadius = new CornerRadius(12),
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Padding = new Thickness(12, 10, 12, 10)
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var txtName = new TextBlock
                {
                    Text = p.Name,
                    FontSize = 15,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(txtName, 0);

                var bottomRow = new Grid();
                Grid.SetRow(bottomRow, 1);

                var txtPrice = new TextBlock
                {
                    Text = p.FormattedPrice + (p.IsBeverage && p.Sizes.Count > 1 ? $" · {p.DefaultSize}" : ""),
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B")),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var btnAdd = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8F2ED")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 3, 10, 3),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Child = new TextBlock
                    {
                        Text = "+",
                        FontSize = 18,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B"))
                    }
                };

                bottomRow.ColumnDefinitions.Add(new ColumnDefinition());
                bottomRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(btnAdd, 1);
                bottomRow.Children.Add(txtPrice);
                bottomRow.Children.Add(btnAdd);

                grid.Children.Add(txtName);
                grid.Children.Add(bottomRow);
                var productImage = LoadProductImage(p.ImageUrl);
                if (productImage != null)
                {
                    card.Height = 218;
                    grid.RowDefinitions.Insert(0, new RowDefinition { Height = new GridLength(110) });
                    Grid.SetRow(txtName, 1);
                    Grid.SetRow(bottomRow, 2);
                    grid.Children.Add(new Image { Source = productImage, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 0, 8) });
                }
                card.Child = grid;

                card.MouseEnter += (s, e) =>
                {
                    card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B"));
                    card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF7F2"));
                };
                card.MouseLeave += (s, e) =>
                {
                    card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                    card.Background = Brushes.White;
                };
                var productButton = new Button { Content = card, Padding = new Thickness(0), BorderThickness = new Thickness(1), Background = Brushes.White, Margin = new Thickness(5), ToolTip = p.Name };
                System.Windows.Automation.AutomationProperties.SetName(productButton, p.Name + " " + p.FormattedPrice);
                productButton.Click += (s, e) =>
                {
                    if (p.IsSoldOut) Toast.Show(this, $"{p.Name} đang hết hàng. Chuột phải để báo có hàng lại.", true);
                    else OpenProductOptionModal(p);
                };
                var toggleSoldOut = new MenuItem { Header = p.IsSoldOut ? "Báo có hàng lại" : "Đánh dấu hết hàng" };
                toggleSoldOut.Click += (s, e) =>
                {
                    try { DataStoreService.Instance.SetProductSoldOut(p.Id, !p.IsSoldOut); RenderProducts(); }
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
            _selectedCategoryId = "ALL";
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
            var modal = new OptionCustomModal(p, _currentDailyOrderNumber) { Owner = this };
            if (modal.ShowDialog() == true && modal.ResultItem != null)
            {
                _cartItems.Add(modal.ResultItem);
                UpdateCartTotal();
            }
        }

        #endregion

        #region Cart & Calculations

        private void UpdateCartTotal()
        {
            decimal total = PayableTotal;
            int totalCups = _cartItems.Sum(i => i.Quantity);

            if (PnlEmptyCart != null)
            {
                PnlEmptyCart.Visibility = (_cartItems.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
            }

            TxtItemCountSummary.Text = $"Tổng cộng · {totalCups} món";
            BtnHold.IsEnabled = BtnQr.IsEnabled = BtnCash.IsEnabled = _cartItems.Count > 0;
            if (_cartItems.Count == 0 || PayableTotal <= 0) { if ((_discount > 0 || _redeemPoints > 0) && _cartItems.Count > 0) Toast.Show(this, "Đã bỏ giảm giá / đổi điểm vì vượt quá tạm tính.", true); _discount = 0; _discountReason = ""; _redeemPoints = 0; total = PayableTotal; }
            var promo = ActivePromo;
            TxtPromoLine.Text = promo is null ? "" : $"{promo.Name} −{promo.Percent}%: −{PromoAmount:N0}đ";
            TxtPromoLine.Visibility = PromoAmount > 0 ? Visibility.Visible : Visibility.Collapsed;
            TxtLoyaltyLine.Text = $"Đổi {_redeemPoints} điểm −{_redeemPoints * DataStoreService.PointValue:N0}đ";
            TxtLoyaltyLine.Visibility = _redeemPoints > 0 ? Visibility.Visible : Visibility.Collapsed;
            var member = DataStoreService.Instance.FindCustomer(_customerPhone);
            TxtCustomerLine.Text = member is null ? "Khách lẻ" : $"👤 {member.Name} · {member.Points} điểm · {member.Tier}";
            BtnCustomer.Content = member is null ? "👤 Thành viên" : "Đổi khách";
            TxtSubtotalLine.Text = $"Tạm tính {Subtotal:N0}đ";
            TxtDiscountLine.Text = $"Giảm giá −{_discount:N0}đ · {_discountReason}";
            TxtDiscountLine.Visibility = _discount > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnDiscount.Content = _discount > 0 ? "Sửa giảm giá" : "＋ Giảm giá";
            BtnDiscount.IsEnabled = _cartItems.Count > 0;
            UpdateServiceTypeButtons();
            UpdateCashSuggestions(total);
            TxtGrandTotal.Text = $"{total:N0}đ";
            Core.TouchInput.SetDue(TxtCashGiven, total);

            CalculateChange();
            UpdateCashShortcutButtons();
        }

        private void BtnMinusItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderItem item)
            {
                if (item.Quantity > 1)
                {
                    item.Quantity--;
                    LbCartItems.Items.Refresh();
                    UpdateCartTotal();
                }
            }
        }

        private void BtnPlusItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderItem item)
            {
                if (item.Quantity >= 999) { Toast.Show(this, "Tối đa 999 sản phẩm trên một dòng.", true); return; }
                item.Quantity++;
                LbCartItems.Items.Refresh();
                UpdateCartTotal();
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is OrderItem item)
            {
                _cartItems.Remove(item);
                UpdateCartTotal();
            }
        }

        private void BtnClearCart_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0) return;

            if (MessageBox.Show(this, "Xóa toàn bộ món trong giỏ chưa thanh toán?", "Xóa giỏ", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes)
            {
                _cartItems.Clear(); TxtCashGiven.Clear(); UpdateCartTotal();
            }
        }

        #endregion

        #region Cash Shortcuts (POS-07)

        private void BtnCashShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tagVal && decimal.TryParse(tagVal, out decimal val))
            {
                decimal total = PayableTotal;
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

            decimal total = PayableTotal;
            decimal.TryParse(TxtCashGiven.Text, out decimal cashGiven);

            var normalBg = Brushes.White;
            var normalFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            var normalBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));

            var activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B"));
            var activeFg = Brushes.White;
            var activeBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));

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
            decimal total = PayableTotal;
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
                    TxtChangeReturned.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
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
            await ProcessOrderPaymentAsync("Cash");
        }

        private async void BtnPayVietQR_Click(object sender, RoutedEventArgs e)
        {
            try { DataStoreService.Instance.RequireOpenShift(); VietQRService.ValidateSettings(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
            if (_cartItems.Count == 0)
            {
                Toast.Show(this, "Giỏ hàng đang trống!", true);
                return;
            }

            decimal total = PayableTotal;
            var qrModal = new VietQRModal(total, _currentDailyOrderNumber, _checkoutId) { Owner = this };
            if (qrModal.ShowDialog() == true)
            {
                await ProcessOrderPaymentAsync("VietQR", true);
            }
        }

        private Task ProcessOrderPaymentAsync(string paymentMethod, bool qrApproved = false)
        {
            try
            {
                decimal cashGiven = 0;
                if (paymentMethod == "Cash" && !decimal.TryParse(TxtCashGiven.Text, out cashGiven))
                    throw new InvalidOperationException("Vui lòng nhập số tiền khách đưa hợp lệ.");
                var order = DataStoreService.Instance.Checkout(_cartItems, paymentMethod, cashGiven, _checkoutId, qrApproved, _serviceType, _discount, _discountReason, _customerPhone, _redeemPoints);
                _discount = 0; _discountReason = ""; _serviceType = "Mang đi"; _customerPhone = ""; _redeemPoints = 0;
                _cartItems.Clear();
                TxtCashGiven.Clear();
                _checkoutId = Guid.NewGuid().ToString();
                _currentDailyOrderNumber = DataStoreService.Instance.GetNextDailyOrderNumber();
                UpdateOrderHeaderInfo(); UpdateCartTotal();
                try
                {
                    StickerPrinterService.Instance.GenerateStickersForOrder(order);
                    DataStoreService.Instance.Save();
                }
                catch (Exception ex) { MessageBox.Show(this, $"Đơn đã lưu, nhưng chưa tạo được dữ liệu tem: {ex.Message}"); return Task.CompletedTask; }
                OrderCompleteModal.Show(this, order);
                TxtSearch.Focus();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Chưa hoàn tất thanh toán"); }
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
            if (sender is Button { Tag: string type }) { _serviceType = type; UpdateServiceTypeButtons(); }
        }

        private void UpdateServiceTypeButtons()
        {
            foreach (var b in new[] { BtnTakeAway, BtnDineIn })
            {
                bool active = (string)b.Tag == _serviceType;
                b.Background = active ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#12634B")) : Brushes.White;
                b.Foreground = active ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
                b.FontWeight = active ? FontWeights.Bold : FontWeights.Normal;
            }
        }

        // Suggest the next notes a customer is likely to hand over (e.g. 47k → 50k, 100k, 200k, 500k).
        private void UpdateCashSuggestions(decimal total)
        {
            var notes = new[] { 10000m, 20000m, 50000m, 100000m, 200000m, 500000m };
            var suggestions = notes.Select(n => Math.Ceiling(total / n) * n).Where(v => v > total).Distinct().OrderBy(v => v).Take(4).ToList();
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
            if (_cartItems.Count == 0) return;
            var modal = new DiscountModal();
            if (!modal.Show(this, Subtotal, _discount, _discountReason)) return;
            if (modal.Amount > 0 && new PinApprovalModal(AuthService.DiscountApprovalAction(_checkoutId, modal.Amount, modal.Reason)) { Owner = this }.ShowDialog() != true) return;
            _discount = modal.Amount; _discountReason = modal.Reason;
            UpdateCartTotal();
        }

        private void BtnCustomer_Click(object sender, RoutedEventArgs e)
        {
            var modal = new CustomerModal();
            if (!modal.Show(this, PayableTotal + _redeemPoints * DataStoreService.PointValue, _customerPhone, _redeemPoints)) return;
            _customerPhone = modal.Phone; _redeemPoints = modal.RedeemPoints;
            UpdateCartTotal();
        }

        private void BtnQueue_Click(object sender, RoutedEventArgs e) => new OrderQueueWindow { Owner = this }.Show();

        private void BtnHoldOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0) return;
            decimal total = PayableTotal;
            try { HoldOrderService.Instance.HoldOrder(_cartItems, Subtotal, serviceType: _serviceType); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không thể giữ đơn"); return; }

            _cartItems.Clear();
            TxtCashGiven.Clear();
            _checkoutId = Guid.NewGuid().ToString();
            UpdateCartTotal();
            UpdateOrderHeaderInfo();

            Toast.Show(this, "Đã tạm giữ đơn thành công!");
        }

        private void BtnHeldOrders_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count > 0) { Toast.Show(this, "Hãy giữ hoặc hoàn tất giỏ hàng hiện tại trước khi gọi đơn khác.", true); return; }
            if (HoldOrderService.Instance.HeldOrders.Count == 0)
            {
                Toast.Show(this, "Hiện không có đơn nào đang giữ!");
                return;
            }

            var modal = new HeldOrdersModal() { Owner = this };
            if (modal.ShowDialog() == true && modal.SelectedHeldOrder != null)
            {
                _checkoutId = Guid.NewGuid().ToString();
                TxtCashGiven.Clear();
                _cartItems.Clear();
                foreach (var item in modal.SelectedHeldOrder.Items)
                {
                    _cartItems.Add(item);
                }
                UpdateCartTotal();
                UpdateOrderHeaderInfo();
            }
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
            try { DataStoreService.Instance.RequireOpenShift(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); return; }
            if (_cartItems.Count > 0) { Toast.Show(this, "Hãy giữ hoặc hoàn tất đơn trước khi chốt ca.", true); return; }
            decimal expectedCash = DataStoreService.Instance.ExpectedCash;
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


