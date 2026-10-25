using Microsoft.Data.Sqlite;
using TheCafePOS_WPF.Services;
using Xunit;

/// <summary>
/// Staff take turns at the shared till by tapping their name and typing a 4–6 digit PIN.
/// The owner keeps signing in and approving with the full password.
/// </summary>
public sealed class PinLoginTests : IDisposable
{
    private const string Owner = "pin-owner", OwnerPass = "PinOwner!2026", Staff = "pin-staff", StaffPass = "PinStaff!2026";
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "thecafepos-tests", Guid.NewGuid().ToString("N"));
    private AuthService Auth => TestHost.Auth;

    public PinLoginTests()
    {
        TestHost.Start(_dataDirectory);
        Auth.CreateAccount(Owner, OwnerPass, "Owner");
        Auth.Login(Owner, OwnerPass);
        Auth.CreateAccount(Staff, StaffPass, "Cashier");
    }

    public void Dispose()
    {
        TestHost.Stop();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dataDirectory, true); }
        catch (IOException) { }
    }

    [Theory]
    [InlineData("1234", true)]
    [InlineData("123456", true)]
    [InlineData("123", false)]
    [InlineData("1234567", false)]
    [InlineData("12a4", false)]
    [InlineData("", false)]
    public void Pin_is_four_to_six_digits(string pin, bool valid) => Assert.Equal(valid, AuthService.IsValidPin(pin));

    [Fact]
    public void Owner_sets_staff_pin_and_staff_signs_in_with_it()
    {
        Auth.SetPin(Staff, "2468");
        Assert.True(Auth.Accounts.Single(a => a.Username == Staff).HasPin);

        Auth.LoginWithPin(Staff, "2468");
        Assert.Equal(Staff, Auth.CurrentUser?.Username);
        Assert.False(Auth.IsManager);
    }

    [Fact]
    public void Pin_survives_restart_and_is_stored_hashed()
    {
        Auth.SetPin(Staff, "2468");
        var stored = Auth.Accounts.Single(a => a.Username == Staff);
        Assert.NotEqual("2468", stored.PinHash);
        Assert.NotEmpty(stored.PinSalt);

        var restarted = TestHost.NewAuth();
        restarted.LoginWithPin(Staff, "2468");
        Assert.Equal(Staff, restarted.CurrentUser?.Username);
    }

    [Fact]
    public void Wrong_pin_is_rejected_and_five_misses_lock_the_account()
    {
        Auth.SetPin(Staff, "2468");
        for (int i = 0; i < 5; i++) Assert.Throws<InvalidOperationException>(() => Auth.LoginWithPin(Staff, "0000"));
        var locked = Assert.Throws<InvalidOperationException>(() => Auth.LoginWithPin(Staff, "2468"));
        Assert.Contains("sai nhiều lần", locked.Message);
    }

    [Fact]
    public void Staff_without_pin_or_locked_staff_cannot_use_pin()
    {
        Assert.Throws<InvalidOperationException>(() => Auth.LoginWithPin(Staff, "2468"));

        Auth.SetPin(Staff, "2468");
        Auth.SetAccountActive(Staff, false);
        Assert.Throws<InvalidOperationException>(() => Auth.LoginWithPin(Staff, "2468"));
    }

    [Fact]
    public void Owner_has_no_pin()
    {
        Assert.Throws<InvalidOperationException>(() => Auth.SetPin(Owner, "1357"));
        Assert.Throws<InvalidOperationException>(() => Auth.ChangePin(OwnerPass, "1357"));
        Assert.Throws<InvalidOperationException>(() => Auth.LoginWithPin(Owner, "1357"));
    }

    [Fact]
    public void Only_the_owner_sets_pins_and_invalid_pins_change_nothing()
    {
        Assert.Throws<InvalidOperationException>(() => Auth.SetPin(Staff, "12"));
        Assert.False(Auth.Accounts.Single(a => a.Username == Staff).HasPin);

        Auth.SetPin(Staff, "2468");
        Auth.LoginWithPin(Staff, "2468");
        Assert.Throws<InvalidOperationException>(() => Auth.SetPin(Staff, "1111"));
    }

    [Fact]
    public void Staff_change_their_own_pin_with_their_password()
    {
        Auth.SetPin(Staff, "2468");
        Auth.LoginWithPin(Staff, "2468");

        Assert.Throws<InvalidOperationException>(() => Auth.ChangePin("wrong-password", "1357"));
        Auth.ChangePin(StaffPass, "1357");

        Assert.Throws<InvalidOperationException>(() => Auth.LoginWithPin(Staff, "2468"));
        Auth.LoginWithPin(Staff, "1357");
        Assert.Equal(Staff, Auth.CurrentUser?.Username);
    }
}
