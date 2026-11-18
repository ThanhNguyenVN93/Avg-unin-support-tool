using System.Diagnostics;

namespace frm_avg_unin_support_tool
{
    // Centralises all Windows system command invocations (bcdedit, shutdown, netsh).
    // Every method returns a bool or exits cleanly so callers can verify success.
    internal static class WindowsSystemCommands
    {
        // ── Safe-boot ─────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the current BCD entry to boot into Safe Mode (Minimal).
        /// </summary>
        /// <returns><c>true</c> if bcdedit exited 0.</returns>
        internal static bool EnableSafeBootMinimal()
        {
            Logger.Write("WindowsSystemCommands: EnableSafeBootMinimal");
            int exit = Program.RunSysCmd("bcdedit.exe", "/set {current} safeboot minimal");
            if (exit != 0)
                Logger.Write("EnableSafeBootMinimal: bcdedit exited {0}", exit);
            return exit == 0;
        }

        /// <summary>
        /// Removes the safeboot flag so the next reboot returns to Normal Mode.
        /// </summary>
        /// <returns>
        /// <c>true</c> if bcdedit exited 0.
        /// IMPORTANT: callers must NOT restart if this returns <c>false</c>.
        /// </returns>
        internal static bool DisableSafeBoot()
        {
            Logger.Write("WindowsSystemCommands: DisableSafeBoot");
            int exit = Program.RunSysCmd("bcdedit.exe", "/deletevalue {current} safeboot");
            if (exit != 0)
                Logger.Write("DisableSafeBoot: bcdedit exited {0}", exit);
            return exit == 0;
        }

        // ── Shutdown / Restart ────────────────────────────────────────────────

        /// <summary>Schedules a forced restart. /f closes blocking apps without prompting.</summary>
        internal static void RestartForced(int delaySeconds)
        {
            Logger.Write("WindowsSystemCommands: RestartForced t={0}", delaySeconds);
            Program.RunSysCmd("shutdown.exe", "/r /f /t " + delaySeconds);
        }

        /// <summary>Cancels a pending shutdown/restart initiated by RestartForced.</summary>
        internal static void AbortShutdown()
        {
            Logger.Write("WindowsSystemCommands: AbortShutdown");
            Program.RunSysCmd("shutdown.exe", "/a");
        }

        /// <summary>Forces an immediate restart (t=0).</summary>
        internal static void RestartImmediate()
            => RestartForced(0);

        // ── Network settings backup / restore ─────────────────────────────────
        // Backs up TCP/IP interface configuration before Safe Mode so it can be
        // restored if the network adapter settings are disrupted by avgclear.

        internal static bool BackupNetworkSettings(string backupFile)
        {
            Logger.Write("WindowsSystemCommands: BackupNetworkSettings → {0}", backupFile);
            int exit = Program.RunSysCmd("netsh.exe",
                "interface dump > \"" + backupFile + "\"");
            // netsh dump uses shell redirect; run via cmd /c for redirection to work
            exit = RunViaCmd("netsh interface dump > \"" + backupFile + "\"");
            if (exit != 0)
                Logger.Write("BackupNetworkSettings: exited {0}", exit);
            return exit == 0;
        }

        internal static bool RestoreNetworkSettings(string backupFile)
        {
            Logger.Write("WindowsSystemCommands: RestoreNetworkSettings ← {0}", backupFile);
            int exit = Program.RunSysCmd("netsh.exe", "exec \"" + backupFile + "\"");
            if (exit != 0)
                Logger.Write("RestoreNetworkSettings: netsh exited {0}", exit);
            return exit == 0;
        }

        // ── Helper: run via cmd.exe for shell features (redirection) ──────────

        private static int RunViaCmd(string command)
        {
            string winDir = System.Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            bool needSysnative = !System.Environment.Is64BitProcess
                                 && System.Environment.Is64BitOperatingSystem;
            string sysDir = needSysnative
                ? System.IO.Path.Combine(winDir, "Sysnative")
                : System.IO.Path.Combine(winDir, "System32");

            Process p = null;
            try
            {
                p = Process.Start(new ProcessStartInfo(
                    System.IO.Path.Combine(sysDir, "cmd.exe"),
                    "/c " + command)
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                });
                if (p == null) return -1;
                p.WaitForExit();
                return p.ExitCode;
            }
            catch (System.Exception ex)
            {
                Logger.WriteException("RunViaCmd", ex);
                return -1;
            }
            finally { p?.Dispose(); }
        }
    }
}
