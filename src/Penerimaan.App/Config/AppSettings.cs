using System.Text.Json;
using System.Text.Json.Serialization;

namespace Penerimaan.App.Config;

/// <summary>
/// Konfigurasi aplikasi, dibaca dari appsettings.json di samping exe.
/// Sengaja tidak memakai Microsoft.Extensions.Configuration agar project
/// tidak butuh NuGet package sama sekali (build bisa jalan offline).
/// </summary>
public sealed class AppSettings
{
    public InlisSettings Inlis { get; set; } = new();
    public PrinterSettings Printer { get; set; } = new();
    public LokasiSettings Lokasi { get; set; } = new();
    public UpdateSettings Update { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Konfigurasi bersama, ikut terbundel ke installer dan ke repositori.
    /// Hanya dibaca: di Program Files berkas ini tidak bisa ditulis user biasa
    /// dan tertimpa saat update. TIDAK boleh berisi token.
    /// </summary>
    public static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>
    /// Folder setting per mesin. Di luar folder program supaya bisa ditulis
    /// tanpa hak admin dan tidak tersentuh saat aplikasi diperbarui. Installer
    /// membuatnya dengan izin users-modify.
    /// </summary>
    public static string LocalDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "SAKEDAP", "Penerimaan");

    /// <summary>
    /// Berkas per mesin: token INLIS (terenkripsi DPAPI), satuan kerja, IP,
    /// dan printer pilihan operator. Satuan kerja berbeda di tiap pemasangan,
    /// jadi tempatnya di sini - bukan di appsettings.json yang sama untuk semua.
    /// </summary>
    public static string LocalFilePath => Path.Combine(LocalDirectory, "settings.json");

    /// <summary>
    /// Lokasi lama berkas per mesin (di samping exe, token polos). Hanya
    /// dibaca untuk migrasi, lalu dicoba dihapus.
    /// </summary>
    private static string LegacyLocalFilePath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

