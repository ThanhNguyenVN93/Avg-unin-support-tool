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

        // #4 — đã sửa: 30 giây, bấm OK để restart ngay
        public static string RestartingNow => Vi
            ? "Quá trình gỡ cài đặt hoàn tất.\n\nHệ thống sẽ tự động khởi động lại sau 30 giây.\nBấm OK để khởi động lại ngay."
            : "Cleanup complete.\n\nThe system will restart automatically in 30 seconds.\nClick OK to restart immediately.";

        public static string CancelledMessage => Vi
            ? "Đã hủy. Máy sẽ không khởi động lại."
            : "Cancelled. The computer will not restart.";

        public static string DoneTitle => Vi ? "Hoàn tất!" : "Done!";

        public static string DoneMessage => Vi
            ? "AVG Antivirus đã được gỡ cài đặt hoàn toàn.\nCảm ơn bạn đã sử dụng công cụ này!"
            : "AVG Antivirus has been completely removed.\nThank you for using this tool!";

        // 1 — BCD failure messages
        public static string BcdSetFailedMessage => Vi
            ? "Không thể cấu hình chế độ khởi động Safe Mode (bcdedit thất bại).\n\n" +
              "Có thể do Fast Startup hoặc BitLocker đang khóa phân vùng BCD.\n" +
              "Vui lòng tắt Fast Startup trong Control Panel → Power Options và thử lại."
            : "Could not configure Safe Mode boot (bcdedit failed).\n\n" +
              "This may be caused by Fast Startup or BitLocker locking the BCD store.\n" +
              "Please disable Fast Startup in Control Panel → Power Options and try again.";

        public static string BcdClearFailedMessage => Vi
            ? "CẢNH BÁO: Không thể hủy cấu hình Safe Mode sau khi gỡ cài đặt.\n\n" +
              "Máy tính sẽ KHÔNG được khởi động lại để tránh bị kẹt vĩnh viễn ở Safe Mode.\n" +
              "Vui lòng khởi động lại thủ công và chạy lại công cụ."
            : "WARNING: Could not revert Safe Mode boot configuration after cleanup.\n\n" +
              "The computer will NOT restart to prevent a permanent Safe Mode loop.\n" +
              "Please restart manually and run the tool again.";

        public static string AvgClearBlocked => Vi
            ? "Không thể chạy avgclear.exe. Có thể đã bị phần mềm bảo mật chặn.\n\nVui lòng tạm tắt Antivirus và thử lại."
            : "Cannot run avgclear.exe. It may have been blocked by security software.\n\nPlease disable your Antivirus temporarily and try again.";
    }
}
