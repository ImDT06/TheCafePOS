using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace TheCafePOS_WPF.Views.Dialogs;

// Side-panel editor shown in place of the "add" placeholder: title with close, scrolling form, delete left and save right.
// Enter saves and Esc closes while focus is inside the panel. Done(true) means the catalog changed.
public abstract class EditorPanel : UserControl
{
    private readonly TextBlock _status;
    private readonly ScrollViewer _form = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    public event Action<bool>? Done;

    protected EditorPanel(string title, string subtitle, string saveText, bool canDelete)
    {
        _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = (System.Windows.Media.Brush)FindResource("PosDanger"), Margin = new Thickness(0, 8, 0, 0) };
        var root = new DockPanel();

        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titles = new StackPanel();
        titles.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle"), Margin = new Thickness(0) });
        titles.Children.Add(new TextBlock { Text = subtitle, Foreground = (System.Windows.Media.Brush)FindResource("PosMuted"), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
        var close = new Button { Content = "×", FontSize = 20, Width = 40, MinWidth = 40, Height = 40, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top, ToolTip = "Đóng (Esc)" };
        close.Click += (_, _) => Close(false);
        Grid.SetColumn(close, 1);
        header.Children.Add(titles); header.Children.Add(close);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);

        var save = new Button { Content = saveText, Style = (Style)FindResource("PrimaryButton"), MinWidth = 130, ToolTip = "Lưu (Enter)" };
        save.Click += (_, _) => TrySave();
        var actions = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(save, Dock.Right); actions.Children.Add(save);
        if (canDelete)
        {
            var remove = new Button { Content = "Xóa", Style = (Style)FindResource("DangerButton"), HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 90 };
            remove.Click += (_, _) => Delete();
            actions.Children.Add(remove);
        }
        DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        DockPanel.SetDock(_status, Dock.Bottom); root.Children.Add(_status);
        root.Children.Add(_form);
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(false); }
            // A combo box uses Enter to pick from its open list.
            else if (e.Key == Key.Enter && e.OriginalSource is not ComboBoxItem) { e.Handled = true; TrySave(); }
        };
    }

    protected Window? Owner => Window.GetWindow(this);
    /// <summary>The fields; set by the subclass once its controls exist.</summary>
    protected FrameworkElement Form { set => _form.Content = value; }
    protected abstract void Save();
    protected virtual void Delete() { }
    protected void Close(bool changed) => Done?.Invoke(changed);
    protected void ShowError(string message) => _status.Text = message;
    protected bool Confirm(string message, string title) =>
        MessageBox.Show(Owner!, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void TrySave()
    {
        try { Save(); Close(true); }
        catch (Exception ex) { ShowError(ex.Message); }
    }
}
