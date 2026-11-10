using System.Text.Json;
using Penerimaan.App.Models;

namespace Penerimaan.App.Services;

/// <summary>
/// Akses data penerimaan paket langsung ke INLIS, meniru pola controller
/// SAKEDAP: susun SQL, kirim lewat QueryAPI, petakan hasilnya.
///
/// BELUM TERVERIFIKASI: nama tabel dan kolom di <see cref="Skema"/> masih dugaan
/// karena struktur tabel letter belum diketahui. Perbaiki di satu tempat itu
/// saja, tidak perlu menyentuh kode lain.
/// </summary>
public sealed class PenerimaanRepository : IDisposable
{
    private readonly InlisClient _inlis;

    /// <summary>
    /// Satuan kerja cadangan dari appsettings, dipakai hanya bila respons
    /// login belum memuat BRANCH_ID. Lihat LokasiSettings.BranchIdOverride.
    /// </summary>
    private readonly string _branchCadangan;

    public PenerimaanRepository(InlisClient inlis, string branchCadangan = "")
    {
        _inlis = inlis;
        _branchCadangan = (branchCadangan ?? "").Trim();
    }

    public Pegawai? PetugasAktif { get; private set; }

    /// <summary>
    /// Satuan kerja petugas yang sedang login. SELURUH query di kelas ini
    /// dibatasi olehnya - repository ini memang bekerja sebagai user yang
    /// masuk, bukan sebagai pengakses seluruh instansi.
    /// </summary>
    private string Branch => PetugasAktif?.BranchId.Trim() ?? "";

    /// <summary>
    /// Satuan kerja untuk operasi yang tidak boleh berjalan tanpanya.
    ///
    /// Sengaja dilempar sebagai error dan bukan diam-diam dilewati: tanpa
    /// satuan kerja, nomor antrian akan terbit dengan format Perpusnas di
    /// mesin provinsi, dan barisnya tidak akan terlihat oleh siapa pun.
    /// Gagal berisik jauh lebih baik daripada data yang salah diam-diam.
    /// </summary>
    private string BranchWajib()
    {
        if (Branch.Length > 0) return Branch;

        var field = PetugasAktif?.FieldLoginTersedia;

        throw new ApiException(
            "Satuan kerja (branch) tidak terbaca dari respons login, jadi nomor "
            + "antrian tidak bisa diterbitkan."
            + (string.IsNullOrWhiteSpace(field)
                ? ""
                : $" Field yang dikembalikan API login: {field}.")
            + " Selama isloginvalid belum memuat BRANCH_ID, isi "
            + @"Lokasi.BranchIdOverride di C:\ProgramData\SAKEDAP\Penerimaan\settings.json (37 untuk Perpusnas).");
    }

    /// <summary>Potongan WHERE pembatas satuan kerja untuk tabel LETTER_ANTRIAN.</summary>
    private string FilterBranch(string alias = "t")
        => $"AND {alias}.{Skema.KolBranchId} = {InlisClient.Kutip(BranchWajib())}";

    public async Task<Pegawai> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var data = await _inlis.LoginAsync(username, password, ct)
                   ?? throw new ApiException("Username atau password salah.");

        // Respons API SELALU menang. Begitu isloginvalid diperbarui memuat
        // BRANCH_ID, nilai cadangan di appsettings otomatis tidak terpakai
        // lagi tanpa perlu mengubah konfigurasi PC mana pun.
        var dariApi = AmbilBranchId(data);

        var petugas = new Pegawai
        {
            Id = Ambil(data, "Id") ?? "",
            Nama = Ambil(data, "Name") ?? Ambil(data, "FullName") ?? username,
            Nip = Ambil(data, "Nip") ?? "",
            BranchId = dariApi ?? _branchCadangan,
            BranchDariConfig = dariApi is null && _branchCadangan.Length > 0,
            FieldLoginTersedia = NamaField(data)
        };

        if (petugas.Id.Length == 0)
            throw new ApiException("Data pegawai tidak ditemukan.");

