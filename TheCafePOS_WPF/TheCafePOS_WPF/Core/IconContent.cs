using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace TheCafePOS_WPF.Core;

// Icon + label button content built from code, using Material Design icons so glyphs look the same on every PC.
public static class IconContent
{
    public static StackPanel Of(PackIconKind kind, string text, double size = 18) => new()
    {
        Orientation = Orientation.Horizontal,
        Children =
        {
            new PackIcon { Kind = kind, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) },
            new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }
        }
    };
}