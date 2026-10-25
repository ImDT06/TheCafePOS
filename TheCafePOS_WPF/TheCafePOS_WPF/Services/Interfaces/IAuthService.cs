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
        /// <summary>Quick sign-in for staff on the shared till; the owner always uses the password.</summary>
        void LoginWithPin(string username, string pin);
        void Logout();
        void RequireSignedIn();
        void RequireManager();
        bool TryRestoreSession();
        void RememberCurrentSession(bool remember);

        void CreateAccount(string username, string password, string role);
        void ChangePassword(string oldPassword, string newPassword);
        /// <summary>Staff change their own PIN, confirmed with their password.</summary>
        void ChangePin(string password, string newPin);
        /// <summary>Owner sets or replaces a staff member's PIN.</summary>
        void SetPin(string username, string pin);
        void ResetPassword(string username, string temporaryPassword);
        void SetAccountActive(string username, bool active);
        void DeleteAccount(string username);

        /// <summary>Records the owner's approval of <paramref name="action"/> after checking their credentials.</summary>
        void Approve(string username, string password, string action);
        /// <summary>Records an approval granted by an external system, e.g. a bank transfer webhook.</summary>
        void ApproveBySystem(string paymentAction, string source);
        /// <summary>Records that the signed-in cashier checked the customer's transfer slip for <paramref name="paymentAction"/>.</summary>
        void ConfirmTransferByStaff(string paymentAction);
        /// <summary>Returns the approver of <paramref name="action"/>, or throws when no valid approval was recorded.</summary>
        string RequireApproval(string action);
        bool HasPaymentApproval(string orderId, decimal amount);
    }
}
