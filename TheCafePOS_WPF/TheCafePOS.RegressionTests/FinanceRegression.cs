using TheCafePOS_WPF.Services;

internal static class FinanceRegression
{
    public static void Run(bool reload)
    {
        int checks = 0;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
        void Reject(Action action, string label)
        {
            try { action(); } catch (InvalidOperationException) { checks++; Console.WriteLine("PASS " + label); return; }
            throw new Exception("Expected rejection: " + label);
        }
        var store = DataStoreService.Instance; var auth = AuthService.Instance;
        auth.Login("owner", "OwnerPass!2026");
        if (reload)
        {
            Check(store.Refunds.Count == 3 && store.Refunds.Sum(r => r.Amount) == 75000, "Refund ledger survives restart");
            Check(store.CashEntries.Count == 2 && store.CashEntries.Sum(e => e.SignedAmount) == 10000, "Cash ledger survives restart");
            Check(store.CompletedOrders.Single(o => o.Id == "cash-order").RefundableAmount == 0, "Fully refunded balance survives restart");
            Check(store.CurrentShift.ExpectedCash == 60000 && store.CurrentShift.Status == "Closed", "Net closing drawer balance persisted");
            Reject(() => auth.Login("finance-cashier", "CashierNew!2026"), "Disabled account remains blocked after restart");
            Console.WriteLine($"Completed {checks} finance restart checks."); return;
        }
        auth.CreateAccount("finance-cashier", "CashierStart!2026", "Cashier");
        auth.CreateAccount("finance-manager", "ManagerStart!2026", "Manager");
        auth.Login("finance-manager", "ManagerStart!2026");
        Reject(() => auth.ResetPassword("owner", "ResetOwner!2026"), "Manager cannot reset owner password");
        Reject(() => auth.SetAccountActive("owner", false), "Owner cannot be disabled");
        auth.ResetPassword("finance-cashier", "Temporary!2026");
        auth.Login("finance-cashier", "Temporary!2026");
        Reject(() => auth.RequireSignedIn(), "Temporary password cannot perform business actions");
        Reject(() => store.OpenShift(0), "Password change required before opening shift");
        Reject(() => auth.ChangePassword("Temporary!2026", "short"), "Reject weak new password and roll back account state");
        auth.ChangePassword("Temporary!2026", "CashierNew!2026");
        Check(!auth.CurrentUser!.MustChangePassword, "Successful password change clears reset flag");
        Reject(() => auth.Login("finance-cashier", "Temporary!2026"), "Old password no longer valid");
        auth.Login("finance-cashier", "CashierNew!2026");
        Reject(() => auth.ResetPassword("finance-manager", "Forbidden!2026"), "Cashier cannot reset manager password");
        auth.Login("owner", "OwnerPass!2026");
        auth.SetAccountActive("finance-cashier", false);
        Reject(() => auth.Login("finance-cashier", "CashierNew!2026"), "Disabled account cannot log in");
        auth.SetAccountActive("finance-cashier", true);
        auth.Login("finance-cashier", "CashierNew!2026");
        Check(auth.CurrentUser!.IsActive, "Re-enabled account can log in");
        store.OpenShift(0);
        auth.Login("owner", "OwnerPass!2026");
        Reject(() => auth.SetAccountActive("finance-cashier", false), "Cannot disable employee with open shift");
        auth.Login("finance-cashier", "CashierNew!2026");
        store.CloseShift(0);
        auth.Login("owner", "OwnerPass!2026");
        Reject(() => auth.SetAccountActive("owner", false), "Cannot disable own account");

        var oldShifts = store.Shifts.ToDictionary(s => s.Id, s => s.ExpectedCash);
        var stock = store.PackagingInventory.ToDictionary(p => p.Id, p => p.StockQuantity);
        store.OpenShift(100000); string shiftId = store.CurrentShift.Id;
        Reject(() => store.RecordCash("unauthorized", "In", 1, "test"), "Cash entry requires manager approval");
        Reject(() => store.RefundOrder("unauthorized", "cash-order", 1, "test", ""), "Refund requires manager approval");
        Reject(() => store.RecordCash("zero", "In", 0, "test"), "Reject zero cash entry");
        Reject(() => store.RecordCash("fraction", "In", 0.5m, "test"), "Reject fractional currency");
        Reject(() => store.RecordCash("overdraw", "Out", 100001, "test"), "Prevent negative cash drawer");
        Reject(() => store.RefundOrder("overrefund", "cash-order", 50001, "test", ""), "Cannot refund more than order balance");
        Reject(() => store.RefundOrder("missing-reference", "qr-order", 1000, "test", ""), "QR refund needs external transaction reference");
        void Approve(string action) => auth.Approve("owner", "OwnerPass!2026", action);
        Approve(DataStoreService.CashAction("deposit", "In", 20000, "Add change"));
        store.RecordCash("deposit", "In", 20000, "Add change");
        store.RecordCash("deposit", "In", 20000, "Add change");
        Check(store.ExpectedCash == 120000 && store.CashEntries.Count == 1, "Deposit retry is idempotent");
        Reject(() => store.RecordCash("deposit", "In", 30000, "Add change"), "Duplicate entry ID cannot change amount");
        Approve(DataStoreService.CashAction("expense", "Out", 10000, "Supplies"));
        store.RecordCash("expense", "Out", 10000, "Supplies");
        Check(store.ExpectedCash == 110000, "Expense subtracts from drawer");
        Approve(DataStoreService.RefundAction("partial", "cash-order", 20000, "Wrong drink", ""));
        store.RefundOrder("partial", "cash-order", 20000, "Wrong drink", "");
        store.RefundOrder("partial", "cash-order", 20000, "Wrong drink", "");
        Check(store.CompletedOrders.Single(o => o.Id == "cash-order").RefundableAmount == 30000 && store.ExpectedCash == 90000, "Partial refund recorded once in current shift");
        Reject(() => store.RefundOrder("partial", "cash-order", 30000, "Wrong drink", ""), "Duplicate refund ID cannot change amount");
        Approve(DataStoreService.RefundAction("remaining", "cash-order", 30000, "Return balance", ""));
        store.RefundOrder("remaining", "cash-order", 30000, "Return balance", "");
        Reject(() => store.RefundOrder("again", "cash-order", 1, "Already refunded", ""), "Fully refunded order cannot be refunded again");
        Approve(DataStoreService.RefundAction("qr-refund", "qr-order", 25000, "Transfer return", "BANK-123"));
        store.RefundOrder("qr-refund", "qr-order", 25000, "Transfer return", "BANK-123");
        Check(store.ExpectedCash == 60000, "QR refunds do not change cash drawer");
        Check(store.PackagingInventory.All(p => p.StockQuantity == stock[p.Id]), "Refund does not restore consumed packaging");
        Check(store.Shifts.All(s => s.ExpectedCash == oldShifts[s.Id]), "Refund never changes previously closed shift");
        var report = ReportService.Build(store.CompletedOrders, DateTime.Today, DateTime.Today, shiftId, store.Refunds, store.CashEntries);
        Check(report.Revenue == 0 && report.Refunded == 75000 && report.NetRevenue == -75000 && report.CashIn == 20000 && report.CashOut == 10000, "Cross-shift refunds use refund shift and cash flows stay separate from sales");
        Check(ReportService.ToCsv(report).Contains("BANK-123") && ReportService.ToCsv(report).Contains("qr-refund"), "CSV includes refund ledger IDs and external reference");
        var nextDayRefund = new TheCafePOS_WPF.Models.RefundEntry { Amount = 123, CreatedAt = DateTime.Today.AddDays(1), ShiftId = shiftId };
        Check(ReportService.Build(store.CompletedOrders, DateTime.Today, DateTime.Today, null, store.Refunds.Append(nextDayRefund)).Refunded == 75000, "Refund reporting uses refund date");
        store.CloseShift(60000);
        Reject(() => store.RecordCash("closed", "In", 100, "test"), "Closed shifts cannot accept cash entries");
        Reject(() => store.RefundOrder("closed", "second-shift", 100, "test", ""), "Closed shifts cannot accept refunds");
        auth.SetAccountActive("finance-cashier", false);
        Console.WriteLine($"Completed {checks} finance and account checks.");
    }
}
