namespace Penerimaan.App.Models;

/// <summary>Petugas keamanan yang sedang login di pos satpam Gedung E.</summary>
public sealed class Pegawai
{
    public string Id { get; set; } = "";
    public string Nip { get; set; } = "";
    public string Nama { get; set; } = "";

    /// <summary>
    /// Satuan kerja tempat petugas ini bertugas, dari respons login INLIS.
    /// 37 = Perpusnas; tiap perpustakaan provinsi punya nilainya sendiri.
    ///
    /// Menentukan lokasi mana yang terlihat oleh aplikasi. Kosong berarti
    /// respons login tidak memuatnya - lihat <see cref="FieldLoginTersedia"/>.
    /// </summary>
    public string BranchId { get; set; } = "";

    /// <summary>
    /// Nama-nama field yang benar-benar dikembalikan API login, apa adanya.
    ///
    /// Hanya untuk pesan kesalahan saat BRANCH_ID tidak ketemu: tanpa ini,
    /// admin harus menebak-nebak nama field yang benar tanpa cara melihat
    /// isi respons dari dalam aplikasi.
    /// </summary>
    public string FieldLoginTersedia { get; set; } = "";

    /// <summary>
    /// True bila <see cref="BranchId"/> datang dari appsettings, bukan dari
    /// respons login - artinya API belum memuatnya.
    ///
    /// Ditampilkan di layar supaya keadaan sementara ini kelihatan, bukan
    /// mengendap jadi kebiasaan yang tidak ada yang ingat lagi asalnya.
    /// </summary>
    public bool BranchDariConfig { get; set; }

    /// <summary>Dipakai untuk request berikutnya, mengikuti konvensi token di API SAKEDAP.</summary>
    public string Token { get; set; } = "";
}
