using System.Drawing.Printing;
using KonfigurasiPrinter = Penerimaan.App.Config.PrinterSettings;
using Penerimaan.App.Models;

namespace Penerimaan.App.Services;

/// <summary>
/// Mencetak label dengan menggambar lewat GDI, bukan mengirim ZPL.
///
/// Dipakai untuk printer non-Zebra - termasuk Microsoft Print to PDF, yang
/// berguna untuk memeriksa tata letak sebelum printer label tersedia.
///
/// Tata letaknya sengaja dibuat sepadan dengan <see cref="LabelTemplate"/>
/// supaya hasil uji di PDF mewakili hasil cetak sebenarnya.
/// </summary>
public static class LabelRenderer
{
    /// <param name="dusMulai">
    /// Dus pertama yang dicetak. Dipakai saat dus susulan datang: labelnya
    /// melanjutkan penomoran, dan label yang sudah tertempel tidak dicetak ulang.
    /// </param>
    public static void Print(PaketPenerimaan paket, KonfigurasiPrinter printer, int dusMulai = 1)
    {
        if (!paket.SiapCetak)
            throw new InvalidOperationException(
                "Nomor antrian belum ada. Label hanya bisa dicetak setelah penerimaan tersimpan.");

        using var doc = new PrintDocument();
        doc.DocumentName = $"Label {paket.NomorAntrian}";

        if (!string.IsNullOrWhiteSpace(printer.Name))
            doc.PrinterSettings.PrinterName = printer.Name;

        // PaperSize memakai satuan 1/100 inci.
        var lebar = (int)Math.Round(printer.LabelWidthMm / 25.4 * 100);
        var tinggi = (int)Math.Round(printer.LabelHeightMm / 25.4 * 100);

        // Utamakan ukuran kertas yang memang dikenal driver. Driver Zebra
        // biasanya sudah punya ukuran labelnya, dan ukuran bawaan driver lebih
        // dipatuhi daripada ukuran kustom.
        //
        // Kalau tidak ada yang cocok, ukuran kustom tetap dicoba - tapi driver
        // berhak menolak. Microsoft Print to PDF, misalnya, akan jatuh ke A4:
        // labelnya tetap tergambar seukuran aslinya, hanya menempel di pojok
        // halaman yang lebih besar.
        doc.DefaultPageSettings.PaperSize =
            UkuranTerdekat(doc.PrinterSettings, lebar, tinggi) ?? new PaperSize("Label", lebar, tinggi);

        doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        doc.OriginAtMargins = false;

        // Semua dus dicetak sebagai halaman berurutan dalam SATU pekerjaan,
        // sehingga Print to PDF menghasilkan satu berkas berisi semua label.
        var total = Math.Max(1, paket.JumlahDus);
        var dus = Math.Clamp(dusMulai, 1, total) - 1;

        doc.PrintPage += (_, e) =>
        {
            dus++;
            Gambar(e, paket, printer, dus, total);
            e.HasMorePages = dus < total;
        };

        doc.Print();
    }

    private static void Gambar(
        PrintPageEventArgs e, PaketPenerimaan paket, KonfigurasiPrinter printer, int dus, int total)
    {
        var g = e.Graphics;
        if (g is null) return;

        // Bekerja dalam milimeter supaya ukuran sama di printer 203 dpi maupun 600 dpi.
        g.PageUnit = GraphicsUnit.Millimeter;

        var lebar = (float)printer.LabelWidthMm;
        var tinggi = (float)printer.LabelHeightMm;

        // NoWrap: teks yang kepanjangan harus dikecilkan fontnya, bukan
        // dipatahkan ke baris kedua atau dipotong di tengah.
        var tengah = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.None
        };

        // Tinggi huruf dalam milimeter, sepadan dengan rasio di LabelTemplate.
        var lebarTeks = lebar * 0.94f;
        var teksResi = $"Nomor Resi: {paket.NomorResi}";
        var teksDus = $"DUS {dus} DARI {total}";

