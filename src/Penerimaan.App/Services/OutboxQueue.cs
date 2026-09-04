using System.Text.Json;
using Penerimaan.App.Models;

namespace Penerimaan.App.Services;

/// <summary>
/// Buffer penerimaan yang belum berhasil dikirim ke SAKEDAP.
///
/// Catatan penting: nomor antrian diterbitkan server dari tabel letter, jadi
/// paket yang masuk antrian ini BELUM punya nomor antrian dan labelnya belum
/// bisa dicetak. Gunanya murni supaya hasil input petugas tidak hilang saat
/// koneksi putus - termasuk kalau PC sempat restart. Setelah tersinkron,
/// petugas mencetak labelnya dari daftar penerimaan.
///
/// Disimpan sebagai satu file JSON di %LOCALAPPDATA%. Cukup untuk ratusan
/// baris; kalau nanti bisa menumpuk ribuan, ganti ke SQLite.
/// </summary>
public sealed class OutboxQueue
{
    private readonly string _path;
    private readonly List<PaketPenerimaan> _items;
    private readonly object _gate = new();

    public OutboxQueue()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SAKEDAP", "Penerimaan");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "outbox.json");

        _items = File.Exists(_path)
            ? JsonSerializer.Deserialize<List<PaketPenerimaan>>(File.ReadAllText(_path)) ?? new()
            : new();
    }

    public int Count
    {
        get { lock (_gate) return _items.Count; }
    }

    public void Enqueue(PaketPenerimaan paket)
    {
        lock (_gate)
        {
            _items.Add(paket);
            Persist();
        }
    }

    /// <summary>
    /// Coba kirim ulang semua antrian. Berhenti pada kegagalan pertama supaya
    /// urutan penerimaan - dan karenanya urutan nomor antrian - tetap terjaga.
    /// Mengembalikan paket yang berhasil tersimpan beserta nomor antriannya.
    /// </summary>
    public async Task<List<PaketPenerimaan>> FlushAsync(
        PenerimaanRepository api, string ip = "", CancellationToken ct = default)
    {
        var terkirim = new List<PaketPenerimaan>();

        while (true)
        {
            PaketPenerimaan paket;
            lock (_gate)
            {
                if (_items.Count == 0) break;
                paket = _items[0];
            }

            PaketPenerimaan tersimpan;
            try
            {
                tersimpan = await api.SimpanPenerimaanAsync(paket, ip, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                break; // koneksi masih bermasalah, coba lagi nanti
            }

            lock (_gate)
            {
                _items.Remove(paket);
                Persist();
            }
            terkirim.Add(tersimpan);
        }

        return terkirim;
    }

    /// <summary>Tulis ke file sementara lalu replace, agar file tidak korup kalau PC mati saat menulis.</summary>
    private void Persist()
    {
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_items));
        File.Move(temp, _path, overwrite: true);
    }
}
