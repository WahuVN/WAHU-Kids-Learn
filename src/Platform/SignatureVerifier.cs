using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace WAHU.Platform
{
    public sealed class SignatureCheckResult
    {
        public bool SignaturePresent { get; set; }
        public bool? SignatureDigestValid { get; set; }
        public bool? PublisherChainTrusted { get; set; }
        public long ElapsedMs { get; set; }
        public string WinVerifyTrustCode { get; set; }
        public string Note { get; set; }
    }

    public static class SignatureVerifier
    {
        private static readonly Guid Action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        private const uint WTD_UI_NONE = 2, WTD_REVOKE_NONE = 0, WTD_CHOICE_FILE = 1, WTD_STATEACTION_IGNORE = 0;
        private const uint WTD_REVOCATION_CHECK_NONE = 0x10, WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000;
        private const int TRUST_E_NOSIGNATURE = unchecked((int)0x800B0100);
        private const int TRUST_E_BAD_DIGEST = unchecked((int)0x80096010);
        private const int CERT_E_UNTRUSTEDROOT = unchecked((int)0x800B0109);
        private const int CERT_E_CHAINING = unchecked((int)0x800B010A);
        private const int TRUST_E_EXPLICIT_DISTRUST = unchecked((int)0x800B0111);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO { public uint cbStruct; [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public uint dwUIChoice;
            public uint fdwRevocationChecks; public uint dwUnionChoice; public IntPtr pFile; public uint dwStateAction;
            public IntPtr hWVTStateData; [MarshalAs(UnmanagedType.LPWStr)] public string pwszURLReference;
            public uint dwProvFlags; public uint dwUIContext; public IntPtr pSignatureSettings;
        }

        [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true, SetLastError = false, CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, IntPtr data);

        public static SignatureCheckResult CheckCacheOnly(string path)
        {
            var result = new SignatureCheckResult();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { result.Note = "signature_target_missing"; return result; }
            result.SignaturePresent = HasEmbeddedCertificate(path);
            if (!result.SignaturePresent)
            {
                result.WinVerifyTrustCode = Hex(TRUST_E_NOSIGNATURE); result.PublisherChainTrusted = false;
                result.Note = "no_embedded_authenticode_signature"; return result;
            }

            var fileInfo = new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)), pcwszFilePath = path };
            IntPtr filePtr = IntPtr.Zero, dataPtr = IntPtr.Zero;
            var sw = Stopwatch.StartNew();
            try
            {
                filePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)));
                Marshal.StructureToPtr(fileInfo, filePtr, false);
                var data = new WINTRUST_DATA
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)), dwUIChoice = WTD_UI_NONE,
                    fdwRevocationChecks = WTD_REVOKE_NONE, dwUnionChoice = WTD_CHOICE_FILE, pFile = filePtr,
                    dwStateAction = WTD_STATEACTION_IGNORE, dwProvFlags = WTD_REVOCATION_CHECK_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL
                };
                dataPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WINTRUST_DATA)));
                Marshal.StructureToPtr(data, dataPtr, false);
                int code = WinVerifyTrust(IntPtr.Zero, Action, dataPtr);
                result.WinVerifyTrustCode = Hex(code);
                result.PublisherChainTrusted = code == 0;
                result.SignatureDigestValid = code == 0 ? (bool?)true : code == TRUST_E_BAD_DIGEST ? (bool?)false : null;
                result.Note = Explain(code);
            }
            catch (Exception ex) { result.Note = "winverifytrust_exception:" + ex.GetType().Name; }
            finally
            {
                sw.Stop(); result.ElapsedMs = sw.ElapsedMilliseconds;
                if (dataPtr != IntPtr.Zero) Marshal.FreeHGlobal(dataPtr);
                if (filePtr != IntPtr.Zero) Marshal.FreeHGlobal(filePtr);
            }
            return result;
        }

        private static bool HasEmbeddedCertificate(string path)
        {
            try { using (var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path))) return cert.Handle != IntPtr.Zero; }
            catch { return false; }
        }
        private static string Explain(int code)
        {
            if (code == 0) return "signature_and_cached_chain_trusted";
            if (code == TRUST_E_BAD_DIGEST) return "bad_authenticode_digest";
            if (code == TRUST_E_NOSIGNATURE) return "no_signature";
            if (code == CERT_E_UNTRUSTEDROOT) return "untrusted_root_or_stale_offline_root_store";
            if (code == CERT_E_CHAINING) return "certificate_chain_could_not_be_built_from_cache";
            if (code == TRUST_E_EXPLICIT_DISTRUST) return "certificate_explicitly_distrusted";
            return "signature_or_trust_check_failed";
        }
        private static string Hex(int code) { return "0x" + unchecked((uint)code).ToString("X8"); }
    }
}
