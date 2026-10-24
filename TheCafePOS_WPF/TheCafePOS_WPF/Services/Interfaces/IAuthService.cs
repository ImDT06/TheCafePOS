using System.Collections.Generic;

namespace TheCafePOS_WPF.Services.Interfaces
{
    public interface IAuthService
    {
        StaffAccount? CurrentUser { get; }
        bool IsManager { get; }
        bool NeedsSetup { get; }
        IReadOnlyList<StaffAccount> Accounts { get; }

        void Login(string username, string password);
        void Logout();
        void RequireSignedIn();
        void RequireManager();
        bool TryRestoreSession();
        void RememberCurrentSession(bool remember);

        void CreateAccount(string username, string password, string role);
        void ChangePassword(string oldPassword, string newPassword);
        void ResetPassword(string username, string temporaryPassword);
        void SetAccountActive(string username, bool active);
        void UpdateAccountRole(string username, string role);
        void DeleteAccount(string username);

        /// <summary>Records a manager's approval of <paramref name="action"/> after checking their credentials.</summary>
        void Approve(string username, string password, string action);
        /// <summary>Records an approval granted by an external system, e.g. a bank transfer webhook.</summary>
        void ApproveBySystem(string paymentAction, string source);
        /// <summary>Returns the approver of <paramref name="action"/>, or throws when no valid approval was recorded.</summary>
        string RequireApproval(string action);
        bool HasPaymentApproval(string orderId, decimal amount);
    }
}
