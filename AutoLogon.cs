using System;
using Microsoft.Win32;

namespace frm_avg_unin_support_tool
{
    // Sets Windows autologon so the tool can run automatically after a Safe Mode reboot
    // without requiring the user to type their password.
    //
    // SECURITY: DefaultPassword is stored in plaintext in the Winlogon registry key —
    // the standard mechanism Windows itself uses for autologon. Remove() MUST be called
    // as the first action in Safe Mode to clear the password before any user code runs.
    internal static class AutoLogon
    {
        private const string WinlogonPath =
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";

        // Returns the current Windows username (domain\user or just user).
        internal static string CurrentUser =>
            System.Security.Principal.WindowsIdentity.GetCurrent().Name;

        // Arms autologon for the given user/password. Backs up pre-existing values
        // so they can be restored on cancel.
        internal static void Setup(string username, string password)
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(WinlogonPath, writable: true))
                {
                    if (key == null)
                    {
                        Logger.Write("AutoLogon.Setup: Winlogon key not found — skipped");
                        return;
                    }

                    // Back up originals (prefix _bak_) so Remove() can restore them
                    Backup(key, "AutoAdminLogon");
                    Backup(key, "DefaultUserName");
                    Backup(key, "DefaultDomainName");

                    // Strip domain prefix to get the bare username Windows expects
                    string bare = username.Contains("\\")
                        ? username.Substring(username.IndexOf('\\') + 1)
                        : username;

                    key.SetValue("AutoAdminLogon",   "1");
                    key.SetValue("DefaultUserName",  bare);
                    key.SetValue("DefaultPassword",  password ?? "");
                    // AutoLogonCount = 1 makes it a one-time logon (auto-cleared by Windows)
                    key.SetValue("AutoLogonCount",   1, RegistryValueKind.DWord);
                }
                Logger.Write("AutoLogon.Setup: armed for user '{0}'", username);
            }
            catch (Exception ex) { Logger.WriteException("AutoLogon.Setup", ex); }
        }

        // Removes autologon credentials and restores previous values.
        // Call this as the VERY FIRST action in Safe Mode.
        internal static void Remove()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(WinlogonPath, writable: true))
                {
                    if (key == null) return;

                    key.DeleteValue("DefaultPassword",  throwOnMissingValue: false);
                    key.DeleteValue("AutoLogonCount",   throwOnMissingValue: false);
                    Restore(key, "AutoAdminLogon");
                    Restore(key, "DefaultUserName");
                    Restore(key, "DefaultDomainName");
                }
                Logger.Write("AutoLogon.Remove: credentials cleared");
            }
            catch (Exception ex) { Logger.WriteException("AutoLogon.Remove", ex); }
        }

        // Removes autologon without restoring backups (used on cancel path).
        internal static void Cancel()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(WinlogonPath, writable: true))
                {
                    if (key == null) return;
                    key.DeleteValue("DefaultPassword",    throwOnMissingValue: false);
                    key.DeleteValue("AutoLogonCount",     throwOnMissingValue: false);
                    Restore(key, "AutoAdminLogon");
                    Restore(key, "DefaultUserName");
                    Restore(key, "DefaultDomainName");
                }
                Logger.Write("AutoLogon.Cancel: credentials cleared");
            }
            catch (Exception ex) { Logger.WriteException("AutoLogon.Cancel", ex); }
        }

        // Returns true if autologon was armed by this tool (backup key present).
        internal static bool IsArmed()
        {
            try
            {
                using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var key  = hklm.OpenSubKey(WinlogonPath))
                    return key?.GetValue("_bak_AutoAdminLogon") != null;
            }
            catch { return false; }
        }

        private static void Backup(RegistryKey key, string name)
        {
            object v = key.GetValue(name);
            if (v != null)
                key.SetValue("_bak_" + name, v.ToString());
        }

        private static void Restore(RegistryKey key, string name)
        {
            object backup = key.GetValue("_bak_" + name);
            if (backup != null)
            {
                key.SetValue(name, backup.ToString());
                key.DeleteValue("_bak_" + name, throwOnMissingValue: false);
            }
            else
            {
                key.DeleteValue(name, throwOnMissingValue: false);
                key.DeleteValue("_bak_" + name, throwOnMissingValue: false);
            }
        }
    }
}
