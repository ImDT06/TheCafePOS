using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TheCafePOS_WPF.Core;

// Non-blocking notification shown at the owner's bottom-right corner; replaces OK-only message boxes.
public static class Toast
{
    // Optional action (e.g. "Hoàn tác") adds a button and keeps the toast up longer.
    public static void Show(Window? owner, string text, bool error = false, (string Text, Action Run)? action = null)
    {
        owner ??= Application.Current.MainWindow;
        var message = new MaterialDesignThemes.Wpf.SnackbarMessage
        {
            Content = new DockPanel { Children = { Icon(error), new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 15, VerticalAlignment = VerticalAlignment.Center } } },
            ActionContent = action?.Text
        };
        var snackbar = new MaterialDesignThemes.Wpf.Snackbar
        {
            Message = message, IsActive = true, MaxWidth = 460, Margin = new Thickness(0),
            Background = error ? Palette.Danger : Palette.AccentDark, Foreground = Brushes.White
        };
        // Hosted in a borderless window so callers can notify from any screen without adding a host to every XAML.
        var toast = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
            ShowInTaskbar = false, ShowActivated = false, Topmost = owner?.Topmost ?? false, SizeToContent = SizeToContent.WidthAndHeight,
            Owner = owner?.IsLoaded == true ? owner : null, Content = snackbar
        };
        if (action is { } act) message.ActionClick += (_, _) => { toast.Close(); act.Run(); };
        toast.MouseLeftButtonUp += (_, _) => toast.Close();
        toast.SizeChanged += (_, _) =>
        {
            var area = owner is { IsLoaded: true, WindowState: not WindowState.Maximized } ? new Rect(owner.Left, owner.Top, owner.ActualWidth, owner.ActualHeight) : SystemParameters.WorkArea;
            toast.Left = area.Right - toast.ActualWidth - 24;
            toast.Top = area.Bottom - toast.ActualHeight - 24;
        };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(action is not null ? 6 : error ? 4 : 2.5) };
        timer.Tick += (_, _) => { timer.Stop(); toast.Close(); };
        toast.Closed += (_, _) => timer.Stop();
        toast.Show();
        timer.Start();
    }

    private static MaterialDesignThemes.Wpf.PackIcon Icon(bool error) => new()
    {
        Kind = error ? MaterialDesignThemes.Wpf.PackIconKind.AlertCircleOutline : MaterialDesignThemes.Wpf.PackIconKind.CheckCircleOutline,
        Width = 20, Height = 20, Foreground = Brushes.White, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center
    };
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
