using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace TheCafePOS_WPF.Core;

public static class TouchInput
{
    public static readonly DependencyProperty NumericProperty = DependencyProperty.RegisterAttached("Numeric", typeof(bool), typeof(TouchInput), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.RegisterAttached("AllowNegative", typeof(bool), typeof(TouchInput), new PropertyMetadata(false));
    public static bool GetNumeric(DependencyObject obj) => (bool)obj.GetValue(NumericProperty);
    public static void SetNumeric(DependencyObject obj, bool value) => obj.SetValue(NumericProperty, value);
    public static bool GetAllowNegative(DependencyObject obj) => (bool)obj.GetValue(AllowNegativeProperty);
    public static void SetAllowNegative(DependencyObject obj, bool value) => obj.SetValue(AllowNegativeProperty, value);
    private static void Changed(DependencyObject obj, DependencyPropertyChangedEventArgs e)
    {
        if (obj is not TextBox box) return;
        if ((bool)e.NewValue) box.PreviewMouseLeftButtonUp += Open;
        else box.PreviewMouseLeftButtonUp -= Open;
    }
    private static void Open(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBox box || box.IsReadOnly) return;
        e.Handled = true;
        var dialog = new NumberPad(box.Text, GetAllowNegative(box)) { Owner = Window.GetWindow(box) };
        if (dialog.ShowDialog() == true) { box.Text = dialog.Value; box.CaretIndex = box.Text.Length; }
    }
}
public sealed class NumberPad : Window
{
    public string Value { get; private set; } = "";
    public NumberPad(string initial, bool negative)
    {
        Title = "Nhập số"; Width = 400; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(16) }; Content = panel;
        var display = new TextBox { Text = initial, IsReadOnly = true, FontSize = 28, TextAlignment = TextAlignment.Right, MinHeight = 66, Margin = new Thickness(0, 0, 0, 10) }; panel.Children.Add(display);
        bool replace = true;
        var keys = new UniformGrid { Columns = 3 }; panel.Children.Add(keys);
        foreach (var key in new[] { "7", "8", "9", "4", "5", "6", "1", "2", "3", "0", "00", "000" })
        {
            var button = new Button { Content = key, FontSize = 24, MinHeight = 60, Margin = new Thickness(3) };
            button.Click += (_, _) => { if (replace) { display.Clear(); replace = false; } if (display.Text.Length + key.Length <= 12) display.Text += key; };
            keys.Children.Add(button);
        }
        var edit = new UniformGrid { Columns = negative ? 3 : 2, Margin = new Thickness(0, 6, 0, 8) }; panel.Children.Add(edit);
        var clear = new Button { Content = "Xóa hết", Margin = new Thickness(3) }; clear.Click += (_, _) => { display.Clear(); replace = false; }; edit.Children.Add(clear);
        var back = new Button { Content = "⌫", Margin = new Thickness(3) }; back.Click += (_, _) => { if (display.Text.Length > 0) display.Text = display.Text[..^1]; replace = false; }; edit.Children.Add(back);
        if (negative) { var sign = new Button { Content = "+ / −", Margin = new Thickness(3) }; sign.Click += (_, _) => { display.Text = display.Text.StartsWith('-') ? display.Text[1..] : "-" + display.Text; replace = false; }; edit.Children.Add(sign); }
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap }; panel.Children.Add(error);
        var actions = new UniformGrid { Columns = 2 }; panel.Children.Add(actions);
        actions.Children.Add(new Button { Content = "Hủy", IsCancel = true, Margin = new Thickness(3) });
        var save = new Button { Content = "Xác nhận", Margin = new Thickness(3), IsDefault = true }; actions.Children.Add(save);
        save.Click += (_, _) =>
        {
            if (!decimal.TryParse(display.Text, out var number) || number != decimal.Truncate(number) || (!negative && number < 0)) { error.Text = "Nhập số nguyên hợp lệ."; return; }
            Value = number.ToString("0"); DialogResult = true;
        };
    }
}
