using System.Windows;
using System.Windows.Media;

namespace TheCafePOS_WPF.Core;

/// <summary>
/// Brushes from Themes/Palette.xaml for UI built in code, so colours are defined in one place only.
/// Read on each use: the palette is loaded with the application resources.
/// </summary>
public static class Palette
{
    public static Brush Brush(string key) => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Black;

    public static Brush Accent => Brush("PosAccent");
    public static Brush AccentDark => Brush("PosAccentDark");
    public static Brush AccentSoft => Brush("PosAccentSoft");
    public static Brush Muted => Brush("PosMuted");
    public static Brush Surface => Brush("PosSurface");
    public static Brush Border => Brush("PosBorder");
    public static Brush Money => Brush("PosMoney");
    public static Brush Danger => Brush("PosDanger");
    public static Brush DangerSoft => Brush("PosDangerSoft");
    public static Brush WarningSoft => Brush("PosWarningSoft");
}
