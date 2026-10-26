using System.Reflection;
using TheCafePOS_WPF.Services;

/// <summary>Lab2 sheet Login: AuthService.Login(string username, string password).</summary>
[TestFixture]
public class LoginTests
{
    private const string WrongLogin = "Tên đăng nhập hoặc mật khẩu không đúng.";
    private const string Locked = "Đăng nhập sai nhiều lần. Thử lại sau 1 phút.";
    private TestHost _host = null!;
    private AuthService Auth => _host.Auth;

    // Precondition: staff / staff123 active, lan locked by the owner.
    [SetUp]
    public void SetUp()
    {
        _host = new TestHost();
        Auth.CreateAccount("staff", "staff123", "Cashier");
        Auth.CreateAccount("lan", "staff123", "Cashier");
        Auth.SetAccountActive("lan", false);
    }

    [TearDown]
    public void TearDown() => _host.Dispose();

    // Failed attempts live in the private auth state; reading it is the only way to see the count and lock time.
    private Dictionary<string, LoginAttempts> Attempts =>
        ((AuthState)typeof(AuthService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Auth)!).Attempts;

    private void FailTimes(int times)
    {
        for (int i = 0; i < times; i++) Assert.Throws<InvalidOperationException>(() => Auth.Login("staff", "sai12345"));
    }

    [TestCase("staff", TestName = "UTCID01")]
    [TestCase("  STAFF  ", TestName = "UTCID02")]
    public void Correct_password_signs_in(string username)
    {
        Auth.Login(username, "staff123");
        Assert.That(Auth.CurrentUser!.Username, Is.EqualTo("staff"));
        Assert.That(Attempts.ContainsKey("staff"), Is.False);
    }

    [TestCase("staff", "sai12345", TestName = "UTCID03")]
    [TestCase("staff", "STAFF123", TestName = "UTCID04")]
    [TestCase("khongco", "sai12345", TestName = "UTCID05")]
    [TestCase("lan", "staff123", TestName = "UTCID06")]
    [TestCase("", "staff123", TestName = "UTCID07")]
    [TestCase("staff", "", TestName = "UTCID08")]
    public void Wrong_credentials_are_refused_and_counted(string username, string password)
    {
        var before = Auth.CurrentUser;
        var ex = Assert.Throws<InvalidOperationException>(() => Auth.Login(username, password));
        Assert.That(ex!.Message, Is.EqualTo(WrongLogin));
        Assert.That(Auth.CurrentUser, Is.SameAs(before));
        Assert.That(Attempts[username].Count, Is.EqualTo(1));
    }

    [TestCase(TestName = "UTCID09")]
    public void Fifth_try_correct_after_four_failures_signs_in()
    {
        FailTimes(4);
        Auth.Login("staff", "staff123");
        Assert.That(Auth.CurrentUser!.Username, Is.EqualTo("staff"));
        Assert.That(Attempts.ContainsKey("staff"), Is.False);
    }

    [TestCase(TestName = "UTCID10")]
    public void Fifth_failure_locks_for_one_minute()
    {
        FailTimes(4);
        var ex = Assert.Throws<InvalidOperationException>(() => Auth.Login("staff", "sai12345"));
        Assert.That(ex!.Message, Is.EqualTo(WrongLogin));
        Assert.That(Attempts["staff"].Count, Is.EqualTo(0));
        Assert.That(Attempts["staff"].UntilUtc, Is.EqualTo(DateTime.UtcNow.AddMinutes(1)).Within(TimeSpan.FromSeconds(5)));
    }

    [TestCase(TestName = "UTCID11")]
    public void Locked_account_refuses_even_the_right_password()
    {
        FailTimes(5);
        var before = Auth.CurrentUser;
        var ex = Assert.Throws<InvalidOperationException>(() => Auth.Login("staff", "staff123"));
        Assert.That(ex!.Message, Is.EqualTo(Locked));
        Assert.That(Auth.CurrentUser, Is.SameAs(before));
    }

    [TestCase(TestName = "UTCID12")]
    public void Lock_expires_after_one_minute()
    {
        FailTimes(5);
        Attempts["staff"].UntilUtc = DateTime.UtcNow.AddSeconds(-1); // stands in for waiting out the minute
        Auth.Login("staff", "staff123");
        Assert.That(Auth.CurrentUser!.Username, Is.EqualTo("staff"));
    }
}
