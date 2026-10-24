using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TheCafePOS_WPF.Core;

// Shows a tool window's content inside another window (e.g. a management tab) instead of opening it on top.
// The tool window is never shown: its Loaded handlers run each time the content appears, and its toasts and
// confirmations go to the host through HostOf.
public static class Embedded
{
    private static readonly ConditionalWeakTable<Window, Window> Hosts = new();

    public static FrameworkElement Take(Window window, Window host)
    {
        var content = (FrameworkElement)window.Content;
        window.Content = null;
        Hosts.AddOrUpdate(window, host);
        // Enter/Esc belong to the host window, not to buttons of a dialog that is no longer one.
        foreach (var button in Descendants<Button>(content)) button.IsDefault = button.IsCancel = false;
        content.Loaded += (_, _) => window.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, window));
        return content;
    }

    public static Window HostOf(Window window) => Hosts.TryGetValue(window, out var host) ? host : window;
    public static bool IsEmbedded(Window window) => Hosts.TryGetValue(window, out _);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
