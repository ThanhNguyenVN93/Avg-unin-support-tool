# Giải thích chi tiết cách hoạt động — Avast Uninstaller Support Tool
# Tài liệu tham khảo để áp dụng cho các tool Windows Forms tương lai

---

## 1. KIẾN TRÚC TỔNG QUAN

```
Program.cs       — Entry point, toàn bộ logic nghiệp vụ
Form1.cs         — UI Normal Mode (countdown + nút restart)
DonateForm.cs    — UI sau khi xong (banner hoàn tất + QR donate)
Strings.cs       — Tất cả chuỗi văn bản, tự chọn ngôn ngữ theo hệ thống
*.Designer.cs    — Khai báo form (size, title, icon) — KHÔNG dùng drag-drop Designer
```

Nguyên tắc: **không có file nào cần mang theo** — tất cả resource (exe, ảnh, icon)
đều được nhúng vào trong file `.exe` output khi build.

---

## 2. NHÚNG FILE VÀO EXE (EmbeddedResource)

### Khai báo trong .csproj
```xml
<!-- File thường — nhúng với tên tự đặt (LogicalName) -->
<EmbeddedResource Include="avast_av_clear.exe">
  <LogicalName>frm_avast_uninstaller.avastclear.exe</LogicalName>
</EmbeddedResource>

<!-- File ảnh — nhúng với tên mặc định: namespace.filename -->
<EmbeddedResource Include="tcb.jpg" />
<!-- Tên resource tự động = "frm_avast_uninstaller.tcb.jpg" -->
```

### Cách đọc resource trong code
```csharp
// Quy tắc đặt tên: RootNamespace + "." + tên file (đúng như trong csproj)
var stream = Assembly.GetExecutingAssembly()
    .GetManifestResourceStream("frm_avast_uninstaller.tcb.jpg");

// Đọc image — PHẢI copy sang MemoryStream riêng
// (Image.FromStream yêu cầu stream sống suốt vòng đời của Image)
var ms = new MemoryStream();
stream.CopyTo(ms);
stream.Dispose();
ms.Position = 0;
return Image.FromStream(ms);  // ms được Image giữ tham chiếu, không dispose
```

### Giải nén exe ra temp rồi chạy
```csharp
string tempDir = Path.Combine(Path.GetTempPath(), "my_tool_temp");
Directory.CreateDirectory(tempDir);
string dest = Path.Combine(tempDir, "tool.exe");

using (var s = Assembly.GetExecutingAssembly()
                       .GetManifestResourceStream("namespace.tool.exe"))
using (var fs = new FileStream(dest, FileMode.Create, FileAccess.Write))
    s.CopyTo(fs);

using (var proc = Process.Start(new ProcessStartInfo(dest)
{
    UseShellExecute  = true,   // cần thiết để tool có thể hiện UAC
    WorkingDirectory = tempDir,
}))
    proc?.WaitForExit();       // chờ tool chạy xong

// Dọn dẹp
try { Directory.Delete(tempDir, recursive: true); } catch { }
```

---

## 3. TỰ CHẠY SAU REBOOT — RUNONCE KEY

### Khái niệm
`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce`
- Key thường: chỉ chạy ở **Normal Mode**
- Key có prefix `*`: chạy cả ở **Safe Mode** (documented Windows behavior)
- RunOnce tự xóa key sau khi chạy — không cần dọn thủ công ở phía Normal Mode

### Code mẫu
```csharp
const string RunOncePath     = @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce";
const string RunOnceKeySafe  = "*MyToolSafeMode";   // chạy trong Safe Mode
const string RunOnceKeyNormal = "MyToolNormalMode";  // chỉ chạy ở Normal Mode

// Đặt key (ghi vào HKLM nên cần admin)
using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
using (var key  = hklm.OpenSubKey(RunOncePath, writable: true))
    key?.SetValue(RunOnceKeySafe, "\"" + Application.ExecutablePath + "\"");

// Xóa key (phòng trường hợp tool bị chạy thủ công thay vì qua RunOnce)
using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
using (var key  = hklm.OpenSubKey(RunOncePath, writable: true))
    key?.DeleteValue(RunOnceKeySafe, throwOnMissingValue: false);
```

### Lưu ý quan trọng
- Phải dùng `RegistryView.Registry64` kể cả khi build x86,
  để tránh WOW64 registry redirection ghi vào nhánh sai
