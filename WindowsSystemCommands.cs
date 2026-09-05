using System.Diagnostics;

namespace frm_avg_unin_support_tool
{
    /// <summary>
    /// 5 — Centralises all Windows system command calls (bcdedit, shutdown).
    ///     Eliminates magic argument strings from business logic and makes call
    ///     sites readable and testable.
    /// </summary>
    internal static class WindowsSystemCommands
    {
        // ── Safe-boot ─────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the current BCD entry to boot into Safe Mode (Minimal).
        /// </summary>
        /// <returns>
        /// <c>true</c> if bcdedit exited with code 0 (success);
        /// <c>false</c> if the BCD store was locked or the command otherwise failed.
        /// </returns>
        internal static bool EnableSafeBootMinimal()
        {
            Debug.WriteLine("WindowsSystemCommands: EnableSafeBootMinimal");
            int exit = Program.RunSysCmd("bcdedit.exe", "/set {current} safeboot minimal");
            if (exit != 0)
                Debug.WriteLine("EnableSafeBootMinimal: bcdedit exited " + exit);
            return exit == 0;
        }

        /// <summary>
        /// Removes the safeboot flag so the next reboot returns to Normal Mode.
        /// </summary>
        /// <returns>
        /// <c>true</c> if bcdedit exited with code 0 (success).
        /// IMPORTANT: callers must not restart the machine if this returns <c>false</c>,
        /// otherwise the machine will be permanently stuck in Safe Mode.
        /// </returns>
        internal static bool DisableSafeBoot()
        {
            Debug.WriteLine("WindowsSystemCommands: DisableSafeBoot");
            int exit = Program.RunSysCmd("bcdedit.exe", "/deletevalue {current} safeboot");
            if (exit != 0)
                Debug.WriteLine("DisableSafeBoot: bcdedit exited " + exit);
            return exit == 0;
        }

        // ── Shutdown / Restart ────────────────────────────────────────────────

        /// <summary>Schedules a forced restart. /f closes blocking apps without prompting.</summary>
        /// <param name="delaySeconds">Seconds before restarting (0 = immediate).</param>
        internal static void RestartForced(int delaySeconds)
        {
            Debug.WriteLine("WindowsSystemCommands: RestartForced t=" + delaySeconds);
            Program.RunSysCmd("shutdown.exe", "/r /f /t " + delaySeconds);
        }

        /// <summary>Forces an immediate restart.</summary>
        internal static void RestartImmediate()
            => RestartForced(0);
    }
}
