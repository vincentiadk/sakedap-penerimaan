using System.Text.Json;
using System.Text.Json.Serialization;
using Penerimaan.App.Config;

namespace Penerimaan.App.Services;

/// <summary>
/// Port C# dari App\Helpers\QueryAPI milik SAKEDAP (Laravel).
///
/// Semua operasi INLIS adalah POST ke Restful.aspx dengan parameter di query
/// string: token, op, dan argumen per operasi. Respons berbentuk
/// { Status: "Success"|"Failed", Data: ... } - perhatikan huruf besar di awal,
/// berbeda dari amplop huruf kecil milik API Laravel.
///
/// PERINGATAN KEAMANAN: kelas ini memegang token master INLIS. Token itu
/// membuka SQL bebas (op=getlistraw) serta add/update/delete ke seluruh tabel.
/// Karena aplikasi ini terpasang di PC pos satpam, token tersebut ikut
/// terdistribusi. Lihat catatan di docs/SPEC.md.
/// </summary>
public sealed class InlisClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly InlisSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public InlisClient(InlisSettings settings)
    {
        _settings = settings;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds) };
    }

    /// <summary>Verifikasi kredensial pegawai (op=isloginvalid).</summary>
    public async Task<JsonElement?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var hasil = await KirimAsync(new Dictionary<string, string?>
        {
            ["op"] = "isloginvalid",
            ["UserName"] = username,
            ["UserPassword"] = password
        }, ct);

        return hasil?.Data;
    }

    /// <summary>
    /// Jalankan SQL dan kembalikan barisnya (op=getlistraw).
    /// Hasil ada di Data.Items; daftar kosong dikembalikan sebagai list kosong.
    /// </summary>
    public async Task<List<T>> GetAsync<T>(string sql, CancellationToken ct = default)
    {
        var hasil = await KirimAsync(new Dictionary<string, string?>
        {
            ["op"] = "getlistraw",
            ["sql"] = sql
        }, ct);

        if (hasil?.Data is not { } data) return new List<T>();
        if (!data.TryGetProperty("Items", out var items)) return new List<T>();
        if (items.ValueKind != JsonValueKind.Array) return new List<T>();

        return items.Deserialize<List<T>>(JsonOptions) ?? new List<T>();
    }

    /// <summary>Baris pertama saja, atau null bila tidak ada.</summary>
    public async Task<T?> GetSingleAsync<T>(string sql, CancellationToken ct = default) where T : class
        => (await GetAsync<T>(sql, ct)).FirstOrDefault();

    /// <summary>Insert satu baris (op=add). Mengembalikan Data dari INLIS, biasanya berisi id baru.</summary>
    public async Task<JsonElement?> CreateAsync(
        string table, IDictionary<string, object?> payload, bool withTimestamp = true, CancellationToken ct = default)
    {
        var items = KeListItem(payload);

        if (withTimestamp)
        {
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            items.Add(new ListItem("create_date", now));
            items.Add(new ListItem("update_date", now));
        }

        var hasil = await KirimAsync(new Dictionary<string, string?>
        {
            ["op"] = "add",
            ["table"] = table,
            ["issavehistory"] = "1",
            ["ListAddItem"] = JsonSerializer.Serialize(items)
        }, ct);

        return hasil?.Data;
    }

    /// <summary>Update satu baris berdasarkan id (op=update).</summary>
    public async Task<bool> UpdateAsync(
        string table, string id, IDictionary<string, object?> payload, bool withTimestamp = true, CancellationToken ct = default)
    {
        var items = KeListItem(payload);

        if (withTimestamp)
            items.Add(new ListItem("update_date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));

        var hasil = await KirimAsync(new Dictionary<string, string?>
        {
            ["op"] = "update",
            ["table"] = table,
            ["id"] = id,
            ["issavehistory"] = "1",
            ["ListUpdateItem"] = JsonSerializer.Serialize(items)
        }, ct);

        return hasil?.Berhasil == true;
    }

    private static List<ListItem> KeListItem(IDictionary<string, object?> payload)
        => payload.Select(p => new ListItem(p.Key, p.Value?.ToString() ?? "")).ToList();

    private async Task<InlisResponse?> KirimAsync(Dictionary<string, string?> parameters, CancellationToken ct)
    {
        parameters["token"] = _settings.Token;

        var url = _settings.ApiUrl + "?" + string.Join("&", parameters
            .Where(p => p.Value is not null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}"));

        using var response = await _http.PostAsync(url, content: null, ct);
        response.EnsureSuccessStatusCode();

        var hasil = await response.Content.ReadFromJsonAsyncSafe(ct);

        if (hasil is null) return null;

        if (!hasil.Berhasil)
            throw new ApiException(string.IsNullOrWhiteSpace(hasil.Message)
                ? "INLIS menolak permintaan."
                : hasil.Message);

        return hasil;
    }

    /// <summary>
    /// Escape nilai untuk disisipkan ke literal SQL.
    ///
    /// Ini wajib: nomor resi datang dari barcode scanner, yaitu input eksternal
    /// yang tidak terkendali, dan INLIS menerima SQL mentah. Tanpa ini, isi
    /// barcode bisa menjadi perintah SQL.
    /// </summary>
    public static string Kutip(string? nilai)
        => "'" + (nilai ?? "").Replace("'", "''") + "'";

    /// <summary>
    /// Literal pola untuk klausa LIKE, sudah termasuk klausa ESCAPE.
    ///
    /// Selain kutip tunggal, karakter _ dan % ikut di-escape karena keduanya
    /// wildcard di LIKE. Ini penting untuk nomor antrian: tanpa escape, pola
    /// 'KCKR_0826_%' membuat setiap _ mencocokkan karakter apa pun.
    /// </summary>
    public static string KutipPola(string? nilai, bool depan = true, bool belakang = true)
    {
        var aman = (nilai ?? "")
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");

        return Kutip((depan ? "%" : "") + aman + (belakang ? "%" : "")) + " ESCAPE '\\'";
    }

    public void Dispose() => _http.Dispose();

    private sealed record ListItem(
        [property: JsonPropertyName("Name")] string Name,
        [property: JsonPropertyName("Value")] string Value);
}

internal sealed class InlisResponse
{
    [JsonPropertyName("Status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("Message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("Data")]
    public JsonElement? Data { get; set; }

    public bool Berhasil => Status.Equals("Success", StringComparison.OrdinalIgnoreCase);
}

internal static class HttpContentExtensions
{
    /// <summary>
    /// INLIS kadang menjawab dengan Content-Type text/plain walau isinya JSON,
    /// jadi jangan pakai ReadFromJsonAsync yang ketat soal media type.
    /// </summary>
    public static async Task<InlisResponse?> ReadFromJsonAsyncSafe(this HttpContent content, CancellationToken ct)
    {
        var teks = await content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(teks)) return null;

        return JsonSerializer.Deserialize<InlisResponse>(teks, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
