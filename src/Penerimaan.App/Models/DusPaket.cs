namespace Penerimaan.App.Models;

/// <summary>
/// Satu kardus dari sebuah penerimaan, dengan posisinya sendiri.
///
/// Dus dari satu kiriman bisa terpisah lokasinya - misalnya dua kardus naik
/// duluan dan satu menyusul - jadi posisi dilacak per kardus, bukan per
/// penerimaan.
/// </summary>
public sealed class DusPaket
{
    public string Id { get; set; } = "";
    public string PenerimaanId { get; set; } = "";
    public int NomorDus { get; set; }

    public string LokasiId { get; set; } = "";
    public string LokasiNama { get; set; } = "";
}
