using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Views.Dialogs;

partial class Preview
{
    static void CheckCartFlows(TheCafePOS_WPF.MainWindow main, DataStoreService store)
    {
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
        void Flush() { main.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle); main.UpdateLayout(); }
        // Cart row buttons are bound to view model commands, which a raised Click event does not run.
        void Click(Button button) { if (button.Command is { } command) command.Execute(button.CommandParameter); else button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(); }
        // Run assertions inside modal loops, always closing the dialog even if an assertion fails.
        Exception modalFailure = null;
        void InModal<T>(Action<T> action) where T : Window => main.Dispatcher.BeginInvoke(new Action(() =>
        {
            var window = Application.Current.Windows.OfType<T>().Last(w => w.IsVisible && w != main);
            try { action(window); }
            catch (Exception ex) { modalFailure = ex; }
            finally { if (window.IsVisible) window.Close(); }
        }), DispatcherPriority.ApplicationIdle);
        void CheckModal() { if (modalFailure != null) throw modalFailure; }

        var product = store.Products.First(p => p.IsActive && p.IsBeverage && p.AllowedToppingIds.Count > 4);
        var search = (TextBox)main.FindName("TxtSearch"); search.Text = product.Name; Flush();
        var products = (Panel)main.FindName("PnlProducts");
        InModal<OptionCustomModal>(options =>
        {
            var rows = (UniformGrid)options.FindName("ToppingRows");
            Check(rows.Children.Count == 4, "Topping first page displays four items");
            var first = rows.Children[0];
            Click(Descendants<Button>(first).Last());
            Click((Button)options.FindName("NextToppings"));
            Check(rows.Children.Count is > 0 and <= 4 && rows.Children[0] != first, "Topping next page changes visible items");
            Render(options, "OptionCustomModal-toppings-page2", options.ActualWidth, options.ActualHeight - 32);
            Click((Button)options.FindName("PreviousToppings"));
            Check(rows.Children[0] == first && Descendants<TextBlock>(first).Any(t => t.Text == "1"), "Topping choice survives paging back");
            Click((Button)options.FindName("PlusQuantity"));
            Click((Button)options.FindName("ConfirmButton"));
        });
        Click(products.Children.OfType<Button>().Single()); CheckModal();
        var cart = (ListBox)main.FindName("LbCartItems");
        Check(cart.Items.Count == 1, "Selecting a product adds a cart row");
        var added = (TheCafePOS_WPF.Models.OrderItem)cart.Items[0];
        Check(added.Quantity == 2 && added.SelectedToppings.Sum(t => t.Quantity) == 1, "Cart retains quantity and selected topping");
        Button RowButton(PackIconKind kind) => Descendants<Button>(cart).First(b => b.Content is PackIcon icon && icon.Kind == kind);
        Check(Descendants<PackIcon>(cart).Count() >= 4, "Cart row renders Material action icons");
        var total = (TextBlock)main.FindName("TxtGrandTotal");
        Click(RowButton(PackIconKind.Plus));
        Check(added.Quantity == 3 && total.Text == $"{added.UnitPrice * 3:N0}đ", "Increasing cart quantity updates the displayed total");
        Click(RowButton(PackIconKind.Minus));
        Check(added.Quantity == 2 && total.Text == $"{added.UnitPrice * 2:N0}đ", "Decreasing cart quantity updates the displayed total");
        foreach (var width in new[] { 960, 1240 }) { main.Width = width; Flush(); Render(main, "MainWindow-cart-" + width, width, 648); }

        Popup OpenMore()
        {
            Click(RowButton(PackIconKind.DotsVertical));
            var root = PresentationSource.CurrentSources.Cast<PresentationSource>().Select(s => s.RootVisual)
                .First(v => v != null && Descendants<TextBlock>(v).Any(t => t.Text == "Sửa tùy chọn"));
            var border = Descendants<Border>(root).First(b => b.Parent is Popup);
            return (Popup)border.Parent;
        }
        var more = OpenMore();
        var actions = Descendants<Button>(more.Child).ToArray();
        Check(actions.Length == 2 && actions[1].IsEnabled, "More menu offers edit and split for quantity two");
        InModal<OptionCustomModal>(options => { }); Click(actions[0]); CheckModal();
        Check(cart.Items.Count == 1 && added.Quantity == 2, "Cancel editing preserves the cart");
        more = OpenMore(); actions = Descendants<Button>(more.Child).ToArray();
        InModal<OptionCustomModal>(options => Click((Button)options.FindName("ConfirmButton")));
        Click(actions[1]); CheckModal();
        Check(cart.Items.Count == 2 && cart.Items.Cast<TheCafePOS_WPF.Models.OrderItem>().All(i => i.Quantity == 1), "Split saves one unit without losing quantity");
        more = OpenMore();
        Check(!Descendants<Button>(more.Child).Last().IsEnabled, "Split is disabled for a single unit"); more.IsOpen = false;

        Button Undo() => Application.Current.Windows.Cast<Window>().Where(w => w.IsVisible)
            .SelectMany(w => Descendants<Button>(w)).Single(b => Equals(b.Content, "Hoàn tác"));
        Click(RowButton(PackIconKind.TrashCanOutline));
        Check(cart.Items.Count == 1, "Delete removes a cart row immediately"); Click(Undo());
        Check(cart.Items.Count == 2 && ReferenceEquals(cart.Items[0], added), "Undo restores the removed row at its original position");

