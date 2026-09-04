using Penerimaan.App.Config;
using Penerimaan.App.Models;
using Penerimaan.App.Services;

namespace Penerimaan.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var settings = AppSettings.Load();

        // Repository hidup selama aplikasi berjalan: sesi hasil login dipakai
        // ulang oleh MainForm, jadi jangan dibuat dua kali.
        using var repo = new PenerimaanRepository(new InlisClient(settings.Inlis));

        // Lokasi ditentukan dari IP PC ini, bukan dipilih petugas. Dicari
        // sebelum login supaya sudah bisa ditampilkan di layar masuk.
        var ip = string.IsNullOrWhiteSpace(settings.Lokasi.IpOverride)
            ? JaringanHelper.IpLokal()
            : settings.Lokasi.IpOverride.Trim();

        var lokasi = CariLokasi(repo, ip);

        using var login = new LoginForm(repo, lokasi, ip);
        if (login.ShowDialog() != DialogResult.OK || login.Petugas is null)
            return;

        Application.Run(new MainForm(settings, repo, login.Petugas, lokasi, ip));
    }

    /// <summary>
    /// Kegagalan pencarian lokasi tidak menghentikan aplikasi: penerimaan paket
    /// harus tetap bisa dicatat walau pemetaan lokasi bermasalah. Yang mati
    /// hanya tab Pindah Lokasi.
    /// </summary>
    private static Lokasi? CariLokasi(PenerimaanRepository repo, string ip)
    {
        try
        {
            return repo.CariLokasiPcAsync(ip).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    }
}