        PetugasAktif = petugas;
        return petugas;
    }

    /// <summary>
    /// Satuan kerja dari respons isloginvalid, field "BranchId".
    ///
    /// Mengembalikan null bila field-nya belum ada - itu keadaan yang masih
    /// wajar selama perubahan API belum terpasang, dan pemanggil akan jatuh ke
    /// Lokasi.BranchIdOverride di appsettings.
    ///
    /// Nilai "0" dan string kosong diperlakukan sama dengan tidak ada: keduanya
    /// bukan satuan kerja yang sah, dan menerimanya akan membuat pencarian
    /// lokasi diam-diam tidak menemukan apa pun tanpa penjelasan.
    /// </summary>
    private static string? AmbilBranchId(JsonElement data)
    {
        var nilai = Ambil(data, "BranchId")?.Trim();

        return string.IsNullOrEmpty(nilai) || nilai == "0" ? null : nilai;
    }

    /// <summary>Daftar nama field respons, untuk pesan kesalahan.</summary>
    private static string NamaField(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object) return "";

        return string.Join(", ", data.EnumerateObject().Select(p => p.Name));
    }

    /// <summary>
    /// Cari paket dari nomor resi yang sudah diisi penerbit.
    ///
    /// Mengembalikan daftar, bukan satu baris: nomor resi tidak dijamin unik di
    /// tabel letter - penerbit bisa salah ketik, atau satu resi dipakai untuk
    /// beberapa surat penyerahan. Kalau lebih dari satu, petugas yang memilih.
    /// Daftar kosong berarti resi tidak terdaftar dan petugas lanjut manual.
    ///
    /// Dibatasi satuan kerja, sama seperti pembacaan LETTER_ANTRIAN. Nomor
    /// resi hanya unik di dalam satu ekspedisi, bukan antar satuan kerja:
    /// tanpa saringan ini, satu scan di provinsi bisa mengisi form dengan
    /// surat penyerahan milik Perpusnas hanya karena resinya kebetulan sama.
    /// </summary>
    public async Task<List<PaketPenerimaan>> CariResiAsync(string nomorResi, CancellationToken ct = default)
    {
        // Jumlah judul dan eksemplar tidak disimpan di letter, melainkan
        // dijumlahkan dari barisnya di letter_detail. Ekspedisi diambil dari
        // tabel jasapengiriman lewat jasa_pengiriman_id.
        //
        // LEFT JOIN dipakai supaya paket yang belum punya baris detail tetap
        // ketemu - petugas cukup melengkapi jumlahnya secara manual.
        //
        // InlisClient.Kutip() wajib: isi barcode adalah input eksternal dan
        // INLIS menerima SQL mentah.
        var sql = $"""
            SELECT l.{Skema.KolLetterId} AS ID,
                   p.{Skema.KolPenerbitNama} AS PELAKSANA_SERAH,
                   jp.{Skema.KolJasaPengirimanNama} AS EKSPEDISI,
                   SUM(ld.{Skema.KolDetailQuantity}) AS JUMLAH_JUDUL,
                   SUM(ld.{Skema.KolDetailCopy}) AS JUMLAH_EKS
            FROM {Skema.TabelLetter} l
            LEFT JOIN {Skema.TabelLetterDetail} ld
                   ON l.{Skema.KolLetterId} = ld.{Skema.KolLetterId}
            LEFT JOIN {Skema.TabelJasaPengiriman} jp
                   ON l.{Skema.KolJasaPengirimanId} = jp.{Skema.KolJasaPengirimanPk}
            LEFT JOIN {Skema.TabelPenerbit} p
                   ON l.{Skema.KolPenerbitId} = p.{Skema.KolPenerbitPk}
            WHERE l.{Skema.KolNomorResi} = {InlisClient.Kutip(nomorResi)}
              AND l.{Skema.KolLetterBranchId} = {InlisClient.Kutip(BranchWajib())}
            GROUP BY l.{Skema.KolLetterId},
                     p.{Skema.KolPenerbitNama},
                     jp.{Skema.KolJasaPengirimanNama}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        return baris.Select(b => new PaketPenerimaan
        {
            LetterId = Teks(b, "ID"),
            NomorResi = nomorResi,
            Ekspedisi = Teks(b, "EKSPEDISI"),
            NamaPelaksanaSerah = Teks(b, "PELAKSANA_SERAH"),
            JumlahJudul = Angka(b, "JUMLAH_JUDUL"),
            JumlahEks = Angka(b, "JUMLAH_EKS"),
            Sumber = SumberData.Sakedap
        }).ToList();
    }

    /// <summary>
    /// Simpan kiriman: satu baris per kardus, masing-masing dengan nomor
    /// antriannya sendiri. Kiriman berisi empat kardus menghasilkan empat
    /// baris yang diikat KIRIMAN_ID sama - nomor antrian kardus pertama.
    ///
    /// PERHATIAN - RACE CONDITION: karena aplikasi menulis langsung ke INLIS
    /// tanpa lapisan server, penerbitan nomor di sini adalah SELECT MAX lalu
    /// INSERT terpisah. Dua pos penerimaan yang menyimpan bersamaan bisa
    /// mendapat nomor yang sama. Aman selama hanya ada SATU pos satpam.
    /// Untuk lebih dari satu pos, penerbitan nomor harus dipindah ke endpoint
    /// server yang bisa mengunci baris - lihat docs/API-CONTRACT.md.
    /// </summary>
    public async Task<HasilSimpan> SimpanPenerimaanAsync(
        PaketPenerimaan paket, string ip = "", CancellationToken ct = default)
    {
        var branch = BranchWajib();
        var periode = paket.WaktuTerima;
        var urutan = await UrutanBerikutnyaAsync(periode, ct);
        var total = Math.Max(1, paket.JumlahDus);

        // KIRIMAN_ID adalah nomor antrian kardus pertama, jadi sudah diketahui
        // sebelum satu baris pun ditulis - tidak perlu menyimpan baris pertama
        // dulu hanya untuk membaca id barunya.
        var kirimanId = NomorAntrian.Format(periode, urutan, branch);

        var tersimpan = new List<PaketPenerimaan>(total);

        for (var nomorDus = 1; nomorDus <= total; nomorDus++)
        {
            var kardus = SalinSebagaiKardus(paket, kirimanId, nomorDus, total,
                NomorAntrian.Format(periode, urutan + nomorDus - 1, branch), branch);

            try
            {
                await TulisKardusAsync(kardus, ip, ct);
            }
            catch (Exception ex) when (nomorDus > 1)
            {
                // Kardus sebelumnya sudah terlanjur tersimpan dan nomor
                // antriannya sudah terpakai. Melempar error di sini membuat
                // petugas mengira tidak ada yang masuk, lalu menyimpan ulang -
                // dan kiriman yang sama tercatat dua kali. Jadi yang sudah
                // berhasil tetap dikembalikan, sisanya dilaporkan apa adanya
                // supaya bisa dilengkapi lewat "tambah dus susulan".
                return new HasilSimpan(tersimpan,
                    $"Hanya {tersimpan.Count} dari {total} dus yang tersimpan. "
                    + $"Dus {nomorDus} gagal: {ex.Message}. Cetak label yang sudah ada, "
                    + "lalu tambahkan sisanya lewat scan resi yang sama.");
            }

            tersimpan.Add(kardus);

            // Cukup sekali sekiriman: yang ditandai surat penyerahannya, dan
            // keempat kardus menunjuk letter yang sama.
            if (nomorDus == 1)
                await TandaiLetterTerkirimAsync(kardus, ct);
        }

        return new HasilSimpan(tersimpan, null);
    }

    /// <summary>
    /// Tandai surat penyerahan sebagai TERKIRIM begitu paketnya benar-benar
    /// sampai di pos penerimaan.
    ///
    /// Hanya jalur SAKEDAP: jalur manual tidak punya baris letter untuk
    /// ditandai. Ini SATU-SATUNYA tempat aplikasi menulis ke tabel letter -
    /// selebihnya letter hanya dibaca.
    ///
    /// Kegagalannya tidak dilempar. Kardusnya sudah tersimpan dan nomor
    /// antriannya sudah terpakai, jadi menggagalkan penyimpanan di sini justru
    /// membuat petugas menyimpan ulang kiriman yang sama. Status letter yang
    /// tertinggal masih bisa dikoreksi dari SAKEDAP.
    /// </summary>
    private async Task TandaiLetterTerkirimAsync(PaketPenerimaan kardus, CancellationToken ct)
    {
        if (kardus.Sumber != SumberData.Sakedap) return;
        if (kardus.LetterId is not { Length: > 0 } letterId) return;

        try
        {
            await _inlis.UpdateAsync("letter", letterId, new Dictionary<string, object?>
            {
                ["status"] = "TERKIRIM"
            }, withTimestamp: true, ct);
        }
        catch
        {
            // sengaja diabaikan - lihat alasannya di ringkasan
        }
    }

    /// <summary>
    /// Susun satu kardus dari data kiriman yang diisi petugas.
    ///
    /// Angka perkiraan judul dan eksemplar berlaku SEKIRIMAN, jadi hanya
    /// menempel di kardus pertama. Kalau ikut disalin ke tiap kardus,
    /// penjumlahan atas satu kiriman menghasilkan angka berlipat - dan itu
    /// juga ditolak CK_LETTER_ANTRIAN_QTY.
    /// </summary>
    private static PaketPenerimaan SalinSebagaiKardus(
        PaketPenerimaan asal, string kirimanId, int nomorDus, int total, string nomorAntrian,
        string branch) =>
        new()
        {
            LetterId = asal.LetterId,
            NomorResi = asal.NomorResi,
            Ekspedisi = asal.Ekspedisi,
            Cara = asal.Cara,
            JenisMediaId = asal.JenisMediaId,
            JenisMedia = asal.JenisMedia,
            NamaPelaksanaSerah = asal.NamaPelaksanaSerah,
            JumlahJudul = nomorDus == 1 ? asal.JumlahJudul : 0,
            JumlahEks = nomorDus == 1 ? asal.JumlahEks : 0,
            NamaPetugasPenerima = asal.NamaPetugasPenerima,
            WaktuTerima = asal.WaktuTerima,
            LokasiId = asal.LokasiId,
            LokasiNama = asal.LokasiNama,
            Sumber = asal.Sumber,
            Status = StatusPaket.DiterimaSatpam,
            JumlahDus = total,
            NomorDus = nomorDus,
            KirimanId = kirimanId,
            NomorAntrian = nomorAntrian,
            BranchId = branch
        };

    /// <summary>
    /// Tulis satu kardus ke LETTER_ANTRIAN, lengkap dengan baris mutasi
    /// penempatan awalnya.
    /// </summary>
    private async Task TulisKardusAsync(PaketPenerimaan kardus, string ip, CancellationToken ct)
    {
        // Penerimaan disimpan di tabelnya sendiri, bukan sebagai kolom
        // tambahan di letter. Alasannya:
        //   - jalur manual tidak punya letter sama sekali; membuat baris letter
        //     palsu akan mengotori data serah simpan
        //   - satu letter bisa datang sebagai beberapa koli, masing-masing
        //     butuh nomor antrian dan labelnya sendiri
        //   - aturan hapus/karantina 24 jam cukup berlaku di tabel ini
        var payload = new Dictionary<string, object?>
        {
            [Skema.KolNomorAntrian] = kardus.NomorAntrian,
            [Skema.KolBranchId] = kardus.BranchId,
            [Skema.KolKirimanId] = kardus.KirimanId,
            [Skema.KolNomorDus] = kardus.NomorDus,
            [Skema.KolResi] = kardus.NomorResi,
            [Skema.KolEkspedisi] = kardus.Cara == CaraDatang.Langsung ? null : kardus.Ekspedisi,
            [Skema.KolCaraDatang] = kardus.Cara == CaraDatang.Langsung ? "langsung" : "ekspedisi",
            [Skema.KolPetugas] = kardus.NamaPetugasPenerima,
            [Skema.KolWaktuTerima] = kardus.WaktuTerima.ToString("yyyy-MM-dd HH:mm:ss"),
            [Skema.KolLokasiId] = kardus.LokasiId is { Length: > 0 } ? kardus.LokasiId : null,
            [Skema.KolSumber] = kardus.Sumber == SumberData.Sakedap ? "sakedap" : "manual",
            [Skema.KolStatus] = StatusPaketDb.KeTeks(StatusPaket.DiterimaSatpam),
            [Skema.KolJumlahDus] = kardus.JumlahDus,
            [Skema.KolJenisMediaId] = kardus.JenisMediaId is { Length: > 0 } ? kardus.JenisMediaId : null
        };

        if (kardus.LetterId is { Length: > 0 })
        {
            // Jalur SAKEDAP: cukup tunjuk ke letter yang sudah ada. Nama
            // penerbit dan jumlah judul/eks tidak disalin - keduanya turunan
            // dan selalu bisa dibaca ulang lewat letter_id.
            payload[Skema.KolLetterRef] = kardus.LetterId;
        }
        else if (kardus.NomorDus == 1)
        {
            // Jalur manual: catatan satpam apa adanya, dan hanya di kardus
            // pertama. Semuanya belum terverifikasi sampai paket dibuka
            // Pegawai KCKR.
            payload[Skema.KolPengirimManual] = kardus.NamaPelaksanaSerah;
            payload[Skema.KolPerkiraanQty] = kardus.JumlahJudul;
            payload[Skema.KolPerkiraanCopy] = kardus.JumlahEks;
        }

        var hasil = await _inlis.CreateAsync(Skema.TabelPenerimaan, payload, withTimestamp: true, ct);
        kardus.Id = hasil is { } d ? Ambil(d, "Id") ?? "" : "";

        // Penempatan awal ikut dicatat sebagai mutasi, supaya riwayat sebuah
        // kardus dimulai dari titik pertama - bukan dari perpindahan kedua.
        // Asal dikosongkan karena sebelum ini kardus memang belum di mana pun.
        if (kardus.LokasiId is { Length: > 0 } lokasiAwal)
            await CatatMutasiAsync(kardus.Id, null, lokasiAwal, kardus.NamaPetugasPenerima, ip, ct);
    }

    /// <summary>
    /// Nama-nama jasa pengiriman untuk dropdown ekspedisi, dari tabel referensi
    /// JASA_PENGIRIMAN.
    ///
    /// Yang disimpan tetap teks, bukan jasa_pengiriman_id: yang dicatat satpam
    /// adalah pengamatan siapa yang mengantar, dan itu bisa saja ekspedisi yang
    /// belum terdaftar. Daftar ini hanya untuk mengurangi salah ketik.
    /// </summary>
    public async Task<List<string>> DaftarEkspedisiAsync(CancellationToken ct = default)
    {
        var sql = $"""
            SELECT jp.{Skema.KolJasaPengirimanNama} AS NAMA
            FROM {Skema.TabelJasaPengiriman} jp
            WHERE jp.{Skema.KolJasaPengirimanNama} IS NOT NULL
            ORDER BY jp.{Skema.KolJasaPengirimanNama}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        return baris.Select(b => Teks(b, "NAMA"))
                    .Where(n => n.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
    }

    /// <summary>
    /// Daftar jenis media untuk dropdown, dari tabel referensi collectionmedia.
    ///
    /// Dimuat sekali saat aplikasi dibuka: isinya tabel referensi yang pendek
    /// dan jarang berubah, jadi tidak perlu ditanyakan ulang tiap transaksi.
    /// </summary>
    public async Task<List<JenisMedia>> DaftarMediaAsync(CancellationToken ct = default)
    {
        // Hanya media yang worksheet-nya berkategori KC (karya cetak) dan KRA
        // (karya rekam) - itu yang masuk lingkup serah simpan. Kategori lain
        // tidak pernah datang sebagai paket ke pos satpam.
        //
        // DISTINCT dipakai karena satu media bisa terhubung ke lebih dari satu
        // worksheet; tanpa itu namanya muncul berulang di dropdown.
        var kategori = string.Join(", ", Skema.KategoriWorksheet.Select(InlisClient.Kutip));

        var sql = $"""
            SELECT DISTINCT m.{Skema.KolMediaPk} AS ID,
                   m.{Skema.KolMediaNama} AS NAMA
            FROM {Skema.TabelCollectionMedia} m
            JOIN {Skema.TabelWorksheet} w
              ON m.{Skema.KolMediaWorksheetId} = w.{Skema.KolWorksheetPk}
            WHERE UPPER(w.{Skema.KolWorksheetKategori}) IN ({kategori})
            ORDER BY m.{Skema.KolMediaNama}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        return baris.Select(b => new JenisMedia
        {
            Id = Teks(b, "ID"),
            Nama = Teks(b, "NAMA")
        }).ToList();
    }

    /// <summary>
    /// Kiriman yang sudah pernah dicatat untuk nomor resi ini, satu wakil per
    /// kiriman - kardus pertamanya.
    ///
    /// Dipakai untuk mencegah satu paket dicatat dua kali karena ter-scan
    /// ulang. Tidak dibatasi tanggal: paket yang ter-scan ulang keesokan
    /// harinya tetap paket yang sama.
    ///
    /// Dikelompokkan per KIRIMAN_ID, bukan per baris: satu resi memang SAH
    /// punya banyak baris sekarang - satu untuk tiap kardus. Menampilkan
    /// semuanya membuat petugas mengira sudah ter-scan berkali-kali padahal
    /// itu kardus-kardus dari kiriman yang sama.
    /// </summary>
    public async Task<List<PaketPenerimaan>> CariAntrianByResiAsync(
        string nomorResi, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nomorResi)) return new List<PaketPenerimaan>();

        var hasil = await DaftarKardusSqlAsync(
            $"AND t.{Skema.KolResi} = {InlisClient.Kutip(nomorResi)}",
            $"ORDER BY t.{Skema.KolWaktuTerima} DESC, t.{Skema.KolNomorDus}", ct);

        return hasil
            .GroupBy(k => k.KirimanId)
            .Select(g => g.OrderBy(k => k.NomorDus).First())
            .ToList();
    }

    /// <summary>
    /// Banyaknya kardus yang benar-benar tersimpan untuk sebuah kiriman.
    ///
    /// Bisa lebih kecil dari JUMLAH_DUS bila penyimpanan gagal di tengah -
    /// dipakai layar "resi sudah tercatat" untuk menawarkan melengkapi
    /// kekurangannya, bukan mencatat kiriman baru.
    /// </summary>
    public async Task<int> JumlahKardusTersimpanAsync(string kirimanId, CancellationToken ct = default)
        => (await DaftarKardusAsync(kirimanId, ct)).Count;

    /// <summary>
    /// Semua kardus dari satu kiriman, urut nomor dus.
    ///
    /// Dipakai untuk melaporkan kemajuan pemindahan ("2 dari 4 dus sudah di
    /// sini") dan untuk mengubah atau menghapus kiriman secara utuh.
    /// </summary>
    public async Task<List<PaketPenerimaan>> DaftarKardusAsync(
        string kirimanId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kirimanId)) return new List<PaketPenerimaan>();

        return await DaftarKardusSqlAsync(
            $"AND t.{Skema.KolKirimanId} = {InlisClient.Kutip(kirimanId)}",
            $"ORDER BY t.{Skema.KolNomorDus}", ct);
    }

    /// <summary>
    /// Lokasi yang terpasang untuk PC dengan IP ini DI SATUAN KERJA petugas
    /// yang sedang login, atau null bila belum dipetakan di
    /// LETTER_ANTRIAN_PC.
    ///
    /// BRANCH_ID wajib ikut disaring, bukan sekadar penyaring tambahan: IP
    /// jaringan lokal hampir selalu memakai rentang privat yang sama, jadi
    /// 192.168.1.10 bisa saja terdaftar di Perpusnas maupun di beberapa
    /// provinsi. Tanpa saringan ini, petugas provinsi bisa memindahkan paket
    /// ke gudang Perpusnas hanya karena IP PC-nya kebetulan sama.
    ///
    /// Sekaligus memperbarui LAST_SEEN, supaya admin bisa melihat pemetaan
    /// mana yang sudah basi karena PC diganti atau IP-nya berubah.
    /// </summary>
    public async Task<Lokasi?> CariLokasiPcAsync(
        string ip, string branchId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;

        // Tanpa satuan kerja, tidak ada jawaban yang bisa dipercaya. Menebak
        // dengan mengabaikan BRANCH_ID justru berbahaya - lebih baik lokasi
        // dianggap tidak diketahui dan tab Pindah Lokasi mati.
        if (string.IsNullOrWhiteSpace(branchId)) return null;

        var sql = $"""
            SELECT p.{Skema.KolPcPk} AS PC_ID,
                   l.{Skema.KolLokasiPk} AS ID,
                   l.{Skema.KolLokasiNama} AS NAMA,
                   l.{Skema.KolLokasiBranchId} AS BRANCH_ID
            FROM {Skema.TabelPc} p
            JOIN {Skema.TabelLokasi} l
              ON p.{Skema.KolPcLokasiId} = l.{Skema.KolLokasiPk}
            WHERE p.{Skema.KolPcIp} = {InlisClient.Kutip(ip.Trim())}
              AND p.{Skema.KolPcBranchId} = {InlisClient.Kutip(branchId.Trim())}
              AND p.{Skema.KolPcAktif} = 1
              AND l.{Skema.KolLokasiAktif} = 1
            """;

        var baris = await _inlis.GetSingleAsync<Dictionary<string, JsonElement>>(sql, ct);
        if (baris is null) return null;

        // Kegagalan mencatat LAST_SEEN tidak boleh menghalangi pekerjaan -
        // itu hanya keterangan untuk admin.
        try
        {
            await _inlis.UpdateAsync(Skema.TabelPc, Teks(baris, "PC_ID"), new Dictionary<string, object?>
            {
                [Skema.KolPcLastSeen] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }, withTimestamp: true, ct);
        }
        catch
        {
            // sengaja diabaikan
        }

        return new Lokasi
        {
            Id = Teks(baris, "ID"),
            Nama = Teks(baris, "NAMA"),
            BranchId = Teks(baris, "BRANCH_ID")
        };
    }

    /// <summary>
    /// Cari satu kardus dari nomor antrian - isi barcode di label.
    /// Baris yang sudah dihapus tidak ikut.
    /// </summary>
    public async Task<PaketPenerimaan?> CariAntrianAsync(string nomorAntrian, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nomorAntrian)) return null;

        var nomor = InlisClient.Kutip(nomorAntrian.Trim().ToUpperInvariant());
        var hasil = await DaftarKardusSqlAsync($"AND UPPER(t.{Skema.KolNomorAntrian}) = {nomor}", "", ct);

        return hasil.FirstOrDefault();
    }

    /// <summary>
    /// Pembacaan baku satu kardus dari LETTER_ANTRIAN. Dipakai pencarian per
    /// nomor antrian maupun pengambilan seluruh kardus sekiriman, supaya
    /// daftar kolom dan pemetaannya tidak ditulis dua kali dan tidak bisa
    /// berbeda diam-diam.
    ///
    /// Batas satuan kerja dipasang DI SINI, bukan diserahkan ke pemanggil -
    /// supaya tidak ada jalur baca yang bisa lupa memasangnya dan diam-diam
    /// menampilkan data instansi lain.
    /// </summary>
    private async Task<List<PaketPenerimaan>> DaftarKardusSqlAsync(
        string filter, string urutan, CancellationToken ct)
    {
        var sql = $"""
            SELECT t.{Skema.KolPenerimaanId} AS ID,
                   t.{Skema.KolNomorAntrian} AS NOMOR_ANTRIAN,
                   t.{Skema.KolBranchId} AS BRANCH_ID,
                   t.{Skema.KolKirimanId} AS KIRIMAN_ID,
                   t.{Skema.KolNomorDus} AS NOMOR_DUS,
                   t.{Skema.KolResi} AS NOMOR_RESI,
                   t.{Skema.KolJumlahDus} AS JUMLAH_DUS,
                   t.{Skema.KolPetugas} AS PETUGAS,
                   t.{Skema.KolWaktuTerima} AS WAKTU_TERIMA,
                   t.{Skema.KolCreateDate} AS CREATE_DATE,
                   t.{Skema.KolStatus} AS STATUS,
                   t.{Skema.KolLokasiId} AS LOKASI_ID,
                   lok.{Skema.KolLokasiNama} AS LOKASI_NAMA
            FROM {Skema.TabelPenerimaan} t
            LEFT JOIN {Skema.TabelLokasi} lok
                   ON t.{Skema.KolLokasiId} = lok.{Skema.KolLokasiPk}
            WHERE t.{Skema.KolDeleteDate} IS NULL
              {FilterBranch()}
              {filter}
            {urutan}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        return baris.Select(b => new PaketPenerimaan
        {
            Id = Teks(b, "ID"),
            NomorAntrian = Teks(b, "NOMOR_ANTRIAN"),
            BranchId = Teks(b, "BRANCH_ID"),
            KirimanId = Teks(b, "KIRIMAN_ID"),
            NomorDus = Math.Max(1, Angka(b, "NOMOR_DUS")),
            NomorResi = Teks(b, "NOMOR_RESI"),
            JumlahDus = Math.Max(1, Angka(b, "JUMLAH_DUS")),
            NamaPetugasPenerima = Teks(b, "PETUGAS"),
            WaktuTerima = Waktu(b, "WAKTU_TERIMA"),
            CreateDate = Waktu(b, "CREATE_DATE"),
            Status = StatusPaketDb.Dari(Teks(b, "STATUS")),
            LokasiId = Teks(b, "LOKASI_ID"),
            LokasiNama = Teks(b, "LOKASI_NAMA")
        }).ToList();
    }

    /// <summary>
    /// Pindahkan satu kardus ke lokasi baru.
    ///
    /// Yang di-scan adalah nomor antrian milik kardus itu sendiri, jadi tidak
    /// ada lagi pertanyaan "kardus yang mana" - cukup satu UPDATE pada barisnya.
    /// Kardus lain sekiriman hanya dibaca untuk melaporkan kemajuan.
    ///
    /// TIDAK dibatasi 24 jam seperti ubah dan hapus: kardus bisa saja baru
    /// dipindahkan beberapa hari kemudian, dan itu wajar. Batas 24 jam hanya
    /// untuk mengoreksi salah input data penerimaan.
    /// </summary>
    public async Task<HasilPindah> PindahLokasiAsync(
        PaketPenerimaan kardus, Lokasi tujuan, string petugas, string ip,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kardus.Id))
            throw new InvalidOperationException("Penerimaan belum tersimpan.");

        var sekiriman = await DaftarKardusAsync(kardus.KirimanId, ct);
        var asalNama = kardus.LokasiNama;

        // Sudah di sini - bukan perpindahan, dan tidak perlu baris riwayat.
        if (kardus.LokasiId == tujuan.Id)
        {
            return new HasilPindah(kardus, asalNama, tujuan,
                DiTujuan(sekiriman, tujuan), sekiriman.Count, true, true);
        }

        await _inlis.UpdateAsync(Skema.TabelPenerimaan, kardus.Id, new Dictionary<string, object?>
        {
            [Skema.KolLokasiId] = tujuan.Id
        }, withTimestamp: true, ct);

        var riwayatLengkap = await CatatMutasiAsync(
            kardus.Id, kardus.LokasiId, tujuan.Id, petugas, ip, ct);

        kardus.LokasiId = tujuan.Id;
        kardus.LokasiNama = tujuan.Nama;

        // Daftar sekiriman dibaca sebelum perpindahan, jadi kardus ini masih
        // tercatat di lokasi lamanya di sana. Diperbarui supaya hitungannya
        // benar tanpa perlu menembak INLIS sekali lagi.
        foreach (var lain in sekiriman.Where(k => k.Id == kardus.Id))
        {
            lain.LokasiId = tujuan.Id;
            lain.LokasiNama = tujuan.Nama;
        }

        return new HasilPindah(kardus, asalNama, tujuan,
            DiTujuan(sekiriman, tujuan), sekiriman.Count, false, riwayatLengkap);
    }

    private static int DiTujuan(List<PaketPenerimaan> sekiriman, Lokasi tujuan)
        => sekiriman.Count(k => k.LokasiId == tujuan.Id);

    /// <summary>
    /// Tulis satu baris riwayat. Mengembalikan false bila gagal.
    ///
    /// Pemanggil sudah terlanjur mengubah data saat sampai ke sini, jadi
    /// kegagalan di sini tidak dilempar sebagai error - yang berubah tetap
    /// berubah, tinggal riwayatnya yang bolong.
    /// </summary>
    private async Task<bool> CatatMutasiAsync(
        string? antrianId, string? asalId, string tujuanId, string petugas, string ip,
        CancellationToken ct)
    {
        try
        {
            // Tidak ada nomor dus di sini: satu baris LETTER_ANTRIAN sudah
            // berarti satu kardus, jadi antrianId sendiri sudah menunjuk
            // kardus yang mana.
            await _inlis.CreateAsync(Skema.TabelMutasi, new Dictionary<string, object?>
            {
                [Skema.KolMutasiAntrianId] = antrianId,
                [Skema.KolMutasiAsal] = asalId is { Length: > 0 } ? asalId : null,
                [Skema.KolMutasiTujuan] = tujuanId,
                [Skema.KolMutasiWaktu] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                [Skema.KolMutasiPetugas] = petugas,
                [Skema.KolMutasiIp] = ip
            }, withTimestamp: false, ct);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tandai SELURUH kiriman sebagai dihapus dengan mengisi DELETE_DATE.
    ///
    /// Menghapus satu kardus saja akan menyisakan kiriman timpang - kardus
    /// tersisa mengaku "dus 2 dari 4" padahal tiga saudaranya sudah hilang
    /// dari daftar. Petugas menghapus karena salah catat kirimannya, bukan
    /// salah catat satu kardusnya.
    ///
    /// Bukan DELETE sungguhan: nomor antrian yang sudah tercetak dan tertempel
    /// di kardus harus tetap bisa ditelusuri. Baris yang DELETE_DATE-nya terisi
    /// tidak lagi muncul di daftar maupun pengecekan resi ganda.
    /// </summary>
    public async Task<int> HapusAsync(PaketPenerimaan kardus, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(kardus.Id))
            throw new InvalidOperationException("Penerimaan belum tersimpan.");

        if (!kardus.MasihBisaDiubah)
            throw new InvalidOperationException("Batas 24 jam sudah lewat, data tidak bisa dihapus.");

        var waktu = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        return await UntukSekirimanAsync(kardus, k =>
            new Dictionary<string, object?> { [Skema.KolDeleteDate] = waktu }, ct);
    }

    /// <summary>
    /// Ubah SELURUH kiriman. Nomor antrian dan nomor resi tidak ikut diubah:
    /// keduanya sudah tercetak di label yang menempel di kardus.
    ///
    /// Data seperti nama petugas dan waktu terima berulang di tiap baris
    /// kardus - itu harga dari rancangan satu-baris-per-kardus. Koreksinya
    /// karena itu harus menyentuh semuanya, kalau tidak kiriman yang sama
    /// akan menyebutkan dua nama petugas berbeda.
    /// </summary>
    public async Task<int> UbahAsync(PaketPenerimaan paket, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paket.Id))
            throw new InvalidOperationException("Penerimaan belum tersimpan.");

        if (!paket.MasihBisaDiubah)
            throw new InvalidOperationException("Batas 24 jam sudah lewat, data tidak bisa diubah.");

        return await UntukSekirimanAsync(paket, _ => new Dictionary<string, object?>
        {
            [Skema.KolPetugas] = paket.NamaPetugasPenerima,
            [Skema.KolWaktuTerima] = paket.WaktuTerima.ToString("yyyy-MM-dd HH:mm:ss"),
            [Skema.KolCaraDatang] = paket.Cara == CaraDatang.Langsung ? "langsung" : "ekspedisi",
            [Skema.KolEkspedisi] = paket.Cara == CaraDatang.Langsung ? null : paket.Ekspedisi,
            [Skema.KolJenisMediaId] = paket.JenisMediaId is { Length: > 0 } ? paket.JenisMediaId : null
        }, ct);
    }

    /// <summary>
    /// Terapkan satu perubahan ke seluruh kardus sekiriman, termasuk kardus
    /// yang jadi acuan. Mengembalikan banyaknya baris yang tersentuh.
    ///
    /// INLIS tidak punya transaksi lintas panggilan, jadi kegagalan di tengah
    /// menyisakan kiriman setengah berubah. Yang bisa dilakukan hanya melempar
    /// error dengan menyebut berapa yang sudah terlanjur - lebih jujur
    /// daripada melaporkan gagal total atas perubahan yang sebagian sudah jadi.
    /// </summary>
    private async Task<int> UntukSekirimanAsync(
        PaketPenerimaan acuan,
        Func<PaketPenerimaan, Dictionary<string, object?>> ubahan,
        CancellationToken ct)
    {
        var sekiriman = await DaftarKardusAsync(acuan.KirimanId, ct);

        // Kiriman lama dari sebelum migrasi bisa saja belum punya KIRIMAN_ID.
        // Yang penting barisnya sendiri tetap terurus.
        if (sekiriman.Count == 0) sekiriman = new List<PaketPenerimaan> { acuan };

        var selesai = 0;

        foreach (var k in sekiriman)
        {
            if (string.IsNullOrWhiteSpace(k.Id)) continue;

            try
            {
                await _inlis.UpdateAsync(Skema.TabelPenerimaan, k.Id, ubahan(k), withTimestamp: true, ct);
                selesai++;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Baru {selesai} dari {sekiriman.Count} dus yang terproses, "
                    + $"lalu {k.NomorAntrian} gagal: {ex.Message}", ex);
            }
        }

        return selesai;
    }

    /// <summary>
    /// Tambahkan dus susulan ke kiriman yang sudah ada.
    ///
    /// Tiap dus susulan mendapat nomor antrian BARU - bukan melanjutkan nomor
    /// yang sudah tercetak, karena di rancangan ini nomor antrian memang milik
    /// kardus. Nomornya tidak bersambung dengan kardus pertama bila ada kiriman
    /// lain masuk di antaranya, dan itu wajar: yang mengikat mereka KIRIMAN_ID,
    /// bukan urutan nomor.
    ///
    /// JUMLAH_DUS ikut naik di SELURUH kardus, supaya label yang dicetak ulang
    /// menyebut "DARI N" yang sama.
    /// </summary>
    public async Task<List<PaketPenerimaan>> TambahDusAsync(
        PaketPenerimaan acuan, int tambahan, string ip = "", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(acuan.Id))
            throw new InvalidOperationException("Penerimaan belum tersimpan, tidak bisa ditambah dus.");

        if (tambahan < 1)
            throw new ArgumentOutOfRangeException(nameof(tambahan), "Jumlah dus tambahan minimal satu.");

        var sekiriman = await DaftarKardusAsync(acuan.KirimanId, ct);
        if (sekiriman.Count == 0) sekiriman = new List<PaketPenerimaan> { acuan };

        var sebelum = sekiriman.Max(k => k.NomorDus);
        var total = sebelum + tambahan;

        // JUMLAH_DUS dinaikkan lebih dulu: CK_LETTER_ANTRIAN_NODUS menolak
        // NOMOR_DUS yang melebihi JUMLAH_DUS, jadi kardus baru tidak akan
        // diterima selama saudara-saudaranya masih menyebut angka lama.
        await UntukSekirimanAsync(acuan,
            _ => new Dictionary<string, object?> { [Skema.KolJumlahDus] = total }, ct);

        var branch = BranchWajib();
        var periode = acuan.WaktuTerima;
        var urutan = await UrutanBerikutnyaAsync(periode, ct);
        var baru = new List<PaketPenerimaan>(tambahan);

        for (var nomorDus = sebelum + 1; nomorDus <= total; nomorDus++)
        {
            var kardus = SalinSebagaiKardus(acuan, acuan.KirimanId, nomorDus, total,
                NomorAntrian.Format(periode, urutan + baru.Count, branch), branch);

            await TulisKardusAsync(kardus, ip, ct);
            baru.Add(kardus);
        }

        foreach (var k in sekiriman) k.JumlahDus = total;
        acuan.JumlahDus = total;

        return baru;
    }

    /// <summary>
    /// Daftar penerimaan yang sudah punya nomor antrian, untuk tab Daftar Antrian.
    ///
    /// Dibatasi <paramref name="maksBaris"/> baris supaya pos satpam tidak
    /// menarik ribuan baris sekaligus lewat sambungan INLIS. Pakai filter
    /// tanggal untuk mempersempit, bukan menaikkan batas ini.
    /// </summary>
    public async Task<List<PaketPenerimaan>> DaftarAsync(
        DateTime dari, DateTime sampai, string? cari = null, int maksBaris = 500, CancellationToken ct = default)
    {
        var filterCari = "";

        if (!string.IsNullOrWhiteSpace(cari))
        {
            // Cari di nomor antrian, nama petugas, atau nomor resi - sesuai mockup.
            var pola = InlisClient.KutipPola(cari.Trim().ToUpperInvariant());
            filterCari = $"""
                  AND ( UPPER(t.{Skema.KolNomorAntrian}) LIKE {pola}
                     OR UPPER(t.{Skema.KolPetugas}) LIKE {pola}
                     OR UPPER(t.{Skema.KolResi}) LIKE {pola} )
                """;
        }

        // ROWNUM dipakai, bukan OFFSET/FETCH, supaya tetap jalan di Oracle lama.
        var sql = $"""
            SELECT * FROM (
                SELECT t.{Skema.KolPenerimaanId} AS ID,
                       t.{Skema.KolLetterRef} AS LETTER_ID,
                       t.{Skema.KolResi} AS NOMOR_RESI,
                       t.{Skema.KolNomorAntrian} AS NOMOR_ANTRIAN,
                       t.{Skema.KolBranchId} AS BRANCH_ID,
                       t.{Skema.KolKirimanId} AS KIRIMAN_ID,
                       t.{Skema.KolNomorDus} AS NOMOR_DUS,
                       t.{Skema.KolPetugas} AS PETUGAS,
                       t.{Skema.KolEkspedisi} AS EKSPEDISI,
                       t.{Skema.KolCaraDatang} AS CARA_DATANG,
                       t.{Skema.KolJumlahDus} AS JUMLAH_DUS,
                       m.{Skema.KolMediaNama} AS JENIS_MEDIA,
                       t.{Skema.KolJenisMediaId} AS JENIS_MEDIA_ID,
                       t.{Skema.KolLokasiId} AS LOKASI_ID,
                       lok.{Skema.KolLokasiNama} AS LOKASI_NAMA,
                       t.{Skema.KolCaraDatang} AS CARA_DATANG_T,
                       t.{Skema.KolStatus} AS STATUS,
                       t.{Skema.KolCreateDate} AS CREATE_DATE,
                       t.{Skema.KolWaktuTerima} AS WAKTU_TERIMA
                FROM {Skema.TabelPenerimaan} t
                LEFT JOIN {Skema.TabelCollectionMedia} m
                       ON t.{Skema.KolJenisMediaId} = m.{Skema.KolMediaPk}
                LEFT JOIN {Skema.TabelLokasi} lok
                       ON t.{Skema.KolLokasiId} = lok.{Skema.KolLokasiPk}
                WHERE t.{Skema.KolDeleteDate} IS NULL
                  {FilterBranch()}
                  AND t.{Skema.KolWaktuTerima} >= {TanggalOracle(dari.Date)}
                  AND t.{Skema.KolWaktuTerima} < {TanggalOracle(sampai.Date.AddDays(1))}
                {filterCari}
                ORDER BY t.{Skema.KolWaktuTerima} DESC, t.{Skema.KolNomorDus}
            ) WHERE ROWNUM <= {maksBaris}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        return baris.Select(b => new PaketPenerimaan
        {
            Id = Teks(b, "ID"),
            LetterId = Teks(b, "LETTER_ID"),
            NomorResi = Teks(b, "NOMOR_RESI"),
            NomorAntrian = Teks(b, "NOMOR_ANTRIAN"),
            BranchId = Teks(b, "BRANCH_ID"),
            KirimanId = Teks(b, "KIRIMAN_ID"),
            NomorDus = Math.Max(1, Angka(b, "NOMOR_DUS")),
            NamaPetugasPenerima = Teks(b, "PETUGAS"),
            Ekspedisi = Teks(b, "EKSPEDISI"),
            Cara = Teks(b, "CARA_DATANG") == "langsung" ? CaraDatang.Langsung : CaraDatang.Ekspedisi,
            JumlahDus = Math.Max(1, Angka(b, "JUMLAH_DUS")),
            JenisMedia = Teks(b, "JENIS_MEDIA"),
            JenisMediaId = Teks(b, "JENIS_MEDIA_ID"),
            LokasiId = Teks(b, "LOKASI_ID"),
            LokasiNama = Teks(b, "LOKASI_NAMA"),
            CreateDate = Waktu(b, "CREATE_DATE"),
            WaktuTerima = Waktu(b, "WAKTU_TERIMA"),
            Status = StatusPaketDb.Dari(Teks(b, "STATUS"))
        }).ToList();
    }

    private static string TanggalOracle(DateTime waktu)
        => $"TO_DATE('{waktu:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')";

    /// <summary>Urutan terakhir pada bulan berjalan, ditambah satu.</summary>
    private async Task<int> UrutanBerikutnyaAsync(DateTime periode, CancellationToken ct)
    {
        var branch = BranchWajib();

        // Awalan sudah membedakan satuan kerja - KCKR_0926_ untuk Perpusnas,
        // KCKR_11_0926_ untuk provinsi 11 - jadi tiap satuan kerja punya deret
        // nomornya sendiri dan tidak pernah berebut angka yang sama.
        //
        // KutipPola meng-escape garis bawah, yang penting di sini: tanpa itu
        // "_" adalah wildcard LIKE dan awalan Perpusnas bisa ikut menangkap
        // nomor provinsi.
        var prefix = NomorAntrian.Awalan(periode, branch);

        var sql = $"""
            SELECT {Skema.KolNomorAntrian} AS NOMOR
            FROM {Skema.TabelPenerimaan}
            WHERE {Skema.KolBranchId} = {InlisClient.Kutip(branch)}
              AND {Skema.KolNomorAntrian} LIKE {InlisClient.KutipPola(prefix, depan: false)}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        var tertinggi = 0;
        foreach (var b in baris)
        {
            if (NomorAntrian.TryParse(Teks(b, "NOMOR"), out _, out var urut, out _) && urut > tertinggi)
                tertinggi = urut;
        }

        return tertinggi + 1;
    }

    private static string? Ambil(JsonElement data, string nama)
    {
        if (data.ValueKind != JsonValueKind.Object) return null;

        foreach (var prop in data.EnumerateObject())
        {
            if (!prop.Name.Equals(nama, StringComparison.OrdinalIgnoreCase)) continue;

            return prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString()
                : prop.Value.ToString();
        }

        return null;
    }

    /// <summary>INLIS mengembalikan nama kolom huruf besar, tapi jangan diandalkan.</summary>
    private static string Teks(Dictionary<string, JsonElement> baris, string kolom)
    {
        foreach (var (key, value) in baris)
        {
            if (!key.Equals(kolom, StringComparison.OrdinalIgnoreCase)) continue;

            return value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : value.ToString();
        }

        return "";
    }

    private static int Angka(Dictionary<string, JsonElement> baris, string kolom)
        => int.TryParse(Teks(baris, kolom), out var n) ? n : 0;

    /// <summary>INLIS mengirim tanggal sebagai teks; formatnya bisa berbeda antar endpoint.</summary>
    private static DateTime Waktu(Dictionary<string, JsonElement> baris, string kolom)
        => DateTime.TryParse(Teks(baris, kolom), out var t) ? t : default;

    public void Dispose() => _inlis.Dispose();
}

