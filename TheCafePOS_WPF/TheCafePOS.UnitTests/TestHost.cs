using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TheCafePOS_WPF.Services;

// The application's own service container over a throwaway data directory, so tests never touch the shop's data.
internal sealed class TestHost : IDisposable
{
    public const string Owner = "owner", OwnerPass = "OwnerPass!2026";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "thecafepos-unit", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;

    public TestHost()
    {
        _services = (ServiceProvider)TheCafePOS_WPF.App.BuildServices(_dataDirectory);
        Auth.CreateAccount(Owner, OwnerPass, "Owner");
        Auth.Login(Owner, OwnerPass);
    }

    public DataStoreService Store => _services.GetRequiredService<DataStoreService>();
    public AuthService Auth => _services.GetRequiredService<AuthService>();

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dataDirectory, true); }
        catch (IOException) { }
    }
}
