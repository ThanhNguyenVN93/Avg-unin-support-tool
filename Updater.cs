using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace frm_avg_unin_support_tool
{
    // Checks GitHub Releases for a newer version of this tool, and can
    // download + apply that update in place (download new exe, swap it in
    // via a tiny helper script once this process exits, relaunch).
    internal static class Updater
    {
        private const string ApiUrl =
            "https://api.github.com/repos/ThanhNguyenVN93/Avg-unin-support-tool/releases/latest";

        internal static string CurrentVersion => Program.AppVersion;

        // Calls onResult(latestTag, downloadUrl) on the calling thread context.
        // Both args are null if the check fails or no release is found.
        internal static void CheckAsync(Action<string, string> onResult)
        {
            Task.Run(() =>
            {
                string tag = null, url = null;
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (var wc = new WebClient())
                    {
                        wc.Headers["User-Agent"] = "AVGClearTool/" + CurrentVersion;
                        string json = wc.DownloadString(ApiUrl);

                        var tagMatch = Regex.Match(json,
                            "\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"");
                        var urlMatch = Regex.Match(json,
                            "\"browser_download_url\"\\s*:\\s*\"([^\"]+\\.exe)\"");

                        if (tagMatch.Success)
                        {
                            tag = tagMatch.Groups[1].Value;
                            url = urlMatch.Success ? urlMatch.Groups[1].Value : null;
                            Logger.Write("Updater: latest={0} current={1}", tag, CurrentVersion);
                        }
                    }
                }
                catch (Exception ex) { Logger.WriteException("Updater.CheckAsync", ex); }

                onResult(tag, url);
            });
        }

        // Returns true if latestTag represents a version strictly newer than the current build.
        internal static bool IsNewer(string latestTag)
        {
            if (string.IsNullOrEmpty(latestTag)) return false;
            try { return new Version(latestTag) > new Version(CurrentVersion); }
            catch { return false; }
        }

        private const long MinDownloadBytes = 50_000; // sanity check — reject tiny/broken downloads

        // Downloads the new exe, stages a swap-and-relaunch script, launches it,
        // then exits this process so the script can replace the running exe.
        // Calls onError(message) on the calling thread if anything fails before exit.
        internal static void DownloadAndApply(string downloadUrl, Action<string> onError)
        {
            Task.Run(() =>
            {
                try
                {
                    string currentExe = Application.ExecutablePath;
                    string exeDir     = Path.GetDirectoryName(currentExe) ?? Path.GetTempPath();
                    string newExePath = Path.Combine(exeDir, "update_download.tmp.exe");
                    string scriptPath = Path.Combine(Path.GetTempPath(), "AvgClearTool_update.bat");

                    Logger.Write("Updater.DownloadAndApply: GET {0}", downloadUrl);
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    using (var wc = new WebClient())
                    {
                        wc.Headers["User-Agent"] = "AVGClearTool/" + CurrentVersion;
                        wc.DownloadFile(downloadUrl, newExePath);
                    }

                    var info = new FileInfo(newExePath);
                    if (!info.Exists || info.Length < MinDownloadBytes)
                    {
                        TryDelete(newExePath);
                        throw new InvalidOperationException(
                            "Downloaded update file is missing or too small (" +
                            (info.Exists ? info.Length.ToString() : "0") + " bytes).");
                    }

                    // The current exe can't overwrite/delete itself while running, so a
                    // small helper script does the swap after this process exits: wait
                    // for the file lock to clear, replace the exe, relaunch it, clean up.
                    string script =
                        "@echo off\r\n" +
                        ":wait\r\n" +
                        "ping -n 2 127.0.0.1 >nul\r\n" +
                        "copy /y \"" + newExePath + "\" \"" + currentExe + "\" >nul 2>&1\r\n" +
                        "if errorlevel 1 goto wait\r\n" +
                        "del \"" + newExePath + "\" >nul 2>&1\r\n" +
                        "start \"\" \"" + currentExe + "\"\r\n" +
                        "del \"%~f0\"\r\n";
                    File.WriteAllText(scriptPath, script);

                    Logger.Write("Updater.DownloadAndApply: staged update, launching swap script and exiting");
                    Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + scriptPath + "\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow  = true,
                        WindowStyle     = ProcessWindowStyle.Hidden,
                    });

                    Application.Exit();
                }
                catch (Exception ex)
                {
                    Logger.WriteException("Updater.DownloadAndApply", ex);
                    onError?.Invoke(ex.Message);
                }
            });
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best effort */ }
        }
    }
}
