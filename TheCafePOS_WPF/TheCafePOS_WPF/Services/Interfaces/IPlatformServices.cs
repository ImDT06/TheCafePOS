using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheCafePOS_WPF.Services.Interfaces
{
    /// <summary>Bank account used for VietQR transfers, and the QR image for an order.</summary>
    public interface IVietQRService
    {
        BankSettings Settings { get; }

        void ValidateSettings();
        void SaveSettings(BankSettings settings);
        string GenerateQRUrl(decimal amount, string orderRef);
    }

    /// <summary>Automatic confirmation of VietQR transfers through SePay.</summary>
    public interface ISePayService
    {
        bool Enabled { get; }

        /// <returns>The SePay transaction id of a matching incoming transfer, or null.</returns>
        Task<string?> FindTransferAsync(string memo, decimal amount, DateTime since, CancellationToken ct = default);
    }

    /// <summary>Copies of the local database kept next to it.</summary>
    public interface IBackupService
    {
        string Folder { get; }
        DateTime? LastBackup { get; }

        string BackupNow();
        /// <summary>At most one automatic backup per day; failures never stop the POS.</summary>
        void BackupDailyQuietly();
    }
}
