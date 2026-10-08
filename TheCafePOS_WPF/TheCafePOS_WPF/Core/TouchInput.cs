using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace TheCafePOS_WPF.Core;

public static class TouchInput
{
    public static readonly RoutedUICommand OpenCommand = new("Mở bàn phím số", "OpenNumberPad", typeof(TouchInput));
    public static readonly DependencyProperty NumericProperty = DependencyProperty.RegisterAttached("Numeric", typeof(bool), typeof(TouchInput), new PropertyMetadata(false, Changed));
    public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.RegisterAttached("AllowNegative", typeof(bool), typeof(TouchInput), new PropertyMetadata(false));
    public static readonly DependencyProperty LabelProperty = DependencyProperty.RegisterAttached("Label", typeof(string), typeof(TouchInput), new PropertyMetadata("Nhập số tiền"));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.RegisterAttached("Unit", typeof(string), typeof(TouchInput), new PropertyMetadata("đ"));
    public static readonly DependencyProperty DueProperty = DependencyProperty.RegisterAttached("Due", typeof(decimal), typeof(TouchInput), new PropertyMetadata(-1m));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.RegisterAttached("Maximum", typeof(decimal), typeof(TouchInput), new PropertyMetadata(999999999999m));
    static TouchInput() => CommandManager.RegisterClassCommandBinding(typeof(TextBox), new CommandBinding(OpenCommand,
        (s, e) => { if (s is TextBox box) Open(box); e.Handled = true; },
        (s, e) => { e.CanExecute = s is TextBox box && GetNumeric(box) && !box.IsReadOnly && box.IsEnabled; e.Handled = true; }));
    public static bool GetNumeric(DependencyObject o) => (bool)o.GetValue(NumericProperty);
    public static void SetNumeric(DependencyObject o, bool v) => o.SetValue(NumericProperty, v);
    public static bool GetAllowNegative(DependencyObject o) => (bool)o.GetValue(AllowNegativeProperty);
    public static void SetAllowNegative(DependencyObject o, bool v) => o.SetValue(AllowNegativeProperty, v);
    public static string GetLabel(DependencyObject o) => (string)o.GetValue(LabelProperty);
    public static void SetLabel(DependencyObject o, string v) => o.SetValue(LabelProperty, v);
    public static string GetUnit(DependencyObject o) => (string)o.GetValue(UnitProperty);
    public static void SetUnit(DependencyObject o, string v) => o.SetValue(UnitProperty, v);
    public static decimal GetDue(DependencyObject o) => (decimal)o.GetValue(DueProperty);
    public static void SetDue(DependencyObject o, decimal v) => o.SetValue(DueProperty, v);
    public static decimal GetMaximum(DependencyObject o) => (decimal)o.GetValue(MaximumProperty);
    public static void SetMaximum(DependencyObject o, decimal v) => o.SetValue(MaximumProperty, v);
    private static void Changed(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not TextBox box) return;
        if ((bool)e.NewValue) box.PreviewTouchUp += TouchOpen;
        else box.PreviewTouchUp -= TouchOpen;
    }
    private static void TouchOpen(object? sender, TouchEventArgs e)
    {
        if (sender is not TextBox box || box.IsReadOnly) return;
        e.Handled = true;
        box.Dispatcher.BeginInvoke(new Action(() => Open(box)));
    }
    private static void Open(TextBox box)
    {
        if (!box.IsEnabled || box.IsReadOnly) return;
        var dialog = new NumberPad(box.Text, GetAllowNegative(box), GetLabel(box), GetUnit(box), GetDue(box), GetMaximum(box)) { Owner = Window.GetWindow(box) };
        // Keep the keypad close to the edited field, constrained to the desktop work area.
        var point = box.PointToScreen(new Point(box.ActualWidth, 0));
        var transform = PresentationSource.FromVisual(box)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        point = transform.Transform(point);
        var area = SystemParameters.WorkArea;
        dialog.WindowStartupLocation = WindowStartupLocation.Manual;
        dialog.Left = Math.Clamp(point.X - dialog.Width, area.Left, Math.Max(area.Left, area.Right - dialog.Width));
        dialog.Top = Math.Clamp(point.Y - dialog.Height / 2, area.Top, Math.Max(area.Top, area.Bottom - dialog.Height));
        if (dialog.ShowDialog() == true) box.Text = dialog.Value;
        box.Focus(); box.CaretIndex = box.Text.Length;
    }
}

