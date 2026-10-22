using Microsoft.Extensions.DependencyInjection;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.ViewModels;
using TheCafePOS_WPF.Views;
using TheCafePOS_WPF.Views.Dialogs;

// Checks the composition root without opening windows: every dialog has exactly one constructor that the
// dialog factory can satisfy from its runtime arguments plus registered services.
internal static class DialogWiringRegression
{
    public static void Run()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS " + message); }
        var services = TheCafePOS_WPF.App.BuildServices();
        var registry = services.GetRequiredService<IServiceProviderIsService>();

        void Wire<T>(params Type[] runtimeArgs)
        {
            var ctor = typeof(T).GetConstructors().Single();
            ActivatorUtilities.CreateFactory(typeof(T), runtimeArgs); // throws when no constructor accepts these arguments
            var missing = ctor.GetParameters()
                .Where(p => !runtimeArgs.Any(a => p.ParameterType.IsAssignableFrom(a)) && !p.HasDefaultValue && !registry.IsService(p.ParameterType))
                .Select(p => p.ParameterType.Name).ToList();
            Check(missing.Count == 0, $"{typeof(T).Name} resolves its services" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing) + ")" : ""));
        }

        Wire<LoginWindow>();
        Wire<PasswordWindow>();
        Wire<PasswordWindow>(typeof(string));
        Wire<PinApprovalModal>(typeof(string));
        Wire<ManagementWindow>();
        Wire<FinanceWindow>();
        Wire<ReportWindow>();
        Wire<PackagingStatusModal>();
        Wire<HeldOrdersModal>();
        Wire<BlindDropModal>(typeof(decimal));
        Wire<ReprintStickerModal>(typeof(PrintedStickerInfo));
        Wire<VietQRModal>(typeof(decimal), typeof(int), typeof(string));
        Wire<OptionCustomModal>(typeof(Product), typeof(int));
        Wire<OptionCustomModal>(typeof(Product), typeof(int), typeof(OrderItem), typeof(bool));
        Wire<ProductConfigurationWindow>(typeof(Product));
        Wire<IngredientWindow>();
        Wire<RecipeWindow>(typeof(Product));
        Wire<StoreSettingsWindow>();
        Wire<PromotionsWindow>();
        Wire<CustomerListWindow>();
        Wire<OrderQueueWindow>();
        Wire<CustomerModal>();
        Wire<TheCafePOS_WPF.MainWindow>();

        Check(services.GetRequiredService<IDialogFactory>() is DialogFactory, "Dialog factory is registered");
        Check(services.GetRequiredService<MainViewModel>() is not null, "Main view model resolves from the container");
    }
}
