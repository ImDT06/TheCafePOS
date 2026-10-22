using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Services;

// Polls SePay's transaction list to confirm a VietQR transfer automatically.
// Docs: https://docs.sepay.vn (User API → transactions/list). Manual manager approval stays as the fallback.
public sealed class SePayService : ISePayService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly IVietQRService _bank;

    public SePayService(IVietQRService bank) => _bank = bank;

    public bool Enabled => !string.IsNullOrWhiteSpace(_bank.Settings.SePayToken);

    // Returns the SePay transaction id of an incoming transfer matching memo and exact amount, or null.
    public async Task<string?> FindTransferAsync(string memo, decimal amount, DateTime since, CancellationToken ct = default)
    {
        var settings = _bank.Settings;
        string url = $"https://my.sepay.vn/userapi/transactions/list?account_number={Uri.EscapeDataString(settings.AccountNo)}&limit=50"
            + $"&transaction_date_min={Uri.EscapeDataString(since.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.SePayToken.Trim());
        using var response = await Http.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) throw new InvalidOperationException("API Token SePay không hợp lệ.");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!json.RootElement.TryGetProperty("transactions", out var list) || list.ValueKind != JsonValueKind.Array) return null;
        string wanted = Normalize(memo);
        foreach (var tx in list.EnumerateArray())
        {
            if (!decimal.TryParse(Text(tx, "amount_in"), NumberStyles.Number, CultureInfo.InvariantCulture, out var paid) || paid != amount) continue;
            // Banks often prepend their own text or drop spaces, so compare on letters/digits only.
            if (!Normalize(Text(tx, "transaction_content")).Contains(wanted)) continue;
            return Text(tx, "id");
        }
        return null;
    }

    private static string Text(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";
    private static string Normalize(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
