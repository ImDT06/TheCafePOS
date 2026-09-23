using System;
using System.Windows;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Views.Dialogs;
using System.Threading;

namespace TheCafePOS_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex? _instanceMutex;
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, _) =>
            {
                if (sender is not Window window) return;
                var area = SystemParameters.WorkArea;
                window.MinWidth = Math.Min(window.MinWidth, area.Width);
                window.MinHeight = Math.Min(window.MinHeight, area.Height);
                window.MaxWidth = area.Width; window.MaxHeight = area.Height;
                window.UseLayoutRounding = true;
                if (window.ReadLocalValue(Window.FontSizeProperty) == DependencyProperty.UnsetValue) window.FontSize = 14;
            }));
            try
            {
                _instanceMutex = new Mutex(true, "Local\\TheCafePOS_WPF_SingleInstance", out bool created);
                if (!created) { MessageBox.Show("TheCafePOS đang chạy. Vui lòng sử dụng cửa sổ hiện có."); Shutdown(); return; }
                _ = DataStoreService.Instance;
                if (!AuthService.Instance.TryRestoreSession() && new LoginWindow().ShowDialog() != true) { Shutdown(); return; }
                MainWindow = new MainWindow();
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                MainWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể khởi động ứng dụng: {ex.Message}", "TheCafePOS");
                Shutdown();
            }
        }
    }
}

