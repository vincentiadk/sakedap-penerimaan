namespace Penerimaan.App.Models;

/// <summary>
/// Lokasi fisik paket, dari tabel LETTER_ANTRIAN_LOKASI.
///
/// Tidak pernah dipilih petugas: lokasi ditentukan oleh PC mana yang menyimpan
/// atau men-scan, lewat pemetaan IP di LETTER_ANTRIAN_PC.
/// </summary>
public sealed class Lokasi
{
    public string Id { get; set; } = "";
    public string Nama { get; set; } = "";

    public override string ToString() => Nama;
}