    public static AppSettings Load()
    {
        var settings = File.Exists(FilePath)
            ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings()
            : new AppSettings();

        // Inlis.Token ber-[JsonIgnore], jadi token di appsettings.json tidak
        // pernah dipakai langsung. Instalasi lama yang masih menyimpannya di
        // sana dimigrasikan ke berkas terenkripsi di bawah.
        var tokenPolosLama = BacaTokenPolos(FilePath);

        var lokal = BacaLokal(LocalFilePath);
        var perluSimpan = false;

        if (File.Exists(LegacyLocalFilePath))
        {
            var lama = BacaLokal(LegacyLocalFilePath);
            tokenPolosLama = BacaTokenPolos(LegacyLocalFilePath) ?? tokenPolosLama;

            // Berkas baru selalu menang; yang lama hanya mengisi yang kosong.
            if (string.IsNullOrWhiteSpace(lokal.Lokasi.BranchIdOverride))
                lokal.Lokasi.BranchIdOverride = lama.Lokasi.BranchIdOverride;
            if (string.IsNullOrWhiteSpace(lokal.Lokasi.IpOverride))
                lokal.Lokasi.IpOverride = lama.Lokasi.IpOverride;

            perluSimpan = true;
        }

        // URL API bukan rahasia: disimpan polos dan boleh berbeda per PC.
        // Bila kosong, pakai URL bawaan dari appsettings.json.
        settings.Inlis.ApiUrlBawaan = settings.Inlis.ApiUrl;
        if (!string.IsNullOrWhiteSpace(lokal.Inlis.ApiUrl))
            settings.Inlis.ApiUrl = lokal.Inlis.ApiUrl.Trim();

        if (!string.IsNullOrWhiteSpace(lokal.Inlis.TokenTerenkripsi))
            settings.Inlis.Token = SecretStore.TryUnprotect(lokal.Inlis.TokenTerenkripsi) ?? "";
        else if (!string.IsNullOrWhiteSpace(tokenPolosLama))
        {
            settings.Inlis.Token = tokenPolosLama.Trim();
            perluSimpan = true;
        }

        if (!string.IsNullOrWhiteSpace(lokal.Lokasi.BranchIdOverride))
            settings.Lokasi.BranchIdOverride = lokal.Lokasi.BranchIdOverride;
        if (!string.IsNullOrWhiteSpace(lokal.Lokasi.IpOverride))
            settings.Lokasi.IpOverride = lokal.Lokasi.IpOverride;
        if (!string.IsNullOrWhiteSpace(lokal.Printer.Name))
            settings.Printer.Name = lokal.Printer.Name;

        if (perluSimpan)
        {
            try
            {
                settings.Save();
                // Jangan biarkan salinan token polos tertinggal. Di Program
                // Files ini biasanya gagal tanpa admin; upgrade/uninstall
                // installer yang membersihkannya.
                File.Delete(LegacyLocalFilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        return settings;
    }

    /// <summary>
    /// Simpan koneksi INLIS ke berkas per mesin. URL disimpan polos, token
    /// terenkripsi. Token null/kosong berarti token yang ada dipertahankan.
    /// </summary>
    public void SimpanKoneksi(string apiUrl, string? token)
    {
        Inlis.ApiUrl = apiUrl.Trim();
        if (!string.IsNullOrWhiteSpace(token))
            Inlis.Token = token.Trim();
        Save();
    }

    /// <summary>
    /// Hanya bagian per mesin yang ditulis, dan selalu ke ProgramData -
    /// appsettings.json di folder program tidak pernah diubah aplikasi.
    /// </summary>
    public void Save()
    {
        var lokal = new LocalSettings();
        // Hanya ditulis bila memang berbeda dari bawaan, supaya perubahan URL
        // bawaan di versi berikutnya tetap sampai ke PC yang tidak menimpanya.
        lokal.Inlis.ApiUrl = Inlis.ApiUrl == Inlis.ApiUrlBawaan ? "" : Inlis.ApiUrl;
        lokal.Inlis.TokenTerenkripsi = string.IsNullOrWhiteSpace(Inlis.Token)
            ? ""
            : SecretStore.Protect(Inlis.Token);
        lokal.Lokasi.BranchIdOverride = Lokasi.BranchIdOverride;
        lokal.Lokasi.IpOverride = Lokasi.IpOverride;
        lokal.Printer.Name = Printer.Name;

        Directory.CreateDirectory(LocalDirectory);

        // Tulis ke berkas sementara dulu: listrik yang padam di tengah
        // penulisan tidak boleh menghapus token yang sudah tersimpan.
        var tmp = LocalFilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(lokal, JsonOptions));
        File.Move(tmp, LocalFilePath, overwrite: true);
    }

    private static LocalSettings BacaLokal(string path)
    {
        if (!File.Exists(path)) return new LocalSettings();
        try
        {
            return JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(path), JsonOptions) ?? new LocalSettings();
        }
        catch (JsonException)
        {
            return new LocalSettings();
        }
    }

    /// <summary>Inlis.Token polos dari berkas format lama, bila ada.</summary>
    private static string? BacaTokenPolos(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var bagian in doc.RootElement.EnumerateObject())
            {
                if (!bagian.Name.Equals("Inlis", StringComparison.OrdinalIgnoreCase)
                    || bagian.Value.ValueKind != JsonValueKind.Object) continue;

                foreach (var p in bagian.Value.EnumerateObject())
                {
                    if (!p.Name.Equals("Token", StringComparison.OrdinalIgnoreCase)
                        || p.Value.ValueKind != JsonValueKind.String) continue;

                    var t = p.Value.GetString();
                    // "isi token INLIS di sini" dari berkas contoh bukan token.
                    return string.IsNullOrWhiteSpace(t) || t.Contains(' ') ? null : t;
                }
            }
        }
        catch (JsonException) { }
        return null;
    }

    /// <summary>Bentuk berkas settings.json di ProgramData.</summary>
    private sealed class LocalSettings
    {
        public LocalInlis Inlis { get; set; } = new();
        public LocalLokasi Lokasi { get; set; } = new();
        public LocalPrinter Printer { get; set; } = new();
    }

    private sealed class LocalInlis
    {
        /// <summary>Penimpa URL API untuk PC ini, polos. Kosong = bawaan.</summary>
        public string ApiUrl { get; set; } = "";


        /// <summary>Token INLIS, DPAPI LocalMachine, base64.</summary>
        public string TokenTerenkripsi { get; set; } = "";
    }

    private sealed class LocalLokasi
    {
        public string IpOverride { get; set; } = "";
        public string BranchIdOverride { get; set; } = "";
    }

    private sealed class LocalPrinter
    {
        public string Name { get; set; } = "";
    }
}

/// <summary>
/// Sambungan langsung ke INLIS (Restful.aspx), meniru App\Helpers\QueryAPI
/// milik SAKEDAP Laravel.
///
/// Token di sini adalah token master INLIS - dia membuka SQL bebas dan
/// add/update/delete ke seluruh tabel. Jangan pernah menaruhnya di source code,
/// appsettings.json, atau repositori. Token diisi lewat layar "Token INLIS"
/// saat pertama dijalankan dan disimpan terenkripsi DPAPI di ProgramData.
/// </summary>
public sealed class InlisSettings
{
    /// <summary>
    /// URL Restful.aspx. Bawaannya dari appsettings.json, bisa ditimpa per PC
    /// lewat layar Koneksi INLIS (disimpan polos - bukan rahasia).
    /// </summary>
    public string ApiUrl { get; set; } = "";

