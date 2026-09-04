using System.Globalization;
using System.Text.RegularExpressions;

namespace Penerimaan.App.Models;

/// <summary>
/// Format nomor antrian paket KCKR: KCKR_[bulan][tahun2digit]_[nomor urut],
/// contoh KCKR_0826_1335 untuk Agustus 2026 urutan 1335.
///
/// Nomor urut diterbitkan server dari tabel letter, jadi kelas ini hanya untuk
/// menyusun dan membaca kembali - bukan untuk menghasilkan nomor baru.
/// </summary>
public static partial class NomorAntrian
{
    public static string Format(DateTime periode, int urutan) =>
        $"KCKR_{periode:MMyy}_{urutan}";

    public static bool TryParse(string nomor, out DateTime periode, out int urutan)
    {
        periode = default;
        urutan = 0;

        var match = Pola().Match(nomor);
        if (!match.Success) return false;

        if (!DateTime.TryParseExact(match.Groups[1].Value, "MMyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out periode))
            return false;

        return int.TryParse(match.Groups[2].Value, out urutan);
    }

    [GeneratedRegex(@"^KCKR_(\d{4})_(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex Pola();

    /// <summary>
    /// Isi barcode pada label: nomor antrian, tanda hubung, nomor dus.
    /// Contoh KCKR_0826_1335-2 untuk kardus kedua.
    /// </summary>
    public static string Barcode(string nomorAntrian, int nomorDus) => $"{nomorAntrian}-{nomorDus}";

    /// <summary>
    /// Baca kembali isi barcode.
    ///
    /// Nomor dus 0 berarti barcode tanpa akhiran - itu label lama yang dicetak
    /// sebelum pelacakan per dus, dan diperlakukan sebagai seluruh penerimaan.
    /// </summary>
    public static (string Nomor, int Dus) BacaBarcode(string isi)
    {
        var teks = (isi ?? "").Trim();
        var pisah = teks.LastIndexOf('-');

        if (pisah > 0 && int.TryParse(teks[(pisah + 1)..], out var dus) && dus > 0)
            return (teks[..pisah], dus);

        return (teks, 0);
    }
}
