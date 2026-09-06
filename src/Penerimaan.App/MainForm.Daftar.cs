using System.Text;
using Penerimaan.App.Models;
using Penerimaan.App.Services;

namespace Penerimaan.App;

/// <summary>
/// Tab 2 - Daftar Antrian. Dipisah dari MainForm.cs supaya logika penerimaan
/// dan logika daftar tidak bercampur dalam satu berkas panjang.
/// </summary>
public partial class MainForm
{
    private readonly List<PaketPenerimaan> _daftar = new();

    private async void btnMuat_Click(object? sender, EventArgs e) => await MuatDaftarAsync();

    private async void txtCari_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        await MuatDaftarAsync();
    }

    private async Task MuatDaftarAsync()
    {
        SetBusy(true);
        try
        {
            var hasil = await _repo.DaftarAsync(dtpDari.Value, dtpSampai.Value, txtCari.Text);

            _daftar.Clear();
            _daftar.AddRange(hasil);
            TampilkanDaftar();

            // Satu baris = satu kardus, jadi jumlah baris bukan jumlah
            // kiriman. Keduanya disebut supaya angkanya tidak salah dibaca.
            var kiriman = hasil.Select(p => p.KirimanId).Distinct().Count();

            Info(hasil.Count == 0
                ? "Tidak ada data pada rentang tanggal ini."
                : $"{hasil.Count} dus dari {kiriman} kiriman dimuat.");
        }
        catch (Exception ex)
        {
            Warn($"Gagal memuat daftar: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void TampilkanDaftar()
    {
        lvDaftar.BeginUpdate();
        lvDaftar.Items.Clear();

        for (var i = 0; i < _daftar.Count; i++)
        {
            var p = _daftar[i];
            lvDaftar.Items.Add(new ListViewItem(new[]
            {
                (i + 1).ToString(),
                p.WaktuTerima == default ? "" : p.WaktuTerima.ToString("dd/MM/yyyy HH:mm"),
                p.NamaPetugasPenerima,
                p.NomorAntrian,
                $"{p.NomorDus}/{p.JumlahDus}",
                p.Ekspedisi,
                p.JenisMedia,
                p.LokasiNama,
                p.NomorResi
            }));
        }

        lvDaftar.EndUpdate();
        btnCetakUlang.Enabled = false;
        btnHapus.Enabled = false;
    }

    private void lvDaftar_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var ada = lvDaftar.SelectedItems.Count > 0;
        btnCetakUlang.Enabled = ada;

        // Hapus hanya dalam 24 jam sejak baris dibuat.
        btnHapus.Enabled = ada && _daftar[lvDaftar.SelectedItems[0].Index].MasihBisaDiubah;
    }

    /// <summary>Klik ganda membuka form ubah, sesuai kebiasaan tabel di aplikasi lain.</summary>
    private async void lvDaftar_DoubleClick(object? sender, EventArgs e)
    {
        if (lvDaftar.SelectedItems.Count == 0) return;

        var paket = _daftar[lvDaftar.SelectedItems[0].Index];

        if (!paket.MasihBisaDiubah)
        {
            Warn($"{paket.NomorAntrian} sudah lewat 24 jam, tidak bisa diubah.");
            return;
        }

        using var dialog = new EditAntrianForm(
            paket, cboEkspedisi.Items.Cast<object>(), cboMedia.Items.Cast<object>(), DatangLangsung);

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        SetBusy(true);
        try
        {
            // Data seperti nama petugas dan waktu terima berulang di tiap
            // kardus, jadi koreksinya menyentuh seluruh kiriman - kalau tidak,
            // kiriman yang sama akan menyebutkan dua nama petugas berbeda.
            var kena = await _repo.UbahAsync(paket);
            await MuatDaftarAsync();

            Info(kena == 1
                ? $"{paket.NomorAntrian} berhasil diubah."
                : $"Kiriman {paket.KirimanId} berhasil diubah ({kena} dus).");
        }
        catch (Exception ex)
        {
            Warn($"Gagal mengubah: {ex.Message}");
            await MuatDaftarAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void btnHapus_Click(object? sender, EventArgs e)
    {
        if (lvDaftar.SelectedItems.Count == 0) return;

        var paket = _daftar[lvDaftar.SelectedItems[0].Index];

        if (!paket.MasihBisaDiubah)
        {
            Warn($"{paket.NomorAntrian} sudah lewat 24 jam, tidak bisa dihapus.");
            return;
        }

        // Yang dihapus SELURUH kiriman, bukan baris yang dipilih saja -
        // menyisakan kardus timpang yang mengaku "dus 2 dari 4" padahal
        // saudaranya sudah hilang dari daftar justru lebih membingungkan.
        var sekiriman = paket.JumlahDus;

        var jawab = MessageBox.Show(this,
            $"Hapus seluruh kiriman {paket.KirimanId}?{Environment.NewLine}{Environment.NewLine}"
            + $"Resi: {(paket.NomorResi.Length == 0 ? "-" : paket.NomorResi)}{Environment.NewLine}"
            + $"Diterima: {paket.WaktuTerima:dd/MM/yyyy HH:mm} oleh {paket.NamaPetugasPenerima}"
            + $"{Environment.NewLine}{Environment.NewLine}"
            + $"{sekiriman} dus akan dihapus, termasuk {paket.NomorAntrian} yang Anda pilih. "
            + "Nomor antriannya tidak akan dipakai ulang, dan label yang sudah tercetak "
            + "harus dilepas dari kardusnya.",
            "Hapus kiriman", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (jawab != DialogResult.Yes) return;

        SetBusy(true);
        try
        {
            var kena = await _repo.HapusAsync(paket);
            Info($"Kiriman {paket.KirimanId} dihapus ({kena} dus).");
            await MuatDaftarAsync();
        }
        catch (Exception ex)
        {
            Warn($"Gagal menghapus: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Cetak ulang label satu kardus - kardus yang barisnya dipilih. Bukan
    /// seluruh kiriman: label yang rusak atau terlepas biasanya cuma satu, dan
    /// mencetak empat label baru untuk kardus yang labelnya masih menempel
    /// hanya membuat kardus punya dua label dengan nomor berbeda.
    /// </summary>
    private void btnCetakUlang_Click(object? sender, EventArgs e)
    {
        if (lvDaftar.SelectedItems.Count == 0) return;

        var kardus = _daftar[lvDaftar.SelectedItems[0].Index];
        Cetak(new[] { kardus });
    }

    private void btnUnduh_Click(object? sender, EventArgs e)
    {
        if (_daftar.Count == 0)
        {
            Warn("Tidak ada data untuk diunduh. Muat daftar terlebih dahulu.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV (dapat dibuka Excel)|*.csv",
            FileName = $"Daftar Penerimaan {dtpDari.Value:yyyy-MM-dd} sd {dtpSampai.Value:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            File.WriteAllText(dialog.FileName, BuatCsv(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            Info($"Data disimpan ke {dialog.FileName}");
        }
        catch (Exception ex)
        {
            Warn($"Gagal menyimpan berkas: {ex.Message}");
        }
    }

    /// <summary>
    /// CSV dengan pemisah titik koma, mengikuti Excel berlokal Indonesia.
    /// BOM UTF-8 ditulis supaya huruf beraksen tidak rusak saat dibuka Excel.
    /// </summary>
    private string BuatCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("No;Tanggal Terima;Nama Petugas;Nomor Antrian;Kiriman;Dus;Dari;Ekspedisi;Jenis Media;Lokasi;Nomor Resi");

        for (var i = 0; i < _daftar.Count; i++)
        {
            var p = _daftar[i];
            sb.AppendLine(string.Join(";",
                i + 1,
                Sel(p.WaktuTerima == default ? "" : p.WaktuTerima.ToString("dd/MM/yyyy HH:mm")),
                Sel(p.NamaPetugasPenerima),
                Sel(p.NomorAntrian),
                // Kiriman, nomor dus, dan totalnya jadi kolom terpisah supaya
                // baris-baris satu kiriman bisa dikelompokkan di Excel.
                Sel(p.KirimanId),
                p.NomorDus,
                p.JumlahDus,
                Sel(p.Ekspedisi),
                Sel(p.JenisMedia),
                Sel(p.LokasiNama),
                Sel(p.NomorResi)));
        }

        return sb.ToString();
    }

    private static string Sel(string nilai)
        => nilai.Contains(';') || nilai.Contains('"')
            ? "\"" + nilai.Replace("\"", "\"\"") + "\""
            : nilai;
}