    /// <summary>URL dari appsettings.json, untuk tahu apakah PC ini menimpanya.</summary>
    [JsonIgnore]
    public string ApiUrlBawaan { get; set; } = "";

    /// <summary>Hanya di memori; tidak pernah dibaca/ditulis polos ke JSON.</summary>
    [JsonIgnore]
    public string Token { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Pembaruan otomatis dari GitHub Releases. Lihat Services/UpdateChecker.cs.
/// </summary>
public sealed class UpdateSettings
{
    public bool Aktif { get; set; } = true;

    /// <summary>"pemilik/nama-repo". Repo harus publik.</summary>
    public string GitHubRepo { get; set; } = "";

    /// <summary>Aset rilis yang dipakai: .exe berawalan ini, plus .sha256-nya.</summary>
    public string AwalanNamaInstaller { get; set; } = "SAKEDAP-Penerimaan-Setup-";

    /// <summary>
    /// Pengecekan berjalan sebelum layar login, jadi dibuat singkat: jaringan
    /// yang lambat tidak boleh menahan petugas lebih dari beberapa detik.
    /// </summary>
    public int BatasWaktuCekDetik { get; set; } = 5;
}

public sealed class LokasiSettings
{
    /// <summary>
    /// Timpa IP yang terdeteksi otomatis. Kosongkan untuk memakai hasil
    /// deteksi. Berguna di PC dengan banyak kartu jaringan.
    /// </summary>
    public string IpOverride { get; set; } = "";

    /// <summary>
    /// SEMENTARA - satuan kerja PC ini, dipakai hanya bila respons login INLIS
    /// belum memuat BRANCH_ID. 37 untuk Perpusnas.
    ///
    /// Respons API selalu menang bila memuatnya, jadi begitu isloginvalid
    /// diperbarui, nilai di sini otomatis tidak terpakai lagi dan barisnya
    /// bisa dibuang tanpa mengubah apa pun.
    ///
    /// Sengaja TIDAK diberi nilai bawaan 37. Kalau diberi, PC provinsi yang
    /// dipasang tanpa menyunting berkas ini akan diam-diam menulis sebagai
    /// Perpusnas - datanya masuk ke lingkup instansi lain dan nomor antriannya
    /// berebut dengan deret Perpusnas. Kosong lalu gagal dengan pesan jelas
    /// jauh lebih aman daripada benar untuk satu instansi dan salah diam-diam
    /// untuk yang lain.
    /// </summary>
    public string BranchIdOverride { get; set; } = "";
}

public sealed class PrinterSettings
{
    /// <summary>Nama printer persis seperti di Control Panel > Devices and Printers.
    /// Kosongkan untuk memakai printer default Windows.</summary>
    public string Name { get; set; } = "";

    /// <summary>Resolusi printer Zebra: 203 dpi untuk seri GK/GC/ZD220, 300 dpi untuk seri 300dpi.</summary>
    public int Dpi { get; set; } = 203;

    public double LabelWidthMm { get; set; } = 100;
    public double LabelHeightMm { get; set; } = 50;

    /// <summary>Cetak Code128 dari nomor antrian supaya paket bisa ditemukan
    /// kembali dengan scan, bukan mengetik ulang nomornya.</summary>
    public bool IncludeBarcode { get; set; } = true;

    /// <summary>
    /// Cara mencetak: "auto", "zpl", atau "gdi".
    ///
    /// zpl  - kirim perintah mentah ke printer Zebra, hasil paling tajam
    /// gdi  - gambar labelnya, jalan di printer Windows apa pun termasuk
    ///        Microsoft Print to PDF; berguna untuk memeriksa tata letak
    /// auto - pilih zpl bila nama printer mengandung Zebra/ZDesigner, selain
    ///        itu gdi. Mengirim ZPL ke printer biasa menghasilkan berkas rusak.
    /// </summary>
    public string Mode { get; set; } = "auto";

    public bool PakaiZpl => Mode.ToLowerInvariant() switch
    {
        "zpl" => true,
        "gdi" => false,
        _ => Name.Contains("zebra", StringComparison.OrdinalIgnoreCase)
             || Name.Contains("zdesigner", StringComparison.OrdinalIgnoreCase)
    };

    public int LabelWidthDots => MmKeDots(LabelWidthMm);
    public int LabelHeightDots => MmKeDots(LabelHeightMm);

    private int MmKeDots(double mm) => (int)Math.Round(mm * Dpi / 25.4);
}
