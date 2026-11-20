using System.Globalization;

namespace frm_avg_unin_support_tool
{
    internal static class Strings
    {
        // Runtime-toggleable language (starts from system locale, flips on user request)
        private static bool Vi =
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

        internal static void ToggleLanguage() => Vi = !Vi;
        internal static bool IsVietnamese    => Vi;
        internal static string LangButtonText => Vi ? "EN" : "VI";

        // ── App identity ──────────────────────────────────────────────────────
        public static string AppTitle => "AVG Uninstaller Support Tool";

        // ── Detection ─────────────────────────────────────────────────────────
        public static string AvgNotFound => Vi
            ? "Không tìm thấy AVG Antivirus trên máy này.\nCông cụ sẽ thoát."
            : "AVG Antivirus was not found on this computer.\nThe tool will exit.";

        // ── Countdown form ────────────────────────────────────────────────────
        public static string SafeModeNotice => Vi
            ? "Đã phát hiện AVG Antivirus.\n\nCông cụ sẽ khởi động lại vào Safe Mode\nđể gỡ cài đặt AVG hoàn toàn."
            : "AVG Antivirus detected.\n\nThe tool will restart into Safe Mode\nto completely remove AVG.";

        public static string Countdown(int s) => Vi
            ? $"Khởi động lại sau {s} giây..."
            : $"Restarting in {s} seconds...";

        public static string BtnRestartNow => Vi ? "Khởi động lại ngay" : "Restart Now";
        public static string BtnCancel     => Vi ? "Hủy"                : "Cancel";
        public static string BtnClose      => Vi ? "Đóng"               : "Close";

        // ── AutoLogon section ─────────────────────────────────────────────────
        public static string ChkAutoLogon => Vi
            ? "Tự động đăng nhập trong Safe Mode (cần mật khẩu)"
            : "Auto-login in Safe Mode (requires password)";

        public static string LblPassword => Vi ? "Mật khẩu Windows:" : "Windows password:";

        public static string AutoLogonNote => Vi
            ? "Mật khẩu chỉ dùng một lần và bị xóa ngay khi khởi động lại xong."
            : "Password is used once and deleted immediately after restart.";

        // ── Downloading avgclear ──────────────────────────────────────────────
        public static string DownloadingAvgClear => Vi
            ? "Đang tải công cụ gỡ cài đặt AVG mới nhất..."
            : "Downloading the latest AVG removal tool...";

        public static string DownloadFailed => Vi
            ? "Không tải được phiên bản mới — dùng bản tích hợp sẵn."
            : "Could not download latest version — using built-in copy.";

        // ── Restart / done ────────────────────────────────────────────────────
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

        public static string DonatePrompt => Vi
            ? "Nếu công cụ này hữu ích, hãy ủng hộ tác giả ☕"
            : "If this tool saved you time, consider a small donation ☕";

        public static string BtnDonate => Vi ? "Ủng hộ ☕" : "Donate ☕";

        // ── BCD / boot errors ─────────────────────────────────────────────────
        public static string BcdSetFailedMessage => Vi
            ? "Không thể cấu hình Safe Mode (bcdedit thất bại).\n\n" +
              "Có thể do Fast Startup hoặc BitLocker đang khóa phân vùng BCD.\n" +
              "Vui lòng tắt Fast Startup trong Control Panel → Power Options và thử lại."
            : "Could not configure Safe Mode boot (bcdedit failed).\n\n" +
              "This may be caused by Fast Startup or BitLocker locking the BCD store.\n" +
              "Please disable Fast Startup in Control Panel → Power Options and try again.";

        public static string BcdClearFailedMessage => Vi
            ? "CẢNH BÁO: Không thể hủy cấu hình Safe Mode sau khi gỡ cài đặt.\n\n" +
              "Máy sẽ KHÔNG khởi động lại để tránh bị kẹt vĩnh viễn ở Safe Mode.\n" +
              "Vui lòng khởi động lại thủ công và chạy lại công cụ."
            : "WARNING: Could not revert Safe Mode boot configuration after cleanup.\n\n" +
              "The computer will NOT restart to prevent a permanent Safe Mode loop.\n" +
              "Please restart manually and run the tool again.";

        // ── Run / process errors ──────────────────────────────────────────────
        public static string RunToolError => Vi
            ? "Lỗi khi chạy avgclear.exe:"
            : "Error running avgclear.exe:";

        public static string AvgClearBlocked => Vi
            ? "Không thể chạy avgclear.exe. Có thể đã bị phần mềm bảo mật chặn.\n\nVui lòng tạm tắt Antivirus và thử lại."
            : "Cannot run avgclear.exe. It may have been blocked by security software.\n\nPlease disable your Antivirus temporarily and try again.";

        // ── Attempt counter ───────────────────────────────────────────────────
        public static string TooManyAttempts => Vi
            ? "Đã thử gỡ cài đặt qua Safe Mode quá 3 lần mà không thành công.\n\n" +
              "Vui lòng thử dùng tùy chọn --nosafemode hoặc gỡ thủ công."
            : "Safe Mode removal has been attempted 3 times without success.\n\n" +
              "Please try the --nosafemode option or remove AVG manually.";

        // ── No-safe-mode path ─────────────────────────────────────────────────
        public static string NoSafeModeRunning => Vi
            ? "Đang chạy avgclear.exe trực tiếp (không qua Safe Mode)..."
            : "Running avgclear.exe directly (bypassing Safe Mode)...";

        // ── Restore point ─────────────────────────────────────────────────────
        public static string RestorePointCreated => Vi
            ? "Đã tạo điểm khôi phục hệ thống."
            : "System restore point created.";

        public static string RestorePointFailed => Vi
            ? "Không tạo được điểm khôi phục (System Restore có thể đang tắt)."
            : "Could not create restore point (System Restore may be disabled).";

        // ── Update footer ─────────────────────────────────────────────────────
        public static string UpdateAvailable(string ver) => Vi
            ? $"Có phiên bản mới: v{ver} — Nhấn để cập nhật"
            : $"Update available: v{ver} — Click to update";

        public static string UpdateDownloading => Vi
            ? "Đang tải bản cập nhật..."
            : "Downloading update...";

        public static string UpdateFailed(string reason) => Vi
            ? $"Không thể cập nhật tự động: {reason}"
            : $"Automatic update failed: {reason}";

        // ── Help ──────────────────────────────────────────────────────────────
        public static string HelpText =>
            "AVG Uninstaller Support Tool v" + Program.AppVersion + "\n\n" +
            "Usage: \"Avg unin support tool.exe\" [options]\n\n" +
            "Options:\n" +
            "  --auto           Skip countdown, restart immediately\n" +
            "  --nosafemode     Run AVG Clear directly without Safe Mode\n" +
            "  --cleanleftovers Clean AVG registry remnants only (no Safe Mode)\n" +
            "  --help           Show this help\n\n" +
            "Exit codes: 0=success  1=cancelled  2=error  3=AVG not found\n\n" +
            "Log: " + Logger.GetLogPath();

        // ── Log path notice ───────────────────────────────────────────────────
        public static string LogLocation => Vi
            ? "Log chẩn đoán: " + Logger.GetLogPath()
            : "Diagnostic log: " + Logger.GetLogPath();
    }
}
