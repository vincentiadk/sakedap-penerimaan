using System.Media;
using Penerimaan.App.Models;

namespace Penerimaan.App;

/// <summary>
/// Tab 3 - Pindah Lokasi.
///
/// Dipakai berulang cepat untuk banyak kardus, jadi tidak ada tombol simpan
/// dan tidak ada dialog: sekali scan langsung berpindah, hasilnya tampil
/// besar, fokus kembali ke kotak scan.
/// </summary>
public partial class MainForm
{
    private int _jumlahPindah;

    private async void txtScanPindah_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;

        e.SuppressKeyPress = true;

        var nomor = txtScanPindah.Text.Trim();
        txtScanPindah.Clear();

        if (nomor.Length == 0) return;

        await PindahkanAsync(nomor);
    }

    private async Task PindahkanAsync(string isiBarcode)
    {
        if (_lokasiPc is null)
        {
            HasilPindahTampil("PC ini belum dipetakan ke lokasi mana pun.", Color.Firebrick);
            return;
        }

        // Barcode label memuat nomor dus: KCKR_0826_1335-2. Label lama tanpa
        // akhiran tetap diterima, dan diperlakukan sebagai seluruh kardus.
        var (nomorAntrian, nomorDus) = NomorAntrian.BacaBarcode(isiBarcode);

        SetBusy(true);
        try
        {
            var paket = await _repo.CariAntrianAsync(nomorAntrian);

            if (paket is null)
            {
                HasilPindahTampil($"{nomorAntrian}{Environment.NewLine}tidak ditemukan", Color.Firebrick);
                return;
            }

            var hasil = await _repo.PindahLokasiAsync(
                paket, nomorDus, _lokasiPc, _petugas.Nama, _ipLokal);

            var labelDus = nomorDus > 0 ? $"DUS {nomorDus}" : "Semua dus";
            var progres = $"{hasil.DusDiTujuan} dari {hasil.TotalDus} dus di {_lokasiPc.Nama}";

            if (hasil.SudahDiSini)
            {
                HasilPindahTampil(
                    $"{paket.NomorAntrian}  |  {labelDus}{Environment.NewLine}"
                    + $"sudah di {_lokasiPc.Nama}{Environment.NewLine}{progres}",
                    Color.DarkGoldenrod);
                return;
            }

            _jumlahPindah++;
            lblJumlahPindah.Text = $"Dipindahkan sesi ini: {_jumlahPindah}";

            var asal = string.IsNullOrWhiteSpace(hasil.LokasiAsal) ? "(belum ada lokasi)" : hasil.LokasiAsal;

            // Warna berbeda saat masih ada kardus tertinggal: petugas perlu
            // langsung tahu tanpa menghitung sendiri.
            var lengkap = hasil.DusDiTujuan == hasil.TotalDus;

            HasilPindahTampil(
                $"{paket.NomorAntrian}  |  {labelDus}{Environment.NewLine}"
                + $"{asal}  →  {_lokasiPc.Nama}{Environment.NewLine}{progres}",
                lengkap ? Color.ForestGreen : Color.DarkOrange);

            if (!hasil.RiwayatLengkap)
                Warn("Lokasi berpindah, tapi riwayatnya gagal dicatat. Periksa tabel LETTER_ANTRIAN_MUTASI.");
        }
        catch (Exception ex)
        {
            HasilPindahTampil($"Gagal: {ex.Message}", Color.Firebrick);
        }
        finally
        {
            SetBusy(false);
            txtScanPindah.Focus();
        }
    }

    /// <summary>
    /// Hasil ditampilkan besar dan berwarna, bukan di status bar: petugas
    /// men-scan sambil berdiri memegang kardus, tidak sedang menatap layar
    /// dari dekat.
    /// </summary>
    private void HasilPindahTampil(string pesan, Color warna)
    {
        lblHasilPindah.ForeColor = warna;
        lblHasilPindah.Text = pesan;

        // Berbunyi untuk apa pun kecuali keberhasilan penuh - petugas men-scan
        // sambil bekerja dan tidak selalu melihat layar.
        if (warna == Color.ForestGreen) return;

        SystemSounds.Beep.Play();
    }

    private void SiapkanTabPindah()
    {
        if (_lokasiPc is null)
        {
            // Memindahkan paket ke lokasi yang tidak diketahui lebih buruk
            // daripada tidak memindahkan sama sekali.
            tabPindah.Enabled = false;
            lblLokasiPindah.Text = "PC ini belum dipetakan ke lokasi. Hubungi admin SAKEDAP.";
            lblLokasiPindah.ForeColor = Color.Firebrick;
            return;
        }

        lblLokasiPindah.Text = $"Paket yang di-scan akan dipindahkan ke: {_lokasiPc.Nama}";
        lblLokasiPindah.ForeColor = SystemColors.ControlText;
    }
}
