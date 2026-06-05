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
        // Tất cả key AVG còn sót cần xóa sau khi avgclear chạy xong
        private static readonly (RegistryHive Hive, string Path, RegistryView View)[] AvgKeys =
        {
            // Core AVG keys
            (RegistryHive.LocalMachine, @"SOFTWARE\AVG",                           RegistryView.Registry64),
            (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\AVG",               RegistryView.Registry64),

            // PersistentStorage (cả HKCR và HKLM\Classes)
            (RegistryHive.LocalMachine, @"SOFTWARE\Classes\AvgPersistentStorage",  RegistryView.Registry64),
            (RegistryHive.ClassesRoot,  @"AvgPersistentStorage",                   RegistryView.Default),

            // Scheduled Task cache
            (RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Schedule\TaskCache\Tree\AVG",
                RegistryView.Registry64),

            // PolicyManager — có thể bị lock bởi Windows
            (RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\PolicyManager\default\ADMX_MicrosoftDefenderAntivirus\Scan_LocalSettingOverrideAvgCPULoadFactor",
                RegistryView.Registry64),
            (RegistryHive.LocalMachine,
                @"SOFTWARE\Microsoft\PolicyManager\default\Defender\AvgCPULoadFactor",
                RegistryView.Registry64),
        };

        // ── Entry point ───────────────────────────────────────────────────────

        internal static void CleanAvgRegistry()
        {
            EnablePrivileges();   // Bật SeTakeOwnership + SeRestore + SeBackup
            foreach (var entry in AvgKeys)
                TryDeleteKey(entry.Hive, entry.Path, entry.View);
        }

        // ── Delete logic (3 bước) ─────────────────────────────────────────────

        private static void TryDeleteKey(RegistryHive hive, string path, RegistryView view)
        {
            // Bước 1: Xóa trực tiếp
            if (DirectDelete(hive, path, view)) return;

            // Bước 2: Takeownership → grant full control → xóa lại
            try { GrantFullControl(hive, path, view); } catch { }
            if (DirectDelete(hive, path, view)) return;

            // Bước 3: Fallback — reg.exe delete /f
            try
            {
                string hiveStr = hive == RegistryHive.ClassesRoot  ? "HKCR" :
                                 hive == RegistryHive.LocalMachine ? "HKLM" : "HKCU";
                Program.RunSysCmd("reg.exe", "delete \"" + hiveStr + "\\" + path + "\" /f");
            }
            catch { }
        }

        private static bool DirectDelete(RegistryHive hive, string path, RegistryView view)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(hive, view))
                    root.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
                return true;
            }
            catch { return false; }
        }

        private static void GrantFullControl(RegistryHive hive, string path, RegistryView view)
        {
            var me = WindowsIdentity.GetCurrent().User;

            // Lấy ownership
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

            // Cấp FullControl
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

        // ── P/Invoke — bật privilege trước khi thao tác registry ─────────────

        private const uint TOKEN_QUERY             = 0x0008;
        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint SE_PRIVILEGE_ENABLED    = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID  Luid;
            public uint  Attributes;
        }

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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static void EnablePrivileges()
        {
            IntPtr token;
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle,
                    TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES, out token))
                return;
            try
            {
                EnablePrivilege(token, "SeTakeOwnershipPrivilege");
                EnablePrivilege(token, "SeRestorePrivilege");
                EnablePrivilege(token, "SeBackupPrivilege");
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
