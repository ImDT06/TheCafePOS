using Microsoft.Extensions.DependencyInjection;
using TheCafePOS_WPF.Services;
using TheCafePOS_WPF.Services.Interfaces;

// The application's own service container, built on first use so THECAFEPOS_DATA_DIR is already set.
internal static class TestHost
{
    private static readonly Lazy<IServiceProvider> Services = new(TheCafePOS_WPF.App.BuildServices);

    public static LocalDatabase Database => Services.Value.GetRequiredService<LocalDatabase>();
    public static DataStoreService Store => Services.Value.GetRequiredService<DataStoreService>();
    public static AuthService Auth => Services.Value.GetRequiredService<AuthService>();
    public static HoldOrderService Holds => Services.Value.GetRequiredService<HoldOrderService>();
    public static StickerPrinterService Stickers => Services.Value.GetRequiredService<StickerPrinterService>();
    public static IVietQRService Bank => Services.Value.GetRequiredService<IVietQRService>();

    /// <summary>A second auth service over the same database, like the next app launch would create.</summary>
    public static AuthService NewAuth() => new(Database, () => Store.CurrentShift);
}
