namespace Penerimaan.App.Models;

/// <summary>Petugas keamanan yang sedang login di pos satpam Gedung E.</summary>
public sealed class Pegawai
{
    public string Id { get; set; } = "";
    public string Nip { get; set; } = "";
    public string Nama { get; set; } = "";

    /// <summary>Dipakai untuk request berikutnya, mengikuti konvensi token di API SAKEDAP.</summary>
    public string Token { get; set; } = "";
}