/// <summary>
/// Nama tabel dan kolom INLIS.
///
/// Dua kelompok:
///   - tabel INLIS yang sudah ada, hampir seluruhnya hanya DIBACA (letter,
///     letter_detail, jasa_pengiriman, penerbit)
///   - tabel penerimaan paket milik fitur ini, yang bebas ditulis
///
/// Satu-satunya tulisan ke tabel INLIS yang sudah ada adalah LETTER.STATUS,
/// diisi "TERKIRIM" saat paketnya sampai - lihat TandaiLetterTerkirimAsync
/// dan docs/SPEC.md.
/// </summary>
internal static class Skema
{
    // ---------- Dibaca saja ----------

    public const string TabelLetter = "letter";
    public const string TabelLetterDetail = "letter_detail";
    public const string TabelPenerbit = "penerbit";

    /// <summary>Sesuai DATA MODEL INLIS SAKEDAP: JASA_PENGIRIMAN, pakai garis bawah.</summary>
    public const string TabelJasaPengiriman = "jasa_pengiriman";

    public const string KolLetterId = "letter_id";
    public const string KolNomorResi = "receipt_no";

    /// <summary>
    /// Satuan kerja pemilik surat penyerahan. Namanya sama dengan kolom
    /// BRANCH_ID di tabel-tabel fitur ini, tapi sengaja dipisah konstantanya:
    /// LETTER milik INLIS dan bisa berubah tanpa sepengetahuan fitur ini.
    /// </summary>
    public const string KolLetterBranchId = "branch_id";
    public const string KolJasaPengirimanId = "jasa_pengiriman_id";
    public const string KolPenerbitId = "penerbit_id";

