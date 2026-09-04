using Penerimaan.App.Models;

namespace Penerimaan.App;

/// <summary>
/// Dialog pemilihan saat satu nomor resi cocok dengan lebih dari satu surat
/// penyerahan di SAKEDAP.
///
/// Petugas yang memilih, bukan aplikasi: satpam memegang paketnya dan bisa
/// mencocokkan nama pengirim di label dengan daftar di layar. Memilih otomatis
/// baris pertama berarti menebak, dan tebakan yang salah menautkan paket ke
/// surat penyerahan milik penerbit lain.
///
/// Dialog sekecil ini ditulis tanpa berkas Designer terpisah.
/// </summary>
public sealed class PilihLetterForm : Form
{
    private readonly ListBox _daftar = new();
    private readonly Label _keterangan = new();
    private readonly Button _pilih = new();
    private readonly Button _batal = new();

    public PaketPenerimaan? Terpilih { get; private set; }

    public PilihLetterForm(IReadOnlyList<PaketPenerimaan> kandidat)
    {
        Text = "Pilih Surat Penyerahan";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 320);

        _keterangan.Text =
            $"Nomor resi ini cocok dengan {kandidat.Count} surat penyerahan.\n" +
            "Pilih yang sesuai dengan pengirim pada paket.";
        _keterangan.Location = new Point(16, 12);
        _keterangan.Size = new Size(528, 44);

        _daftar.Location = new Point(16, 64);
        _daftar.Size = new Size(528, 190);
        _daftar.IntegralHeight = false;

        foreach (var k in kandidat)
        {
            var nama = string.IsNullOrWhiteSpace(k.NamaPelaksanaSerah) ? "(tanpa nama penerbit)" : k.NamaPelaksanaSerah;
            var ekspedisi = string.IsNullOrWhiteSpace(k.Ekspedisi) ? "-" : k.Ekspedisi;

            _daftar.Items.Add(
                $"{nama}  |  {ekspedisi}  |  {k.JumlahJudul} judul / {k.JumlahEks} eks  |  ID {k.LetterId}");
        }

        _daftar.SelectedIndex = 0;
        _daftar.DoubleClick += (_, _) => Pilih(kandidat);

        _pilih.Text = "PILIH";
        _pilih.Location = new Point(324, 268);
        _pilih.Size = new Size(100, 34);
        _pilih.Click += (_, _) => Pilih(kandidat);

        _batal.Text = "Batal";
        _batal.Location = new Point(436, 268);
        _batal.Size = new Size(108, 34);
        _batal.DialogResult = DialogResult.Cancel;

        AcceptButton = _pilih;
        CancelButton = _batal;

        Controls.AddRange(new Control[] { _keterangan, _daftar, _pilih, _batal });
    }

    private void Pilih(IReadOnlyList<PaketPenerimaan> kandidat)
    {
        if (_daftar.SelectedIndex < 0) return;

        Terpilih = kandidat[_daftar.SelectedIndex];
        DialogResult = DialogResult.OK;
        Close();
    }
}
