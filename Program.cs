using System;
using System.ComponentModel;          // Win32Exception
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;         // Task.Run
using System.Windows.Forms;
using Microsoft.Win32;

namespace frm_avg_unin_support_tool
{
    internal static class Program
    {
        // ── Constants ──────────────────────────────────────────────────────────
        private const string MutexName         = "Global\\AvgClearToolSingleInstance";
        private const string RunPath           = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RunKey            = "*AvgClearTool";
        private const string FlagRegKey        = @"SOFTWARE\AvgClearTool";
        private const string FlagDoneValue     = "CleanupDone";
        private const string FlagSafeModeValue = "SafeModeTriggered";
        private const string TempDirName       = "avg_clear_temp";
        private const int    AvgClearTimeoutMs = 20 * 60 * 1000; // 20 min hard timeout

        private static Mutex _mutex;

        // ── Entry point ────────────────────────────────────────────────────────
        // 3 — DPI awareness is declared via app.manifest (<dpiAware>true/pm</dpiAware>
        //     + <dpiAwareness>PerMonitorV2</dpiAwareness>) and app.config
        //     (DpiAwareness = PerMonitorV2).  The old manual SetProcessDPIAware()
        //     P/Invoke is removed: calling it alongside the runtime's own scaling
        //     mechanism can create orphaned GDI handles.  The manifest approach
        //     is the Microsoft-recommended path for .NET Framework 4.8.

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Single instance
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                MessageBox.Show("The tool is already running.",
                    "AVG Uninstaller Support Tool",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // Minimum OS: Windows 7 SP1 (6.1.7601)
                if (Environment.OSVersion.Version < new Version(6, 1, 7601))
                {
                    MessageBox.Show(
                        "This tool requires Windows 7 SP1 or later.\n" +
                        "Windows 7 RTM and below are not supported.",
                        "Unsupported OS",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!IsRunningAsAdmin())
                {
                    MessageBox.Show(
                        "This tool must be run as Administrator.\n" +
                        "Please right-click the exe and select 'Run as administrator'.",
                        "Administrator Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (SystemInformation.BootMode != BootMode.Normal)
                    HandleSafeMode();
                else
                    HandleNormalMode();
            }
            finally
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _mutex.Dispose();
            }
        }

        // ── Admin check ────────────────────────────────────────────────────────

        private static bool IsRunningAsAdmin()
        {
            try
            {
                var id        = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex) { Debug.WriteLine("IsRunningAsAdmin: " + ex.Message); return false; }
        }

        // ── Normal Mode ────────────────────────────────────────────────────────

        private static void HandleNormalMode()
        {
            CleanTempFolder();

            if (FlagDoneExists())
            {
                DeleteFlagDone();
                RegistryHelper.CleanAvgRegistry();
                Application.Run(new DoneForm());
                return;
            }

            if (!AvgInstalled())
            {
                MessageBox.Show(Strings.AvgNotFound, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            WriteSafeModeTrigger();
            SetRunKey();

            // 1 — Verify bcdedit succeeded before showing the countdown form.
            //     If the BCD store is locked (BitLocker / Fast Startup), abort
            //     and clean up so the Run key + trigger don't orphan.
            if (!WindowsSystemCommands.EnableSafeBootMinimal())
            {
                DeleteRunKey();
                DeleteSafeModeTrigger();
                MessageBox.Show(Strings.BcdSetFailedMessage, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Application.Run(new Form1());
        }

        // ── Safe Mode ──────────────────────────────────────────────────────────

        private static void HandleSafeMode()
        {
            CleanTempFolder();

            if (!SafeModeTriggerExists())
            {
                Debug.WriteLine("HandleSafeMode: trigger absent — manual Safe Mode, exiting.");
                return;
            }

            DeleteRunKey();   // self-remove; we own the lifecycle from here

            try
            {
                EnableMsiInSafeMode();
                ExtractAndRunAvgClear();
            }
            catch (Win32Exception w32)
            {
                // Direct Win32Exception from EnableMsiInSafeMode (registry write blocked)
                Debug.WriteLine("HandleSafeMode Win32Exception " + w32.NativeErrorCode + ": " + w32.Message);
                MessageBox.Show(Strings.AvgClearBlocked, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (InvalidOperationException ioe)
            {
                // 2 — Process-launch failure wrapped in ExtractAndRunAvgClear.
                //     ioe.Message is already the user-friendly AvgClearBlocked string.
                Debug.WriteLine("HandleSafeMode launch error: " + ioe.Message +
                    (ioe.InnerException != null ? " ← " + ioe.InnerException.Message : ""));
                MessageBox.Show(ioe.Message, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("HandleSafeMode/run: " + ex.Message);
                MessageBox.Show(Strings.RunToolError + "\n" + ex.Message,
                    Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // 1 — CRITICAL: verify bcdedit /deletevalue succeeded before restarting.
            //     If the BCD store is locked, abort entirely — do NOT restart and do NOT
            //     delete the trigger flag.  Next Safe Mode boot will retry automatically.
            if (!WindowsSystemCommands.DisableSafeBoot())
            {
                Debug.WriteLine("HandleSafeMode: DisableSafeBoot failed — aborting restart to prevent infinite Safe Mode loop.");
                MessageBox.Show(Strings.BcdClearFailedMessage, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // BCD reverted successfully — safe to clean up and restart
            DeleteSafeModeTrigger();
            WriteFlagDone();

            // 1 — Fire RestartForced(30) on a background thread so the 30-s
            //     auto-restart countdown starts immediately in the Windows kernel.
            //     shutdown.exe itself returns in milliseconds; the countdown is
            //     owned by the kernel, so no thread is blocked while waiting.
            //     Fire-and-forget is intentional: we do not need the return value.
            _ = Task.Run(() => WindowsSystemCommands.RestartForced(30));

            // Show notification while the Win32 GUI is fully responsive.
            // The MessageBox renders cleanly because no shutdown UI is in
            // progress yet (shutdown.exe exited already; kernel owns countdown).
            // If user walks away: Windows auto-restarts after 30 s.
            // If user clicks OK: RestartImmediate overrides the countdown to 0.
            MessageBox.Show(Strings.RestartingNow, Strings.AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            WindowsSystemCommands.RestartImmediate();
        }

        // ── Extract & run avgclear ─────────────────────────────────────────────

        private static void ExtractAndRunAvgClear()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), TempDirName);
            Directory.CreateDirectory(tempDir);
            string dest = Path.Combine(tempDir, "avgclear.exe");

            using (var s = Assembly.GetExecutingAssembly()
                                   .GetManifestResourceStream("frm_avg_unin_support_tool.avgclear.exe"))
            {
                if (s == null)
                    throw new InvalidOperationException(
                        "Embedded resource 'avgclear.exe' not found. " +
                        "Verify Build Action = EmbeddedResource in the project.");

                using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write))
                    s.CopyTo(fs);
            }

            Process proc = null;
            try
            {
                // 2 — Exception filter: catch all process-launch failures uniformly
                //     and surface a user-friendly message regardless of error type.
                //     • Win32Exception     = AV blocked execution (0x5 Access Denied)
                //     • FileNotFoundException = AV deleted the temp file after extraction
                //     • InvalidOperationException = process state invalid
                //     All three are wrapped as InvalidOperationException(AvgClearBlocked)
                //     so HandleSafeMode's catch chain shows one friendly dialog.
                try
                {
                    proc = Process.Start(new ProcessStartInfo(dest)
                    {
                        UseShellExecute  = true,
                        WorkingDirectory = tempDir,
                    });
                }
                catch (Exception launchEx) when (launchEx is Win32Exception
                                              || launchEx is FileNotFoundException
                                              || launchEx is InvalidOperationException)
                {
                    Debug.WriteLine("Process.Start (" + launchEx.GetType().Name + "): " + launchEx.Message);
                    throw new InvalidOperationException(Strings.AvgClearBlocked, launchEx);
                }

                if (proc != null)
                {
                    bool finished = proc.WaitForExit(AvgClearTimeoutMs);
                    if (!finished)
                    {
                        Debug.WriteLine("avgclear.exe exceeded 20-min timeout — killing.");
                        try { proc.Kill(); } catch (Exception ex) { Debug.WriteLine("Kill: " + ex.Message); }
                    }
                }
            }
            finally
            {
                proc?.Dispose();    // guaranteed even if launchEx was rethrown
            }

            DeleteTempFolderWithRetry(tempDir); // 3 — retry loop instead of fixed Sleep
        }

        private static void EnableMsiInSafeMode()
        {
            string[] paths =
            {
                @"SYSTEM\CurrentControlSet\Control\SafeBoot\Minimal\MSIServer",
                @"SYSTEM\CurrentControlSet\Control\SafeBoot\Network\MSIServer",
            };
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    foreach (string p in paths)
                    {
                        using (var key = hklm.CreateSubKey(p))
                            key?.SetValue("", "Service");
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("EnableMsiInSafeMode: " + ex.Message); }
        }

        private static void CleanTempFolder()
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), TempDirName);
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex) { Debug.WriteLine("CleanTempFolder: " + ex.Message); }
        }

        // ── AVG detection ──────────────────────────────────────────────────────

        private static bool AvgInstalled()
        {
            string[] regPaths =
            {
                @"SOFTWARE\AVG",
                @"SOFTWARE\WOW6432Node\AVG",
                @"SOFTWARE\AVGTechnologies",
                @"SOFTWARE\WOW6432Node\AVGTechnologies",
            };

            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                {
                    foreach (string path in regPaths)
                    {
                        using (var key = hklm.OpenSubKey(path))
                            if (key != null) return true;
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine("AvgInstalled (registry): " + ex.Message); }

            // 5 — Use environment variables instead of hardcoded C:\ paths
            string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (Directory.Exists(Path.Combine(pf64, "AVG", "Antivirus"))) return true;
            if (Directory.Exists(Path.Combine(pf64, "AVG")))               return true;
            if (Directory.Exists(Path.Combine(pf86, "AVG")))               return true;

            return false;
        }

        // ── Run key ────────────────────────────────────────────────────────────

        // ── 1 — Temp-folder deletion with retry ───────────────────────────────
        //    Thread.Sleep(fixed) is unreliable on slow HDD machines.
        //    This loop retries up to 5 times with 1-second gaps, then gives up
        //    gracefully — CleanTempFolder() will remove leftovers on the next run.
        private static void DeleteTempFolderWithRetry(string tempDir)
        {
            const int maxRetries  = 5;
            const int retryDelayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (!Directory.Exists(tempDir)) return;
                    Directory.Delete(tempDir, recursive: true);
                    Debug.WriteLine("DeleteTempFolderWithRetry: deleted on attempt " + attempt);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("DeleteTempFolderWithRetry attempt " +
                        attempt + "/" + maxRetries + ": " + ex.Message);
                    if (attempt < maxRetries)
                        Thread.Sleep(retryDelayMs);
                }
            }
            Debug.WriteLine("DeleteTempFolderWithRetry: all attempts failed — " +
                "CleanTempFolder() will handle it on the next launch.");
        }

        internal static void SetRunKey()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(RunPath, writable: true))
                    key?.SetValue(RunKey, "\"" + Application.ExecutablePath + "\"");
            }
            catch (Exception ex) { Debug.WriteLine("SetRunKey: " + ex.Message); }
        }

