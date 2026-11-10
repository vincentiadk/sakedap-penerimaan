using Penerimaan.App.Config;
using Penerimaan.App.Models;
using Penerimaan.App.Services;

namespace Penerimaan.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var settings = AppSettings.Load();

        // Token tidak lagi ikut installer. PC baru (atau yang berkas
        // settingnya disalin dari PC lain) diminta mengisinya sekali.
        // --atur-token tetap diterima sebagai nama lama.
        var aturKoneksi = args.Any(a =>
            a.Equals("--atur-koneksi", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--atur-token", StringComparison.OrdinalIgnoreCase));

        bool KoneksiKurang() =>
            string.IsNullOrWhiteSpace(settings.Inlis.Token) || string.IsNullOrWhiteSpace(settings.Inlis.ApiUrl);

        if (aturKoneksi || KoneksiKurang())
        {
            using var form = new KoneksiForm(settings);
            if (form.ShowDialog() != DialogResult.OK && KoneksiKurang())
                return;
        }

        // Repository hidup selama aplikasi berjalan: sesi hasil login dipakai
        // ulang oleh MainForm, jadi jangan dibuat dua kali.
        // BranchIdOverride hanya cadangan selama isloginvalid belum memuat
        // BRANCH_ID. Respons API menang bila sudah ada.
        using var repo = new PenerimaanRepository(
            new InlisClient(settings.Inlis), settings.Lokasi.BranchIdOverride);

        // Lokasi ditentukan dari IP PC ini, bukan dipilih petugas.
        var ip = string.IsNullOrWhiteSpace(settings.Lokasi.IpOverride)
            ? JaringanHelper.IpLokal()
            : settings.Lokasi.IpOverride.Trim();

        using var login = new LoginForm(repo, ip);
        if (login.ShowDialog() != DialogResult.OK || login.Petugas is null)
            return;

        // Pencarian lokasi HARUS setelah login: IP saja tidak cukup untuk
        // menentukan lokasi, karena rentang IP privat yang sama dipakai di
        // banyak satuan kerja. Yang membedakan BRANCH_ID milik petugas, dan
        // itu baru diketahui dari respons login.
        var (lokasi, galat) = CariLokasi(repo, ip, login.Petugas.BranchId);

        Application.Run(new MainForm(settings, repo, login.Petugas, lokasi, ip, galat));
    }

    /// <summary>
    /// Kegagalan pencarian lokasi tidak menghentikan aplikasi: penerimaan paket
    /// harus tetap bisa dicatat walau pemetaan lokasi bermasalah. Yang mati
    /// hanya tab Pindah Lokasi.
    ///
    /// Tapi pesan errornya IKUT dibawa, tidak ditelan. Query yang gagal dan
    /// query yang tidak menemukan baris adalah dua hal berbeda: yang pertama
    /// biasanya kolom atau tabel yang belum ada, yang kedua benar-benar data
    /// yang belum diisi. Menyamakan keduanya jadi "belum dipetakan" membuat
    /// admin memeriksa data yang sebenarnya sudah benar.
    /// </summary>
    private static (Lokasi? Lokasi, string? Galat) CariLokasi(
        PenerimaanRepository repo, string ip, string branchId)
    {
        try
        {
            return (repo.CariLokasiPcAsync(ip, branchId).GetAwaiter().GetResult(), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }
}
