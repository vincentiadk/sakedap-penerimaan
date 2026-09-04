using System.Runtime.InteropServices;
using System.Text;

namespace Penerimaan.App.Services;

/// <summary>
/// Mengirim data mentah (ZPL/TSPL/EPL) langsung ke spooler Windows.
///
/// PENTING: jangan pakai PrintDocument/GDI untuk printer barcode. GDI akan
/// merender label sebagai bitmap sehingga barcode jadi buram dan cetak lambat.
/// Data mentah dikirim dengan datatype "RAW" supaya diteruskan apa adanya
/// ke firmware printer.
/// </summary>
public static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DOCINFOW
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, EntryPoint = "OpenPrinterW", SetLastError = true)]
    private static extern bool OpenPrinter(string printerName, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, EntryPoint = "StartDocPrinterW", SetLastError = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFOW di);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    /// <summary>Kirim perintah printer dalam bentuk teks. ZPL/TSPL memakai ASCII.</summary>
    public static void SendString(string printerName, string command, string docName = "Label Penerimaan")
        => SendBytes(printerName, Encoding.ASCII.GetBytes(command), docName);

    public static void SendBytes(string printerName, byte[] data, string docName = "Label Penerimaan")
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Nama printer belum diisi.", nameof(printerName));

        if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            throw Win32("OpenPrinter gagal. Pastikan nama printer benar dan printer tersedia.");

        var unmanaged = IntPtr.Zero;
        var docStarted = false;
        var pageStarted = false;
        try
        {
            var di = new DOCINFOW
            {
                pDocName = docName,
                pOutputFile = null,
                pDataType = "RAW"
            };

            if (!StartDocPrinter(hPrinter, 1, ref di))
                throw Win32("StartDocPrinter gagal.");
            docStarted = true;

            if (!StartPagePrinter(hPrinter))
                throw Win32("StartPagePrinter gagal.");
            pageStarted = true;

            unmanaged = Marshal.AllocCoTaskMem(data.Length);
            Marshal.Copy(data, 0, unmanaged, data.Length);

            if (!WritePrinter(hPrinter, unmanaged, data.Length, out var written) || written != data.Length)
                throw Win32("WritePrinter gagal atau data tidak terkirim penuh.");
        }
        finally
        {
            if (unmanaged != IntPtr.Zero) Marshal.FreeCoTaskMem(unmanaged);
            if (pageStarted) EndPagePrinter(hPrinter);
            if (docStarted) EndDocPrinter(hPrinter);
            ClosePrinter(hPrinter);
        }
    }

    private static InvalidOperationException Win32(string message) =>
        new($"{message} ({Marshal.GetLastWin32Error()})");

    /// <summary>Daftar printer yang terpasang, untuk dropdown di form pengaturan.</summary>
    public static IEnumerable<string> InstalledPrinters =>
        System.Drawing.Printing.PrinterSettings.InstalledPrinters.Cast<string>();

    public static string DefaultPrinter =>
        new System.Drawing.Printing.PrinterSettings().PrinterName;
}
