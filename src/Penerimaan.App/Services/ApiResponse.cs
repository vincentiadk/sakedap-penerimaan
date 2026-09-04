using System.Text.Json.Serialization;

namespace Penerimaan.App.Services;

/// <summary>
/// Amplop respons standar API SAKEDAP: { status, message, data }.
/// Status berisi "Success" atau "Failed" - perhatikan bahwa endpoint yang ada
/// kadang mengembalikan HTTP 200 dengan status "Failed", jadi jangan hanya
/// mengandalkan kode HTTP.
/// </summary>
public sealed class ApiResponse<T>
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    public bool Berhasil => Status.Equals("Success", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Dilempar saat API menjawab status "Failed".</summary>
public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}
