using System.Text;
using Penerimaan.App.Config;
using Penerimaan.App.Models;

namespace Penerimaan.App.Services;

/// <summary>
/// Menyusun perintah ZPL untuk label nomor antrian. Printer target Zebra.
///
/// Dipisah dari RawPrinterHelper supaya mengubah tata letak label tidak
/// menyentuh kode printer sama sekali.
/// </summary>
public static class LabelTemplate
{
    /// <summary>Karakter yang jadi perintah kontrol di ZPL.</summary>
    private static readonly char[] Terlarang = { '^', '~', '\\', '\r', '\n' };

    /// <summary>
    /// Satu berkas ZPL berisi label untuk seluruh kardus yang diberikan.
    /// Dikirim sebagai satu pekerjaan cetak supaya labelnya keluar berurutan
    /// dan tidak terselip di antara cetakan lain.
    ///
    /// Tiap kardus adalah objek <see cref="PaketPenerimaan"/> tersendiri
    /// dengan nomor antriannya sendiri, jadi tidak ada lagi parameter
    /// "mulai dari dus ke berapa" - pemanggil cukup mengirim kardus yang
    /// memang mau dicetak.
    /// </summary>
    public static string Build(
        IEnumerable<PaketPenerimaan> kardus, PrinterSettings printer)
    {
        var sb = new StringBuilder();

        foreach (var k in kardus)
        {
            if (!k.SiapCetak)
                throw new InvalidOperationException(
                    "Nomor antrian belum ada. Label hanya bisa dicetak setelah penerimaan tersimpan di SAKEDAP.");

            sb.Append(BuildSatu(k, printer, k.NomorDus, Math.Max(k.NomorDus, k.JumlahDus)));
        }

        return sb.ToString();
    }

    /// <summary>Satu kardus saja.</summary>
    public static string Build(PaketPenerimaan kardus, PrinterSettings printer)
        => Build(new[] { kardus }, printer);

    private static string BuildSatu(PaketPenerimaan paket, PrinterSettings printer, int dus, int total)
    {
        var lebar = printer.LabelWidthDots;
        var tinggi = printer.LabelHeightDots;

        var sb = new StringBuilder();
        sb.Append("^XA");
        sb.Append("^CI28");                                  // UTF-8
        sb.Append($"^PW{lebar}^LL{tinggi}");
        sb.Append("^LH0,0");

        // Nomor resi, kecil, rata tengah.
        Baris(sb, lebar, Skala(tinggi, 0.04), Skala(tinggi, 0.10),
            $"Nomor Resi: {Bersihkan(paket.NomorResi, 40)}");

        // Nomor antrian, besar, rata tengah - ini yang dibaca petugas dari jauh.
        Baris(sb, lebar, Skala(tinggi, 0.17), Skala(tinggi, 0.26),
            Bersihkan(paket.NomorAntrian, 30));

        // Penanda dus. Selalu dicetak walau hanya satu dus, supaya petugas
        // langsung tahu apakah masih ada dus lain yang menyusul.
        Baris(sb, lebar, Skala(tinggi, 0.46), Skala(tinggi, 0.14),
            $"DUS {dus} DARI {total}");

        if (printer.IncludeBarcode)
        {
            // Code128 dari nomor antrian, supaya paket bisa ditemukan kembali
            // dengan scan, bukan mengetik ulang nomornya.
            sb.Append($"^FO0,{Skala(tinggi, 0.65)}^BY2");
            sb.Append($"^FB{lebar},1,0,C,0");
            // Isi barcode persis sama dengan nomor antrian yang tercetak besar
            // di atasnya - nomor itu sudah milik kardus ini sendiri.
            sb.Append($"^BCN,{Skala(tinggi, 0.26)},N,N,N^FD{Bersihkan(NomorAntrian.Barcode(paket.NomorAntrian), 30)}^FS");
        }

        sb.Append("^XZ");
        return sb.ToString();
    }

    /// <summary>
    /// Satu baris teks rata tengah, dengan lebar huruf dipersempit bila
    /// teksnya tidak muat.
    ///
    /// Font bawaan Zebra tidak mengecil sendiri: ^FB dengan satu baris akan
    /// MEMOTONG teks yang kepanjangan. Karena nomor antrian memanjang seiring
    /// urutan (KCKR_0826_7 sampai KCKR_0826_12345), lebar hurufnya dihitung
    /// dari panjang teks.
    /// </summary>
    private static void Baris(StringBuilder sb, int lebar, int y, int tinggiFont, string teks)
    {
        // Font A0 pada Zebra kira-kira selebar 0,6 kali tingginya.
        var lebarWajar = (int)Math.Round(tinggiFont * 0.6);
        var lebarMuat = (int)Math.Floor(lebar * 0.94 / Math.Max(1, teks.Length));
        var lebarFont = Math.Max(2, Math.Min(lebarWajar, lebarMuat));

        sb.Append($"^FO0,{y}^A0N,{tinggiFont},{lebarFont}");
        sb.Append($"^FB{lebar},1,0,C,0^FD{teks}^FS");
    }

    private static int Skala(int tinggi, double rasio) => (int)Math.Round(tinggi * rasio);

    /// <summary>
    /// Buang karakter kontrol supaya data dari API tidak bisa merusak
    /// atau menyisipkan perintah label.
    /// </summary>
    private static string Bersihkan(string value, int maxLength)
    {
        var cleaned = new string(value.Where(c => !Terlarang.Contains(c)).ToArray());
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