- Bọc trong try/catch riêng để lỗi không làm crash flow chính

---

## 4. KHỞI ĐỘNG VÀO SAFE MODE

```csharp
// Bật Safe Mode (Minimal = không có network)
RunSysCmd("bcdedit.exe", "/set {current} safeboot minimal");

// Tắt Safe Mode (gọi ở cuối flow Safe Mode, trước khi restart về Normal)
RunSysCmd("bcdedit.exe", "/deletevalue {current} safeboot");

// Restart sau 5 giây
RunSysCmd("shutdown.exe", "/r /t 5");

// Phát hiện đang ở Safe Mode
if (SystemInformation.BootMode != BootMode.Normal)
    HandleSafeMode();
```

### Helper chạy lệnh hệ thống
```csharp
internal static void RunSysCmd(string exe, string args)
{
    string winDir = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
    // x86 process trên Windows 64-bit phải dùng Sysnative để gọi bcdedit 64-bit
    string sysDir = Environment.Is64BitProcess
        ? Path.Combine(winDir, "System32")
        : Path.Combine(winDir, "Sysnative");

    using (var p = Process.Start(new ProcessStartInfo(Path.Combine(sysDir, exe))
    {
        Arguments       = args,
        UseShellExecute = false,
        CreateNoWindow  = true,
    }))
        p?.WaitForExit();
}
```

### Tại sao dùng Sysnative thay vì System32 với process x86?
Trên Windows 64-bit, process 32-bit truy cập `System32` bị WOW64 redirect sang
`SysWOW32`. `bcdedit.exe` chỉ tồn tại bản 64-bit. `Sysnative` là virtual folder
cho phép process 32-bit gọi thẳng `System32` 64-bit, bypass redirect.

---

## 5. COMPLETION FLAG — TRUYỀN THÔNG TIN QUA REBOOT

Sau reboot, process mới khởi động từ đầu — không có memory, không có biến nào
còn tồn tại. Dùng registry để "nhớ" trạng thái qua reboot.

```csharp
const string FlagRegKey   = @"SOFTWARE\MyTool";
const string FlagRegValue = "PhaseTwoNeeded";

// Ghi flag (cuối Safe Mode, trước khi restart)
using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
using (var key  = hklm.CreateSubKey(FlagRegKey))
    key.SetValue(FlagRegValue, DateTime.UtcNow.Ticks, RegistryValueKind.QWord);
    //                         ^ Lưu timestamp thay vì số 1
    //                           để tránh false positive từ run cũ còn sót

// Kiểm tra flag (đầu Normal Mode)
private static bool FlagExists()
{
    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
    using (var key  = hklm.OpenSubKey(FlagRegKey))
    {
        if (key == null) return false;
        var raw = key.GetValue(FlagRegValue);
        if (raw == null) return false;
        try
        {
            var written = new DateTime(Convert.ToInt64(raw), DateTimeKind.Utc);
            return (DateTime.UtcNow - written).TotalHours < 2; // hết hạn sau 2 giờ
        }
        catch { return false; }
    }
}

// Xóa flag sau khi xử lý xong
using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
    hklm.DeleteSubKeyTree(FlagRegKey, throwOnMissingSubKey: false);
```

### Tại sao lưu timestamp thay vì giá trị tĩnh?
Nếu lưu `1`: flag tồn tại vô thời hạn → lần chạy tiếp theo báo "đã xong" sai.
Nếu lưu timestamp + check `< 2h`: flag cũ (từ lần test trước, debug, crash...) tự
vô hiệu hóa theo thời gian.

---

## 6. ĐA NGÔN NGỮ THEO HỆ THỐNG

```csharp
// Strings.cs — pattern chuẩn
internal static class Strings
{
    // Đọc một lần khi app khởi động
    private static readonly bool Vi =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi";

    // Mỗi string là một property
    public static string ButtonOk => Vi ? "Đồng ý" : "OK";

    // String có tham số dùng method
    public static string Countdown(int s) =>
        Vi ? $"Còn {s} giây" : $"{s} seconds remaining";
}
```

### Mở rộng thêm ngôn ngữ
```csharp
private static readonly string Lang =
    CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

public static string ButtonOk => Lang switch
{
    "vi" => "Đồng ý",
    "fr" => "D'accord",
    "ja" => "はい",
    _    => "OK",
};
```

