namespace Penerimaan.App.Services;

/// <summary>
/// Encoder Code 128 (Code Set B) untuk jalur cetak GDI.
///
/// Hanya dipakai saat mencetak ke printer biasa. Pada printer Zebra, barcode
/// dihasilkan firmware lewat perintah ^BC - jauh lebih tepat karena presisinya
/// mengikuti resolusi printer, bukan hasil pembulatan piksel.
///
/// Code Set B dipilih karena mencakup ASCII 32-126, termasuk garis bawah pada
/// nomor antrian (KCKR_0826_1335). Code Set C lebih padat untuk angka murni
/// tapi tidak bisa memuat garis bawah.
/// </summary>
public static class Code128
{
    private const int StartB = 104;
    private const int Stop = 106;

    /// <summary>
    /// Lebar batang untuk tiap simbol, bergantian hitam-putih. Tiap digit
    /// adalah kelipatan modul. Indeks 0-102 data, 103-105 start, 106 stop.
    /// </summary>
    private static readonly string[] Pola =
    {
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    };

    /// <summary>
    /// Ubah teks jadi deretan lebar batang. Elemen ganjil (indeks genap) hitam,
    /// selanjutnya bergantian putih.
    /// </summary>
    public static IReadOnlyList<int> Encode(string teks)
    {
        var simbol = new List<int> { StartB };
        var jumlah = StartB;

        for (var i = 0; i < teks.Length; i++)
        {
            var c = teks[i];

            // Di luar Code Set B tidak bisa direpresentasikan; ganti spasi
            // supaya barcode tetap terbaca alih-alih gagal total.
            var nilai = c is >= ' ' and <= '~' ? c - 32 : 0;

            simbol.Add(nilai);
            jumlah += nilai * (i + 1);
        }

        simbol.Add(jumlah % 103);
        simbol.Add(Stop);

        var lebar = new List<int>();
        foreach (var s in simbol)
            foreach (var d in Pola[s])
                lebar.Add(d - '0');

        return lebar;
    }

    /// <summary>Jumlah modul, untuk menghitung lebar barcode sebelum digambar.</summary>
    public static int JumlahModul(string teks) => Encode(teks).Sum();
}
