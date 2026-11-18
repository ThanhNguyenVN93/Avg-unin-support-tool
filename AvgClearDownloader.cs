using System;
using System.IO;
using System.Net;
using System.Reflection;

namespace frm_avg_unin_support_tool
{
    // Downloads the latest avgclear.exe from AVG's CDN and verifies its Authenticode
    // signature. Falls back to the embedded version if download or verification fails.
    internal static class AvgClearDownloader
    {
        private const string CdnUrl      = "https://www.avg.com/filedir/util/avgclear.exe";
        private const long   MinFileSize = 100_000; // sanity check

        // Returns the full path to a ready-to-run avgclear.exe in destDir.
        // Always succeeds: on any failure it falls back to the embedded copy.
        internal static string GetAvgClear(string destDir)
        {
            Directory.CreateDirectory(destDir);
            string dest = Path.Combine(destDir, "avgclear.exe");

            if (TryDownload(dest))
            {
                Logger.Write("AvgClearDownloader: using downloaded avgclear.exe");
                return dest;
            }

            Logger.Write("AvgClearDownloader: falling back to embedded avgclear.exe");
            ExtractEmbedded(dest);
            return dest;
        }

        // ── Download ──────────────────────────────────────────────────────────

        private static bool TryDownload(string dest)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers["User-Agent"] = "AVGClearTool/" + Program.AppVersion;
                    Logger.Write("AvgClearDownloader: GET " + CdnUrl);
                    wc.DownloadFile(CdnUrl, dest);
                }

                if (!File.Exists(dest) || new FileInfo(dest).Length < MinFileSize)
                {
                    Logger.Write("AvgClearDownloader: download too small or missing — rejected");
                    TryDelete(dest);
                    return false;
                }

                if (!Verify.IsSignedByAvg(dest))
                {
                    Logger.Write("AvgClearDownloader: signature FAILED — rejecting download");
                    TryDelete(dest);
                    return false;
                }

                Logger.Write("AvgClearDownloader: download OK ({0:N0} bytes)", new FileInfo(dest).Length);
                return true;
            }
            catch (Exception ex)
            {
                Logger.WriteException("AvgClearDownloader.TryDownload", ex);
                TryDelete(dest);
                return false;
            }
        }

        // ── Embedded fallback ─────────────────────────────────────────────────

        internal static void ExtractEmbedded(string dest)
        {
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
            Logger.Write("AvgClearDownloader: embedded extracted to {0}", dest);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
