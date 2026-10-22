namespace TheCafePOS_WPF.Services.Interfaces
{
    public interface IAuthService
    {
        StaffAccount? CurrentUser { get; }
        bool IsManager { get; }

        void Login(string username, string password);
        void Logout();
        void RequireSignedIn();
        void RequireManager();
        bool TryRestoreSession();
        void RememberCurrentSession(bool remember);
    }
}
