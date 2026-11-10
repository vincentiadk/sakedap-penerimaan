using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Penerimaan.App.Config;

/// <summary>
/// Enkripsi rahasia per mesin memakai DPAPI Windows (CryptProtectData).
///
/// Dipanggil langsung lewat P/Invoke ke crypt32.dll, bukan lewat
/// System.Security.Cryptography.ProtectedData, karena paket itu NuGet dan
/// project ini sengaja tanpa NuGet.
///
/// Scope LocalMachine: semua akun Windows di PC ini bisa membuka rahasianya
/// (PC pos satpam dipakai bergantian), tetapi berkas yang disalin ke PC lain
/// tidak bisa dibuka. Artinya ini melindungi dari bocor lewat repositori,
/// installer, atau salinan berkas - BUKAN dari orang yang sudah bisa
/// menjalankan program di PC ini.
/// </summary>
internal static class SecretStore
{
    // Entropy tambahan: aplikasi lain di PC yang sama yang memanggil DPAPI
    // tanpa nilai ini tidak bisa membuka blob-nya begitu saja.
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("SAKEDAP.Penerimaan/Inlis.Token/v1");

    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;
    private const int CRYPTPROTECT_LOCAL_MACHINE = 0x4;

    public static string Protect(string plaintext)
    {
        var data = Encoding.UTF8.GetBytes(plaintext);
        return Convert.ToBase64String(
            Call(data, protect: true));
    }

    /// <summary>Null bila blob rusak atau dibuat di PC lain.</summary>
    public static string? TryUnprotect(string base64)
    {
        try
        {
            return Encoding.UTF8.GetString(
                Call(Convert.FromBase64String(base64), protect: false));
        }
        catch (Exception ex) when (ex is FormatException or Win32Exception)
        {
            return null;
        }
    }

    private static byte[] Call(byte[] input, bool protect)
    {
        var inBlob = new DataBlob();
        var entropyBlob = new DataBlob();
        var outBlob = new DataBlob();

        try
        {
            inBlob = DataBlob.From(input);
            entropyBlob = DataBlob.From(Entropy);

            var ok = protect
                ? CryptProtectData(ref inBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN | CRYPTPROTECT_LOCAL_MACHINE, ref outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero,
                    CRYPTPROTECT_UI_FORBIDDEN, ref outBlob);

            if (!ok)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var hasil = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, hasil, 0, outBlob.cbData);
            return hasil;
        }
        finally
        {
            inBlob.Free();
            entropyBlob.Free();
            if (outBlob.pbData != IntPtr.Zero)
            {
                // Hasil dekripsi berisi token polos: timpa sebelum dilepas.
                for (var i = 0; i < outBlob.cbData; i++)
                    Marshal.WriteByte(outBlob.pbData, i, 0);
                LocalFree(outBlob.pbData);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;

        public static DataBlob From(byte[] data)
        {
            var blob = new DataBlob { cbData = data.Length, pbData = Marshal.AllocHGlobal(Math.Max(data.Length, 1)) };
            Marshal.Copy(data, 0, blob.pbData, data.Length);
            return blob;
        }

        public void Free()
        {
            if (pbData == IntPtr.Zero) return;
            Marshal.FreeHGlobal(pbData);
            pbData = IntPtr.Zero;
        }
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn, string? szDataDescr, ref DataBlob pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn, IntPtr ppszDataDescr, ref DataBlob pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
