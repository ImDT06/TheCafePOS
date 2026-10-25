using TheCafePOS_WPF.Services;

/// <summary>Lab2 sheet IsValidPin: AuthService.IsValidPin(string pin).</summary>
[TestFixture]
public class IsValidPinTests
{
    [TestCase("2468", ExpectedResult = true, TestName = "UTCID01")]
    [TestCase("24680", ExpectedResult = true, TestName = "UTCID02")]
    [TestCase("1234", ExpectedResult = true, TestName = "UTCID03")]
    [TestCase("123456", ExpectedResult = true, TestName = "UTCID04")]
    [TestCase("123", ExpectedResult = false, TestName = "UTCID05")]
    [TestCase("1234567", ExpectedResult = false, TestName = "UTCID06")]
    [TestCase("12a4", ExpectedResult = false, TestName = "UTCID07")]
    [TestCase("", ExpectedResult = false, TestName = "UTCID08")]
    [TestCase(" 1234", ExpectedResult = false, TestName = "UTCID09")]
    [TestCase("１２３４", ExpectedResult = false, TestName = "UTCID10")]
    public bool Pin_is_checked(string pin) => AuthService.IsValidPin(pin);

    [TestCase(TestName = "UTCID11")]
    public void Null_pin_throws() =>
        Assert.Throws<NullReferenceException>(() => AuthService.IsValidPin(null!));
}
