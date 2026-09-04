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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string FilePath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>
    /// Berkas rahasia per mesin. Tidak ikut ke repositori, dan menimpa Token
    /// dari appsettings.json bila ada - supaya token master INLIS tidak pernah
    /// ter-commit.
    /// </summary>
    public static string LocalFilePath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

    public static AppSettings Load()
    {
        var settings = File.Exists(FilePath)
            ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new AppSettings()
            : new AppSettings();

        if (File.Exists(LocalFilePath))
        {
            var lokal = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(LocalFilePath), JsonOptions);

            if (!string.IsNullOrWhiteSpace(lokal?.Inlis.Token))
                settings.Inlis.Token = lokal.Inlis.Token;
        }

        return settings;
    }

    public void Save() =>
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
}

/// <summary>
/// Sambungan langsung ke INLIS (Restful.aspx), meniru App\Helpers\QueryAPI
/// milik SAKEDAP Laravel.
///
/// Token di sini adalah token master INLIS - dia membuka SQL bebas dan
/// add/update/delete ke seluruh tabel. Jangan pernah menaruhnya di source code
/// atau ikut ke repositori; isi lewat appsettings.json di mesin masing-masing.
/// </summary>
public sealed class InlisSettings
{
    public string ApiUrl { get; set; } = "";
    public string Token { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
}

public sealed class LokasiSettings
{
    /// <summary>
    /// Timpa IP yang terdeteksi otomatis. Kosongkan untuk memakai hasil
    /// deteksi. Berguna di PC dengan banyak kartu jaringan.
    /// </summary>
    public string IpOverride { get; set; } = "";
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
