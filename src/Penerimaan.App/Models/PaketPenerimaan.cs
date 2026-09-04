namespace Penerimaan.App.Models;

/// <summary>Satu paket SSKCKR yang diterima petugas keamanan Gedung E.</summary>
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

    /// <summary>Banyaknya dus dalam satu kiriman. Tiap dus dapat labelnya sendiri.</summary>
    public int JumlahDus { get; set; } = 1;

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
