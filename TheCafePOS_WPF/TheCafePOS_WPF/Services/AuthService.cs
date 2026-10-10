using System.Security.Cryptography;

namespace TheCafePOS_WPF.Services;

public class StaffAccount
{
    public int SessionVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "Cashier";
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
public class ApprovalRecord
{
    public DateTime At { get; set; } = DateTime.Now;
    public string Operator { get; set; } = "";
    public string Approver { get; set; } = "";
    public string Action { get; set; } = "";
}
public class AuthState
{
    public List<StaffAccount> Accounts { get; set; } = new();
    public List<ApprovalRecord> Audit { get; set; } = new();
}
public sealed partial class AuthService
{
    static AuthService() { }
    public static AuthService Instance { get; } = new();
    private AuthState _state = LocalDatabase.Instance.Read<AuthState>("auth") ?? new();
    private readonly Dictionary<string, (int Count, DateTime Until)> _attempts = new(StringComparer.OrdinalIgnoreCase);
    public StaffAccount? CurrentUser { get; private set; }
    public bool NeedsSetup => _state.Accounts.Count == 0;
    public bool IsManager => CurrentUser is { IsActive: true, MustChangePassword: false } && CurrentUser.Role is "Owner" or "Manager";
    public void RequireSignedIn()
    {
        if (CurrentUser is not { IsActive: true, MustChangePassword: false }) throw new InvalidOperationException("Vui lòng đăng nhập và đổi mật khẩu tạm trước khi tiếp tục.");
    }
    public void Logout()
    {
        LocalDatabase.Instance.Write("session", "");
        CurrentUser = null;
    }
    public string RequireApproval(string action)
    {
        RequireSignedIn();
        var record = _state.Audit.LastOrDefault(a => a.Operator == CurrentUser!.Username && a.Action == action
            && _state.Accounts.Any(u => u.Username == a.Approver && u.IsActive && !u.MustChangePassword && u.Role is "Owner" or "Manager"));
        return record?.Approver ?? throw new InvalidOperationException("Cần quản lý phê duyệt thao tác này.");
    }
    public static string PaymentApprovalAction(string orderId, decimal amount) => $"Đã kiểm tra nhận {amount:N0}đ — POS {orderId}";
    public static string DiscountApprovalAction(string orderId, decimal amount, string reason) => $"Giảm giá {amount:N0}đ ({reason.Trim()}) — POS {orderId}";
    public bool HasPaymentApproval(string orderId, decimal amount)
    {
        string action = PaymentApprovalAction(orderId, amount);
        if (_state.Audit.Any(a => a.Operator == CurrentUser?.Username && a.Action == action && a.Approver.StartsWith("SePay:"))) return true;
        try { RequireApproval(action); return true; }
        catch (InvalidOperationException) { return false; }
    }
    public IReadOnlyList<StaffAccount> Accounts => _state.Accounts.AsReadOnly();
    public void RequireManager()
    {
        if (!IsManager) throw new InvalidOperationException("Chức năng chỉ dành cho quản lý.");
    }
    public void CreateAccount(string username, string password, string role)
    {
        bool first = NeedsSetup;
        if (!first) RequireManager();
        username = username.Trim();
        if (username.Length < 3 || password.Length < 8) throw new InvalidOperationException("Tên đăng nhập tối thiểu 3 ký tự; mật khẩu tối thiểu 8 ký tự.");
        if (_state.Accounts.Any(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");
        if (!first && role is not ("Cashier" or "Manager")) throw new InvalidOperationException("Vai trò không hợp lệ.");
        if (!first && role == "Manager" && CurrentUser?.Role != "Owner") throw new InvalidOperationException("Chỉ chủ quán được tạo quản lý.");
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        var account = new StaffAccount { Username = username, Role = first ? "Owner" : role, Salt = Convert.ToBase64String(salt), PasswordHash = Convert.ToBase64String(Hash(password, salt)) };
        _state.Accounts.Add(account);
        try { LocalDatabase.Instance.Write("auth", _state); }
        catch { _state.Accounts.Remove(account); throw; }
    }
    private static byte[] Hash(string password, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
    private StaffAccount Verify(string username, string password)
    {
        username = username.Trim();
        var attempt = _attempts.GetValueOrDefault(username);
        if (attempt.Until > DateTime.UtcNow) throw new InvalidOperationException("Đăng nhập sai nhiều lần. Thử lại sau 1 phút.");
        var account = _state.Accounts.FirstOrDefault(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (account is null || !account.IsActive || !CryptographicOperations.FixedTimeEquals(Hash(password, Convert.FromBase64String(account.Salt)), Convert.FromBase64String(account.PasswordHash)))
        {
            int count = attempt.Count + 1;
            _attempts[username] = (count >= 5 ? 0 : count, count >= 5 ? DateTime.UtcNow.AddMinutes(1) : DateTime.MinValue);
            throw new InvalidOperationException("Tên đăng nhập hoặc mật khẩu không đúng.");
        }
        _attempts.Remove(username);
        return account;
    }
    public void Login(string username, string password) => CurrentUser = Verify(username, password);
    // Payment confirmed by a bank feed (e.g. SePay) instead of a manager; only valid for payment actions.
    public void ApproveBySystem(string paymentAction, string source)
    {
        RequireSignedIn();
        var record = new ApprovalRecord { Operator = CurrentUser!.Username, Approver = source, Action = paymentAction };
        _state.Audit.Add(record);
        try { LocalDatabase.Instance.Write("auth", _state); }
        catch { _state.Audit.Remove(record); throw; }
    }
    public void Approve(string username, string password, string action)
    {
        RequireSignedIn();
        var account = Verify(username, password);
        if (account.MustChangePassword || account.Role is not ("Owner" or "Manager")) throw new InvalidOperationException("Tài khoản không có quyền duyệt hoặc cần đổi mật khẩu tạm.");
        var record = new ApprovalRecord { Operator = CurrentUser?.Username ?? "", Approver = account.Username, Action = action };
        _state.Audit.Add(record);
        try { LocalDatabase.Instance.Write("auth", _state); }
        catch { _state.Audit.Remove(record); throw; }
    }

    private void UpdateAccount(StaffAccount account, string action, Action update)
    {
        var before = System.Text.Json.JsonSerializer.Serialize(_state);
        string? currentName = CurrentUser?.Username;
        try
        {
            update();
            account.SessionVersion++;
            _state.Audit.Add(new ApprovalRecord { Operator = currentName ?? "", Approver = currentName ?? "", Action = action + " | " + account.Username });
            LocalDatabase.Instance.Write("auth", _state);
        }
        catch
        {
            _state = System.Text.Json.JsonSerializer.Deserialize<AuthState>(before)!;
            CurrentUser = _state.Accounts.FirstOrDefault(a => a.Username == currentName); throw;
        }
    }
    private static void SetPassword(StaffAccount account, string password)
    {
        if (password.Length < 8) throw new InvalidOperationException("Mật khẩu tối thiểu 8 ký tự.");
        var salt = RandomNumberGenerator.GetBytes(16);
        account.Salt = Convert.ToBase64String(salt); account.PasswordHash = Convert.ToBase64String(Hash(password, salt));
    }
    public void ChangePassword(string oldPassword, string newPassword)
    {
        if (CurrentUser is null) throw new InvalidOperationException("Vui lòng đăng nhập.");
        var account = Verify(CurrentUser.Username, oldPassword);
        if (oldPassword == newPassword) throw new InvalidOperationException("Mật khẩu mới phải khác mật khẩu hiện tại.");
        UpdateAccount(account, "Đổi mật khẩu", () => { SetPassword(account, newPassword); account.MustChangePassword = false; });
    }
    private StaffAccount ManagedAccount(string username)
    {
        RequireManager();
        var target = _state.Accounts.FirstOrDefault(a => a.Username == username) ?? throw new InvalidOperationException("Tài khoản không tồn tại.");
        if (target.Username == CurrentUser!.Username || target.Role == "Owner" || CurrentUser.Role != "Owner" && target.Role != "Cashier") throw new InvalidOperationException("Bạn không được thay đổi tài khoản này. Dùng Đổi mật khẩu cho tài khoản của mình.");
        return target;
    }
    public void ResetPassword(string username, string temporaryPassword)
    {
        var target = ManagedAccount(username);
        UpdateAccount(target, "Đặt lại mật khẩu", () => { SetPassword(target, temporaryPassword); target.MustChangePassword = true; });
        _attempts.Remove(username);
    }
    public void SetAccountActive(string username, bool active)
    {
        var target = ManagedAccount(username);
        var shift = DataStoreService.Instance.CurrentShift;
        if (!active && shift.Status == "Open" && shift.CashierName == username) throw new InvalidOperationException("Nhân viên đang có ca mở. Chốt ca trước khi khóa tài khoản.");
        UpdateAccount(target, active ? "Mở khóa tài khoản" : "Khóa tài khoản", () => target.IsActive = active);
        if (active) _attempts.Remove(username);
    }
    public void DeleteAccount(string username)
    {
        var target = ManagedAccount(username);
        var shift = DataStoreService.Instance.CurrentShift;
        if (shift.Status == "Open" && shift.CashierName == username) throw new InvalidOperationException("Nhân viên đang có ca mở. Chốt ca trước khi xóa tài khoản.");
        // Orders, shifts and audit keep the username as text, so history stays readable.
        UpdateAccount(target, "Xóa tài khoản", () => _state.Accounts.Remove(target));
        _attempts.Remove(username);
    }
    public void UpdateAccountRole(string username, string role)
    {
        var target = ManagedAccount(username);
        if (role is not ("Cashier" or "Manager")) throw new InvalidOperationException("Vai trò không hợp lệ.");
        if (role == "Manager" && CurrentUser?.Role != "Owner") throw new InvalidOperationException("Chỉ chủ quán được bổ nhiệm quản lý.");
        if (target.Role == role) return;
        UpdateAccount(target, $"Đổi vai trò {target.Role} → {role}", () => target.Role = role);
    }
}