public sealed class NumberPad : Window
{
    public string Value { get; private set; } = "";
    public TextBox Input { get; }
    public TextBlock AmountPreview { get; }
    public TextBlock BalancePreview { get; }
    public Button ApplyButton { get; }
    private readonly bool _negative;
    private readonly decimal _maximum, _due;
    private readonly string _unit;
    private readonly TextBlock _error;
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    public NumberPad(string initial, bool negative, string label = "Nhập số tiền", string unit = "đ", decimal due = -1, decimal maximum = 999999999999m)
    {
        _negative = negative; _maximum = maximum; _due = due; _unit = unit;
        Title = label; Width = due >= 0 ? 580 : 400; Height = due >= 0 ? 620 : 580;
        MinWidth = Width; MinHeight = Height;
        MaxWidth = SystemParameters.WorkArea.Width; MaxHeight = SystemParameters.WorkArea.Height;
        MinWidth = Math.Min(MinWidth, MaxWidth); MinHeight = Math.Min(MinHeight, MaxHeight);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize; Style = (Style)FindResource("PosWindow");
        var root = new Grid { Margin = new Thickness(16) }; Content = root;
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto }) root.RowDefinitions.Add(new RowDefinition { Height = height });
        root.Children.Add(new TextBlock { Text = label, FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0,0,0,8) });
        var display = new StackPanel { Margin = new Thickness(0,0,0,10) }; Grid.SetRow(display,1);root.Children.Add(display);
        Input = new TextBox { Text = initial, FontSize = 24, TextAlignment = TextAlignment.Right, MinHeight = 48, MaxLength = 13 };
        System.Windows.Automation.AutomationProperties.SetName(Input, label + " (số nguyên " + unit + ")");
        display.Children.Add(Input);
        AmountPreview = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right, Margin = new Thickness(0,4,0,0) };display.Children.Add(AmountPreview);
        var body = new Grid();Grid.SetRow(body,2);root.Children.Add(body);
        body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition { Width = due >= 0 ? new GridLength(176) : new GridLength(0) });
        var pad = new Grid { Margin = new Thickness(0,0,due >= 0 ? 10 : 0,0) };body.Children.Add(pad);
        pad.RowDefinitions.Add(new RowDefinition());pad.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var keys = new UniformGrid { Columns = 3, Rows = 4 };pad.Children.Add(keys);
        foreach (var key in new[] { "7","8","9","4","5","6","1","2","3","0","00","000" })
        {
            var button = MakeButton(key, () => Insert(key));button.FontSize=23;keys.Children.Add(button);
        }
        var edit = new UniformGrid { Columns = negative ? 3 : 2 };Grid.SetRow(edit,1);pad.Children.Add(edit);
        edit.Children.Add(MakeButton("Xóa hết", () => { Input.Clear(); Input.Focus(); }));
        edit.Children.Add(MakeButton("⌫", Backspace));
        if (negative) edit.Children.Add(MakeButton("+/−", () => { Input.Text=Input.Text.StartsWith('-') ? Input.Text[1..] : "-"+Input.Text;Input.CaretIndex=Input.Text.Length;Input.Focus(); }));
        var side=new StackPanel { Visibility=due >= 0 ? Visibility.Visible : Visibility.Collapsed };Grid.SetColumn(side,1);body.Children.Add(side);
        side.Children.Add(new TextBlock { Text="Cần thu: "+Format(due),FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap });
        BalancePreview=new TextBlock { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,8),FontWeight=FontWeights.SemiBold };side.Children.Add(BalancePreview);
        foreach(var value in new[]{due,50000m,100000m,200000m,500000m})
        {
            var button=MakeButton(value==due && side.Children.Count==2 ? "Đúng số" : Format(value),()=>SetAmount(value));
            button.ToolTip="Đặt tiền khách đưa bằng "+Format(value);side.Children.Add(button);
        }
        _error=new TextBlock { Foreground=Brushes.Firebrick,TextWrapping=TextWrapping.Wrap,MinHeight=20,Margin=new Thickness(0,6,0,4) };Grid.SetRow(_error,3);root.Children.Add(_error);
        var actions=new UniformGrid { Columns=2 };Grid.SetRow(actions,4);root.Children.Add(actions);
        actions.Children.Add(new Button { Content="Hủy",IsCancel=true,Margin=new Thickness(0,0,8,0) });
        ApplyButton=new Button { Content="Áp dụng",IsDefault=true,Style=(Style)FindResource("PrimaryButton") };actions.Children.Add(ApplyButton);
        ApplyButton.Click+=(_,_)=>{if(TryValue(out var number)){Value=number.ToString("0",CultureInfo.InvariantCulture);DialogResult=true;}};
        Input.TextChanged+=(_,_)=>Refresh();
        Loaded+=(_,_)=>{Input.Focus();Input.SelectAll();};
        Refresh();
    }
    private Button MakeButton(string text, Action action)
    {
        var button=new Button { Content=text,Margin=new Thickness(2),MinHeight=48,Padding=new Thickness(4),Focusable=false };
        System.Windows.Automation.AutomationProperties.SetName(button,text=="⌫" ? "Xóa chữ số trước con trỏ" : text);
        button.Click+=(_,_)=>action();return button;
    }
    public void Insert(string digits)
    {
        if (Input.Text.Length-Input.SelectionLength+digits.Length > Input.MaxLength) return;
        int caret=Input.SelectionStart;Input.SelectedText=digits;Input.CaretIndex=caret+digits.Length;Input.Focus();
    }
    public void Backspace()
    {
        if(Input.SelectionLength>0) Input.SelectedText="";
        else if(Input.CaretIndex>0){int caret=Input.CaretIndex;Input.Text=Input.Text.Remove(caret-1,1);Input.CaretIndex=caret-1;}
        Input.Focus();
    }
    public void SetAmount(decimal amount){Input.Text=amount.ToString("0",CultureInfo.InvariantCulture);Input.Focus();Input.SelectAll();}
    private bool TryValue(out decimal value) => decimal.TryParse(Input.Text,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out value)
        && Input.Text.TrimStart('-').All(c=>c is >= '0' and <= '9') && value==decimal.Truncate(value) && (_negative || value>=0) && value<=_maximum && value>=(_negative ? -_maximum : 0);
    private string Format(decimal number)=>number.ToString("N0",Vietnamese)+" "+_unit;
    private void Refresh()
    {
        bool valid=TryValue(out var number);ApplyButton.IsEnabled=valid;
        AmountPreview.Text=valid ? Format(number) : "—";
        _error.Text=valid || Input.Text.Length==0 ? "" : $"Nhập số nguyên {(_negative ? "" : "không âm, ")}tối đa {Format(_maximum)}.";
        BalancePreview.Text=!valid || _due<0 ? "" : number>=_due ? "Tiền thối: "+Format(number-_due) : "Còn thiếu: "+Format(_due-number);
        BalancePreview.Foreground=number>=_due ? new SolidColorBrush(Color.FromRgb(18,99,75)) : Brushes.Firebrick;
    }
}