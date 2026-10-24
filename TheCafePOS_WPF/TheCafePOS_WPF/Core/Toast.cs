using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TheCafePOS_WPF.Core;

// Non-blocking notification shown at the owner's bottom-right corner; replaces OK-only message boxes.
public static class Toast
{
    // Optional action (e.g. "Hoàn tác") adds a button and keeps the toast up longer.
    // One compact row: icon, message, action pill. Confirmations stay on one line (trimmed, full text in the
    // tooltip); errors wrap so the whole reason can be read.
    public static void Show(Window? owner, string text, bool error = false, (string Text, Action Run)? action = null)
    {
        owner ??= Application.Current.MainWindow;
        var message = new TextBlock
        {
            Text = text, FontSize = 15, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 380, ToolTip = text,
            TextWrapping = error ? TextWrapping.Wrap : TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis
        };
        var row = new DockPanel { LastChildFill = true };
        var icon = Icon(error); DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon);
        Button? actionButton = null;
        if (action is { } a)
        {
            actionButton = Pill(a.Text);
            DockPanel.SetDock(actionButton, Dock.Right); row.Children.Add(actionButton);
        }
        row.Children.Add(message);
        var card = new Border
        {
            Child = row, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 10, action is null ? 18 : 10, 10), Margin = new Thickness(12),
            Background = error ? Palette.Danger : Palette.Brush("PosInk"),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.25 }
        };
        // Hosted in a borderless window so callers can notify from any screen without adding a host to every XAML.
        var toast = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
            ShowInTaskbar = false, ShowActivated = false, Topmost = owner?.Topmost ?? false, SizeToContent = SizeToContent.WidthAndHeight,
            Owner = owner?.IsLoaded == true ? owner : null, Content = card
        };
        if (action is { } act) actionButton!.Click += (_, e) => { e.Handled = true; toast.Close(); act.Run(); };
        toast.MouseLeftButtonUp += (_, _) => toast.Close();
        toast.SizeChanged += (_, _) =>
        {
            var area = owner is { IsLoaded: true, WindowState: not WindowState.Maximized } ? new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight) : SystemParameters.WorkArea;
            toast.Left = area.Right - toast.ActualWidth - 12;
            toast.Top = area.Bottom - toast.ActualHeight - 12;
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(action is not null ? 6 : error ? 4 : 2.5) };
        timer.Tick += (_, _) => { timer.Stop(); toast.Close(); };
        toast.Closed += (_, _) => timer.Stop();
        toast.Show();
        timer.Start();
    }

    private static MaterialDesignThemes.Wpf.PackIcon Icon(bool error) => new()
    {
        Kind = error ? MaterialDesignThemes.Wpf.PackIconKind.AlertCircleOutline : MaterialDesignThemes.Wpf.PackIconKind.CheckCircle,
        Width = 20, Height = 20, Foreground = error ? Brushes.White : Palette.Brush("PosMoneySoft"), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center
    };

    // Rounded action on the dark toast: white text on a faint white fill that brightens on hover.
    private static Button Pill(string text)
    {
        var fill = new FrameworkElementFactory(typeof(Border));
        fill.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        fill.SetValue(Border.PaddingProperty, new Thickness(12, 5, 12, 5));
        fill.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Control.Background)) { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        fill.AppendChild(presenter);
        var idle = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        var hover = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        var button = new Button
        {
            Content = new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White },
            Template = new ControlTemplate(typeof(Button)) { VisualTree = fill }, Background = idle,
            // The app-wide button style sets a 48px touch height; this pill sits inside an already tappable toast.
            MinHeight = 0, MinWidth = 0, Height = 32,
            Margin = new Thickness(14, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Focusable = false
        };
        button.MouseEnter += (_, _) => button.Background = hover;
        button.MouseLeave += (_, _) => button.Background = idle;
        return button;
    }
}

// Marks an input red with a tooltip until the user edits it again.
public static class FieldError
{
    private static Brush ErrorBrush => Palette.Danger;

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
        var hint = new VisualBrush(new TextBlock { Text = (string)e.NewValue, Foreground = Palette.Muted, FontSize = 14, Margin = new Thickness(8) })
        { Stretch = Stretch.None, AlignmentY = AlignmentY.Center };
        void Update() { if (list.Items.Count == 0) list.Background = hint; else list.ClearValue(Control.BackgroundProperty); }
        ((System.Collections.Specialized.INotifyCollectionChanged)list.Items).CollectionChanged += (_, _) => Update();
        Update();
    }
}