    // penerbit.id dan penerbit.name terkonfirmasi dari SsoController
    // di API SAKEDAP (SELECT id, name, ... FROM penerbit).
    public const string KolPenerbitPk = "id";
    public const string KolPenerbitNama = "name";

    /// <summary>Jumlah judul = SUM(letter_detail.quantity).</summary>
    public const string KolDetailQuantity = "quantity";

    /// <summary>Jumlah eksemplar = SUM(letter_detail.copy).</summary>
    public const string KolDetailCopy = "copy";

    /// <summary>Primary key tabel jasa_pengiriman. MASIH DUGAAN.</summary>
    public const string KolJasaPengirimanPk = "id";

    /// <summary>Kolom nama ekspedisi di jasa_pengiriman. MASIH DUGAAN.</summary>
    public const string KolJasaPengirimanNama = "name";

    public const string TabelCollectionMedia = "collectionmedias";

    /// <summary>Primary key tabel collectionmedia. MASIH DUGAAN.</summary>
    public const string KolMediaPk = "id";

    /// <summary>Kolom nama jenis media di collectionmedia. MASIH DUGAAN.</summary>
    public const string KolMediaNama = "name";

    /// <summary>Penghubung collectionmedia ke worksheet. MASIH DUGAAN.</summary>
    public const string KolMediaWorksheetId = "worksheet_id";