---

## 7. ICON CHO PROJECT

### Tạo icon bằng PowerShell (không cần tool bên ngoài)
```powershell
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(32, 32)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# Vẽ nền tròn màu cam
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,105,0))
$g.FillEllipse($brush, 1, 1, 30, 30)

# Vẽ chữ trắng căn giữa
$fg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$font = New-Object System.Drawing.Font("Arial", 15, [System.Drawing.FontStyle]::Bold)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$g.DrawString("X", $font, $fg, [System.Drawing.RectangleF]::new(0,0,32,32), $sf)
$g.Dispose()

$handle = $bmp.GetHicon()
$icon   = [System.Drawing.Icon]::FromHandle($handle)
$fs     = [System.IO.File]::Create("app.ico")
$icon.Save($fs); $fs.Dispose()
$icon.Dispose(); $bmp.Dispose()
```

### Gắn icon vào project
```xml
<!-- Trong .csproj PropertyGroup -->
<ApplicationIcon>app.ico</ApplicationIcon>
```

```csharp
// Trong Form.Designer.cs — đọc từ exe đã được embed bởi compiler
try
{
    this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
        System.Windows.Forms.Application.ExecutablePath);
}
catch { }
```

---

## 8. FORM KHÔNG DÙNG DESIGNER — VIẾT CONTROL BẰNG CODE

Ưu điểm: không phụ thuộc Designer, dễ đọc, dễ version control (không có XML rác).

```csharp
// Trong Form.Designer.cs — chỉ khai báo form settings
private void InitializeComponent()
{
    this.ClientSize      = new System.Drawing.Size(500, 200);
    this.Text            = "My Tool";
    this.BackColor       = System.Drawing.Color.White;
    this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
    this.MaximizeBox     = false;
    this.StartPosition   = System.Windows.Forms.FormStartPosition.CenterScreen;
}

// Trong Form.cs — tạo control trong OnLoad
protected override void OnLoad(EventArgs e)
{
    base.OnLoad(e);

    var btn = new Button
    {
        Text      = "Click me",
        Location  = new Point(150, 80),
        Size      = new Size(200, 36),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(255, 105, 0),
        ForeColor = Color.White,
        Cursor    = Cursors.Hand,
    };
    btn.FlatAppearance.BorderSize = 0;
    btn.Click += (s, ev) => DoSomething();
    Controls.Add(btn);
}
```

---

## 9. CHECKLIST KHI TẠO TOOL MỚI

```
[ ] Tạo .csproj với ApplicationIcon + ApplicationManifest (requireAdministrator)
[ ] Tạo app.ico bằng PowerShell
[ ] Tạo Strings.cs với pattern Vi/En
[ ] Nhúng tất cả resource vào EmbeddedResource trong .csproj
[ ] Dùng RegistryView.Registry64 cho mọi thao tác registry
[ ] Dùng Sysnative thay System32 nếu build x86 cần chạy lệnh 64-bit
[ ] RunOnce key: * prefix nếu cần chạy trong Safe Mode
[ ] Completion flag: lưu timestamp (DateTime.UtcNow.Ticks) + check < N giờ
[ ] Dọn flag trong finally/catch để tránh vòng lặp vô tận
[ ] Process.Start với UseShellExecute=true cho app cần hiện UAC/GUI
[ ] proc?.WaitForExit() — null-safe
[ ] try/catch riêng cho từng bước độc lập để lỗi không dừng cả flow
[ ] .gitignore: loại bin/, obj/, .vs/, *.log
[ ] README.md song ngữ
[ ] LICENSE (MIT)
```

---

## 10. CẤU TRÚC FILE ĐỀ XUẤT CHO TOOL MỚI

```
MyTool/
├── MyTool.csproj          — ApplicationIcon, ApplicationManifest, EmbeddedResource
├── app.manifest           — requestedExecutionLevel = requireAdministrator
├── app.ico                — tạo bằng PowerShell
├── Program.cs             — Main(), logic nghiệp vụ, helpers
├── MainForm.cs            — UI chính
├── MainForm.Designer.cs   — chỉ form settings (size, title, icon)
├── Strings.cs             — tất cả text, đa ngôn ngữ
├── .gitignore
├── LICENSE
└── README.md
```
