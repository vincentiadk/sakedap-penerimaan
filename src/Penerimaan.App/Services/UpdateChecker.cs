using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Penerimaan.App.Config;

namespace Penerimaan.App.Services;

/// <summary>
/// Pembaruan otomatis dari GitHub Releases.
///
/// Alurnya: tanya rilis terbaru ke api.github.com, bandingkan tag-nya
/// (v1.2.3) dengan versi exe ini, lalu bila lebih baru unduh installer Inno
/// Setup beserta berkas .sha256-nya, cocokkan hash, dan jalankan installer
/// secara senyap. Installer menutup aplikasi, menimpa berkas di Program Files,
/// lalu menjalankannya kembali.
///
/// Yang aman tertimpa: hanya isi folder program. Setting per mesin ada di
/// ProgramData dan antrian tertunda di LocalAppData, jadi tidak tersentuh.
///
/// Repo harus publik: rilis dari repo private butuh token GitHub, dan token
/// itu akan menjadi rahasia baru di setiap PC.
/// </summary>
public sealed class UpdateChecker
{
    private readonly UpdateSettings _settings;

    public UpdateChecker(UpdateSettings settings) => _settings = settings;

    public static Version VersiSekarang
    {
        get
        {
            var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    public sealed record Rilis(Version Versi, string Tag, string Catatan, string UrlInstaller, string UrlSha256, string NamaInstaller);

    /// <summary>
    /// Null bila tidak ada versi lebih baru, pembaruan dimatikan, atau GitHub
    /// tidak terjangkau. Kegagalan sengaja ditelan: tidak ada internet di pos
    /// satpam tidak boleh menghalangi penerimaan paket.
    /// </summary>
    public async Task<Rilis?> CariVersiBaruAsync(CancellationToken ct = default)
    {
        if (!_settings.Aktif || string.IsNullOrWhiteSpace(_settings.GitHubRepo)) return null;

        try
        {
            using var http = BuatHttp(TimeSpan.FromSeconds(_settings.BatasWaktuCekDetik));
            var url = $"https://api.github.com/repos/{_settings.GitHubRepo.Trim()}/releases/latest";
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var versi)) return null;
            versi = new Version(versi.Major, versi.Minor, Math.Max(versi.Build, 0));
            if (versi <= VersiSekarang) return null;

            string? urlExe = null, namaExe = null, urlSha = null;
            foreach (var aset in root.GetProperty("assets").EnumerateArray())
            {
                var nama = aset.GetProperty("name").GetString() ?? "";
                var unduh = aset.GetProperty("browser_download_url").GetString() ?? "";
                if (nama.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && nama.StartsWith(_settings.AwalanNamaInstaller, StringComparison.OrdinalIgnoreCase))
                {
                    urlExe = unduh;
                    namaExe = nama;
                }
            }
            if (urlExe is null || namaExe is null) return null;

            foreach (var aset in root.GetProperty("assets").EnumerateArray())
                if (aset.GetProperty("name").GetString() == namaExe + ".sha256")
                    urlSha = aset.GetProperty("browser_download_url").GetString();

            // Tanpa hash, installer tidak bisa diperiksa keutuhannya - lewati.
            if (urlSha is null) return null;

            var catatan = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
            return new Rilis(versi, tag, catatan, urlExe, urlSha, namaExe);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Unduh installer ke folder sementara dan pastikan SHA256-nya cocok.
    /// Melempar bila gagal; berkas yang tidak cocok dihapus.
    /// </summary>
    public async Task<string> UnduhAsync(Rilis rilis, IProgress<int>? progres = null, CancellationToken ct = default)
    {
        using var http = BuatHttp(TimeSpan.FromMinutes(10));

        var shaTeks = await http.GetStringAsync(rilis.UrlSha256, ct);
        // Format sha256sum: "<hex>  <nama>"; ambil kata pertama saja.
        var hashDiharapkan = shaTeks.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        if (hashDiharapkan.Length != 64)
            throw new InvalidDataException("Berkas .sha256 di rilis tidak valid.");

        var dir = Path.Combine(Path.GetTempPath(), "SAKEDAP-Penerimaan-Update");
        Directory.CreateDirectory(dir);
        var tujuan = Path.Combine(dir, Path.GetFileName(rilis.NamaInstaller));

        using (var resp = await http.GetAsync(rilis.UrlInstaller, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1;

            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(tujuan);
            var buf = new byte[81920];
            long terbaca = 0;
            int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct);
                terbaca += n;
                if (total > 0) progres?.Report((int)(terbaca * 100 / total));
            }
        }

        string hashAktual;
        await using (var fs = File.OpenRead(tujuan))
            hashAktual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));

        if (!hashAktual.Equals(hashDiharapkan, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(tujuan);
            throw new InvalidDataException("Installer yang diunduh tidak cocok dengan hash rilis. Pembaruan dibatalkan.");
        }

        return tujuan;
    }

    /// <summary>
    /// Jalankan installer senyap. UseShellExecute wajib agar Windows bisa
    /// menampilkan permintaan izin admin (UAC) - installer memasang ke
    /// Program Files. Pemanggil harus segera menutup aplikasi sesudahnya.
    /// </summary>
    public static void JalankanInstaller(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
            UseShellExecute = true
        });
    }

    private static HttpClient BuatHttp(TimeSpan timeout)
    {
        var http = new HttpClient { Timeout = timeout };
        // api.github.com menolak permintaan tanpa User-Agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"SAKEDAP-Penerimaan/{VersiSekarang}");
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return http;
    }
}