    public const string TabelWorksheet = "worksheets";

    /// <summary>Primary key tabel worksheet. MASIH DUGAAN.</summary>
    public const string KolWorksheetPk = "id";

    public const string KolWorksheetKategori = "category";

    /// <summary>KC = karya cetak, KRA = karya rekam. Hanya keduanya yang
    /// masuk lingkup serah simpan fisik ke pos satpam.</summary>
    public static readonly string[] KategoriWorksheet = { "KC", "KRA" };

    // ---------- Tabel baru milik fitur ini ----------
    // Belum ada di INLIS. DDL-nya ada di docs/ddl/sakedap_penerimaan.sql.

    public const string TabelPenerimaan = "letter_antrian";

    public const string KolPenerimaanId = "id";

    /// <summary>Referensi ke letter. NULL pada jalur manual.</summary>
    public const string KolLetterRef = "letter_id";

    public const string KolNomorAntrian = "nomor_antrian";

    /// <summary>
    /// Pengikat kardus sekiriman: nomor antrian kardus pertama. Satu baris
    /// tabel ini adalah satu KARDUS, bukan satu kiriman.
    /// </summary>
    /// <summary>Satuan kerja pemilik baris. 37 = Perpusnas.</summary>
    public const string KolBranchId = "branch_id";

    public const string KolKirimanId = "kiriman_id";

