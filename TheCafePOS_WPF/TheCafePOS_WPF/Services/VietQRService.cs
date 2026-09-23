using System;

namespace TheCafePOS_WPF.Services
{
    public class BankSettings
    {
        public string BankId { get; set; } = "";
        public string AccountNo { get; set; } = "";
        public string AccountName { get; set; } = "";
    }
    public class VietQRService
    {
        public static BankSettings Settings => LocalDatabase.Instance.Read<BankSettings>("bank") ?? new();
        public static void ValidateSettings() => Validate(Settings);
        private static void Validate(BankSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.BankId) || !System.Text.RegularExpressions.Regex.IsMatch(settings.BankId, "^[A-Za-z0-9]+$") || !System.Text.RegularExpressions.Regex.IsMatch(settings.AccountNo, "^[0-9]{4,30}$") || string.IsNullOrWhiteSpace(settings.AccountName))
                throw new InvalidOperationException("Vui lòng cấu hình tài khoản nhận tiền hợp lệ trong Quản trị > Chuyển khoản.");
        }
        public static void SaveSettings(BankSettings settings)
        {
            AuthService.Instance.RequireManager(); Validate(settings); LocalDatabase.Instance.Write("bank", settings);
        }

        public static string GenerateQRUrl(decimal amount, string orderRef)
        {
            var settings = Settings;
            Validate(settings);
            string memo = Uri.EscapeDataString($"POS {orderRef}");
            return $"https://img.vietqr.io/image/{settings.BankId}-{settings.AccountNo}-compact2.png?amount={(long)amount}&addInfo={memo}&accountName={Uri.EscapeDataString(settings.AccountName)}";
        }
    }
}
