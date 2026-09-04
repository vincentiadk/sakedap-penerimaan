namespace Penerimaan.App.Models;

/// <summary>Jenis media koleksi, dari tabel referensi collectionmedia di INLIS.</summary>
public sealed class JenisMedia
{
    public string Id { get; set; } = "";
    public string Nama { get; set; } = "";

    /// <summary>Ditampilkan di dropdown.</summary>
    public override string ToString() => Nama;
}
