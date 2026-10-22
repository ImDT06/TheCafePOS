using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using TheCafePOS_WPF.Services.Interfaces;

namespace TheCafePOS_WPF.Services;

/// <summary>Which local backups already reached the cloud, and how the last attempt went.</summary>
public sealed class CloudBackupStatus
{
    public List<string> Uploaded { get; set; } = new();
    public DateTime? LastUploadAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string LastError { get; set; } = "";
}

// Copies the local database into a Backups folder next to it, keeps the newest copies, and queues each
// copy for upload so a broken or stolen machine does not take the shop's history with it.
public sealed class BackupService : IBackupService
{
    private const int Keep = 14;
    private const string CloudStateId = "cloud-backups";
    private readonly LocalDatabase _db;
    private readonly ICloudBackupClient _cloud;
    private readonly IAuthService _auth;
    private readonly SemaphoreSlim _uploading = new(1, 1);

    public BackupService(LocalDatabase db, ICloudBackupClient cloud, IAuthService auth)
    {
        _db = db;
        _cloud = cloud;
        _auth = auth;
    }

    public string Folder => Path.Combine(Path.GetDirectoryName(_db.DatabasePath)!, "Backups");

    public DateTime? LastBackup => LocalBackups().Select(f => (DateTime?)f.CreationTime).Max();

    public string BackupNow() => BackupNow("");

    private string BackupNow(string label)
    {
        string source = _db.DatabasePath;
        if (!File.Exists(source)) throw new InvalidOperationException("Chưa có dữ liệu để sao lưu.");
        Directory.CreateDirectory(Folder);
        string suffix = label.Length > 0 ? "-" + label : "";
        string target = Path.Combine(Folder, $"{Path.GetFileNameWithoutExtension(source)}-{DateTime.Now:yyyyMMdd-HHmmss}{suffix}{Path.GetExtension(source)}");
        _db.BackupTo(target);
        foreach (var old in LocalBackups().OrderByDescending(f => f.Name, StringComparer.Ordinal).Skip(Keep)) old.Delete();
        return target;
    }

    public async Task BackupDailyQuietlyAsync()
    {
        try { if (LastBackup?.Date != DateTime.Today) BackupNow(); }
        catch (Exception) { }
        // Also retries anything an earlier offline day could not upload.
        await UploadPendingAsync();
    }

    public async Task BackupAfterShiftQuietlyAsync()
    {
        try { BackupNow("chot-ca"); }
        catch (Exception) { }
        await UploadPendingAsync();
    }

    public string? CloudProblem => _cloud.Problem;

    public CloudBackupStatus CloudStatus => _db.Read<CloudBackupStatus>(CloudStateId) ?? new();

    public int PendingUploads
    {
        get
        {
            var uploaded = CloudStatus.Uploaded.ToHashSet();
            return LocalBackups().Count(f => !uploaded.Contains(f.Name));
        }
    }

    public async Task UploadPendingAsync(CancellationToken ct = default)
    {
        if (_cloud.Problem is not null) return;
        if (!await _uploading.WaitAsync(0, ct)) return; // an upload run is already going
        try
        {
            var status = CloudStatus;
            var local = LocalBackups().OrderBy(f => f.Name, StringComparer.Ordinal).ToList();
            foreach (var file in local.Where(f => !status.Uploaded.Contains(f.Name)))
            {
                status.LastAttemptAt = DateTime.Now;
                try
                {
                    await _cloud.UploadAsync($"{DeviceFolder}/{file.Name}", await File.ReadAllBytesAsync(file.FullName, ct), ct);
                    status.Uploaded.Add(file.Name);
                    status.LastUploadAt = DateTime.Now;
                    status.LastError = "";
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    // Offline or rejected: keep the file queued and try again with the next backup.
                    status.LastError = ex is HttpRequestException or TaskCanceledException ? "Không kết nối được Supabase (mất mạng?)." : ex.Message;
                    break;
                }
            }
            // Local copies rotate out; their names no longer need tracking.
            status.Uploaded.RemoveAll(name => local.All(f => f.Name != name));
            try { _db.Write(CloudStateId, status); }
            catch (Exception) { } // status is informational; never fail a backup over it
        }
        finally { _uploading.Release(); }
    }

    public string Restore(string backupFile)
    {
        _auth.RequireManager();
        if (!LocalDatabase.IsValidBackup(backupFile)) throw new InvalidOperationException("File đã chọn không phải bản sao lưu TheCafePOS hợp lệ.");
        // Keep today's data too, in case the wrong file was picked.
        string safetyCopy = BackupNow("truoc-khoi-phuc");
        _db.RestoreFrom(backupFile);
        return safetyCopy;
    }

    private IEnumerable<FileInfo> LocalBackups()
    {
        if (!Directory.Exists(Folder)) return Enumerable.Empty<FileInfo>();
        string prefix = Path.GetFileNameWithoutExtension(_db.DatabasePath) + "-";
        string extension = Path.GetExtension(_db.DatabasePath);
        return new DirectoryInfo(Folder).GetFiles("*" + extension).Where(f => f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    // One folder per till in the bucket, so several machines can share a project without name clashes.
    private static string DeviceFolder => Regex.Replace(Environment.MachineName.ToLowerInvariant(), "[^a-z0-9_-]", "-");
}