    /// <summary>Kardus ke berapa dalam kirimannya, 1 sampai jumlah_dus.</summary>
    public const string KolNomorDus = "nomor_dus";

    public const string KolResi = "receipt_no";
    public const string KolEkspedisi = "ekspedisi";

    /// <summary>'ekspedisi' atau 'langsung'.</summary>
    public const string KolCaraDatang = "cara_datang";

    /// <summary>Banyaknya kardus dalam kiriman; sama di seluruh barisnya.</summary>
    public const string KolJumlahDus = "jumlah_dus";

    /// <summary>Referensi ke collectionmedia.</summary>
    public const string KolJenisMediaId = "jenis_media_id";

    /// <summary>Saat baris dibuat. Dasar batas ubah/hapus 24 jam.</summary>
    public const string KolCreateDate = "create_date";

    /// <summary>Penanda hapus. Terisi berarti baris dianggap dihapus.</summary>
    public const string KolDeleteDate = "delete_date";
    public const string KolPetugas = "nama_petugas";
    public const string KolWaktuTerima = "waktu_terima";
    /// <summary>Posisi terakhir paket, mengacu ke LETTER_ANTRIAN_LOKASI.</summary>
    public const string KolLokasiId = "lokasi_id";

    // ---------- Lokasi ----------

    public const string TabelLokasi = "letter_antrian_lokasi";
    public const string TabelPc = "letter_antrian_pc";
    public const string TabelMutasi = "letter_antrian_mutasi";

