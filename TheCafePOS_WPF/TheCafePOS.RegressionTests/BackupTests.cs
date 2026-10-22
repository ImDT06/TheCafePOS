using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Data.Sqlite;
using TheCafePOS_WPF.Models;
using TheCafePOS_WPF.Services;
using Xunit;

/// <summary>Local backups, the upload queue to Supabase, restore, and the Supabase client itself (no real network).</summary>
public sealed class BackupTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "thecafepos-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeCloud _cloud = new();

    public BackupTests()
    {
        TestHost.Start(_dataDirectory);
        TestHost.Store.Save(); // the store document is what makes a file a TheCafePOS backup
    }

    public void Dispose()
    {
        TestHost.Stop();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dataDirectory, true); }
        catch (IOException) { }
    }

    private BackupService Backup() => new(TestHost.Database, _cloud, TestHost.Auth);

    private static void SignInOwner()
    {
        if (TestHost.Auth.NeedsSetup) TestHost.Auth.CreateAccount("backup-owner", "BackupPass!2026", "Owner");
        TestHost.Auth.Login("backup-owner", "BackupPass!2026");
    }

    [Fact]
    public void Backup_is_a_complete_database_copy()
    {
        string path = Backup().BackupNow();
        Assert.True(LocalDatabase.IsValidBackup(path));
        Assert.StartsWith(Backup().Folder, path);
    }

    [Fact]
    public async Task Backups_stay_queued_until_the_cloud_accepts_them()
    {
        var backup = Backup();
        string path = backup.BackupNow();
        _cloud.Fail = new HttpRequestException("offline");

        await backup.UploadPendingAsync();
        Assert.Equal(1, backup.PendingUploads);
        Assert.Contains("mất mạng", backup.CloudStatus.LastError);

        _cloud.Fail = null;
        await backup.UploadPendingAsync();
        Assert.Equal(0, backup.PendingUploads);
        Assert.Equal("", backup.CloudStatus.LastError);
        var (objectPath, content) = Assert.Single(_cloud.Uploads);
        Assert.EndsWith("/" + Path.GetFileName(path), objectPath);
        Assert.Equal(File.ReadAllBytes(path), content);

        await backup.UploadPendingAsync();
        Assert.Single(_cloud.Uploads); // nothing is sent twice
    }

    [Fact]
    public async Task Nothing_is_uploaded_when_the_cloud_is_not_configured()
    {
        _cloud.Problem = "Chưa cấu hình";
        var backup = Backup();
        backup.BackupNow();
        await backup.UploadPendingAsync();
        Assert.Empty(_cloud.Uploads);
        Assert.Equal("Chưa cấu hình", backup.CloudProblem);
    }

    [Fact]
    public async Task Closing_a_shift_backs_up_and_uploads()
    {
        await Backup().BackupAfterShiftQuietlyAsync();
        Assert.Contains(_cloud.Uploads, u => u.Path.EndsWith("-chot-ca.db"));
    }

    [Fact]
    public void Restore_brings_back_the_backed_up_data_and_keeps_a_safety_copy()
    {
        var backup = Backup();
        Assert.Throws<InvalidOperationException>(() => backup.Restore("missing.db")); // not signed in as manager
        SignInOwner();
        string before = backup.BackupNow();
        TestHost.Store.SaveCategory(new Category { Name = "Thêm sau khi sao lưu" });

        string notABackup = Path.Combine(_dataDirectory, "notes.db");
        File.WriteAllText(notABackup, "not a database");
        Assert.Throws<InvalidOperationException>(() => backup.Restore(notABackup));

        string safetyCopy = backup.Restore(before);
        Assert.True(LocalDatabase.IsValidBackup(safetyCopy));

        TestHost.Start(_dataDirectory); // reopening the app
        Assert.DoesNotContain(TestHost.Store.Categories, c => c.Name == "Thêm sau khi sao lưu");
        var restoredFromSafety = new LocalDatabase(Path.Combine(_dataDirectory, "check.db"));
        restoredFromSafety.RestoreFrom(safetyCopy);
        Assert.Contains("Thêm sau khi sao lưu", restoredFromSafety.Read<StoreState>("store")!.Categories.Select(c => c.Name));
    }

    [Theory]
    [InlineData("https://abc.supabase.co/rest/v1/", "https://abc.supabase.co")]
    [InlineData(" https://abc.supabase.co/ ", "https://abc.supabase.co")]
    public void Options_accept_the_dashboard_url(string pasted, string expected)
    {
        var options = SupabaseOptions.Create(pasted, "sb_publishable_x", null);
        Assert.Equal(expected, options.Url);
        Assert.Equal("backups", options.Bucket);
        Assert.Null(options.Problem);
    }

    [Theory]
    [InlineData("sb_secret_abc")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJyb2xlIjoic2VydmljZV9yb2xlIn0.sig")] // {"role":"service_role"}
    public void Options_refuse_keys_that_could_read_or_delete_backups(string key)
    {
        Assert.Contains("secret", SupabaseOptions.Create("https://abc.supabase.co", key, null).Problem);
    }

    [Fact]
    public void Options_report_missing_configuration()
    {
        Assert.NotNull(SupabaseOptions.Create(null, null, null).Problem);
    }

    [Fact]
    public async Task Client_inserts_a_new_object_with_the_publishable_key()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{}");
        var client = new SupabaseStorageClient(new HttpClient(handler), SupabaseOptions.Create("https://abc.supabase.co", "sb_publishable_x", null));
        await client.UploadAsync("quay 1/cafe-1.db", new byte[] { 1, 2, 3 });
        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://abc.supabase.co/storage/v1/object/backups/quay%201/cafe-1.db", request.RequestUri!.AbsoluteUri);
        Assert.Equal("sb_publishable_x", request.Headers.GetValues("apikey").Single());
        Assert.Equal("false", request.Headers.GetValues("x-upsert").Single());
        Assert.False(request.Headers.Contains("Authorization"));
    }

    [Fact]
    public async Task Client_treats_an_existing_object_as_uploaded()
    {
        var client = new SupabaseStorageClient(new HttpClient(new FakeHandler(HttpStatusCode.BadRequest, "{\"error\":\"Duplicate\",\"statusCode\":\"409\"}")), SupabaseOptions.Create("https://abc.supabase.co", "sb_publishable_x", null));
        await client.UploadAsync("a/b.db", new byte[] { 1 });
    }

    [Fact]
    public async Task Client_explains_a_bucket_that_rejects_uploads()
    {
        var client = new SupabaseStorageClient(new HttpClient(new FakeHandler(HttpStatusCode.BadRequest, "{\"message\":\"new row violates row-level security policy\"}")), SupabaseOptions.Create("https://abc.supabase.co", "sb_publishable_x", null));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.UploadAsync("a/b.db", new byte[] { 1 }));
        Assert.Contains("supabase-backup.sql", error.Message);
    }

    private sealed class FakeCloud : ICloudBackupClient
    {
        public string? Problem { get; set; }
        public Exception? Fail { get; set; }
        public List<(string Path, byte[] Content)> Uploads { get; } = new();

        public Task UploadAsync(string objectPath, byte[] content, CancellationToken ct = default)
        {
            if (Fail is not null) throw Fail;
            Uploads.Add((objectPath, content));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public FakeHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") });
        }
    }
}
