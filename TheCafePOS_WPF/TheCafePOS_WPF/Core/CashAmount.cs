using System.Globalization;

namespace TheCafePOS_WPF.Core;

public static class CashAmount
{
    public const decimal Maximum = 999999999999m;
    public static bool IsValid(decimal value) => value >= 0 && value <= Maximum && value == decimal.Truncate(value);
    public static bool TryParse(string text, out decimal value) =>
        decimal.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value) && IsValid(value);
}
