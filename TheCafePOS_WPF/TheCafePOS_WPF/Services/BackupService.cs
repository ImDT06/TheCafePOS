using System.IO;

namespace TheCafePOS_WPF.Services;

// Copies the local database into a Backups folder next to it; keeps the newest copies only.
public static class BackupService
{
    private const int Keep = 14;
    public static string Folder => Path.Combine(Path.GetDirectoryName(LocalDatabase.Instance.DatabasePath)!, "Backups");

    public static string BackupNow()
    {
        string source = LocalDatabase.Instance.DatabasePath;
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
    public static void BackupDailyQuietly()
    {
        try
        {
            if (Directory.Exists(Folder) && new DirectoryInfo(Folder).GetFiles().Any(f => f.CreationTime.Date == DateTime.Today)) return;
            BackupNow();
        }
        catch (Exception) { }
    }

    public static DateTime? LastBackup =>
        Directory.Exists(Folder) ? new DirectoryInfo(Folder).GetFiles().Select(f => (DateTime?)f.CreationTime).Max() : null;
}
