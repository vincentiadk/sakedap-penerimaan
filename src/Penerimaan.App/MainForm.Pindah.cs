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

        // Isi barcode adalah nomor antrian kardus itu sendiri. Akhiran "-n"
        // dari label versi lama dibuang oleh BacaBarcode.
        var nomorAntrian = NomorAntrian.BacaBarcode(isiBarcode);

        SetBusy(true);
        try
        {
            var kardus = await _repo.CariAntrianAsync(nomorAntrian);

            if (kardus is null)
            {
                HasilPindahTampil($"{nomorAntrian}{Environment.NewLine}tidak ditemukan", Color.Firebrick);
                return;
            }

            var hasil = await _repo.PindahLokasiAsync(kardus, _lokasiPc, _petugas.Nama, _ipLokal);

            var labelDus = $"DUS {kardus.NomorDus} DARI {hasil.TotalDus}";
            var progres = $"{hasil.DusDiTujuan} dari {hasil.TotalDus} dus di {_lokasiPc.Nama}";

            if (hasil.SudahDiSini)
            {
                HasilPindahTampil(
                    $"{kardus.NomorAntrian}  |  {labelDus}{Environment.NewLine}"
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
                $"{kardus.NomorAntrian}  |  {labelDus}{Environment.NewLine}"
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
            lblLokasiPindah.Text = AlasanLokasiKosong();
            lblLokasiPindah.ForeColor = Color.Firebrick;
            return;
        }

        lblLokasiPindah.Text = $"Paket yang di-scan akan dipindahkan ke: {_lokasiPc.Nama}";
        lblLokasiPindah.ForeColor = SystemColors.ControlText;
    }

    /// <summary>
    /// Dua sebab lokasi tidak ketemu, dan penanganannya berbeda - jadi
    /// pesannya juga harus berbeda. "Hubungi admin" saja membuat admin
    /// menebak-nebak, padahal aplikasi sudah tahu persis mana yang kurang.
    /// </summary>
    private string AlasanLokasiKosong()
    {
        // Query-nya GAGAL, bukan tidak menemukan baris. Sebabnya biasanya
        // kolom atau tabel yang belum ada - misalnya BRANCH_ID belum
        // ditambahkan ke LETTER_ANTRIAN_PC. Menyebutnya "belum dipetakan"
        // akan membuat admin memeriksa data yang sebenarnya sudah benar.
        if (!string.IsNullOrWhiteSpace(_galatLokasi))
        {
            return "Pencarian lokasi GAGAL - ini bukan berarti pemetaannya belum ada."
                 + $"{Environment.NewLine}{_galatLokasi}"
                 + $"{Environment.NewLine}Periksa apakah kolom BRANCH_ID sudah ada di "
                 + "LETTER_ANTRIAN_PC dan LETTER_ANTRIAN_LOKASI.";
        }

        // Satuan kerja tidak terbaca dari respons login. Ini masalah nama
        // field, bukan masalah data - jadi field yang tersedia ikut disebut
        // supaya bisa langsung dicocokkan tanpa membongkar respons API.
        if (string.IsNullOrWhiteSpace(_petugas.BranchId))
        {
            var field = string.IsNullOrWhiteSpace(_petugas.FieldLoginTersedia)
                ? "(respons login kosong)"
                : _petugas.FieldLoginTersedia;

            return "Satuan kerja (branch) tidak terbaca dari respons login, "
                 + "sehingga lokasi tidak bisa ditentukan."
                 + $"{Environment.NewLine}Field yang dikembalikan API: {field}"
                 + $"{Environment.NewLine}Sementara ini, isi Lokasi.BranchIdOverride "
                 + "di appsettings.json (37 untuk Perpusnas).";
        }

        // Satuan kerja terbaca, tapi IP ini belum terdaftar untuknya. Ini
        // masalah data yang bisa langsung diperbaiki admin.
        var ip = string.IsNullOrWhiteSpace(_ipLokal) ? "(tidak terdeteksi)" : _ipLokal;

        return $"IP {ip} belum dipetakan ke lokasi mana pun pada satuan kerja "
             + $"{_petugas.BranchId}.{Environment.NewLine}"
             + "Minta admin SAKEDAP menambahkannya di LETTER_ANTRIAN_PC.";
    }
}
