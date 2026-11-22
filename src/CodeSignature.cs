using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BgInfoReloaded;

/// <summary>
/// Authenticode-Pruefung einer exe ueber WinVerifyTrust (dieselbe Pruefung wie der Reiter "Digitale
/// Signaturen" bzw. Get-AuthenticodeSignature). Gebraucht beim Update aus einem Ordner: ist die
/// installierte exe signiert, darf nur eine gueltig signierte Fassung desselben Herausgebers an ihre
/// Stelle - sonst bestimmte jeder, der in den Update-Ordner schreiben kann, was auf den Rechnern laeuft.
/// </summary>
[SupportedOSPlatform("windows")]
public static class CodeSignature
{
    /// <summary>
    /// Subject des Signaturzertifikats, wenn die Datei eine gueltige Authenticode-Signatur hat
    /// (Zertifikatskette bis zu einer vertrauenswuerdigen Stammzertifizierungsstelle, Datei unveraendert);
    /// sonst null. Ohne Sperrlistenabfrage - die waere ohne Netz langsam oder schluege fehl.
    /// </summary>
    public static string? ValidSigner(string file)
    {
        if (!IsTrusted(file))
        {
            return null;
        }
        try
        {
            using var certificate = SignerCertificate(file);
            return certificate.Subject;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Subject des Signaturzertifikats ohne Gueltigkeitspruefung (nur fuer Meldungen); null ohne Signatur.</summary>
    public static string? AnySigner(string file)
    {
        try
        {
            using var certificate = SignerCertificate(file);
            return certificate.Subject;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    // SYSLIB0057: der Ersatz X509CertificateLoader liest keine signierten PE-Dateien; fuer das Zertifikat aus
    // einer Authenticode-Signatur gibt es in .NET keinen anderen Weg als diesen.
#pragma warning disable SYSLIB0057
    private static X509Certificate SignerCertificate(string file) => X509Certificate.CreateFromSignedFile(file);
#pragma warning restore SYSLIB0057

    private static bool IsTrusted(string file)
    {
        var fileInfo = new WINTRUST_FILE_INFO
        {
            cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(),
            pcwszFilePath = file
        };
        var filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
        try
        {
            Marshal.StructureToPtr(fileInfo, filePointer, fDeleteOld: false);
            var data = new WINTRUST_DATA
            {
                cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                dwUIChoice = WTD_UI_NONE,
                fdwRevocationChecks = WTD_REVOKE_NONE,
                dwUnionChoice = WTD_CHOICE_FILE,
                pFile = filePointer,
                dwStateAction = WTD_STATEACTION_VERIFY,
                dwProvFlags = WTD_REVOCATION_CHECK_NONE | WTD_CACHE_ONLY_URL_RETRIEVAL
            };
            var result = WinVerifyTrust(InvalidHandle, GenericVerifyV2, ref data);

            // Zustand immer wieder freigeben, auch wenn die Pruefung fehlschlug.
            data.dwStateAction = WTD_STATEACTION_CLOSE;
            WinVerifyTrust(InvalidHandle, GenericVerifyV2, ref data);
            return result == 0;
        }
        finally
        {
            Marshal.DestroyStructure<WINTRUST_FILE_INFO>(filePointer);
            Marshal.FreeHGlobal(filePointer);
        }
    }

    private static readonly IntPtr InvalidHandle = new(-1);
    private static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
    private const uint WTD_UI_NONE = 2;
    private const uint WTD_REVOKE_NONE = 0;
    private const uint WTD_CHOICE_FILE = 1;
    private const uint WTD_STATEACTION_VERIFY = 1;
    private const uint WTD_STATEACTION_CLOSE = 2;
    private const uint WTD_REVOCATION_CHECK_NONE = 0x10;
    private const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x1000;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public IntPtr pSignatureSettings;
    }

    [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid pgActionID, ref WINTRUST_DATA pWVTData);
}
