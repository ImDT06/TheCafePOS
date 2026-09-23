using TheCafePOS_WPF.Services;

internal static class SessionRegression
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    internal static void Run(bool reload)
    {
        var auth = new AuthService();
        if (!reload)
        {
            auth.CreateAccount("session-owner", "SessionPass!2026", "Owner");
            auth.Login("session-owner", "SessionPass!2026");
            auth.RememberCurrentSession(true);
            var stored = LocalDatabase.Instance.Read<string>("session")!;
            Check(!stored.Contains("SessionPass") && !stored.Contains("session-owner"), "Saved login contains no cleartext credentials");
            return;
        }
        Check(auth.TryRestoreSession() && auth.CurrentUser?.Username == "session-owner", "Session restores in a new process");
        auth.Logout();
        Check(!new AuthService().TryRestoreSession(), "Logout removes saved login");
        auth.Login("session-owner", "SessionPass!2026");
        auth.RememberCurrentSession(true);
        auth.RememberCurrentSession(false);
        Check(!new AuthService().TryRestoreSession(), "Unchecked remember option clears older session");
        auth.RememberCurrentSession(true);
        auth.ChangePassword("SessionPass!2026", "ChangedPass!2026");
        Check(!new AuthService().TryRestoreSession(), "Password change invalidates saved login");
        auth.CreateAccount("session-cashier", "CashierPass!2026", "Cashier");
        auth.Login("session-cashier", "CashierPass!2026");
        auth.RememberCurrentSession(true);
        auth.Login("session-owner", "ChangedPass!2026");
        auth.SetAccountActive("session-cashier", false);
        Check(!new AuthService().TryRestoreSession(), "Disabled account cannot restore session");
        auth.SetAccountActive("session-cashier", true);
        Check(!new AuthService().TryRestoreSession(), "Re-enabling does not revive revoked session");
        auth.Login("session-cashier", "CashierPass!2026");
        auth.RememberCurrentSession(true);
        auth.Login("session-owner", "ChangedPass!2026");
        auth.ResetPassword("session-cashier", "TemporaryPass!2026");
        Check(!new AuthService().TryRestoreSession(), "Reset password invalidates saved login");
        LocalDatabase.Instance.Write("session", "not-base64");
        Check(!new AuthService().TryRestoreSession(), "Damaged session falls back to login");
        LocalDatabase.Instance.Write("session", Convert.ToBase64String(new byte[32]));
        Check(!new AuthService().TryRestoreSession(), "Invalid encrypted token falls back to login");
    }
}
