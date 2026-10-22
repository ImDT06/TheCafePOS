using System;

namespace TheCafePOS_WPF.Services
{
    public class BankSettings
    {
        public string BankId { get; set; } = "";
        public string AccountNo { get; set; } = "";
        public string AccountName { get; set; } = "";
        // Optional; enables automatic transfer confirmation through SePay.
        [System.Text.Json.Serialization.JsonIgnore] public string SePayToken { get; set; } = "";
        // Stored form: DPAPI-encrypted for the current Windows user, base64. Legacy plain value is migrated on next save.
        public string SePayTokenProtected { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("SePayToken")] public string LegacySePayToken { get; set; } = "";
    }
    public class VietQRService
    {
        public static BankSettings Settings
        {
            get
            {
                var s = LocalDatabase.Instance.Read<BankSettings>("bank") ?? new();
                try
                {
                    s.SePayToken = s.SePayTokenProtected.Length > 0
                        ? System.Text.Encoding.UTF8.GetString(WindowsSessionProtection.Transform(Convert.FromBase64String(s.SePayTokenProtected), false))
                        : s.LegacySePayToken;
                }
                catch (Exception) { s.SePayToken = ""; } // another Windows user or damaged value: treat as not configured
                return s;
            }
        }
        public static void ValidateSettings() => Validate(Settings);
        private static void Validate(BankSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.BankId) || !System.Text.RegularExpressions.Regex.IsMatch(settings.BankId, "^[A-Za-z0-9]+$") || !System.Text.RegularExpressions.Regex.IsMatch(settings.AccountNo, "^[0-9]{4,30}$") || string.IsNullOrWhiteSpace(settings.AccountName))
                throw new InvalidOperationException("Vui lòng cấu hình tài khoản nhận tiền hợp lệ trong Quản trị > Chuyển khoản.");
        }
        public static void SaveSettings(BankSettings settings)
        {
            AuthService.Instance.RequireManager(); Validate(settings);
            settings.SePayTokenProtected = string.IsNullOrWhiteSpace(settings.SePayToken) ? "" : Convert.ToBase64String(WindowsSessionProtection.Transform(System.Text.Encoding.UTF8.GetBytes(settings.SePayToken.Trim()), true));
            settings.LegacySePayToken = "";
            LocalDatabase.Instance.Write("bank", settings);
        }

        // Short, letters/digits only (banks strip symbols): CF + ddMM + 4 chars of the checkout id, e.g. "CF1010A3F9".
        // Unique enough to match a transfer to its order, and easy for staff to read on the bank app.
        public static string TransferMemo(string orderRef)
        {
            string tail = new string(orderRef.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            return $"CF{DateTime.Now:ddMM}{(tail.Length >= 4 ? tail[..4] : tail.PadLeft(4, '0'))}";
        }
        public static string GenerateQRUrl(decimal amount, string orderRef)
        {
            var settings = Settings;
            Validate(settings);
            string memo = Uri.EscapeDataString(TransferMemo(orderRef));
            // img.vietqr.io now redirects .png to SVG, which WPF cannot decode; SePay's generator returns a real PNG.
            return $"https://qr.sepay.vn/img?acc={settings.AccountNo}&bank={Uri.EscapeDataString(settings.BankId)}&amount={(long)amount}&des={memo}&template=compact";
        }
    }
}
