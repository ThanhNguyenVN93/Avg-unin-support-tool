using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace frm_avg_unin_support_tool
{
    // Authenticode signature verification via WinVerifyTrust + X509 subject check.
    // Returns true only if the file is signed by Gen Digital / Avast / AVG.
    internal static class Verify
    {
        internal static bool IsSignedByAvg(string filePath)
        {
            try
            {
                uint hr = WinVerifyTrust(filePath);
                if (hr != 0)
                {
                    Logger.Write("Verify: WinVerifyTrust 0x{0:X8} for {1}", hr, System.IO.Path.GetFileName(filePath));
                    return false;
                }

                var cert = new X509Certificate2(filePath);
                string subject = cert.Subject;
                Logger.Write("Verify: signer = {0}", subject);

                return subject.IndexOf("Gen Digital", StringComparison.OrdinalIgnoreCase) >= 0
                    || subject.IndexOf("Avast Software", StringComparison.OrdinalIgnoreCase) >= 0
                    || subject.IndexOf("AVG Technologies", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception ex)
            {
                Logger.WriteException("Verify.IsSignedByAvg", ex);
                return false;
            }
        }

        // ── P/Invoke ──────────────────────────────────────────────────────────

        private static readonly Guid ActionGenericVerifyV2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO
        {
            public uint   cbStruct;
            public string pcwszFilePath;
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public uint   cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint   dwUIChoice;           // 2 = WTD_UI_NONE
            public uint   fdwRevocationChecks;  // 0 = WTD_REVOKE_NONE
            public uint   dwUnionChoice;        // 1 = WTD_CHOICE_FILE
            public IntPtr pFile;
            public uint   dwStateAction;        // 0 = WTD_STATEACTION_IGNORE
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint   dwProvFlags;          // 0x1000 = offline (no revocation network)
            public uint   dwUIContext;
        }

        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        private static extern uint WinVerifyTrust(
            IntPtr hwnd,
            [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID,
            ref WINTRUST_DATA pWVTData);

        private static uint WinVerifyTrust(string filePath)
        {
            var fileInfo = new WINTRUST_FILE_INFO
            {
                cbStruct       = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)),
                pcwszFilePath  = filePath,
                hFile          = IntPtr.Zero,
                pgKnownSubject = IntPtr.Zero,
            };

            IntPtr pFile = Marshal.AllocHGlobal(Marshal.SizeOf(fileInfo));
            try
            {
                Marshal.StructureToPtr(fileInfo, pFile, false);
                var data = new WINTRUST_DATA
                {
                    cbStruct            = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)),
                    dwUIChoice          = 2,
                    fdwRevocationChecks = 0,
                    dwUnionChoice       = 1,
                    pFile               = pFile,
                    dwStateAction       = 0,
                    dwProvFlags         = 0x1000,
                };
                var guid = ActionGenericVerifyV2;
                return WinVerifyTrust(IntPtr.Zero, guid, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(pFile);
            }
        }
    }
}
