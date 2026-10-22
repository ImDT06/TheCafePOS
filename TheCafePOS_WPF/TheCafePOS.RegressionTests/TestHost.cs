using Microsoft.Extensions.DependencyInjection;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

// The application's own service container over an isolated data directory. Scenarios read the current
// services through these properties; starting again on the same directory is what an app restart looks like.
internal static class TestHost
{
    private static ServiceProvider? _services;

    private static IServiceProvider Services => _services ?? throw new InvalidOperationException("Call TestHost.Start before using services.");

    public static void Start(string dataDirectory)
    {
        Stop();
        _services = (ServiceProvider)TheCafePOS_WPF.App.BuildServices(dataDirectory);
    }

    public static void Stop()
    {
        _services?.Dispose();
        _services = null;
    }

    public static IServiceProvider Container => Services;
    public static LocalDatabase Database => Services.GetRequiredService<LocalDatabase>();
    public static DataStoreService Store => Services.GetRequiredService<DataStoreService>();
    public static AuthService Auth => Services.GetRequiredService<AuthService>();
    public static HoldOrderService Holds => Services.GetRequiredService<HoldOrderService>();
    public static StickerPrinterService Stickers => Services.GetRequiredService<StickerPrinterService>();
    public static IVietQRService Bank => Services.GetRequiredService<IVietQRService>();

    /// <summary>A second auth service over the same database, like the next app launch would create.</summary>
    public static AuthService NewAuth() => new(Database, () => Store.CurrentShift);
}
