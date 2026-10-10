using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TheCafePOS_WPF.Core;

// Non-blocking notification shown at the owner's bottom-right corner; replaces OK-only message boxes.
public static class Toast
{
    public static void Show(Window? owner, string message, bool error = false)
    {
        owner ??= Application.Current.MainWindow;
        var toast = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
            ShowInTaskbar = false, ShowActivated = false, Topmost = owner?.Topmost ?? false, SizeToContent = SizeToContent.WidthAndHeight,
            Owner = owner?.IsLoaded == true ? owner : null,
            Content = new Border
            {
                Background = new SolidColorBrush(error ? Color.FromRgb(0xB9, 0x1C, 0x1C) : Color.FromRgb(0x12, 0x63, 0x4B)),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12), MaxWidth = 420,
                Child = new TextBlock { Text = (error ? "⚠  " : "✓  ") + message, Foreground = Brushes.White, FontSize = 15, TextWrapping = TextWrapping.Wrap }
            }
        };
        toast.MouseLeftButtonUp += (_, _) => toast.Close();
        toast.Loaded += (_, _) =>
        {
            var area = owner is { IsLoaded: true, WindowState: not WindowState.Maximized } ? new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight) : SystemParameters.WorkArea;
            toast.Left = area.Right - toast.ActualWidth - 24;
            toast.Top = area.Bottom - toast.ActualHeight - 24;
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(error ? 4 : 2.5) };
        timer.Tick += (_, _) => { timer.Stop(); toast.Close(); };
        toast.Show();
        timer.Start();
    }
}

// Marks an input red with a tooltip until the user edits it again.
public static class FieldError
{
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));

    public static InvalidOperationException Mark(Control field, string message)
    {
        var original = field.BorderBrush;
        object? originalTip = field.ToolTip;
        field.BorderBrush = ErrorBrush;
        field.ToolTip = message;
        field.Focus();
        void Clear(object? s, RoutedEventArgs e)
        {
            field.BorderBrush = original; field.ToolTip = originalTip;
            field.RemoveHandler(TextBox.TextChangedEvent, (RoutedEventHandler)Clear);
            field.RemoveHandler(PasswordBox.PasswordChangedEvent, (RoutedEventHandler)Clear);
        }
        field.AddHandler(TextBox.TextChangedEvent, (RoutedEventHandler)Clear);
        field.AddHandler(PasswordBox.PasswordChangedEvent, (RoutedEventHandler)Clear);
        return new InvalidOperationException(message);
    }
}

// Shows a centered hint inside an ItemsControl while it has no items: touch:EmptyState.Text="Chưa có món".
public static class EmptyState
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(EmptyState), new PropertyMetadata(null, OnTextChanged));
    public static void SetText(DependencyObject d, string value) => d.SetValue(TextProperty, value);
    public static string GetText(DependencyObject d) => (string)d.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ItemsControl list) return;
        var hint = new VisualBrush(new TextBlock { Text = (string)e.NewValue, Foreground = Brushes.Gray, FontSize = 14, Margin = new Thickness(8) })
        { Stretch = Stretch.None, AlignmentY = AlignmentY.Center };
        void Update() { if (list.Items.Count == 0) list.Background = hint; else list.ClearValue(Control.BackgroundProperty); }
        ((System.Collections.Specialized.INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => Update();
        Update();
    }
}
