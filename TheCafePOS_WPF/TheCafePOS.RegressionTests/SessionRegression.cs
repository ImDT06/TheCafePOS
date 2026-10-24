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
        var auth = TestHost.NewAuth();
        if (!reload)
        {
            auth.CreateAccount("session-owner", "SessionPass!2026", "Owner");
            auth.Login("session-owner", "SessionPass!2026");
            auth.RememberCurrentSession(true);
            var stored = TestHost.Database.Read<string>("session")!;
            Check(!stored.Contains("SessionPass") && !stored.Contains("session-owner"), "Saved login contains no cleartext credentials");
            return;
        }
        Check(auth.TryRestoreSession() && auth.CurrentUser?.Username == "session-owner", "Session restores in a new process");
        auth.Logout();
        Check(!TestHost.NewAuth().TryRestoreSession(), "Logout removes saved login");
        auth.Login("session-owner", "SessionPass!2026");
        auth.RememberCurrentSession(true);
        auth.RememberCurrentSession(false);
        Check(!TestHost.NewAuth().TryRestoreSession(), "Unchecked remember option clears older session");
        auth.RememberCurrentSession(true);
        auth.ChangePassword("SessionPass!2026", "ChangedPass!2026");
        Check(!TestHost.NewAuth().TryRestoreSession(), "Password change invalidates saved login");
        auth.CreateAccount("session-cashier", "CashierPass!2026", "Cashier");
        auth.Login("session-cashier", "CashierPass!2026");
        auth.RememberCurrentSession(true);
        auth.Login("session-owner", "ChangedPass!2026");
        auth.SetAccountActive("session-cashier", false);
        Check(!TestHost.NewAuth().TryRestoreSession(), "Disabled account cannot restore session");
        auth.SetAccountActive("session-cashier", true);
        Check(!TestHost.NewAuth().TryRestoreSession(), "Re-enabling does not revive revoked session");
        auth.Login("session-cashier", "CashierPass!2026");
        auth.RememberCurrentSession(true);
        auth.Login("session-owner", "ChangedPass!2026");
        auth.ResetPassword("session-cashier", "TemporaryPass!2026");
        Check(!TestHost.NewAuth().TryRestoreSession(), "Reset password invalidates saved login");
        TestHost.Database.Write("session", "not-base64");
        Check(!TestHost.NewAuth().TryRestoreSession(), "Damaged session falls back to login");
        TestHost.Database.Write("session", Convert.ToBase64String(new byte[32]));
        Check(!TestHost.NewAuth().TryRestoreSession(), "Invalid encrypted token falls back to login");
    }
}
