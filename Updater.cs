using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace frm_avg_unin_support_tool
{
    // Checks GitHub Releases for a newer version of this tool.
    // Runs on a background thread to avoid blocking the UI.
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
    }
}
