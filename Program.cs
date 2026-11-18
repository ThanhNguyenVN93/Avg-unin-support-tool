using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace frm_avg_unin_support_tool
{
    internal static class Program
    {
        // ── Version ────────────────────────────────────────────────────────────
        public const string AppVersion = "1.1.0";

        // ── Mutex / single-instance ────────────────────────────────────────────
        private const string MutexName = "Global\\AvgClearToolSingleInstance";

        // ── Registry keys ──────────────────────────────────────────────────────
        private const string RunPath           = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RunKey            = "*AvgClearTool";
        private const string FlagRegKey        = @"SOFTWARE\AvgClearTool";
        private const string FlagDoneValue     = "CleanupDone";
        private const string FlagSafeModeValue = "SafeModeTriggered";
        private const string AttemptCountValue = "AttemptCount";
        private const int    MaxSafeModeAttempts = 3;

        // ── Timing ─────────────────────────────────────────────────────────────
        private const int AvgClearTimeoutMs = 20 * 60 * 1000;  // 20 min hard limit

        // ── Staging directory (ProgramData) ────────────────────────────────────
        internal static string StagingDir =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AvgClearTool");
        private static string StagedExePath    => Path.Combine(StagingDir, "Avg unin support tool.exe");
        private static string StagedAvgClear   => Path.Combine(StagingDir, "avgclear.exe");
        private static string NetBackupFile    => Path.Combine(StagingDir, "netcfg.bak");

        // ── CLI flags ──────────────────────────────────────────────────────────
        internal static bool ArgAuto           { get; private set; }
        internal static bool ArgNoSafeMode     { get; private set; }
        private  static bool _argCleanLeftovers;

        // ── Mutex handle ───────────────────────────────────────────────────────
        private static Mutex _mutex;

        // ── Entry point ────────────────────────────────────────────────────────
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Logger.Init();
            ParseArgs(args);

            // Single instance
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                _mutex.Dispose();
                MessageBox.Show("The tool is already running.",
                    Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.Exit(1);
                return;
            }

            try
            {
                if (ArgAuto || _argCleanLeftovers)
                {
                    // CLI paths that don't need OS/admin checks first
                }
                else if (Environment.OSVersion.Version < new Version(6, 1, 7601))
                {
                    MessageBox.Show(
                        "This tool requires Windows 7 SP1 or later.",
                        "Unsupported OS", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(2);
                    return;
                }

                if (!IsRunningAsAdmin())
                {
                    MessageBox.Show(
                        "This tool must be run as Administrator.\n" +
                        "Please right-click the exe and select 'Run as administrator'.",
                        "Administrator Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(2);
                    return;
                }

                // Show help and exit
                if (Array.Exists(args, a => a.Equals("--help", StringComparison.OrdinalIgnoreCase)
                                         || a.Equals("-h", StringComparison.OrdinalIgnoreCase)
                                         || a.Equals("/?", StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(Strings.HelpText, Strings.AppTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Environment.Exit(0);
                    return;
                }

                // --cleanleftovers: registry-only cleanup, no Safe Mode
                if (_argCleanLeftovers)
                {
                    HandleCleanLeftovers();
                    return;
                }

                if (SystemInformation.BootMode != BootMode.Normal)
                    HandleSafeMode();
                else if (ArgNoSafeMode)
                    HandleNoSafeMode();
                else
                    HandleNormalMode();
            }
            finally
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _mutex.Dispose();
            }
        }

        // ── CLI parsing ────────────────────────────────────────────────────────

        private static void ParseArgs(string[] args)
        {
            foreach (var a in args)
            {
                string arg = a.ToLowerInvariant();
                if (arg == "--auto")            ArgAuto = true;
                else if (arg == "--nosafemode") ArgNoSafeMode = true;
                else if (arg == "--cleanleftovers") _argCleanLeftovers = true;
            }
            Logger.Write("Args: auto={0} nosafe={1} cleanleftovers={2}",
                ArgAuto, ArgNoSafeMode, _argCleanLeftovers);
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
            catch (Exception ex) { Logger.WriteException("IsRunningAsAdmin", ex); return false; }
        }

        // ── --cleanleftovers ───────────────────────────────────────────────────

        private static void HandleCleanLeftovers()
        {
            Logger.Write("HandleCleanLeftovers");
            RegistryHelper.CleanAvgRegistry();
            if (!ArgAuto)
                Application.Run(new DoneForm());
            Environment.Exit(0);
        }

        // ── --nosafemode ───────────────────────────────────────────────────────

        private static void HandleNoSafeMode()
        {
            Logger.Write("HandleNoSafeMode");

            if (!AvgInstalled())
            {
                MessageBox.Show(Strings.AvgNotFound, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.Exit(3);
                return;
            }

            Directory.CreateDirectory(StagingDir);
            string avgClearPath = AvgClearDownloader.GetAvgClear(StagingDir);

            Logger.Write("HandleNoSafeMode: running avgclear directly");
            RunAvgClearProcess(avgClearPath);

            RegistryHelper.CleanAvgRegistry();
            CleanStagingDir();
            Application.Run(new DoneForm());
            Environment.Exit(0);
        }

        // ── Normal Mode ────────────────────────────────────────────────────────

        private static void HandleNormalMode()
        {
            Logger.Write("HandleNormalMode");
            CleanOldTemp();

            // Post-cleanup run: Safe Mode finished, clean registry remnants
            if (FlagDoneExists())
            {
                Logger.Write("HandleNormalMode: FlagDone detected — running post-cleanup");
                DeleteFlagDone();
                RegistryHelper.CleanAvgRegistry();
                BackupNetworkIfExists();   // restore network settings if backed up
                ResetAttemptCount();
                CleanStagingDir();
                Application.Run(new DoneForm());
                Environment.Exit(0);
                return;
            }

            if (!AvgInstalled())
            {
                MessageBox.Show(Strings.AvgNotFound, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                Environment.Exit(3);
                return;
            }

            // Guard: too many failed Safe Mode attempts
            int attempts = GetAttemptCount();
            if (attempts >= MaxSafeModeAttempts)
            {
                Logger.Write("HandleNormalMode: attempt count {0} >= {1} — aborting", attempts, MaxSafeModeAttempts);
                ResetAttemptCount();
                MessageBox.Show(Strings.TooManyAttempts, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Environment.Exit(2);
                return;
            }

            // Create a system restore point before making any changes
            TryCreateRestorePoint();

            // Prepare staging: download/extract avgclear + copy this exe
            Directory.CreateDirectory(StagingDir);
            Logger.Write("HandleNormalMode: preparing avgclear in StagingDir");
            AvgClearDownloader.GetAvgClear(StagingDir);   // saves to StagedAvgClear

            StageSelf();
            WriteSafeModeTrigger();
            IncrementAttemptCount();
            SetRunKey();

            // Back up network settings before bcdedit
            WindowsSystemCommands.BackupNetworkSettings(NetBackupFile);

            if (!WindowsSystemCommands.EnableSafeBootMinimal())
            {
                Logger.Write("HandleNormalMode: bcdedit failed — rolling back");
                DeleteRunKey();
                DeleteSafeModeTrigger();
                DecrementAttemptCount();
                MessageBox.Show(Strings.BcdSetFailedMessage, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(2);
                return;
            }

            // Show countdown form (or restart immediately in --auto mode)
            if (ArgAuto)
            {
                Logger.Write("HandleNormalMode: --auto restart");
                _ = Task.Run(() => WindowsSystemCommands.RestartForced(5));
                Thread.Sleep(1000);
            }
            else
            {
                Application.Run(new Form1());
            }
        }

        // ── Safe Mode ──────────────────────────────────────────────────────────

        private static void HandleSafeMode()
        {
            Logger.Write("HandleSafeMode");
            CleanOldTemp();

            // Always remove autologon credentials first (security: clear password ASAP)
            AutoLogon.Remove();

            if (!SafeModeTriggerExists())
            {
                Logger.Write("HandleSafeMode: trigger absent — manual Safe Mode, exiting");
                return;
            }

            DeleteRunKey();

            try
            {
                EnableMsiInSafeMode();

                // Use pre-downloaded avgclear if available, else extract embedded
                string avgClearPath = File.Exists(StagedAvgClear)
                    ? StagedAvgClear
                    : Path.Combine(StagingDir, "avgclear.exe");

                if (!File.Exists(avgClearPath))
                {
                    Directory.CreateDirectory(StagingDir);
                    AvgClearDownloader.ExtractEmbedded(avgClearPath);
                }

                RunAvgClearProcess(avgClearPath);
            }
            catch (Win32Exception w32)
            {
                Logger.Write("HandleSafeMode Win32Exception {0}: {1}", w32.NativeErrorCode, w32.Message);
                MessageBox.Show(Strings.AvgClearBlocked, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (InvalidOperationException ioe)
            {
                Logger.Write("HandleSafeMode launch error: {0}", ioe.Message);
                MessageBox.Show(ioe.Message, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Logger.WriteException("HandleSafeMode/run", ex);
                MessageBox.Show(Strings.RunToolError + "\n" + ex.Message,
                    Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // CRITICAL: verify bcdedit /deletevalue succeeded before restarting.
            // If the BCD store is locked, do NOT restart — stay in Safe Mode and retry.
            if (!WindowsSystemCommands.DisableSafeBoot())
            {
                Logger.Write("HandleSafeMode: DisableSafeBoot FAILED — aborting restart to prevent loop");
                MessageBox.Show(Strings.BcdClearFailedMessage, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            DeleteSafeModeTrigger();
            WriteFlagDone();

            _ = Task.Run(() => WindowsSystemCommands.RestartForced(30));

            MessageBox.Show(Strings.RestartingNow, Strings.AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            WindowsSystemCommands.RestartImmediate();
        }

        // ── avgclear process runner ────────────────────────────────────────────

        internal static void RunAvgClearProcess(string exePath)
        {
            Logger.Write("RunAvgClearProcess: {0}", exePath);
            string workDir = Path.GetDirectoryName(exePath) ?? StagingDir;

            Process proc = null;
            try
            {
                try
                {
                    proc = Process.Start(new ProcessStartInfo(exePath)
                    {
                        UseShellExecute  = true,
                        WorkingDirectory = workDir,
                    });
                }
                catch (Exception launchEx) when (launchEx is Win32Exception
                                              || launchEx is FileNotFoundException
                                              || launchEx is InvalidOperationException)
                {
                    Logger.WriteException("RunAvgClearProcess.Start", launchEx);
                    throw new InvalidOperationException(Strings.AvgClearBlocked, launchEx);
                }

                if (proc != null)
                {
                    bool finished = proc.WaitForExit(AvgClearTimeoutMs);
                    if (!finished)
                    {
                        Logger.Write("RunAvgClearProcess: timeout after 20 min — killing");
                        try { proc.Kill(); } catch (Exception ex) { Logger.WriteException("Kill", ex); }
                    }
                    else
                    {
                        Logger.Write("RunAvgClearProcess: exited with code {0}", proc.ExitCode);
                    }
                }
            }
            finally { proc?.Dispose(); }
        }

        // ── MSI in Safe Mode ───────────────────────────────────────────────────

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
                Logger.Write("EnableMsiInSafeMode: OK");
            }
            catch (Exception ex) { Logger.WriteException("EnableMsiInSafeMode", ex); }
        }

        // ── Self-staging ───────────────────────────────────────────────────────

        private static void StageSelf()
        {
            try
            {
                string src = Assembly.GetExecutingAssembly().Location;
                File.Copy(src, StagedExePath, overwrite: true);
                Logger.Write("StageSelf: → {0}", StagedExePath);
            }
            catch (Exception ex) { Logger.WriteException("StageSelf", ex); }
        }

        // ── System Restore Point ──────────────────────────────────────────────

        private static void TryCreateRestorePoint()
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\default");
                using (var cls = new ManagementClass(scope,
                    new ManagementPath("SystemRestore"), new ObjectGetOptions()))
                using (var inParams = cls.GetMethodParameters("CreateRestorePoint"))
                {
                    inParams["Description"]      = "Before AVG removal — AVG Uninstaller Support Tool";
                    inParams["RestorePointType"] = 12u;  // APPLICATION_UNINSTALL
                    inParams["EventType"]        = 100u; // BEGIN_SYSTEM_CHANGE
                    cls.InvokeMethod("CreateRestorePoint", inParams, null);
                }
                Logger.Write("TryCreateRestorePoint: OK");
            }
            catch (Exception ex)
            {
                Logger.WriteException("TryCreateRestorePoint", ex);
                // Non-fatal: System Restore may be disabled on this machine
            }
        }

        // ── Network backup helper ─────────────────────────────────────────────

        private static void BackupNetworkIfExists()
        {
            if (!File.Exists(NetBackupFile)) return;
            try
            {
                // Silently attempt to restore; failure is non-fatal
                WindowsSystemCommands.RestoreNetworkSettings(NetBackupFile);
                File.Delete(NetBackupFile);
                Logger.Write("BackupNetworkIfExists: network settings restored");
            }
            catch (Exception ex) { Logger.WriteException("BackupNetworkIfExists", ex); }
        }

        // ── Temp / staging cleanup ─────────────────────────────────────────────

        private static void CleanOldTemp()
        {
            try
            {
                // Legacy temp dir from older builds
                string old = Path.Combine(Path.GetTempPath(), "avg_clear_temp");
                if (Directory.Exists(old)) Directory.Delete(old, recursive: true);
            }
            catch (Exception ex) { Logger.WriteException("CleanOldTemp", ex); }
        }

        internal static void CleanStagingDir()
        {
            DeleteTempFolderWithRetry(StagingDir);
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
                            if (key != null) { Logger.Write("AvgInstalled: found registry {0}", path); return true; }
                    }
                }
            }
            catch (Exception ex) { Logger.WriteException("AvgInstalled(registry)", ex); }

            string pf64 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (Directory.Exists(Path.Combine(pf64, "AVG", "Antivirus"))) { Logger.Write("AvgInstalled: found PF64\\AVG\\Antivirus"); return true; }
            if (Directory.Exists(Path.Combine(pf64, "AVG")))              { Logger.Write("AvgInstalled: found PF64\\AVG"); return true; }
            if (Directory.Exists(Path.Combine(pf86, "AVG")))              { Logger.Write("AvgInstalled: found PF86\\AVG"); return true; }

            Logger.Write("AvgInstalled: not found");
            return false;
        }

        // ── RunOnce key ────────────────────────────────────────────────────────

        internal static void SetRunKey()
        {
            try
            {
                // Always point RunOnce to the STAGED exe so the path survives reboot
                string target = File.Exists(StagedExePath)
                    ? StagedExePath
                    : Assembly.GetExecutingAssembly().Location;

                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(RunPath, writable: true))
                    key?.SetValue(RunKey, "\"" + target + "\"");
                Logger.Write("SetRunKey: → {0}", target);
            }
            catch (Exception ex) { Logger.WriteException("SetRunKey", ex); }
        }

        internal static void DeleteRunKey()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(RunPath, writable: true))
                    key?.DeleteValue(RunKey, throwOnMissingValue: false);
                Logger.Write("DeleteRunKey: done");
            }
            catch (Exception ex) { Logger.WriteException("DeleteRunKey", ex); }
        }

        // ── SafeMode trigger flag ──────────────────────────────────────────────

        private static void WriteSafeModeTrigger()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.CreateSubKey(FlagRegKey))
                    key.SetValue(FlagSafeModeValue, DateTime.UtcNow.Ticks, RegistryValueKind.QWord);
                Logger.Write("WriteSafeModeTrigger: written");
            }
            catch (Exception ex) { Logger.WriteException("WriteSafeModeTrigger", ex); }
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
                    return (DateTime.UtcNow - written).TotalHours < 6;  // 6-hour staleness guard
                }
            }
            catch (Exception ex) { Logger.WriteException("SafeModeTriggerExists", ex); return false; }
        }

        internal static void DeleteSafeModeTrigger()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(FlagRegKey, writable: true))
                    key?.DeleteValue(FlagSafeModeValue, throwOnMissingValue: false);
                Logger.Write("DeleteSafeModeTrigger: done");
            }
            catch (Exception ex) { Logger.WriteException("DeleteSafeModeTrigger", ex); }
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
            catch (Exception ex) { Logger.WriteException("FlagDoneExists", ex); return false; }
        }

        private static void WriteFlagDone()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.CreateSubKey(FlagRegKey))
                    key.SetValue(FlagDoneValue, DateTime.UtcNow.Ticks, RegistryValueKind.QWord);
                Logger.Write("WriteFlagDone: written");
            }
            catch (Exception ex) { Logger.WriteException("WriteFlagDone", ex); }
        }

        private static void DeleteFlagDone()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    hklm.DeleteSubKeyTree(FlagRegKey, throwOnMissingSubKey: false);
                Logger.Write("DeleteFlagDone: done");
            }
            catch (Exception ex) { Logger.WriteException("DeleteFlagDone", ex); }
        }

        // ── Attempt counter ────────────────────────────────────────────────────

        private static int GetAttemptCount()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(FlagRegKey))
                    return (int)(key?.GetValue(AttemptCountValue, 0) ?? 0);
            }
            catch { return 0; }
        }

        private static void IncrementAttemptCount()
        {
            try
            {
                int n = GetAttemptCount() + 1;
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.CreateSubKey(FlagRegKey))
                    key.SetValue(AttemptCountValue, n, RegistryValueKind.DWord);
                Logger.Write("AttemptCount: {0}", n);
            }
            catch (Exception ex) { Logger.WriteException("IncrementAttemptCount", ex); }
        }

        private static void DecrementAttemptCount()
        {
            try
            {
                int n = Math.Max(0, GetAttemptCount() - 1);
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.CreateSubKey(FlagRegKey))
                    key.SetValue(AttemptCountValue, n, RegistryValueKind.DWord);
            }
            catch (Exception ex) { Logger.WriteException("DecrementAttemptCount", ex); }
        }

        internal static void ResetAttemptCount()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(FlagRegKey, writable: true))
                    key?.DeleteValue(AttemptCountValue, throwOnMissingValue: false);
                Logger.Write("AttemptCount: reset");
            }
            catch (Exception ex) { Logger.WriteException("ResetAttemptCount", ex); }
        }

        // ── Temp folder with retry ─────────────────────────────────────────────

        private static void DeleteTempFolderWithRetry(string dir)
        {
            const int maxRetries   = 5;
            const int retryDelayMs = 1000;
            for (int i = 1; i <= maxRetries; i++)
            {
                try
                {
                    if (!Directory.Exists(dir)) return;
                    Directory.Delete(dir, recursive: true);
                    Logger.Write("DeleteTempFolderWithRetry: deleted {0} on attempt {1}", dir, i);
                    return;
                }
                catch (Exception ex)
                {
                    Logger.Write("DeleteTempFolderWithRetry attempt {0}/{1}: {2}", i, maxRetries, ex.Message);
                    if (i < maxRetries) Thread.Sleep(retryDelayMs);
                }
            }
            Logger.Write("DeleteTempFolderWithRetry: gave up — will retry on next launch");
        }

        // ── System command runner ──────────────────────────────────────────────

        /// <returns>Process exit code, or -1 if the process could not start.</returns>
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
                return p.ExitCode;
            }
            catch (Exception ex)
            {
                Logger.WriteException("RunSysCmd [" + exe + " " + args + "]", ex);
                return -1;
            }
            finally { p?.Dispose(); }
        }
    }
}
