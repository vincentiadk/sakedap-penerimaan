using Penerimaan.App.Models;

namespace Penerimaan.App;

/// <summary>Tindakan yang dipilih petugas saat nomor resi sudah pernah dicatat.</summary>
public enum TindakanResiTercatat
{
    Batal,

    /// <summary>Dus susulan dari kiriman yang sama - nomor antrian tetap.</summary>
    TambahDus,

    /// <summary>Kiriman berbeda yang kebetulan nomor resinya sama.</summary>
    AntrianBaru
}

/// <summary>
/// Dialog saat nomor resi sudah pernah dicatat.
///
/// Dua kemungkinan yang harus dibedakan petugas, karena aplikasi tidak bisa
/// menentukannya sendiri:
///   - dus susulan dari kiriman yang sama, penomoran dus dilanjutkan
///   - kiriman lain yang kebetulan nomor resinya sama, perlu antrian sendiri
///
/// Dialog sekecil ini ditulis tanpa berkas Designer terpisah.
/// </summary>
public sealed class ResiTercatatForm : Form
{
    private readonly NumericUpDown _tambahan = new();

    public TindakanResiTercatat Tindakan { get; private set; } = TindakanResiTercatat.Batal;

    /// <summary>Penerimaan yang akan ditambah dusnya - yang terbaru untuk resi ini.</summary>
    public PaketPenerimaan Terpilih { get; }

    public int JumlahTambahan => (int)_tambahan.Value;

    public ResiTercatatForm(string resi, IReadOnlyList<PaketPenerimaan> sudahAda)
    {
        Terpilih = sudahAda[0];

        Text = "Resi sudah tercatat";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(600, 340);

        var judul = new Label
        {
            Text = $"Nomor resi {resi} sudah pernah dicatat:",
            Location = new Point(16, 14),
            Size = new Size(568, 24)
        };

        var daftar = new ListBox
        {
            Location = new Point(16, 44),
            Size = new Size(568, 100),
            IntegralHeight = false
        };

        foreach (var p in sudahAda)
        {
            daftar.Items.Add(
                $"{p.NomorAntrian}   {p.JumlahDus} dus   {p.WaktuTerima:dd/MM/yyyy HH:mm}   oleh {p.NamaPetugasPenerima}");
        }

        var pilihan = new Label
        {
            Text = "Paket yang baru datang ini dus susulan dari kiriman yang sama, "
                 + "atau kiriman lain yang kebetulan nomor resinya sama?",
            Location = new Point(16, 156),
            Size = new Size(568, 40)
        };

        var lblTambahan = new Label
        {
            Text = $"Dus susulan yang datang sekarang (akan jadi DUS {Terpilih.JumlahDus + 1} dan seterusnya)",
            Location = new Point(16, 206),
            Size = new Size(460, 24)
        };

        _tambahan.Location = new Point(490, 202);
        _tambahan.Size = new Size(94, 27);
        _tambahan.Minimum = 1;
        _tambahan.Maximum = 999;
        _tambahan.Value = 1;

        var btnTambah = new Button
        {
            Text = "TAMBAH DUS",
            Location = new Point(16, 250),
            Size = new Size(160, 38)
        };
        btnTambah.Click += (_, _) => Pilih(TindakanResiTercatat.TambahDus);

        var btnBaru = new Button
        {
            Text = "Buat Antrian Baru",
            Location = new Point(188, 250),
            Size = new Size(160, 38)
        };
        btnBaru.Click += (_, _) => Pilih(TindakanResiTercatat.AntrianBaru);

        var btnBatal = new Button
        {
            Text = "Batal",
            Location = new Point(476, 250),
            Size = new Size(108, 38),
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = btnTambah;
        CancelButton = btnBatal;

        Controls.AddRange(new Control[]
        {
            judul, daftar, pilihan, lblTambahan, _tambahan, btnTambah, btnBaru, btnBatal
        });
    }

    private void Pilih(TindakanResiTercatat tindakan)
    {
        Tindakan = tindakan;
        DialogResult = DialogResult.OK;
        Close();
    }
}