    public const string KolLokasiPk = "id";
    public const string KolLokasiNama = "nama";
    public const string KolLokasiAktif = "aktif";

    /// <summary>Satuan kerja pemilik lokasi. 37 = Perpusnas.</summary>
    public const string KolLokasiBranchId = "branch_id";

    public const string KolPcPk = "id";
    public const string KolPcIp = "ip_address";
    public const string KolPcLokasiId = "lokasi_id";

    /// <summary>Selalu sama dengan branch_id lokasinya - dijaga FK gabungan.</summary>
    public const string KolPcBranchId = "branch_id";
    public const string KolPcAktif = "aktif";
    public const string KolPcLastSeen = "last_seen";

    // Tidak ada nomor_dus di sini: letter_antrian_id sudah menunjuk satu
    // kardus, karena satu barisnya memang satu kardus.
    public const string KolMutasiAntrianId = "letter_antrian_id";
    public const string KolMutasiAsal = "lokasi_asal_id";
    public const string KolMutasiTujuan = "lokasi_tujuan_id";
    public const string KolMutasiWaktu = "waktu";
    public const string KolMutasiPetugas = "petugas";
    public const string KolMutasiIp = "ip_pc";

    public const string KolSumber = "sumber";
    public const string KolStatus = "status";

    // Hanya terisi pada jalur manual DAN hanya di kardus pertama - angkanya
    // berlaku sekiriman. Lihat CK_LETTER_ANTRIAN_QTY.
    public const string KolPengirimManual = "pengirim_manual";
    public const string KolPerkiraanQty = "perkiraan_qty";
    public const string KolPerkiraanCopy = "perkiraan_copy";
}

