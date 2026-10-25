using TheCafePOS_WPF.Core;

/// <summary>Lab2 sheet CashTryParse: CashAmount.TryParse(string text, out decimal value).</summary>
[TestFixture]
public class CashTryParseTests
{
    [TestCase("110000", 110000, TestName = "UTCID01")]
    [TestCase("  5000  ", 5000, TestName = "UTCID02")]
    [TestCase("0", 0, TestName = "UTCID03")]
    [TestCase("999999999999", 999999999999, TestName = "UTCID04")]
    public void Valid_amount_is_read(string text, long expected)
    {
        Assert.That(CashAmount.TryParse(text, out var value), Is.True);
        Assert.That(value, Is.EqualTo(expected));
    }

    [TestCase("1000000000000", TestName = "UTCID05")]
    [TestCase("", TestName = "UTCID06")]
    [TestCase("abc", TestName = "UTCID07")]
    [TestCase("-1", TestName = "UTCID08")]
    [TestCase("110.000", TestName = "UTCID09")]
    [TestCase("110000.5", TestName = "UTCID10")]
    [TestCase("1e5", TestName = "UTCID11")]
    public void Invalid_amount_is_rejected(string text) =>
        Assert.That(CashAmount.TryParse(text, out _), Is.False);

    [TestCase(TestName = "UTCID12")]
    public void Null_text_throws() =>
        Assert.Throws<NullReferenceException>(() => CashAmount.TryParse(null!, out _));
}