        internal static void DeleteRunKey()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(RunPath, writable: true))
                    key?.DeleteValue(RunKey, throwOnMissingValue: false);
            }
            catch (Exception ex) { Debug.WriteLine("DeleteRunKey: " + ex.Message); }
        }

        // ── SafeMode trigger flag ──────────────────────────────────────────────

        private static void WriteSafeModeTrigger()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.CreateSubKey(FlagRegKey))
                    key.SetValue(FlagSafeModeValue, DateTime.UtcNow.Ticks, RegistryValueKind.QWord);
            }
            catch (Exception ex) { Debug.WriteLine("WriteSafeModeTrigger: " + ex.Message); }
        }

        private static bool SafeModeTriggerExists()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(FlagRegKey))
                {
                    if (key == null) return false;
                    object raw = key.GetValue(FlagSafeModeValue);
                    if (raw == null) return false;
                    var written = new DateTime(Convert.ToInt64(raw), DateTimeKind.Utc);
                    return (DateTime.UtcNow - written).TotalHours < 24;
                }
            }
            catch (Exception ex) { Debug.WriteLine("SafeModeTriggerExists: " + ex.Message); return false; }
        }

        internal static void DeleteSafeModeTrigger()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(FlagRegKey, writable: true))
                    key?.DeleteValue(FlagSafeModeValue, throwOnMissingValue: false);
            }
            catch (Exception ex) { Debug.WriteLine("DeleteSafeModeTrigger: " + ex.Message); }
        }

        // ── Completion flag ────────────────────────────────────────────────────

        private static bool FlagDoneExists()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(FlagRegKey))
                {
                    if (key == null) return false;
                    object raw = key.GetValue(FlagDoneValue);
                    if (raw == null) return false;
                    var written = new DateTime(Convert.ToInt64(raw), DateTimeKind.Utc);
                    return (DateTime.UtcNow - written).TotalHours < 2;
                }
            }
            catch (Exception ex) { Debug.WriteLine("FlagDoneExists: " + ex.Message); return false; }
        }

        private static void WriteFlagDone()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.CreateSubKey(FlagRegKey))
                    key.SetValue(FlagDoneValue, DateTime.UtcNow.Ticks, RegistryValueKind.QWord);
            }
            catch (Exception ex) { Debug.WriteLine("WriteFlagDone: " + ex.Message); }
        }

        private static void DeleteFlagDone()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    hklm.DeleteSubKeyTree(FlagRegKey, throwOnMissingSubKey: false);
            }
            catch (Exception ex) { Debug.WriteLine("DeleteFlagDone: " + ex.Message); }
        }

        // ── System helper ──────────────────────────────────────────────────────

        /// <summary>
        /// 1+2 — Runs an exe from System32/Sysnative.
        ///   • Returns the process exit code so callers (bcdedit) can verify success.
        ///   • Uses try/finally + explicit Dispose to prevent handle leaks when
        ///     Process.Start throws Win32Exception before the variable is assigned.
        /// </summary>
        /// <returns>Exit code (0 = success), or -1 if the process could not start.</returns>
        internal static int RunSysCmd(string exe, string args)
        {
            string winDir = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            bool needSysnative = !Environment.Is64BitProcess && Environment.Is64BitOperatingSystem;
            string sysDir = needSysnative
                ? Path.Combine(winDir, "Sysnative")
                : Path.Combine(winDir, "System32");

            Process p = null;
            try
            {
                p = Process.Start(new ProcessStartInfo(Path.Combine(sysDir, exe))
                {
                    Arguments       = args,
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                });
                if (p == null) return -1;
                p.WaitForExit();
                return p.ExitCode;   // 1 — caller can check bcdedit success
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RunSysCmd [" + exe + " " + args + "]: " + ex.Message);
                return -1;
            }
            finally
            {
                p?.Dispose();        // 2 — guaranteed even if Start() threw
            }
        }
    }
}
