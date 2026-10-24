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

    /// <summary>Copies of the local database, kept next to it and uploaded off the machine.</summary>
    public interface IBackupService
    {
        string Folder { get; }
        DateTime? LastBackup { get; }

        /// <returns>Path of the new local backup; it is queued for upload.</returns>
        string BackupNow();
        /// <summary>At most one automatic backup per day, then uploads the queue; never throws.</summary>
        Task BackupDailyQuietlyAsync();
        /// <summary>Backs up after a shift is closed, then uploads the queue; never throws.</summary>
        Task BackupAfterShiftQuietlyAsync();

        /// <summary>Why cloud upload is off, or null when it is configured.</summary>
        string? CloudProblem { get; }
        CloudBackupStatus CloudStatus { get; }
        int PendingUploads { get; }
        /// <summary>Uploads local backups not yet in the cloud; failures are recorded in <see cref="CloudStatus"/>.</summary>
        Task UploadPendingAsync(CancellationToken ct = default);

        /// <summary>
        /// Replaces the shop data with a backup after saving the current data as a safety copy (returned).
        /// The running app still holds the old data in memory and must exit without saving.
        /// </summary>
        string Restore(string backupFile);
    }
}
