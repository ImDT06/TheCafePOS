using System;
using System.Windows;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Views.Dialogs;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private Mutex? _instanceMutex;

        /// <summary>Composition root: mọi service và window chính được đăng ký tại đây.</summary>
        public static IServiceProvider Services { get; private set; } = null!;

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            // Các service vẫn giữ .Instance trong giai đoạn chuyển tiếp, nên container trỏ về đúng các instance đó.
            services.AddSingleton<IDataStoreService>(_ => DataStoreService.Instance);
            services.AddSingleton<IAuthService>(_ => AuthService.Instance);
            services.AddSingleton<IHoldOrderService>(_ => HoldOrderService.Instance);
            services.AddSingleton<IStickerPrinterService>(_ => StickerPrinterService.Instance);
            services.AddTransient<ViewModels.MainViewModel>();
            services.AddTransient<MainWindow>();
            return services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            System.Threading.Tasks.Task.Run(BackupService.BackupDailyQuietly);
            // Esc closes any dialog; the main POS window is never closed by keyboard.
            EventManager.RegisterClassHandler(typeof(Window), UIElement.PreviewKeyDownEvent, new System.Windows.Input.KeyEventHandler((s, k) =>
            {
                if (k.Key == System.Windows.Input.Key.Escape && s is Window w && w is not TheCafePOS_WPF.MainWindow && w.Owner is not null) { k.Handled = true; w.Close(); }
            }));
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
                Services = ConfigureServices();
                _ = Services.GetRequiredService<IDataStoreService>();
                if (!Services.GetRequiredService<IAuthService>().TryRestoreSession() && new LoginWindow().ShowDialog() != true) { Shutdown(); return; }
                MainWindow = Services.GetRequiredService<MainWindow>();
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

