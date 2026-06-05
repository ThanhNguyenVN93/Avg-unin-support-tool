using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace frm_avg_unin_support_tool
{
    internal static class Program
    {
        private const string RunOncePath  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
        private const string RunOnceKey   = "*AvgClearSafeMode";
        private const string FlagRegKey   = @"SOFTWARE\AvgClearTool";
        private const string FlagRegValue = "CleanupDone";

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (SystemInformation.BootMode != BootMode.Normal)
                HandleSafeMode();
            else
                HandleNormalMode();
        }

        // ── Normal Mode ────────────────────────────────────────────────────────

        private static void HandleNormalMode()
        {
            if (FlagExists())
            {
                DeleteFlag();
                RegistryHelper.CleanAvgRegistry();   // xóa registry còn sót — ẩn, không hiện dialog
                Application.Run(new DoneForm());
                return;
            }

            if (!AvgInstalled())
            {
                MessageBox.Show(Strings.AvgNotFound, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetRunOnce();
            RunSysCmd("bcdedit.exe", "/set {current} safeboot minimal");

            Application.Run(new Form1());
        }

        internal static void SetRunOnce()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(RunOncePath, writable: true))
                    key?.SetValue(RunOnceKey, "\"" + Application.ExecutablePath + "\"");
            }
            catch { }
        }

        internal static void CancelRunOnce()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(RunOncePath, writable: true))
                    key?.DeleteValue(RunOnceKey, throwOnMissingValue: false);
            }
            catch { }
        }

        // ── Safe Mode ──────────────────────────────────────────────────────────

        private static void HandleSafeMode()
        {
            CancelRunOnce();

            try
            {
                ExtractAndRunAvgClear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Strings.RunToolError + "\n" + ex.Message,
                    Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                RunSysCmd("bcdedit.exe", "/deletevalue {current} safeboot");
                WriteFlag();
                MessageBox.Show(Strings.RestartingNow, Strings.AppTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                RunSysCmd("shutdown.exe", "/r /t 5");
            }
        }

        private static void ExtractAndRunAvgClear()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "avg_clear_temp");
            Directory.CreateDirectory(tempDir);
            string dest = Path.Combine(tempDir, "avgclear.exe");

            using (var s  = Assembly.GetExecutingAssembly()
                                    .GetManifestResourceStream("frm_avg_unin_support_tool.avgclear.exe"))
            using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write))
                s.CopyTo(fs);

            using (var proc = Process.Start(new ProcessStartInfo(dest)
            {
                UseShellExecute  = true,
                WorkingDirectory = tempDir,
            }))
                proc?.WaitForExit();

            try { Directory.Delete(tempDir, recursive: true); } catch { }
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
            catch { }

            if (Directory.Exists(@"C:\Program Files\AVG\Antivirus")) return true;
            if (Directory.Exists(@"C:\Program Files\AVG"))           return true;
            if (Directory.Exists(@"C:\Program Files (x86)\AVG"))     return true;

            return false;
        }

        // ── Completion flag ────────────────────────────────────────────────────

        private static bool FlagExists()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(FlagRegKey))
                {
                    if (key == null) return false;
                    object raw = key.GetValue(FlagRegValue);
                    if (raw == null) return false;
                    var written = new DateTime(Convert.ToInt64(raw), DateTimeKind.Utc);
                    return (DateTime.UtcNow - written).TotalHours < 2;
                }
            }
            catch { return false; }
        }

        private static void WriteFlag()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.CreateSubKey(FlagRegKey))
                    key.SetValue(FlagRegValue, DateTime.UtcNow.Ticks, RegistryValueKind.QWord);
            }
            catch { }
        }

        private static void DeleteFlag()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    hklm.DeleteSubKeyTree(FlagRegKey, throwOnMissingSubKey: false);
            }
            catch { }
        }

        // ── System helpers ─────────────────────────────────────────────────────

        internal static void RunSysCmd(string exe, string args)
        {
            string winDir = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            string sysDir = Environment.Is64BitProcess
                ? Path.Combine(winDir, "System32")
                : Path.Combine(winDir, "Sysnative");
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(Path.Combine(sysDir, exe))
                {
                    Arguments       = args,
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                }))
                    p?.WaitForExit();
            }
            catch { }
        }
    }
}
