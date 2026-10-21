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
        void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush(); }
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

        var orderCount = store.CompletedOrders.Count;
        InModal<Window>(cash =>
        {
            Check(cash.Title == "Thanh toán tiền mặt", "Cash button opens the cash dialog");
            var amount = Descendants<TextBox>(cash).Single();
            var pay = Descendants<Button>(cash).Single(b => b.IsDefault);
            amount.Text = "1"; Click(pay);
            Check(cash.IsVisible && store.CompletedOrders.Count == orderCount, "Insufficient cash cannot submit payment");
            amount.Text = "200000"; Flush();
            Check(Descendants<TextBlock>(cash).Any(t => t.Text.StartsWith("Tiền thối:")), "Cash dialog previews change");
            Render(cash, "CashPaymentModal", cash.ActualWidth, cash.ActualHeight - 32);
        });
        Click((Button)main.FindName("BtnCash")); CheckModal();
        Check(cart.Items.Count == 2 && store.CompletedOrders.Count == orderCount, "Cancel cash payment preserves cart and orders");

        // Undo belongs to the draft where deletion occurred, even while the toast remains visible.
        Click(RowButton(PackIconKind.TrashCanOutline)); var staleUndo = Undo();
        Click((Button)main.FindName("BtnHold")); Click(staleUndo);
        Check(cart.Items.Count == 0, "Undo from a held order cannot insert an item into the next draft");
    }
}
