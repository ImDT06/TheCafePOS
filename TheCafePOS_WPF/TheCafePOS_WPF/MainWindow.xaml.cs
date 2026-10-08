using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        public MainWindow()
        {
            InitializeComponent();
            LbCartItems.ItemsSource = _cartItems;
            LvRecentStickers.ItemsSource = StickerPrinterService.Instance.RecentPrintedStickers;

            Loaded += MainWindow_Loaded;
            Closing += (_, e) =>
            {
                if (_cartItems.Count == 0) return;
                try { HoldOrderService.Instance.HoldOrder(_cartItems, _cartItems.Sum(i => i.TotalPrice), "Tự lưu khi đóng ứng dụng"); }
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
            TxtNextOrderNumber.Text = $"🎫 STT: #{_currentDailyOrderNumber:D2}";
            TxtCartTitle.Text = $"🧾 ĐƠN HÀNG #{_currentDailyOrderNumber:D2}";
            TxtCartTime.Text = $" ({DateTime.Now:HH:mm})";
            BtnHeldOrders.Content = $"⏸️ Đơn Giữ ({HoldOrderService.Instance.HeldOrders.Count})";
        }

        #region Category & Product Rendering (POS-01)

        private void RenderCategories()
        {
            PnlCategories.Children.Clear();

            var categories = new List<(string Id, string Name)> { ("ALL", "⭐ TẤT CẢ") };
            categories.AddRange(DataStoreService.Instance.Categories.Where(c => c.IsActive).OrderBy(c => c.DisplayOrder).Select(c => (c.Id, c.Name)));

            foreach (var cat in categories)
            {
                bool isSelected = cat.Id == _selectedCategoryId;
                var btn = new Button
                {
                    Content = cat.Name,
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = isSelected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
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
            }
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

            foreach (var p in filtered)
            {
                var card = new Border
                {
                    Width = 190,
                    Height = 112,
                    Margin = new Thickness(0),
                    CornerRadius = new CornerRadius(8),
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                    BorderThickness = new Thickness(1.5),
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
                    Text = p.FormattedPrice,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var btnAdd = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0F2FE")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 3, 10, 3),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Child = new TextBlock
                    {
                        Text = "+ Chọn",
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"))
                    }
                };

                bottomRow.Children.Add(txtPrice);
                bottomRow.Children.Add(btnAdd);

                grid.Children.Add(txtName);
                grid.Children.Add(bottomRow);
                card.Child = grid;

                card.MouseEnter += (s, e) =>
                {
                    card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
                    card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F9FF"));
                };
                card.MouseLeave += (s, e) =>
                {
                    card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));
                    card.Background = Brushes.White;
                };
                var productButton = new Button { Content = card, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Margin = new Thickness(5), ToolTip = p.Name };
                System.Windows.Automation.AutomationProperties.SetName(productButton, p.Name + " " + p.FormattedPrice);
                productButton.Click += (s, e) => OpenProductOptionModal(p);

                PnlProducts.Children.Add(productButton);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSearchPlaceholder != null)
            {
                TxtSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            RenderProducts();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
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
            decimal total = _cartItems.Sum(i => i.TotalPrice);
            int totalCups = _cartItems.Sum(i => i.Quantity);

            if (PnlEmptyCart != null)
            {
                PnlEmptyCart.Visibility = (_cartItems.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
            }

            TxtItemCountSummary.Text = $"TỔNG TIỀN ({totalCups} món):";
            TxtGrandTotal.Text = $"{total:N0}đ";

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
                if (item.Quantity >= 999) { MessageBox.Show(this, "Tối đa 999 sản phẩm trên một dòng."); return; }
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

            if (MessageBox.Show(this, "Xóa toàn bộ món trong giỏ chưa thanh toán?", "Xóa giỏ", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
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
                decimal total = _cartItems.Sum(i => i.TotalPrice);
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

            decimal total = _cartItems.Sum(i => i.TotalPrice);
            decimal.TryParse(TxtCashGiven.Text, out decimal cashGiven);

            var normalBg = Brushes.White;
            var normalFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#334155"));
            var normalBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1"));

            var activeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
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
            decimal total = _cartItems.Sum(i => i.TotalPrice);
            if (decimal.TryParse(TxtCashGiven.Text, out decimal cashGiven))
            {
                decimal change = cashGiven - total;
                if (change >= 0)
                {
                    TxtChangeReturned.Text = $"{change:N0}đ";
                    TxtChangeReturned.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
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
                TxtChangeReturned.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
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
                MessageBox.Show("Giỏ hàng đang trống!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal total = _cartItems.Sum(i => i.TotalPrice);
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
                var order = DataStoreService.Instance.Checkout(_cartItems, paymentMethod, cashGiven, _checkoutId, qrApproved);
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
                MessageBox.Show(this, $"Đã lưu thanh toán đơn #{order.DailyOrderNumber:D2}: {order.TotalAmount:N0}đ.\nĐã tạo dữ liệu tem (chưa kết nối máy in).", "Thanh toán thành công");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Chưa hoàn tất thanh toán"); }
            return Task.CompletedTask;
        }

        #endregion

        #region Operational Actions (POS-05, POS-08, POS-10)

        private void BtnHoldOrder_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count == 0) return;
            decimal total = _cartItems.Sum(i => i.TotalPrice);
            try { HoldOrderService.Instance.HoldOrder(_cartItems, total); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không thể giữ đơn"); return; }

            _cartItems.Clear();
            TxtCashGiven.Clear();
            _checkoutId = Guid.NewGuid().ToString();
            UpdateCartTotal();
            UpdateOrderHeaderInfo();

            MessageBox.Show("Đã tạm giữ đơn thành công!", "Giữ Đơn", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnHeldOrders_Click(object sender, RoutedEventArgs e)
        {
            if (_cartItems.Count > 0) { MessageBox.Show(this, "Hãy giữ hoặc hoàn tất giỏ hàng hiện tại trước khi gọi đơn khác."); return; }
            if (HoldOrderService.Instance.HeldOrders.Count == 0)
            {
                MessageBox.Show("Hiện không có đơn nào đang giữ!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (_cartItems.Count > 0) { MessageBox.Show(this, "Hãy giữ hoặc hoàn tất đơn trước khi chốt ca."); return; }
            decimal expectedCash = DataStoreService.Instance.ExpectedCash;
            var modal = new BlindDropModal(expectedCash) { Owner = this };
            if (modal.ShowDialog() == true)
            {
                MessageBox.Show("Đã hoàn tất chốt ca mù!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshSession();
            }
        }

        #endregion
    }
}


