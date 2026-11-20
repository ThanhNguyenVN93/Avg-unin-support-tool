using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace frm_avg_unin_support_tool
{
    internal static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "AvgClearTool", "diagnostic.log");

        private static readonly object _lock = new object();

        internal static string GetLogPath() => LogPath;

        internal static void Init()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 1 * 1024 * 1024)
                    File.Delete(LogPath);

                Write("=== Session start {0} ===", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Write("Version : {0}", Program.AppVersion);
                Write("OS      : {0}", Environment.OSVersion);
                Write("BootMode: {0}", SystemInformation.BootMode);
                Write("CLR     : {0}", Environment.Version);
                Write("Exe     : {0}", System.Reflection.Assembly.GetExecutingAssembly().Location);
                Write("Args    : {0}", Environment.CommandLine);
            }
            catch (Exception ex) { Debug.WriteLine("Logger.Init: " + ex.Message); }
        }

        internal static void Write(string message)
        {
            try
            {
                string line = DateTime.Now.ToString("HH:mm:ss.fff") + " | " + message;
                Debug.WriteLine(line);
                lock (_lock)
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        internal static void Write(string fmt, params object[] args)
            => Write(string.Format(fmt, args));

        internal static void WriteException(string context, Exception ex)
            => Write("{0}: [{1}] {2}", context, ex.GetType().Name, ex.Message);
    }
}
