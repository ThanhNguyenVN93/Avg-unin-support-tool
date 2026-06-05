using System.Globalization;

namespace frm_avg_unin_support_tool
{
    internal static class Strings
    {
        private static readonly bool Vi =
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

        public static string AppTitle => "AVG Uninstaller Support Tool";

        public static string AvgNotFound => Vi
            ? "Không tìm thấy AVG Antivirus trên máy này.\nCông cụ sẽ thoát."
            : "AVG Antivirus was not found on this computer.\nThe tool will exit.";

        public static string SafeModeNotice => Vi
            ? "Đã phát hiện AVG Antivirus.\n\nCông cụ sẽ khởi động lại vào Safe Mode\nđể gỡ cài đặt AVG hoàn toàn."
            : "AVG Antivirus detected.\n\nThe tool will restart into Safe Mode\nto completely remove AVG.";

        public static string Countdown(int s) => Vi
            ? $"Khởi động lại sau {s} giây..."
            : $"Restarting in {s} seconds...";

        public static string BtnRestartNow => Vi ? "Khởi động lại ngay" : "Restart Now";
        public static string BtnCancel     => Vi ? "Hủy"                : "Cancel";
        public static string BtnClose      => Vi ? "Đóng"               : "Close";

        public static string RunToolError => Vi
            ? "Lỗi khi chạy avgclear.exe:"
            : "Error running avgclear.exe:";

        public static string RestartingNow => Vi
            ? "Quá trình gỡ cài đặt hoàn tất.\nMáy sẽ khởi động lại sau 5 giây."
            : "Cleanup complete.\nThe computer will restart in 5 seconds.";

        public static string CancelledMessage => Vi
            ? "Đã hủy. Máy sẽ không khởi động lại."
            : "Cancelled. The computer will not restart.";

        public static string DoneTitle => Vi ? "Hoàn tất!" : "Done!";

        public static string DoneMessage => Vi
            ? "AVG Antivirus đã được gỡ cài đặt hoàn toàn.\nCảm ơn bạn đã sử dụng công cụ này!"
            : "AVG Antivirus has been completely removed.\nThank you for using this tool!";
    }
}
