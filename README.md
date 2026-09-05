# AVG Uninstaller Support Tool

A lightweight Windows Forms utility that fully removes **AVG Antivirus** from your PC — including stubborn registry leftovers — by running the official AVG removal tool (`avgclear.exe`) automatically in Safe Mode.

---

## Requirements

| | |
|---|---|
| OS | **Windows 7 SP1** or later (Win 7 RTM and below are not supported) |
| Runtime | .NET Framework 4.8 |
| Privileges | **Administrator** required (UAC prompt on launch) |

> .NET Framework 4.8 can be installed on Windows 7 SP1+ via [Microsoft's official download](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48).

---

## How It Works

```
Normal Mode (first run)
  ├─ OS version < Win 7 SP1  →  error and exit
  ├─ AVG not found           →  notify and exit
  └─ AVG detected
       ├─ Register RunOnce key (* prefix — fires in Safe Mode too)
       ├─ bcdedit safeboot minimal
       └─ Countdown form (10 s)  →  restart into Safe Mode

Safe Mode  (RunOnce fires automatically)
  ├─ Remove RunOnce key
  ├─ Extract embedded avgclear.exe → run → wait for user to finish
  ├─ bcdedit deletevalue safeboot
  ├─ Write completion flag (timestamp)
  └─ Restart back to Normal Mode

Normal Mode (second run — after cleanup)
  ├─ Detect completion flag
  ├─ Delete flag
  ├─ Silently delete all AVG registry remnants
  │    • HKLM\SOFTWARE\AVG
  │    • HKLM\SOFTWARE\WOW6432Node\AVG
  │    • HKLM\SOFTWARE\Classes\AvgPersistentStorage
  │    • HKCR\AvgPersistentStorage
  │    • HKLM\...\Schedule\TaskCache\Tree\AVG
  │    • HKLM\...\PolicyManager\...\Scan_LocalSettingOverrideAvgCPULoadFactor
  │    • HKLM\...\PolicyManager\...\Defender\AvgCPULoadFactor
  └─ Show "Done" screen  →  exit
```

Registry cleanup uses a 3-step fallback:
1. Direct `DeleteSubKeyTree`
2. Take ownership + grant full control → delete again
3. Shell fallback via `reg.exe delete /f`

---

## Usage

1. Double-click `Avg unin support tool.exe`
2. Accept the UAC prompt
3. If AVG is detected, the tool counts down 10 seconds then restarts into Safe Mode
4. The AVG removal tool (`avgclear.exe`) launches automatically — follow its on-screen steps
5. The computer restarts back to Normal Mode
6. Registry leftovers are cleaned silently in the background
7. A confirmation screen appears — click **Close** to finish

> **Cancel button** — available during the 10-second countdown. Clicking it aborts the process and reverts all boot settings safely.

---

## Build

```
Visual Studio 2022 / 2019  (.NET Framework 4.8, WinForms)
```

Open `frm_avg_unin_support_tool.slnx` and build in **Release** mode.  
All resources (`avgclear.exe`, icon) are embedded into the output `.exe` — no extra files needed alongside it.

---

## Donate

If this tool saved you time, a small tip is greatly appreciated ☕

**Techcombank (TCB)**

<img src="tcb.jpg" alt="Techcombank QR" width="260"/>

**MoMo**

<img src="momo.jpg" alt="MoMo QR" width="260"/>

---

## License

This project is licensed under the **MIT License** — see [LICENSE](LICENSE) for details.

> `avgclear.exe` is the official AVG removal tool provided by Gen Digital Inc. and is subject to AVG's own terms of use.