/// <summary>
/// Hasil satu kali scan di tab Pindah Lokasi. Selalu tentang satu kardus,
/// karena yang di-scan adalah nomor antrian milik kardus itu.
/// </summary>
/// <param name="DusDiTujuan">Berapa kardus sekiriman yang kini sudah di tujuan.</param>
/// <param name="SudahDiSini">Tidak ada yang berpindah karena posisinya memang sudah di tujuan.</param>
public sealed record HasilPindah(
    PaketPenerimaan Kardus,
    string LokasiAsal,
    Lokasi Tujuan,
    int DusDiTujuan,
    int TotalDus,
    bool SudahDiSini,
    bool RiwayatLengkap);

/// <summary>
/// Hasil menyimpan satu kiriman.
/// </summary>
/// <param name="Kardus">Kardus yang benar-benar tersimpan, urut nomor dus.</param>
/// <param name="Peringatan">
/// Terisi bila hanya sebagian kardus tersimpan. Bukan error: yang sudah masuk
/// tetap sah dan labelnya tetap harus dicetak.
/// </param>
public sealed record HasilSimpan(List<PaketPenerimaan> Kardus, string? Peringatan)
{
    public PaketPenerimaan Pertama => Kardus[0];
    public bool Lengkap => Peringatan is null;
}
