using Microsoft.Data.Sqlite;
using Xunit;

// Scenarios share TestHost, so they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

/// <summary>
/// Each test runs the real services over its own temporary database. "Restart" builds a fresh container
/// over the same directory, so persistence is checked exactly as the next app launch would see it.
/// </summary>
public sealed class RegressionTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "thecafepos-tests", Guid.NewGuid().ToString("N"));

    private void Start() => TestHost.Start(_dataDirectory);

    public void Dispose()
    {
        TestHost.Stop();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dataDirectory, true); }
        catch (IOException) { } // a leftover temp folder must not fail the run
    }

    [Fact]
    public void Core_sales_and_finance_survive_restarts()
    {
        Start(); CoreRegression.Run(reload: false);
        Start(); CoreRegression.Run(reload: true);
        Start(); FinanceRegression.Run(false);
        Start(); FinanceRegression.Run(true);
    }

    [Fact]
    public void Saved_login_survives_restart_until_revoked()
    {
        Start(); SessionRegression.Run(false);
        Start(); SessionRegression.Run(true);
    }

    [Fact]
    public void Checkout_validates_before_taking_payment() { Start(); CheckoutFlowRegression.Run(); }

    [Fact]
    public void Cart_view_model_follows_pos_rules() { Start(); CartViewModelRegression.Run(); }

    [Fact]
    public void Every_dialog_resolves_from_the_container() { Start(); DialogWiringRegression.Run(); }

    [Fact]
    public void Americano_menu_and_labels() { Start(); AmericanoRegression.Run(); }

    [Fact]
    public void Management_catalog_staff_and_inventory() { Start(); ManagementRegression.Run(); }

    [Fact]
    public void Menu_options_price_and_validate() { Start(); MenuOptionsRegression.Run(); }

    [Fact]
    public void Report_dashboard_totals() { Start(); ReportDashboardRegression.Run(); }
}
