using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using TheCafePOS_WPF.Core;
using TheCafePOS_WPF.Views.Dialogs;
namespace TheCafePOS_WPF;
// Locking the till signs the current person out but keeps the cart, so they can step away mid-order and come back.
public partial class MainWindow
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo { public int Size; public uint Time; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
    // Any keyboard, mouse or touch input on this PC; the till has nothing else running.
    private static TimeSpan IdleTime()
    {
        var info = new LastInputInfo { Size = Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info) ? TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time)) : TimeSpan.Zero;
    }

    private readonly DispatcherTimer _idleCheck = new() { Interval = TimeSpan.FromSeconds(15) };
    private bool _locked;

    private void StartIdleLock()
    {
        _idleCheck.Tick += (_, _) =>
        {
            int minutes = _settings.Settings.AutoLockMinutes;
            if (_locked || minutes <= 0 || _auth.CurrentUser is null || IdleTime() < TimeSpan.FromMinutes(minutes)) return;
            // A dialog (payment, refund…) is mid-task; locking over it would strand it. Try again next tick.
            if (ComponentDispatcher.IsThreadModal || _vm.CheckoutInProgress) return;
            LockTill($"Máy tự khóa sau {minutes} phút không dùng.");
        };
        _idleCheck.Start();
        Closed += (_, _) => _idleCheck.Stop();
    }

    private void LockTill_Click(object sender, RoutedEventArgs e) => LockTill(null);

    private void LockTill(string? reason)
    {
        UtilitiesPopup.IsOpen = false;
        string? previous = _auth.CurrentUser?.Username;
        try { _auth.Logout(); }
        catch (Exception ex) { MessageBox.Show(this, "Không thể khóa máy: " + ex.Message); return; }
        _locked = true;
        Hide();
        bool unlocked = _dialogs.Create<LoginWindow>(null).AsLockScreen(previous, reason).ShowDialog() == true;
        _locked = false;
        // Closing saves the cart as a held order.
        if (!unlocked) { Close(); return; }
        // The cart was rung up by the previous person; park it under their name rather than hand it over.
        string? parked = null;
        if (_vm.HasItems && _auth.CurrentUser?.Username != previous)
        {
            try { _vm.HoldCurrentOrder($"Đơn đang làm của {previous} (khóa máy)"); parked = $"Đơn đang làm của {previous} đã được tạm giữ (F9)."; }
            catch (Exception ex) { MessageBox.Show(this, "Không thể tạm giữ đơn đang làm: " + ex.Message); }
        }
        RefreshSession(); Show(); Activate();
        if (parked is not null) Toast.Show(this, parked);
    }
}
