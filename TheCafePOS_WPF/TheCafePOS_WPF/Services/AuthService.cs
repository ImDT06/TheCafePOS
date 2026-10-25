using System.Security.Cryptography;
using TheCafePOS_WPF.Models;

namespace TheCafePOS_WPF.Services;

public class StaffAccount
{
    public int SessionVersion { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public string Username { get; set; } = "";
    public string Role { get; set; } = "Cashier";
    [System.Text.Json.Serialization.JsonIgnore] public string RoleLabel => RoleName(Role);

    /// <summary>Vietnamese label for a stored role; the stored values stay English.</summary>
    // Two roles: the owner runs the shop; "Cashier" is the stored value for Nhân viên (sells, makes and hands over drinks).
    public static string RoleName(string role) => role switch { "Owner" => "Chủ quán", "Cashier" => "Nhân viên", _ => role };
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    // Staff quick login on the shared till; empty = no PIN. Hashed like the password, with its own salt.
    public string PinSalt { get; set; } = "";
    public string PinHash { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public bool HasPin => PinHash.Length > 0;
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
public sealed partial class AuthService : Interfaces.IAuthService
{
    private readonly LocalDatabase _db;
    private readonly Func<Shift> _currentShift;
    private readonly Func<string, bool> _hasHistory;
    private AuthState _state;
    /// <param name="currentShift">Read when an account change needs it; the shift store itself depends on this service.</param>
    /// <param name="hasHistory">Whether a username appears in shifts, refunds or other store records; null treats every account as unused.</param>
    public AuthService(LocalDatabase db, Func<Shift> currentShift, Func<string, bool>? hasHistory = null)
    {
        _db = db;
        _currentShift = currentShift;
        _hasHistory = hasHistory ?? (_ => false);
        _state = db.Read<AuthState>("auth") ?? new();
        // The Quản lý role was dropped: its accounts become Nhân viên and their remembered sessions stop working.
        foreach (var account in _state.Accounts.Where(a => a.Role == "Manager")) { account.Role = "Cashier"; account.SessionVersion++; }
    }
    private readonly Dictionary<string, (int Count, DateTime Until)> _attempts = new(StringComparer.OrdinalIgnoreCase);
    public StaffAccount? CurrentUser { get; private set; }
    public bool NeedsSetup => _state.Accounts.Count == 0;
    // Management rights (menu, stock, reports, approvals) belong to the owner only.
    public bool IsManager => CurrentUser is { IsActive: true, MustChangePassword: false, Role: "Owner" };
    public void RequireSignedIn()
    {
        if (CurrentUser is not { IsActive: true, MustChangePassword: false }) throw new InvalidOperationException("Vui lòng đăng nhập và đổi mật khẩu tạm trước khi tiếp tục.");
    }
    public void Logout()
    {
        _db.Write("session", "");
        CurrentUser = null;
    }
    public string RequireApproval(string action)
    {
        RequireSignedIn();
        var record = _state.Audit.LastOrDefault(a => a.Operator == CurrentUser!.Username && a.Action == action
            && _state.Accounts.Any(u => u.Username == a.Approver && u.IsActive && !u.MustChangePassword && u.Role == "Owner"));
        return record?.Approver ?? throw new InvalidOperationException("Cần chủ quán phê duyệt thao tác này.");
    }
    public static string PaymentApprovalAction(string orderId, decimal amount) => $"Đã kiểm tra nhận {amount:N0}đ — POS {orderId}";
    public static string DiscountApprovalAction(string orderId, decimal amount, string reason) => $"Giảm giá {amount:N0}đ ({reason.Trim()}) — POS {orderId}";
    public bool HasPaymentApproval(string orderId, decimal amount)
    {
        string action = PaymentApprovalAction(orderId, amount);
        if (_state.Audit.Any(a => a.Operator == CurrentUser?.Username && a.Action == action && (a.Approver.StartsWith("SePay:") || a.Approver.StartsWith(StaffCheckPrefix)))) return true;
        try { RequireApproval(action); return true; }
        catch (InvalidOperationException) { return false; }
    }
    public IReadOnlyList<StaffAccount> Accounts => _state.Accounts.AsReadOnly();
    public void RequireManager()
    {
        if (!IsManager) throw new InvalidOperationException("Chức năng chỉ dành cho chủ quán.");
    }
    public void CreateAccount(string username, string password, string role)
    {
        bool first = NeedsSetup;
        if (!first) RequireManager();
        username = username.Trim();
        if (username.Length < 3 || password.Length < 8) throw new InvalidOperationException("Tên đăng nhập tối thiểu 3 ký tự; mật khẩu tối thiểu 8 ký tự.");
        if (_state.Accounts.Any(a => a.Username.Equals(username, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");
        if (!first && role != "Cashier") throw new InvalidOperationException("Chỉ tạo được tài khoản nhân viên.");
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        var account = new StaffAccount { Username = username, Role = first ? "Owner" : role, Salt = Convert.ToBase64String(salt), PasswordHash = Convert.ToBase64String(Hash(password, salt)) };
        _state.Accounts.Add(account);
        try { _db.Write("auth", _state); }
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
            RecordFailure(username);
            throw new InvalidOperationException("Tên đăng nhập hoặc mật khẩu không đúng.");
        }
        _attempts.Remove(username);
        return account;
    }
    // Wrong passwords and wrong PINs share one counter, so switching between them does not reset the lock.
    private void RecordFailure(string username)
    {
        int count = _attempts.GetValueOrDefault(username).Count + 1;
        _attempts[username] = (count >= 5 ? 0 : count, count >= 5 ? DateTime.UtcNow.AddMinutes(1) : DateTime.MinValue);
    }
    public void Login(string username, string password) => CurrentUser = Verify(username, password);
    public static bool IsValidPin(string pin) => pin.Length is >= 4 and <= 6 && pin.All(char.IsAsciiDigit);
    // Staff only: the owner signs in and approves with the full password.
    public void LoginWithPin(string username, string pin)
    {
        if (_attempts.GetValueOrDefault(username).Until > DateTime.UtcNow) throw new InvalidOperationException("Đăng nhập sai nhiều lần. Thử lại sau 1 phút.");
        var account = _state.Accounts.FirstOrDefault(a => a.Username == username);
        if (account is not { IsActive: true, Role: "Cashier", HasPin: true }
            || !CryptographicOperations.FixedTimeEquals(Hash(pin, Convert.FromBase64String(account.PinSalt)), Convert.FromBase64String(account.PinHash)))
        {
            RecordFailure(username);
            throw new InvalidOperationException("Mã PIN không đúng.");
        }
        _attempts.Remove(username);
        CurrentUser = account;
    }
    public const string StaffCheckPrefix = "Staff:";
    // The cashier checked the customer's transfer slip (amount, time, payee). Recorded under their own name so a shortfall can be traced.
    public void ConfirmTransferByStaff(string paymentAction) => ApproveBySystem(paymentAction, StaffCheckPrefix + CurrentUser?.Username);
    // Payment confirmed by a bank feed (e.g. SePay) instead of a manager; only valid for payment actions.
    public void ApproveBySystem(string paymentAction, string source)
    {
        RequireSignedIn();
        var record = new ApprovalRecord { Operator = CurrentUser!.Username, Approver = source, Action = paymentAction };
        _state.Audit.Add(record);
        try { _db.Write("auth", _state); }
        catch { _state.Audit.Remove(record); throw; }
    }
    public void Approve(string username, string password, string action)
    {
        RequireSignedIn();
        var account = Verify(username, password);
        if (account.MustChangePassword || account.Role != "Owner") throw new InvalidOperationException("Tài khoản không có quyền duyệt hoặc cần đổi mật khẩu tạm.");
        var record = new ApprovalRecord { Operator = CurrentUser?.Username ?? "", Approver = account.Username, Action = action };
        _state.Audit.Add(record);
        try { _db.Write("auth", _state); }
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
            _db.Write("auth", _state);
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
    private static void SetPinHash(StaffAccount account, string pin)
    {
        if (!IsValidPin(pin)) throw new InvalidOperationException("Mã PIN gồm 4–6 chữ số.");
        var salt = RandomNumberGenerator.GetBytes(16);
        account.PinSalt = Convert.ToBase64String(salt); account.PinHash = Convert.ToBase64String(Hash(pin, salt));
    }
    public void SetPin(string username, string pin)
    {
        var target = ManagedAccount(username);
        UpdateAccount(target, "Đặt mã PIN", () => SetPinHash(target, pin));
        _attempts.Remove(username);
    }
    public void ChangePin(string password, string newPin)
    {
        if (CurrentUser is not { Role: "Cashier" }) throw new InvalidOperationException("Mã PIN chỉ dành cho nhân viên; chủ quán đăng nhập bằng mật khẩu.");
        var account = Verify(CurrentUser.Username, password);
        UpdateAccount(account, "Đổi mã PIN", () => SetPinHash(account, newPin));
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
        if (target.Username == CurrentUser!.Username || target.Role == "Owner") throw new InvalidOperationException("Bạn không được thay đổi tài khoản này. Dùng Đổi mật khẩu cho tài khoản của mình.");
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
        var shift = _currentShift();
        if (!active && shift.Status == "Open" && shift.CashierName == username) throw new InvalidOperationException("Nhân viên đang có ca mở. Chốt ca trước khi khóa tài khoản.");
        UpdateAccount(target, active ? "Mở khóa tài khoản" : "Khóa tài khoản", () => target.IsActive = active);
        if (active) _attempts.Remove(username);
    }
    public void DeleteAccount(string username)
    {
        var target = ManagedAccount(username);
        var shift = _currentShift();
        if (shift.Status == "Open" && shift.CashierName == username) throw new InvalidOperationException("Nhân viên đang có ca mở. Chốt ca trước khi xóa tài khoản.");
        // Records keep the username as text: deleting a used account would let a new account take over its history.
        if (_hasHistory(username)) throw new InvalidOperationException("Tài khoản đã có ca hoặc giao dịch nên không xóa được. Hãy khóa tài khoản thay vì xóa.");
        UpdateAccount(target, "Xóa tài khoản", () => _state.Accounts.Remove(target));
        _attempts.Remove(username);
    }
}