        var cashButton = (Button)main.FindName("BtnCash");
        var notice = (TextBlock)main.FindName("TxtCheckoutNotice");
        store.CloseShift(store.ExpectedCash);
        Click(RowButton(PackIconKind.Plus)); Click(RowButton(PackIconKind.Minus));
        Check(notice.Text.Contains("Chưa mở ca") && ((Button)main.FindName("BtnOpenShiftHint")).IsVisible, "Cart prompts to open a shift before payment is attempted");
        Click(cashButton);
        Check(cart.Items.Count == 2 && notice.Text.Contains("mở ca"), "Closed-shift error stays beside the intact cart");
        store.OpenShift(0);
        var cup = store.PackagingInventory.Single(p => p.Id == added.PackagingId);
        int cupStock = cup.StockQuantity;
        store.UpdateStock(cup.Id, "Kiểm kê", 0, "UI shortage test");
        Click(cashButton);
        Check(notice.Text.Contains(cup.Name) && notice.Text.Contains("còn 0") && cart.Items.Count == 2, "Payment preview identifies the missing packaging and retains the cart");
        Render(main, "MainWindow-checkout-error", 1240, 648);
        store.UpdateStock(cup.Id, "Kiểm kê", cupStock, "UI restore stock");

        var orderCount = store.CompletedOrders.Count;
        InModal<Window>(cash =>
        {
            Check(cash.Title == "Thanh toán tiền mặt", "Cash button opens the cash dialog");
            var amount = Descendants<TextBox>(cash).Single();
            var pay = Descendants<Button>(cash).Single(b => b.IsDefault);
            Check(!cashButton.IsEnabled && !((Button)main.FindName("BtnQr")).IsEnabled, "Payment actions are locked while the cash dialog is open");
            Click(cashButton);
            Check(Application.Current.Windows.Cast<Window>().Count(w => w.IsVisible && w.Title == "Thanh toán tiền mặt") == 1, "Repeated payment event cannot open another cash dialog");
            foreach (var invalid in new[] { "", "-1", "200000.5", "200,000", "abc", "1000000000000" })
            {
                amount.Text = invalid; Click(pay);
                Check(cash.IsVisible && !pay.IsEnabled, "Cash dialog rejects invalid amount: " + invalid);
            }
            amount.Text = "1"; Click(pay);
            Check(cash.IsVisible && store.CompletedOrders.Count == orderCount, "Insufficient cash cannot submit payment");
            amount.Text = "200000"; Flush();
            Check(Descendants<TextBlock>(cash).Any(t => t.Text.StartsWith("Tiền thối:")), "Cash dialog previews change");
            Render(cash, "CashPaymentModal", cash.ActualWidth, cash.ActualHeight - 32);
        });
        Click((Button)main.FindName("BtnCash")); CheckModal();
        Check(cart.Items.Count == 2 && store.CompletedOrders.Count == orderCount, "Cancel cash payment preserves cart and orders");

        // Stock can change while a modal is open: final validation must preserve the draft on failure.
        InModal<Window>(cash =>
        {
            store.UpdateStock(cup.Id, "Kiểm kê", 0, "UI changed stock");
            Click(Descendants<Button>(cash).Single(b => b.IsDefault));
        });
        Click(cashButton); CheckModal();
        Check(cart.Items.Count == 2 && store.CompletedOrders.Count == orderCount && notice.Text.Contains(cup.Name) && cashButton.IsEnabled,
            "Checkout failure after cash confirmation preserves cart and unlocks retry");
        store.UpdateStock(cup.Id, "Kiểm kê", cupStock, "UI restore stock");

        // Undo belongs to the draft where deletion occurred, even while the toast remains visible.
        Click(RowButton(PackIconKind.TrashCanOutline)); var staleUndo = Undo();
        Click((Button)main.FindName("BtnHold")); Click(staleUndo);
        Check(cart.Items.Count == 0, "Undo from a held order cannot insert an item into the next draft");

        InModal<OptionCustomModal>(options => Click((Button)options.FindName("ConfirmButton")));
        Click(products.Children.OfType<Button>().Single()); CheckModal();
        var previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        previewTimer.Tick += (_, _) =>
        {
            var completed = Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsVisible && w.Title == "Thanh toán thành công");
            if (completed == null) return;
            previewTimer.Stop();
            Render(completed, "OrderCompleteModal-countdown", completed.ActualWidth, completed.ActualHeight - 32);
        };
        InModal<Window>(cash =>
        {
            var amount = Descendants<TextBox>(cash).Single(); amount.Text = "200000";
            // Leave the success window open so its real three-second timer is exercised.
            previewTimer.Start();
            Click(Descendants<Button>(cash).Single(b => b.IsDefault));
        });
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        Click(cashButton); previewTimer.Stop(); CheckModal(); elapsed.Stop();
        Check(elapsed.Elapsed.TotalSeconds >= 2.5 && elapsed.Elapsed.TotalSeconds < 10 && !Application.Current.Windows.Cast<Window>().Any(w => w.IsVisible && w.Title == "Thanh toán thành công"),
            "Success dialog automatically closes after approximately three seconds");
        Check(store.CompletedOrders.Count == orderCount + 1 && cart.Items.Count == 0 && !cashButton.IsEnabled,
            "Successful UI checkout saves exactly one order and clears the cart");
    }
}
