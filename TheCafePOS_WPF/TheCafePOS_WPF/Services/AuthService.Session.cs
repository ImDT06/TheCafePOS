using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace TheCafePOS_WPF.Services;

// Windows user-scoped DPAPI: copying the database does not copy a usable login.
internal static class WindowsSessionProtection
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    internal static byte[] Transform(byte[] data, bool protect)
    {
        var input = new Blob { Length = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(data, 0, input.Data, data.Length);
            bool success = protect
                ? CryptProtectData(ref input, "TheCafePOS session", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!success) throw new CryptographicException(new Win32Exception(Marshal.GetLastWin32Error()).Message);
            var result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, result.Length);
            return result;
        }
        finally
        {
            Marshal.Copy(new byte[input.Length], 0, input.Data, input.Length);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
            {
                Marshal.Copy(new byte[output.Length], 0, output.Data, output.Length);
                LocalFree(output.Data);
            }
        }
    }
}

public sealed partial class AuthService
{
    private sealed record SavedSession(string Username, int Version, string CredentialId, DateTime ExpiresUtc);
    private static string CredentialId(StaffAccount account) => Convert.ToHexString(
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(account.Salt + account.PasswordHash)));

    public void RememberCurrentSession(bool remember)
    {
        RequireSignedIn();
        if (!remember) { LocalDatabase.Instance.Write("session", ""); return; }
        var account = CurrentUser!;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SavedSession(account.Username,
            account.SessionVersion, CredentialId(account), DateTime.UtcNow.AddDays(30)));
        try { LocalDatabase.Instance.Write("session", Convert.ToBase64String(WindowsSessionProtection.Transform(payload, true))); }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }

    public bool TryRestoreSession()
    {
        // Only called before login; never replace a current operator on a failed restore.
        if (CurrentUser != null) return false;
        try
        {
            var encoded = LocalDatabase.Instance.Read<string>("session");
            if (string.IsNullOrEmpty(encoded)) return false;
            var payload = WindowsSessionProtection.Transform(Convert.FromBase64String(encoded), false);
            SavedSession? saved;
            try { saved = JsonSerializer.Deserialize<SavedSession>(payload); }
            finally { CryptographicOperations.ZeroMemory(payload); }
            if (saved == null || saved.ExpiresUtc <= DateTime.UtcNow) return false;
            var account = _state.Accounts.FirstOrDefault(a => a.Username == saved.Username);
            if (account is not { IsActive: true, MustChangePassword: false }
                || account.SessionVersion != saved.Version || CredentialId(account) != saved.CredentialId) return false;
            CurrentUser = account;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            // Expired, damaged, or another Windows user's session falls back to password login.
            return false;
        }
    }
}
