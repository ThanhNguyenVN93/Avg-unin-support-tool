using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace frm_avg_unin_support_tool
{
    // Automates the avgclear GUI so the tool runs fully unattended in Safe Mode.
    // Flow: wait for window → click Remove → wait for completion → click Restart Later
    // (Restart Later, not Restart Now, so our HandleSafeMode code owns the actual restart
    // and can run DisableSafeBoot + WriteFlagDone cleanly beforehand.)
    //
    // All automation is non-fatal: if a button is not found within its timeout the
    // user can still click manually — WaitForExit in RunAvgClearProcess continues.
    internal static class AvgClearAutomation
    {
        // ── Win32 P/Invoke ────────────────────────────────────────────────────

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hWndParent,
            EnumChildCallback lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private delegate bool EnumChildCallback(IntPtr hWnd, IntPtr lParam);

        private const uint BM_CLICK = 0x00F5;

        // ── Window / button name tables ───────────────────────────────────────

        // avgclear main window titles (checked across several versions)
        private static readonly string[] WindowTitles =
        {
            "AVG AV Clear",
            "AVG Clear",
            "AVGClear",
            "AVG Removal Tool",
        };

        // "Start removal" buttons on the first / main page
        private static readonly string[] RemoveButtons =
        {
            "Remove",
            "Uninstall",
            "Start",
            "Start removal",
        };

        // Click "Restart later" so OUR code calls DisableSafeBoot + RestartImmediate.
        // We try these before "Restart now" to keep the restart path in our control.
        private static readonly string[] RestartLaterButtons =
        {
            "Restart later",
            "Restart Later",
            "Later",
            "No, restart later",
            "Finish",
            "Close",
            "Done",
        };

        // Fallback: avgclear schedules its own restart; we still call RestartImmediate
        // after WaitForExit returns, which overrides the pending countdown.
        private static readonly string[] RestartNowButtons =
        {
            "Restart now",
            "Restart Now",
            "Restart",
            "Reboot now",
        };

        // ── Timeouts ──────────────────────────────────────────────────────────

        private const int PollMs           = 500;
        private const int WindowAppearMs   = 60_000;   // 60 s for the window to appear
        private const int RemoveBtnMs      = 30_000;   // 30 s to find Remove button
        private const int CompletionMs     = 300_000;  // 5 min for removal to complete
        private const int FallbackBtnMs    = 10_000;   // 10 s fallback for Restart now

        // ── Public API ────────────────────────────────────────────────────────

        // Launches automation on a background thread. Non-blocking.
        internal static void RunAsync()
        {
            var t = new Thread(Run) { IsBackground = true, Name = "AvgClearAuto" };
            t.Start();
        }

        // ── Automation sequence ───────────────────────────────────────────────

        private static void Run()
        {
            try
            {
                // Step 1: wait for the avgclear window to appear
                IntPtr hwnd = WaitForWindow(WindowAppearMs);
                if (hwnd == IntPtr.Zero)
                {
                    Logger.Write("AvgClearAutomation: window not found — user must click manually");
                    return;
                }
                Logger.Write("AvgClearAutomation: window found hWnd=0x{0:X}", hwnd.ToInt64());

                // Brief pause after the window appears so the UI finishes rendering
                Thread.Sleep(1500);

                // Step 2: click Remove (or equivalent start button)
                if (!WaitAndClick(RemoveButtons, RemoveBtnMs))
                {
                    Logger.Write("AvgClearAutomation: Remove button not found — user must click manually");
                    return;
                }
                Logger.Write("AvgClearAutomation: clicked Remove, waiting for completion");

                // Step 3a: wait for "Restart later" and click it so WE own the restart
                if (WaitAndClick(RestartLaterButtons, CompletionMs))
                {
                    Logger.Write("AvgClearAutomation: clicked Restart later — HandleSafeMode will restart");
                    return;
                }

                // Step 3b: "Restart later" never appeared; try "Restart now" as a fallback
                Logger.Write("AvgClearAutomation: Restart later not found, trying Restart now");
                if (WaitAndClick(RestartNowButtons, FallbackBtnMs))
                    Logger.Write("AvgClearAutomation: clicked Restart now (fallback)");
                else
                    Logger.Write("AvgClearAutomation: completion buttons not found — user must click manually");
            }
            catch (ThreadAbortException) { /* safe — happens when process exits */ }
            catch (Exception ex) { Logger.WriteException("AvgClearAutomation", ex); }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Polls until a known avgclear window title is visible.
        private static IntPtr WaitForWindow(int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                foreach (var title in WindowTitles)
                {
                    IntPtr h = FindWindow(null, title);
                    if (h != IntPtr.Zero && IsWindowVisible(h))
                        return h;
                }
                Thread.Sleep(PollMs);
            }
            return IntPtr.Zero;
        }

        // Polls all known window titles until a matching button appears and clicks it.
        private static bool WaitAndClick(string[] buttonTexts, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                foreach (var title in WindowTitles)
                {
                    IntPtr hwnd = FindWindow(null, title);
                    if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd)) continue;

                    IntPtr btn = FindButton(hwnd, buttonTexts);
                    if (btn != IntPtr.Zero && IsWindowVisible(btn) && IsWindowEnabled(btn))
                    {
                        SetForegroundWindow(hwnd);
                        Thread.Sleep(200);
                        SendMessage(btn, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                        Logger.Write("AvgClearAutomation: BM_CLICK → '{0}'", GetText(btn));
                        return true;
                    }
                }
                Thread.Sleep(PollMs);
            }
            return false;
        }

        // Enumerates ALL descendant controls of parent and returns the first button
        // whose visible text matches any of the given texts (case-insensitive,
        // accelerator & stripped, leading/trailing whitespace trimmed).
        private static IntPtr FindButton(IntPtr parent, string[] texts)
        {
            IntPtr result = IntPtr.Zero;
            // Keep the delegate in a local variable so the GC does not collect it
            // while EnumChildWindows is running (synchronous, but be explicit).
            EnumChildCallback cb = (hWnd, _) =>
            {
                string raw   = GetText(hWnd).Replace("&", "").Trim();
                foreach (var t in texts)
                {
                    if (string.Equals(raw, t.Replace("&", "").Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        result = hWnd;
                        return false; // stop enumeration
                    }
                }
                return true; // continue
            };
            EnumChildWindows(parent, cb, IntPtr.Zero);
            GC.KeepAlive(cb); // defensive: prevent GC before native returns
            return result;
        }

        private static string GetText(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            return sb.ToString();
        }
    }
}
