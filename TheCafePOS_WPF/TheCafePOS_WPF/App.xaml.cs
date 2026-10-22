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

        /// <summary>Builds the application's service container; regression and UI smoke harnesses use the same wiring.</summary>
        public static IServiceProvider BuildServices()
        {
            var services = new ServiceCollection();
            // Các service vẫn giữ .Instance trong giai đoạn chuyển tiếp, nên container trỏ về đúng các instance đó.
            services.AddSingleton<IDataStoreService>(_ => DataStoreService.Instance);
            // Each focused view resolves to the same store, so all of them see one consistent snapshot.
            services.AddSingleton<ICatalogService>(sp => sp.GetRequiredService<IDataStoreService>());
            services.AddSingleton<IInventoryService>(sp => sp.GetRequiredService<IDataStoreService>());
            services.AddSingleton<IShiftService>(sp => sp.GetRequiredService<IDataStoreService>());
            services.AddSingleton<IFinanceService>(sp => sp.GetRequiredService<IDataStoreService>());
            services.AddSingleton<ICustomerService>(sp => sp.GetRequiredService<IDataStoreService>());
            services.AddSingleton<IPromotionService>(sp => sp.GetRequiredService<IDataStoreService>());
            services.AddSingleton<IStoreSettingsService>(sp => sp.GetRequiredService<IDataStoreService>());
            services.AddSingleton<IOrderService>(sp => sp.GetRequiredService<IDataStoreService>());
            services.AddSingleton<IAuthService>(_ => AuthService.Instance);
            services.AddSingleton<IHoldOrderService>(_ => HoldOrderService.Instance);
            services.AddSingleton<IStickerPrinterService>(_ => StickerPrinterService.Instance);
            services.AddSingleton<Views.IDialogFactory, Views.DialogFactory>();
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
                Services = BuildServices();
                _ = Services.GetRequiredService<IDataStoreService>();
                if (!Services.GetRequiredService<IAuthService>().TryRestoreSession() && Services.GetRequiredService<Views.IDialogFactory>().Create<LoginWindow>(null).ShowDialog() != true) { Shutdown(); return; }
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