        using var fontResi = FontMuat(g, teksResi, tinggi * 0.10f, FontStyle.Regular, lebarTeks);
        using var fontAntrian = FontMuat(g, paket.NomorAntrian, tinggi * 0.26f, FontStyle.Bold, lebarTeks);
        using var fontDus = FontMuat(g, teksDus, tinggi * 0.13f, FontStyle.Bold, lebarTeks);

        g.DrawString(teksResi, fontResi, Brushes.Black,
            new RectangleF(0, tinggi * 0.03f, lebar, tinggi * 0.14f), tengah);

        g.DrawString(paket.NomorAntrian, fontAntrian, Brushes.Black,
            new RectangleF(0, tinggi * 0.16f, lebar, tinggi * 0.30f), tengah);

        g.DrawString(teksDus, fontDus, Brushes.Black,
            new RectangleF(0, tinggi * 0.45f, lebar, tinggi * 0.17f), tengah);

        if (printer.IncludeBarcode)
            GambarBarcode(g, NomorAntrian.Barcode(paket.NomorAntrian, dus), lebar, tinggi);
    }

    /// <summary>
    /// Ukuran kertas driver yang paling mendekati ukuran label, atau null bila
    /// tidak ada yang cukup dekat. Toleransi 1 mm - cukup untuk menyerap
    /// pembulatan, tapi tidak sampai memilih kertas yang jelas beda.
    /// </summary>
    private static PaperSize? UkuranTerdekat(PrinterSettings printer, int lebar, int tinggi)
    {
        const int toleransi = 4; // 1/100 inci, kira-kira 1 mm

        PaperSize? terbaik = null;
        var selisihTerkecil = int.MaxValue;

        foreach (PaperSize kertas in printer.PaperSizes)
        {
            var selisih = Math.Abs(kertas.Width - lebar) + Math.Abs(kertas.Height - tinggi);

            if (Math.Abs(kertas.Width - lebar) > toleransi) continue;
            if (Math.Abs(kertas.Height - tinggi) > toleransi) continue;
            if (selisih >= selisihTerkecil) continue;

            terbaik = kertas;
            selisihTerkecil = selisih;
        }

        return terbaik;
    }

    /// <summary>
    /// Font sebesar mungkin yang masih muat selebar label. Nomor antrian
    /// panjangnya berubah-ubah mengikuti urutan (KCKR_0826_7 sampai
    /// KCKR_0826_12345), jadi ukuran tetap pasti terpotong suatu saat.
    /// </summary>
    private static Font FontMuat(Graphics g, string teks, float tinggiMm, FontStyle gaya, float lebarMaksMm)
    {
        var ukuran = tinggiMm;

        while (ukuran > 1f)
        {
            var font = new Font("Segoe UI", ukuran, gaya, GraphicsUnit.Millimeter);
            var lebarTeks = g.MeasureString(teks, font, PointF.Empty, StringFormat.GenericTypographic).Width;

            if (lebarTeks <= lebarMaksMm) return font;

            font.Dispose();
            ukuran -= tinggiMm * 0.04f;
        }

        return new Font("Segoe UI", 1f, gaya, GraphicsUnit.Millimeter);
    }

    private static void GambarBarcode(Graphics g, string data, float lebar, float tinggi)
    {
        var batang = Code128.Encode(data);
        var totalModul = batang.Sum();
        if (totalModul == 0) return;

        // Sisakan margin kiri-kanan; zona tenang Code 128 minimal 10 modul.
        var lebarCetak = lebar * 0.86f;
        var modul = lebarCetak / totalModul;

        var x = (lebar - lebarCetak) / 2f;
        var y = tinggi * 0.64f;
        var tinggiBatang = tinggi * 0.26f;

        var hitam = true;
        foreach (var w in batang)
        {
            var lebarBatang = modul * w;

            if (hitam)
                g.FillRectangle(Brushes.Black, x, y, lebarBatang, tinggiBatang);

            x += lebarBatang;
            hitam = !hitam;
        }
    }
}
