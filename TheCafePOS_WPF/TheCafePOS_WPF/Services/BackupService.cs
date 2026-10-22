using System.IO;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Services;

// Copies the local database into a Backups folder next to it; keeps the newest copies only.
public sealed class BackupService : IBackupService
{
    private const int Keep = 14;
    private readonly LocalDatabase _db;

    public BackupService(LocalDatabase db) => _db = db;

    public string Folder => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "Backups");

    public string BackupNow()
    {
        string source = _db.DatabasePath;
        if (!File.Exists(source)) throw new InvalidOperationException("Chưa có dữ liệu để sao lưu.");
        Directory.CreateDirectory(Folder);
        string target = Path.Combine(Folder, $"{Path.GetFileNameWithoutExtension(source)}-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(source)}");
        // Read with shared access so a backup never blocks or fails because the app has the file open.
        using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var output = File.Create(target)) input.CopyTo(output);
        foreach (var old in new DirectoryInfo(Folder).GetFiles().OrderByDescending(f => f.CreationTimeUtc).Skip(Keep)) old.Delete();
        return target;
    }

    // At most one automatic backup per day; failures never stop the POS.
    public void BackupDailyQuietly()
    {
        try
        {
            if (Directory.Exists(Folder) && new DirectoryInfo(Folder).GetFiles().Any(f => f.CreationTime.Date == DateTime.Today)) return;
            BackupNow();
        }
        catch (Exception) { }
    }

    public DateTime? LastBackup =>
        Directory.Exists(Folder) ? new DirectoryInfo(Folder).GetFiles().Select(f => (DateTime?)f.CreationTime).Max() : null;
}
