// AdeeboMeter - Signed: is a program on this PC really from the publisher it claims? (WinVerifyTrust + the signer's name)
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace AdeeboMeter
{
    static class Signed
    {
        // AM-CG-222: a helper found in a folder anyone can write to (Local AppData) is only started when Windows says its
        // signature is valid AND the signer is who we expect - an exe dropped there by something else is refused
        public static bool By(string file, string publisher)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return false;
            try
            {
                if (!Valid(file)) return false;
                using (var c = new X509Certificate2(X509Certificate.CreateFromSignedFile(file)))
                {
                    string o = c.GetNameInfo(X509NameType.SimpleName, false) ?? "";
                    return o.Equals(publisher, StringComparison.OrdinalIgnoreCase) || c.Subject.IndexOf("O=" + publisher + ",", StringComparison.OrdinalIgnoreCase) >= 0 || c.Subject.EndsWith("O=" + publisher, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception e) { if (!(e is CryptographicException || e is IOException || e is UnauthorizedAccessException || e is DllNotFoundException || e is EntryPointNotFoundException)) throw; return false; }
        }
        static bool Valid(string file)
        {
            var fi = new FILE_INFO { cbStruct = (uint)Marshal.SizeOf(typeof(FILE_INFO)), pcwszFilePath = file };
            IntPtr pf = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FILE_INFO)));
            try
            {
                Marshal.StructureToPtr(fi, pf, false);
                var td = new TRUST_DATA { cbStruct = (uint)Marshal.SizeOf(typeof(TRUST_DATA)), dwUIChoice = 2 /* none */, fdwRevocationChecks = 0, dwUnionChoice = 1 /* file */, pFile = pf, dwStateAction = 0, dwProvFlags = 0x1000 /* cache only url retrieval */ };
                Guid act = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");   // WINTRUST_ACTION_GENERIC_VERIFY_V2
                return WinVerifyTrust(new IntPtr(-1), ref act, ref td) == 0;
            }
            finally { Marshal.FreeHGlobal(pf); }
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct FILE_INFO { public uint cbStruct; [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }
        [StructLayout(LayoutKind.Sequential)]
        struct TRUST_DATA { public uint cbStruct; public IntPtr pPolicyCallbackData, pSIPClientData; public uint dwUIChoice, fdwRevocationChecks, dwUnionChoice; public IntPtr pFile; public uint dwStateAction; public IntPtr hWVTStateData, pwszURLReference; public uint dwProvFlags, dwUIContext; public IntPtr pSignatureSettings; }
        [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] ref Guid action, ref TRUST_DATA data);
    }
}
