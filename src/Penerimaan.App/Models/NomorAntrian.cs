using System.Globalization;
using System.Text.RegularExpressions;

namespace Penerimaan.App.Models;

/// <summary>
/// Format nomor antrian paket KCKR, berbeda menurut satuan kerja:
///
///   Perpusnas : KCKR_[bulan][tahun2digit]_[urutan]            KCKR_0826_1335
///   Provinsi  : KCKR_[branch]_[bulan][tahun2digit]_[urutan]   KCKR_11_0826_1335
///
/// Perpusnas sengaja TANPA sisipan branch supaya nomor yang sudah beredar dan
/// tertempel di kardus di sana tidak berubah bentuk. Provinsi memakainya karena
/// deret nomor berjalan sendiri-sendiri per satuan kerja - tanpa pembeda, dua
/// satuan kerja akan menerbitkan nomor yang sama dan salah satunya ditolak
/// UQ_LETTER_ANTRIAN_NOMOR.
///
/// Nomor urut diterbitkan dengan membaca urutan terakhir dari tabel, jadi kelas
/// ini hanya untuk menyusun dan membaca kembali - bukan menghasilkan nomor baru.
/// </summary>
public static partial class NomorAntrian
{
    /// <summary>Satuan kerja Perpusnas - satu-satunya yang nomornya tanpa branch.</summary>
    public const string BranchPerpusnas = "37";

    public static bool DariPerpusnas(string? branchId) =>
        (branchId ?? "").Trim() == BranchPerpusnas;

    public static string Format(DateTime periode, int urutan, string branchId) =>
        $"{Awalan(periode, branchId)}{urutan}";

    /// <summary>
    /// Bagian nomor sebelum angka urut. Dipakai juga sebagai pola LIKE saat
    /// mencari urutan terakhir, supaya deret Perpusnas dan provinsi tidak
    /// pernah saling terbaca.
    /// </summary>
    public static string Awalan(DateTime periode, string branchId) =>
        DariPerpusnas(branchId)
            ? $"KCKR_{periode:MMyy}_"
            : $"KCKR_{branchId.Trim()}_{periode:MMyy}_";

    /// <summary>
    /// Baca kembali sebuah nomor antrian. <paramref name="branchId"/> kosong
    /// berarti nomor Perpusnas.
    ///
    /// Kedua bentuk dibedakan dari banyaknya ruas, dan itu tidak ambigu:
    /// periode selalu 4 digit, jadi tiga ruas hanya bisa berarti Perpusnas.
    /// </summary>
    public static bool TryParse(string nomor, out DateTime periode, out int urutan, out string branchId)
    {
        periode = default;
        urutan = 0;
        branchId = "";

        var match = Pola().Match(nomor);
        if (!match.Success) return false;

        if (!DateTime.TryParseExact(match.Groups[2].Value, "MMyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out periode))
            return false;

        branchId = match.Groups[1].Success ? match.Groups[1].Value : BranchPerpusnas;

        return int.TryParse(match.Groups[3].Value, out urutan);
    }

    // Ruas branch dibuat opsional. Untuk KCKR_0826_1335 mesin regex sempat
    // mencoba membaca 0826 sebagai branch, gagal karena sisanya tidak muat
    // pola [4 digit]_[angka], lalu mundur dan membacanya sebagai periode.
    [GeneratedRegex(@"^KCKR_(?:(\d+)_)?(\d{4})_(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex Pola();

    /// <summary>
    /// Isi barcode pada label: nomor antriannya sendiri, tanpa tambahan apa pun.
    ///
    /// Tidak perlu akhiran nomor dus seperti dulu: satu kardus sudah punya
    /// nomor antrian sendiri, jadi nomor itu sudah menunjuk kardus yang mana.
    /// </summary>
    public static string Barcode(string nomorAntrian) => nomorAntrian;

    /// <summary>
    /// Baca kembali isi barcode.
    ///
    /// Akhiran "-n" dari label versi lama dibuang: dulu satu kiriman hanya
    /// punya satu nomor antrian dan kardusnya dibedakan lewat akhiran itu.
    /// Label lama jadi tetap terbaca, dan menunjuk kardus pertama kirimannya.
    /// </summary>
    public static string BacaBarcode(string isi)
    {
        var teks = (isi ?? "").Trim();
        var pisah = teks.LastIndexOf('-');

        return pisah > 0 && int.TryParse(teks[(pisah + 1)..], out _)
            ? teks[..pisah]
            : teks;
    }
}
