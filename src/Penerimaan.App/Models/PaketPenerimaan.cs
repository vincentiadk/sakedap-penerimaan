namespace Penerimaan.App.Models;

/// <summary>
/// Satu KARDUS dari paket SSKCKR yang diterima petugas keamanan Gedung E.
///
/// Kiriman berisi empat kardus menghasilkan empat objek seperti ini, masing-
/// masing dengan nomor antrian dan labelnya sendiri, terikat oleh
/// <see cref="KirimanId"/> yang sama. Posisinya pun dilacak sendiri-sendiri,
/// karena kardus satu kiriman sering berpindah tidak bersamaan.
/// </summary>
public sealed class PaketPenerimaan
{
    /// <summary>Id baris di tabel penerimaan_paket. Kosong sebelum tersimpan.</summary>
    public string? Id { get; set; }

    /// <summary>Referensi ke letter di INLIS. Kosong pada jalur manual.</summary>
    public string LetterId { get; set; } = "";

    public string NomorResi { get; set; } = "";
    public string Ekspedisi { get; set; } = "";

    /// <summary>Diantar ekspedisi atau diserahkan langsung ke pos satpam.</summary>
    public CaraDatang Cara { get; set; } = CaraDatang.Ekspedisi;

    /// <summary>Referensi ke collectionmedia. Kosong bila belum dipilih.</summary>
    public string JenisMediaId { get; set; } = "";

    /// <summary>Nama jenis media, untuk ditampilkan tanpa perlu join ulang.</summary>
    public string JenisMedia { get; set; } = "";
    public string NamaPelaksanaSerah { get; set; } = "";
    public int JumlahJudul { get; set; }
    public int JumlahEks { get; set; }

    /// <summary>
    /// Banyaknya kardus dalam kiriman ini. Nilainya sama di seluruh kardus
    /// sekiriman, dan dicetak di label sebagai "DUS n DARI N".
    /// </summary>
    public int JumlahDus { get; set; } = 1;

    /// <summary>Kardus ke berapa dalam kirimannya, 1 sampai <see cref="JumlahDus"/>.</summary>
    public int NomorDus { get; set; } = 1;

    /// <summary>
    /// Pengikat kardus sekiriman: nomor antrian kardus pertama. Kosong sampai
    /// kiriman tersimpan, karena nomor antrian baru terbit di sisi server.
    /// </summary>
    public string KirimanId { get; set; } = "";

    /// <summary>Satuan kerja pemilik kardus ini. 37 = Perpusnas.</summary>
    public string BranchId { get; set; } = "";

    public string NamaPetugasPenerima { get; set; } = "";
    public DateTime WaktuTerima { get; set; } = DateTime.Now;

    /// <summary>Diterbitkan server dari tabel letter. Kosong sampai penerimaan tersimpan.</summary>
    public string NomorAntrian { get; set; } = "";

    /// <summary>Posisi terakhir paket. Ditentukan PC yang menyimpan atau men-scan.</summary>
    public string LokasiId { get; set; } = "";

    public string LokasiNama { get; set; } = "";
    public SumberData Sumber { get; set; } = SumberData.Manual;
    public StatusPaket Status { get; set; } = StatusPaket.DiterimaSatpam;

    /// <summary>Saat baris dibuat di LETTER_ANTRIAN. Dasar batas ubah/hapus 24 jam.</summary>
    public DateTime CreateDate { get; set; }

    /// <summary>Label hanya bisa dicetak setelah server memberi nomor antrian.</summary>
    public bool SiapCetak => !string.IsNullOrWhiteSpace(NomorAntrian);

    /// <summary>
    /// Batas ubah dan hapus: 24 jam sejak baris dibuat.
    ///
    /// Dihitung dari CREATE_DATE, bukan WAKTU_TERIMA - waktu terima bisa
    /// diisi mundur oleh petugas, sedangkan create_date adalah fakta kapan
    /// datanya benar-benar masuk sistem.
    /// </summary>
    public bool MasihBisaDiubah =>
        CreateDate != default && DateTime.Now - CreateDate < TimeSpan.FromHours(24);

    public TimeSpan SisaWaktuUbah =>
        CreateDate == default ? TimeSpan.Zero
        : TimeSpan.FromHours(24) - (DateTime.Now - CreateDate);
}

public enum CaraDatang
{
    /// <summary>Diantar jasa pengiriman, punya nomor resi.</summary>
    Ekspedisi,

    /// <summary>Diserahkan langsung ke pos satpam, biasanya tanpa nomor resi.</summary>
    Langsung
}

public enum SumberData
{
    /// <summary>Nomor resi ditemukan lewat integrasi SAKEDAP.</summary>
    Sakedap,

    /// <summary>Nomor resi diketik petugas karena tidak ditemukan di SAKEDAP.</summary>
    Manual
}

public enum StatusPaket
{
    MenungguPenerimaan,
    DiterimaSatpam,
    DiLokasiTransit,
    DiterimaPegawaiKckr,
    Selesai
}

/// <summary>
/// Padanan <see cref="StatusPaket"/> dengan kolom STATUS di LETTER_ANTRIAN.
///
/// Disimpan sebagai teks, bukan angka. Angka akan membuat arti sebuah baris
/// bergantung pada urutan deklarasi enum di atas: menyisipkan satu anggota
/// baru sebelum DiterimaSatpam diam-diam mengubah arti seluruh baris lama
/// yang sudah ada di produksi, tanpa error apa pun. Teks juga sejalan dengan
/// SUMBER dan CARA_DATANG di tabel yang sama, yang keduanya sudah varchar
/// berpasangan dengan CHECK constraint.
/// </summary>
public static class StatusPaketDb
{
    public static string KeTeks(StatusPaket status) => status switch
    {
        StatusPaket.MenungguPenerimaan => "menunggu",
        StatusPaket.DiterimaSatpam => "diterima_satpam",
        StatusPaket.DiLokasiTransit => "transit",
        StatusPaket.DiterimaPegawaiKckr => "diterima_kckr",
        StatusPaket.Selesai => "selesai",
        _ => "diterima_satpam"
    };

    /// <summary>
    /// Nilai kosong atau tak dikenal dianggap "diterima_satpam": baris di
    /// LETTER_ANTRIAN hanya lahir dari pencatatan di pos satpam, jadi itu
    /// tebakan yang paling aman - lebih baik daripada melempar error dan
    /// membuat seluruh daftar antrian gagal tampil karena satu baris aneh.
    /// </summary>
    public static StatusPaket Dari(string? teks) => teks?.Trim().ToLowerInvariant() switch
    {
        "menunggu" => StatusPaket.MenungguPenerimaan,
        "diterima_satpam" => StatusPaket.DiterimaSatpam,
        "transit" => StatusPaket.DiLokasiTransit,
        "diterima_kckr" => StatusPaket.DiterimaPegawaiKckr,
        "selesai" => StatusPaket.Selesai,
        _ => StatusPaket.DiterimaSatpam
    };
}
