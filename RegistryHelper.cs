using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace frm_avg_unin_support_tool
{
    internal static class RegistryHelper
    {
        private const string AvgPersistentStorageName = "AvgPersistentStorage";

        private struct AvgRegistryEntry
        {
            public RegistryHive Hive;
            public string       Path;
            public RegistryView View;
            /// <summary>
            /// When true, verifies AVG-owned sub-keys before deletion to prevent
            /// false-positive removal of unrelated software with similar names.
            /// </summary>
            public bool         ValidateOwner;
        }

        private static readonly AvgRegistryEntry[] AvgKeys =
        {
            new AvgRegistryEntry { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64,
                Path = @"SOFTWARE\AVG" },
            new AvgRegistryEntry { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64,
                Path = @"SOFTWARE\WOW6432Node\AVG" },

            // PersistentStorage — ValidateOwner = true (name collision risk)
            new AvgRegistryEntry { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64,
                Path = @"SOFTWARE\Classes\" + AvgPersistentStorageName, ValidateOwner = true },
            new AvgRegistryEntry { Hive = RegistryHive.ClassesRoot,  View = RegistryView.Default,
                Path = AvgPersistentStorageName,                            ValidateOwner = true },

            // Scheduled Task cache
            new AvgRegistryEntry { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64,
                Path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\AVG" },

            // PolicyManager — may be protected by Windows
            new AvgRegistryEntry { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64,
                Path = @"SOFTWARE\Microsoft\PolicyManager\default\ADMX_MicrosoftDefenderAntivirus\Scan_LocalSettingOverrideAvgCPULoadFactor" },
            new AvgRegistryEntry { Hive = RegistryHive.LocalMachine, View = RegistryView.Registry64,
                Path = @"SOFTWARE\Microsoft\PolicyManager\default\Defender\AvgCPULoadFactor" },
        };

        internal static void CleanAvgRegistry()
        {
            Logger.Write("RegistryHelper: CleanAvgRegistry — start");
            DisableRegistryVirtualization();
            EnablePrivileges();

            foreach (var entry in AvgKeys)
            {
                if (entry.ValidateOwner && !IsAvgOwnedPersistentStorage(entry.Hive, entry.Path, entry.View))
                {
                    Logger.Write("RegistryHelper: skip {0} (AVG ownership not confirmed)", entry.Path);
                    continue;
                }
                TryDeleteKey(entry.Hive, entry.Path, entry.View);
            }
            Logger.Write("RegistryHelper: CleanAvgRegistry — done");
        }

        // ── False-positive guard ──────────────────────────────────────────────

        private static bool IsAvgOwnedPersistentStorage(RegistryHive hive, string path, RegistryView view)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var key  = root.OpenSubKey(path))
                {
                    if (key == null) return false;
                    foreach (var sub in key.GetSubKeyNames())
                        if (sub.StartsWith("avg-", StringComparison.OrdinalIgnoreCase)) return true;
                    return key.GetValue("GUID") != null;
                }
            }
            catch (Exception ex)
            {
                Logger.WriteException("RegistryHelper.IsAvgOwnedPersistentStorage", ex);
                return false;
            }
        }

        // ── 3-step delete with fallback ───────────────────────────────────────

        private static void TryDeleteKey(RegistryHive hive, string path, RegistryView view)
        {
            if (!KeyExists(hive, path, view)) return;

            Logger.Write("RegistryHelper: deleting {0}\\{1}", HiveStr(hive), path);

            if (DirectDelete(hive, path, view)) return;

            if (!KeyExists(hive, path, view)) return;
            try { GrantFullControl(hive, path, view); }
            catch (Exception ex) { Logger.WriteException("RegistryHelper.GrantFullControl[" + path + "]", ex); }

            if (DirectDelete(hive, path, view)) return;

            if (!KeyExists(hive, path, view)) return;
            try
            {
                // /reg:64 forces 64-bit hive; without it a 32-bit process silently
                // operates on the WOW64-redirected branch instead.
                Program.RunSysCmd("reg.exe",
                    "delete \"" + HiveStr(hive) + "\\" + path + "\" /f /reg:64");
                Logger.Write("RegistryHelper: shell fallback for {0}", path);
            }
            catch (Exception ex) { Logger.WriteException("RegistryHelper.ShellDelete[" + path + "]", ex); }
        }

        private static bool KeyExists(RegistryHive hive, string path, RegistryView view)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                using (var key  = root.OpenSubKey(path))
                    return key != null;
            }
            catch { return false; }
        }

        private static bool DirectDelete(RegistryHive hive, string path, RegistryView view)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                    root.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
                Logger.Write("RegistryHelper: deleted {0}", path);
                return true;
            }
            catch (Exception ex) { Logger.WriteException("RegistryHelper.DirectDelete[" + path + "]", ex); return false; }
        }

        private static void GrantFullControl(RegistryHive hive, string path, RegistryView view)
        {
            var me = WindowsIdentity.GetCurrent().User;

            using (var root = RegistryKey.OpenBaseKey(hive, view))
            using (var key  = root.OpenSubKey(path,
                       RegistryKeyPermissionCheck.ReadWriteSubTree,
                       RegistryRights.TakeOwnership))
            {
                if (key == null) return;
                var sec = key.GetAccessControl(AccessControlSections.Owner);
                sec.SetOwner(me);
                key.SetAccessControl(sec);
            }

            using (var root = RegistryKey.OpenBaseKey(hive, view))
            using (var key  = root.OpenSubKey(path,
                       RegistryKeyPermissionCheck.ReadWriteSubTree,
                       RegistryRights.ChangePermissions))
            {
                if (key == null) return;
                var sec = key.GetAccessControl(AccessControlSections.Access);
                sec.AddAccessRule(new RegistryAccessRule(
                    me,
                    RegistryRights.FullControl,
                    InheritanceFlags.ContainerInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow));
                key.SetAccessControl(sec);
            }
        }

        private static string HiveStr(RegistryHive hive)
            => hive == RegistryHive.ClassesRoot  ? "HKCR"
             : hive == RegistryHive.LocalMachine ? "HKLM"
             : "HKCU";

        // ── P/Invoke ──────────────────────────────────────────────────────────

        private const uint TOKEN_QUERY             = 0x0008;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_ADJUST_DEFAULT    = 0x0080;
        private const uint SE_PRIVILEGE_ENABLED    = 0x0002;
        private const int  TOKEN_VIRTUALIZATION_ENABLED = 24;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public LUID_AND_ATTRIBUTES[] Privileges;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(
            IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool LookupPrivilegeValue(
            string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr TokenHandle, bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState, uint BufferLength,
            IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetTokenInformation(
            IntPtr TokenHandle, int TokenInformationClass,
            ref uint TokenInformation, uint TokenInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static void DisableRegistryVirtualization()
        {
            IntPtr token;
            if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle,
                    TOKEN_QUERY | TOKEN_ADJUST_DEFAULT, out token))
            {
                Logger.Write("DisableRegistryVirtualization: OpenProcessToken failed err={0}",
                    Marshal.GetLastWin32Error());
                return;
            }
            try
            {
                uint disabled = 0;
                if (!SetTokenInformation(token, TOKEN_VIRTUALIZATION_ENABLED, ref disabled, 4))
                    Logger.Write("DisableRegistryVirtualization: failed err={0}",
                        Marshal.GetLastWin32Error());
            }
            finally { CloseHandle(token); }
        }

        private static void EnablePrivileges()
        {
            IntPtr token;
            if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle,
                    TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES, out token))
                return;
            try
            {
                EnablePrivilege(token, "SeTakeOwnershipPrivilege");
                EnablePrivilege(token, "SeRestorePrivilege");
                EnablePrivilege(token, "SeBackupPrivilege");
                EnablePrivilege(token, "SeDebugPrivilege");
            }
            finally { CloseHandle(token); }
        }

        private static void EnablePrivilege(IntPtr token, string name)
        {
            LUID luid;
            if (!LookupPrivilegeValue(null, name, out luid)) return;
            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privileges     = new[] { new LUID_AND_ATTRIBUTES
                    { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED } }
            };
            AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
