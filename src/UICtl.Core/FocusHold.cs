using UICtl.Core.Interop;

namespace UICtl.Core;

/// <summary>
/// Lets an agent pin uictl to one target window so every subsequent
/// click/move/scroll/type/key call re-asserts that window's foreground
/// status first - countering a human's own mouse/keyboard use (e.g.
/// clicking into the terminal running uictl) stealing it away from whatever
/// uictl is mid-task automating. Mirrors macOS's FocusHold.swift. `Hold` also
/// snapshots whatever was frontmost right before it, so `Release` can put
/// focus back there afterward. Only ever touched from the daemon's single
/// accept-loop thread (see DaemonServer's one-connection-at-a-time comment),
/// so no synchronization is needed.
/// </summary>
public static class FocusHold
{
    private sealed record Held(int Pid, long WindowId, string Label);

    private enum RefocusResult { NotHeld, AlreadyFrontmost, Reactivated, Failed }

    private static Held? _held;
    private static Held? _previousFocus;

    public static Dictionary<string, object?> Hold(long? windowId, string? appSelector)
    {
        var resolved = WindowResolver.Resolve(windowId, appSelector);

        // Only the first `hold` in a hold/[hold...]/release sequence snapshots
        // pre-automation focus - re-targeting to a different window without
        // releasing in between shouldn't overwrite what `Release` will
        // eventually restore.
        if (_held is null)
            _previousFocus = CaptureCurrentFocus();
        _held = new Held(resolved.Pid, resolved.WindowId, AppLabel(resolved.Pid));
        return Status();
    }

    public static Dictionary<string, object?> Release()
    {
        _held = null;
        var result = Status();
        if (_previousFocus is { } previous)
            result["restoredFocus"] = ToJson(Reactivate(previous));
        _previousFocus = null;
        return result;
    }

    public static Dictionary<string, object?> Status()
    {
        if (_held is not { } held)
            return new Dictionary<string, object?> { ["held"] = false };

        return new Dictionary<string, object?>
        {
            ["held"] = true,
            ["app"] = held.Label,
            ["pid"] = held.Pid,
            ["windowId"] = held.WindowId,
            ["isFrontmost"] = NativeMethods.GetForegroundWindow() == new IntPtr(held.WindowId),
            ["restoresTo"] = _previousFocus?.Label,
        };
    }

    /// <summary>
    /// Called before every focus-sensitive action. Best-effort: if the held
    /// window has since closed (or its app quit), this doesn't throw - a
    /// stale hold shouldn't wedge every subsequent action just because
    /// whoever set it forgot to release it. Returns "notHeld" (with no other
    /// side effect) when nothing is held, so callers can call this
    /// unconditionally and only attach the result to their response when it's
    /// not "notHeld".
    /// </summary>
    public static string EnsureFocused()
    {
        if (_held is not { } held) return ToJson(RefocusResult.NotHeld);
        return ToJson(Reactivate(held));
    }

    /// <summary>
    /// Best guess at "whatever the human/agent was looking at" right before
    /// `Hold` redirects things. Unlike macOS (which has no direct API for
    /// "the system-wide frontmost window" and has to search the frontmost
    /// app's windows for one), GetForegroundWindow() already *is* that
    /// window - no search needed. Null (rather than throwing) if there's no
    /// foreground window, since a failed snapshot shouldn't block the hold
    /// that triggered it; `Release` just won't have anything to restore.
    /// </summary>
    private static Held? CaptureCurrentFocus()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return null;
        NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
        if (pid == 0) return null;
        return new Held((int)pid, fg.ToInt64(), AppLabel((int)pid));
    }

    /// <summary>
    /// Best-effort: activates `target`'s window and judges success by
    /// re-checking the actual foreground window afterward rather than
    /// trusting AppsAndWindows.BringToFront's completion alone, since Windows
    /// can report success slightly ahead of the foreground-lock/window-server
    /// state actually settling.
    /// </summary>
    private static RefocusResult Reactivate(Held target)
    {
        //The PID comparison defeats the window-level hold: if another window owned by the same process is foreground,
        //this returns alreadyFrontmost without raising the held HWND. It also prevents release from restoring a previous window
        //when both windows belong to one process. Compare the actual foreground HWND with target.WindowId before and after activation.
        if (ForegroundPid() == target.Pid) return RefocusResult.AlreadyFrontmost;
           //Should this be using: NativeMethods.GetForegroundWindow() == new IntPtr(held.WindowId)
        
        try { AppsAndWindows.BringToFront(new IntPtr(target.WindowId)); }
        catch { return RefocusResult.Failed; }

        Thread.Sleep(80);
        return ForegroundPid() == target.Pid ? RefocusResult.Reactivated : RefocusResult.Failed;
    }

    private static int ForegroundPid()
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        if (fg == IntPtr.Zero) return 0;
        NativeMethods.GetWindowThreadProcessId(fg, out uint pid);
        return (int)pid;
    }

    private static string AppLabel(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch
        {
            return $"pid {pid}";
        }
    }

    private static string ToJson(RefocusResult r) => r switch
    {
        RefocusResult.NotHeld => "notHeld",
        RefocusResult.AlreadyFrontmost => "alreadyFrontmost",
        RefocusResult.Reactivated => "reactivated",
        RefocusResult.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(r)),
    };
}
