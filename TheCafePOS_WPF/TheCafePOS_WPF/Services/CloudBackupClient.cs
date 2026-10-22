using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace TheCafePOS_WPF.Services;

/// <summary>Where off-site backups go. Read from THECAFEPOS_SUPABASE_URL, THECAFEPOS_SUPABASE_KEY and THECAFEPOS_SUPABASE_BUCKET.</summary>
public sealed record SupabaseOptions(string Url, string Key, string Bucket)
{
    public static SupabaseOptions FromEnvironment() => Create(
        Environment.GetEnvironmentVariable("THECAFEPOS_SUPABASE_URL"),
        Environment.GetEnvironmentVariable("THECAFEPOS_SUPABASE_KEY"),
        Environment.GetEnvironmentVariable("THECAFEPOS_SUPABASE_BUCKET"));

    public static SupabaseOptions Create(string? url, string? key, string? bucket)
    {
        // People paste the REST endpoint shown in the dashboard; only the project URL is needed.
        string projectUrl = (url ?? "").Trim().TrimEnd('/');
        int api = projectUrl.IndexOf("/rest/v1", StringComparison.OrdinalIgnoreCase);
        if (api >= 0) projectUrl = projectUrl[..api];
        return new(projectUrl, (key ?? "").Trim(), string.IsNullOrWhiteSpace(bucket) ? "backups" : bucket.Trim());
    }

    /// <summary>Why cloud backup cannot run with these settings, or null when it can.</summary>
    public string? Problem
    {
        get
        {
            if (Url.Length == 0 || Key.Length == 0) return "Chưa cấu hình (đặt THECAFEPOS_SUPABASE_URL và THECAFEPOS_SUPABASE_KEY).";
            if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return "THECAFEPOS_SUPABASE_URL phải là địa chỉ https của project.";
            // A secret key on a till would let anyone with the machine read and delete every backup.
            if (IsSecretKey(Key)) return "Không dùng secret/service_role key trên máy bán hàng. Dùng publishable (anon) key.";
            return null;
        }
    }

    private static bool IsSecretKey(string key)
    {
        if (key.StartsWith("sb_secret_", StringComparison.Ordinal)) return true;
        var parts = key.Split('.');
        if (parts.Length != 3) return false;
        try
        {
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return json.RootElement.TryGetProperty("role", out var role) && role.GetString() == "service_role";
        }
        catch (Exception ex) when (ex is FormatException or JsonException) { return false; }
    }
}

/// <summary>Stores backup files off the machine.</summary>
public interface ICloudBackupClient
{
    /// <summary>Why uploads are disabled, or null when they can run.</summary>
    string? Problem { get; }

    /// <summary>Adds a new file; an existing file with the same path counts as already uploaded.</summary>
    Task UploadAsync(string objectPath, byte[] content, CancellationToken ct = default);
}

/// <summary>
/// Uploads to a private Supabase Storage bucket with the publishable key. The bucket policy only allows
/// inserting new files, so this key can never list, read, overwrite or delete backups.
/// </summary>
public sealed class SupabaseStorageClient : ICloudBackupClient
{
    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;

    public SupabaseStorageClient(HttpClient http, SupabaseOptions options)
    {
        _http = http;
        _options = options;
    }

    public string? Problem => _options.Problem;

    public async Task UploadAsync(string objectPath, byte[] content, CancellationToken ct = default)
    {
        if (Problem is { } problem) throw new InvalidOperationException(problem);
        string path = string.Join('/', objectPath.Split('/').Select(Uri.EscapeDataString));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Url}/storage/v1/object/{Uri.EscapeDataString(_options.Bucket)}/{path}");
        request.Headers.Add("apikey", _options.Key);
        request.Headers.Add("x-upsert", "false");
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new("application/octet-stream");
        using var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return;
        string body = await response.Content.ReadAsStringAsync(ct);
        // A retry after a lost response finds the file already there.
        if (response.StatusCode == HttpStatusCode.Conflict || body.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)) return;
        string reason = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "khóa không hợp lệ",
            HttpStatusCode.Forbidden => "khóa không có quyền tải lên bucket này",
            _ when body.Contains("Bucket not found", StringComparison.OrdinalIgnoreCase) => $"chưa có bucket '{_options.Bucket}' (chạy docs/supabase-backup.sql)",
            _ when body.Contains("row-level security", StringComparison.OrdinalIgnoreCase) => "bucket chưa cho phép máy bán hàng tải lên (chạy docs/supabase-backup.sql)",
            _ => $"lỗi {(int)response.StatusCode}",
        };
        throw new InvalidOperationException($"Supabase từ chối bản sao lưu: {reason}.");
    }
}
