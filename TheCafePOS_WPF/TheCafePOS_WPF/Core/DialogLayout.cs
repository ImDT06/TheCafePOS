using System.Windows;
using System.Windows.Controls;

namespace TheCafePOS_WPF.Core;

internal static class DialogLayout
{
    public static void Apply(Window window, string title, string description, FrameworkElement form, FrameworkElement actions)
    {
        window.Style = (Style)window.FindResource("PosWindow");
        window.MaxWidth = SystemParameters.WorkArea.Width;
        window.MaxHeight = SystemParameters.WorkArea.Height;
        window.Width = Math.Min(window.Width, window.MaxWidth);
        window.Height = Math.Min(window.Height, window.MaxHeight);
        window.MinWidth = Math.Min(window.Width, SystemParameters.WorkArea.Width);
        window.MinHeight = Math.Min(380, SystemParameters.WorkArea.Height);
        var root = new DockPanel { Margin = new Thickness(24) };
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = title, Style = (Style)window.FindResource("PageTitle") });
        heading.Children.Add(new TextBlock { Text = description, Style = (Style)window.FindResource("Hint") });
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        actions.Margin = new Thickness(0, 16, 0, 0);
        DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        form.Margin = new Thickness(0);
        root.Children.Add(new Border {
            Style = (Style)window.FindResource("SurfaceCard"),
            Child = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }
        });
        window.Content = root;
    }
}
