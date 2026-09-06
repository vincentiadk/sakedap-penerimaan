using Penerimaan.App.Models;

namespace Penerimaan.App;

/// <summary>
/// Ubah penerimaan yang sudah tersimpan, dalam batas 24 jam sejak dibuat.
///
/// Nomor antrian dan nomor resi tidak bisa diubah: keduanya sudah tercetak di
/// label yang menempel di kardus. Mengubahnya membuat data di layar tidak lagi
/// cocok dengan barang fisiknya, yang justru menghambat temu kembali.
///
/// Lokasi juga tidak ada di sini - lokasi tidak pernah diketik, melainkan
/// ditentukan PC mana yang men-scan lewat tab Pindah Lokasi.
///
/// Jumlah dus juga tidak: menambah dus berarti menerbitkan nomor antrian baru
/// dan mencetak label baru, yang jalurnya lewat scan resi yang sama lalu
/// "tambah dus susulan". Menguranginya berarti menghapus kardus yang labelnya
/// sudah tertempel, dan itu urusan tombol Hapus. Menjadikannya kotak angka di
/// sini akan tampak sepele padahal keduanya bukan sekadar mengubah data.
///
/// Yang diubah di sini berlaku untuk SELURUH kardus sekiriman - nama petugas
/// dan waktu terima berulang di tiap barisnya.
///
/// Dialog sekecil ini ditulis tanpa berkas Designer terpisah.
/// </summary>
public sealed class EditAntrianForm : Form
{
    private readonly PaketPenerimaan _paket;

    private readonly TextBox _petugas = new();
    private readonly ComboBox _ekspedisi = new();
    private readonly ComboBox _media = new();
    private readonly DateTimePicker _waktu = new();

    private readonly string _labelDatangLangsung;

    public EditAntrianForm(PaketPenerimaan paket, IEnumerable<object> pilihanEkspedisi,
        IEnumerable<object> pilihanMedia, string labelDatangLangsung)
    {
        _paket = paket;
        _labelDatangLangsung = labelDatangLangsung;

        Text = $"Ubah {paket.NomorAntrian}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 304);

        var sisa = paket.SisaWaktuUbah;
        var info = new Label
        {
            Text = $"{paket.NomorAntrian}   |   Resi: {(paket.NomorResi.Length == 0 ? "-" : paket.NomorResi)}"
                 + $"{Environment.NewLine}Sisa waktu ubah: {(int)sisa.TotalHours} jam {sisa.Minutes} menit",
            Location = new Point(16, 12),
            Size = new Size(528, 44),
            ForeColor = SystemColors.GrayText
        };

        _petugas.Text = paket.NamaPetugasPenerima;
        _media.DropDownStyle = ComboBoxStyle.DropDownList;
        _waktu.Format = DateTimePickerFormat.Custom;
        _waktu.CustomFormat = "dd/MM/yyyy HH:mm";
        _waktu.Value = paket.WaktuTerima == default ? DateTime.Now : paket.WaktuTerima;

        _ekspedisi.Items.AddRange(pilihanEkspedisi.ToArray());
        _ekspedisi.Text = paket.Cara == CaraDatang.Langsung ? labelDatangLangsung : paket.Ekspedisi;

        _media.Items.AddRange(pilihanMedia.ToArray());
        PilihMedia(paket.JenisMediaId);

        var y = 68;
        Tambah("Nama Petugas *", _petugas, ref y);
        Tambah("Ekspedisi *", _ekspedisi, ref y);
        Tambah("Jenis Media *", _media, ref y);
        Tambah("Tanggal Terima *", _waktu, ref y);

        var simpan = new Button
        {
            Text = "SIMPAN PERUBAHAN",
            Location = new Point(300, 252),
            Size = new Size(180, 36)
        };
        simpan.Click += (_, _) => Simpan();

        var batal = new Button
        {
            Text = "Batal",
            Location = new Point(488, 252),
            Size = new Size(56, 36),
            DialogResult = DialogResult.Cancel
        };

        AcceptButton = simpan;
        CancelButton = batal;

        Controls.Add(info);
        Controls.Add(simpan);
        Controls.Add(batal);
    }

    private void Tambah(string judul, Control kendali, ref int y)
    {
        Controls.Add(new Label { Text = judul, Location = new Point(16, y + 4), Size = new Size(160, 22) });

        kendali.Location = new Point(184, y);
        kendali.Size = kendali is NumericUpDown ? new Size(100, 27) : new Size(360, 27);
        Controls.Add(kendali);

        y += 36;
    }

    private void PilihMedia(string mediaId)
    {
        foreach (var item in _media.Items)
        {
            if (item is JenisMedia m && m.Id == mediaId)
            {
                _media.SelectedItem = item;
                return;
            }
        }

        if (_media.Items.Count > 0) _media.SelectedIndex = 0;
    }

    private void Simpan()
    {
        if (_petugas.Text.Trim().Length == 0)
        {
            MessageBox.Show(this, "Nama petugas wajib diisi.", "Belum lengkap",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _petugas.Focus();
            return;
        }

        if (_ekspedisi.Text.Trim().Length == 0)
        {
            MessageBox.Show(this, "Ekspedisi wajib dipilih.", "Belum lengkap",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _ekspedisi.Focus();
            return;
        }

        if (_media.SelectedItem is not JenisMedia)
        {
            MessageBox.Show(this, "Jenis media wajib dipilih.", "Belum lengkap",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _media.Focus();
            return;
        }

        var langsung = _ekspedisi.Text.Trim() == _labelDatangLangsung;

        _paket.NamaPetugasPenerima = _petugas.Text.Trim();
        _paket.Cara = langsung ? CaraDatang.Langsung : CaraDatang.Ekspedisi;
        _paket.Ekspedisi = langsung ? "" : _ekspedisi.Text.Trim();
        _paket.WaktuTerima = _waktu.Value;

        var media = (JenisMedia)_media.SelectedItem!;
        _paket.JenisMediaId = media.Id;
        _paket.JenisMedia = media.Nama;

        DialogResult = DialogResult.OK;
        Close();
    }
}
