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

    public PenerimaanRepository(InlisClient inlis) => _inlis = inlis;

    public Pegawai? PetugasAktif { get; private set; }

    public async Task<Pegawai> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var data = await _inlis.LoginAsync(username, password, ct)
                   ?? throw new ApiException("Username atau password salah.");

        var petugas = new Pegawai
        {
            Id = Ambil(data, "Id") ?? "",
            Nama = Ambil(data, "Name") ?? Ambil(data, "FullName") ?? username,
            Nip = Ambil(data, "Nip") ?? ""
        };

        if (petugas.Id.Length == 0)
            throw new ApiException("Data pegawai tidak ditemukan.");

        PetugasAktif = petugas;
        return petugas;
    }

    /// <summary>
    /// Cari paket dari nomor resi yang sudah diisi penerbit.
    ///
    /// Mengembalikan daftar, bukan satu baris: nomor resi tidak dijamin unik di
    /// tabel letter - penerbit bisa salah ketik, atau satu resi dipakai untuk
    /// beberapa surat penyerahan. Kalau lebih dari satu, petugas yang memilih.
    /// Daftar kosong berarti resi tidak terdaftar dan petugas lanjut manual.
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
    /// Simpan penerimaan dan terbitkan nomor antrian dari tabel letter.
    ///
    /// PERHATIAN - RACE CONDITION: karena aplikasi menulis langsung ke INLIS
    /// tanpa lapisan server, penerbitan nomor di sini adalah SELECT MAX lalu
    /// INSERT terpisah. Dua pos penerimaan yang menyimpan bersamaan bisa
    /// mendapat nomor yang sama. Aman selama hanya ada SATU pos satpam.
    /// Untuk lebih dari satu pos, penerbitan nomor harus dipindah ke endpoint
    /// server yang bisa mengunci baris - lihat docs/API-CONTRACT.md.
    /// </summary>
    public async Task<PaketPenerimaan> SimpanPenerimaanAsync(
        PaketPenerimaan paket, string ip = "", CancellationToken ct = default)
    {
        var periode = paket.WaktuTerima;
        var urutan = await UrutanBerikutnyaAsync(periode, ct);
        var nomorAntrian = NomorAntrian.Format(periode, urutan);

        // Penerimaan disimpan di tabelnya sendiri, bukan sebagai kolom
        // tambahan di letter. Alasannya:
        //   - jalur manual tidak punya letter sama sekali; membuat baris letter
        //     palsu akan mengotori data serah simpan
        //   - satu letter bisa datang sebagai beberapa koli, masing-masing
        //     butuh nomor antrian dan labelnya sendiri
        //   - aturan hapus/karantina 24 jam cukup berlaku di tabel ini
        var payload = new Dictionary<string, object?>
        {
            [Skema.KolNomorAntrian] = nomorAntrian,
            [Skema.KolResi] = paket.NomorResi,
            [Skema.KolEkspedisi] = paket.Cara == CaraDatang.Langsung ? null : paket.Ekspedisi,
            [Skema.KolCaraDatang] = paket.Cara == CaraDatang.Langsung ? "langsung" : "ekspedisi",
            [Skema.KolPetugas] = paket.NamaPetugasPenerima,
            [Skema.KolWaktuTerima] = paket.WaktuTerima.ToString("yyyy-MM-dd HH:mm:ss"),
            [Skema.KolLokasiId] = paket.LokasiId is { Length: > 0 } ? paket.LokasiId : null,
            [Skema.KolSumber] = paket.Sumber == SumberData.Sakedap ? "sakedap" : "manual",
            [Skema.KolStatus] = (int)StatusPaket.DiterimaSatpam,
            [Skema.KolJumlahDus] = paket.JumlahDus,
            [Skema.KolJenisMediaId] = paket.JenisMediaId is { Length: > 0 } ? paket.JenisMediaId : null
        };

        if (paket.LetterId is { Length: > 0 })
        {
            // Jalur SAKEDAP: cukup tunjuk ke letter yang sudah ada. Nama
            // penerbit dan jumlah judul/eks tidak disalin - keduanya turunan
            // dan selalu bisa dibaca ulang lewat letter_id.
            payload[Skema.KolLetterRef] = paket.LetterId;
        }
        else
        {
            // Jalur manual: catatan satpam apa adanya. Semuanya belum
            // terverifikasi sampai paket dibuka Pegawai KCKR.
            payload[Skema.KolPengirimManual] = paket.NamaPelaksanaSerah;
            payload[Skema.KolPerkiraanQty] = paket.JumlahJudul;
            payload[Skema.KolPerkiraanCopy] = paket.JumlahEks;
        }

        var hasil = await _inlis.CreateAsync(Skema.TabelPenerimaan, payload, withTimestamp: true, ct);
        paket.Id = hasil is { } d ? Ambil(d, "Id") ?? "" : "";

        paket.NomorAntrian = nomorAntrian;
        paket.Status = StatusPaket.DiterimaSatpam;

        // Tiap kardus punya barisnya sendiri karena posisinya bisa berbeda.
        await BuatBarisDusAsync(paket, 1, paket.JumlahDus, ct);

        // Penempatan awal ikut dicatat sebagai mutasi, supaya riwayat sebuah
        // paket dimulai dari titik pertama - bukan dari perpindahan kedua.
        // Asal dikosongkan karena sebelum ini paket memang belum ada di mana pun.
        if (paket.LokasiId is { Length: > 0 } lokasiAwal)
            await CatatMutasiAsync(paket.Id, null, lokasiAwal, paket.NamaPetugasPenerima, ip, ct);

        return paket;
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
    /// Penerimaan yang sudah pernah dicatat untuk nomor resi ini.
    ///
    /// Dipakai untuk mencegah satu paket dapat dua nomor antrian karena
    /// ter-scan dua kali. Tidak dibatasi tanggal: paket yang ter-scan ulang
    /// keesokan harinya tetap paket yang sama.
    /// </summary>
    public async Task<List<PaketPenerimaan>> CariAntrianByResiAsync(
        string nomorResi, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nomorResi)) return new List<PaketPenerimaan>();

        var sql = $"""
            SELECT t.{Skema.KolPenerimaanId} AS ID,
                   t.{Skema.KolNomorAntrian} AS NOMOR_ANTRIAN,
                   t.{Skema.KolPetugas} AS PETUGAS,
                   t.{Skema.KolWaktuTerima} AS WAKTU_TERIMA,
                   t.{Skema.KolJumlahDus} AS JUMLAH_DUS
            FROM {Skema.TabelPenerimaan} t
            WHERE t.{Skema.KolDeleteDate} IS NULL
              AND t.{Skema.KolResi} = {InlisClient.Kutip(nomorResi)}
            ORDER BY t.{Skema.KolWaktuTerima} DESC
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        return baris.Select(b => new PaketPenerimaan
        {
            Id = Teks(b, "ID"),
            NomorResi = nomorResi,
            NomorAntrian = Teks(b, "NOMOR_ANTRIAN"),
            NamaPetugasPenerima = Teks(b, "PETUGAS"),
            WaktuTerima = Waktu(b, "WAKTU_TERIMA"),
            JumlahDus = Math.Max(1, Angka(b, "JUMLAH_DUS"))
        }).ToList();
    }

    /// <summary>
    /// Buat baris posisi untuk dus nomor <paramref name="dari"/> sampai
    /// <paramref name="sampai"/>, memakai lokasi penerimaan sebagai posisi awal.
    /// </summary>
    private async Task BuatBarisDusAsync(PaketPenerimaan paket, int dari, int sampai, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(paket.Id)) return;

        for (var nomor = dari; nomor <= sampai; nomor++)
        {
            await _inlis.CreateAsync(Skema.TabelDus, new Dictionary<string, object?>
            {
                [Skema.KolDusAntrianId] = paket.Id,
                [Skema.KolDusNomor] = nomor,
                [Skema.KolDusLokasiId] = paket.LokasiId is { Length: > 0 } ? paket.LokasiId : null
            }, withTimestamp: true, ct);
        }
    }

    /// <summary>Posisi tiap kardus dari sebuah penerimaan, urut nomor dus.</summary>
    public async Task<List<DusPaket>> DaftarDusAsync(string penerimaanId, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT d.{Skema.KolDusPk} AS ID,
                   d.{Skema.KolDusNomor} AS NOMOR_DUS,
                   d.{Skema.KolDusLokasiId} AS LOKASI_ID,
                   lok.{Skema.KolLokasiNama} AS LOKASI_NAMA
            FROM {Skema.TabelDus} d
            LEFT JOIN {Skema.TabelLokasi} lok
                   ON d.{Skema.KolDusLokasiId} = lok.{Skema.KolLokasiPk}
            WHERE d.{Skema.KolDusAntrianId} = {InlisClient.Kutip(penerimaanId)}
            ORDER BY d.{Skema.KolDusNomor}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        return baris.Select(b => new DusPaket
        {
            Id = Teks(b, "ID"),
            PenerimaanId = penerimaanId,
            NomorDus = Angka(b, "NOMOR_DUS"),
            LokasiId = Teks(b, "LOKASI_ID"),
            LokasiNama = Teks(b, "LOKASI_NAMA")
        }).ToList();
    }

    /// <summary>
    /// Lokasi yang terpasang untuk PC dengan IP ini, atau null bila belum
    /// dipetakan di LETTER_ANTRIAN_PC.
    ///
    /// Sekaligus memperbarui LAST_SEEN, supaya admin bisa melihat pemetaan
    /// mana yang sudah basi karena PC diganti atau IP-nya berubah.
    /// </summary>
    public async Task<Lokasi?> CariLokasiPcAsync(string ip, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;

        var sql = $"""
            SELECT p.{Skema.KolPcPk} AS PC_ID,
                   l.{Skema.KolLokasiPk} AS ID,
                   l.{Skema.KolLokasiNama} AS NAMA
            FROM {Skema.TabelPc} p
            JOIN {Skema.TabelLokasi} l
              ON p.{Skema.KolPcLokasiId} = l.{Skema.KolLokasiPk}
            WHERE p.{Skema.KolPcIp} = {InlisClient.Kutip(ip.Trim())}
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
            Nama = Teks(baris, "NAMA")
        };
    }

    /// <summary>
    /// Cari penerimaan dari nomor antrian - isi barcode di label.
    /// Baris yang sudah dihapus tidak ikut.
    /// </summary>
    public async Task<PaketPenerimaan?> CariAntrianAsync(string nomorAntrian, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nomorAntrian)) return null;

        var sql = $"""
            SELECT t.{Skema.KolPenerimaanId} AS ID,
                   t.{Skema.KolNomorAntrian} AS NOMOR_ANTRIAN,
                   t.{Skema.KolResi} AS NOMOR_RESI,
                   t.{Skema.KolJumlahDus} AS JUMLAH_DUS,
                   t.{Skema.KolPetugas} AS PETUGAS,
                   t.{Skema.KolWaktuTerima} AS WAKTU_TERIMA,
                   t.{Skema.KolCreateDate} AS CREATE_DATE,
                   t.{Skema.KolLokasiId} AS LOKASI_ID,
                   lok.{Skema.KolLokasiNama} AS LOKASI_NAMA
            FROM {Skema.TabelPenerimaan} t
            LEFT JOIN {Skema.TabelLokasi} lok
                   ON t.{Skema.KolLokasiId} = lok.{Skema.KolLokasiPk}
            WHERE t.{Skema.KolDeleteDate} IS NULL
              AND UPPER(t.{Skema.KolNomorAntrian}) = {InlisClient.Kutip(nomorAntrian.Trim().ToUpperInvariant())}
            """;

        var b = await _inlis.GetSingleAsync<Dictionary<string, JsonElement>>(sql, ct);
        if (b is null) return null;

        return new PaketPenerimaan
        {
            Id = Teks(b, "ID"),
            NomorAntrian = Teks(b, "NOMOR_ANTRIAN"),
            NomorResi = Teks(b, "NOMOR_RESI"),
            JumlahDus = Math.Max(1, Angka(b, "JUMLAH_DUS")),
            NamaPetugasPenerima = Teks(b, "PETUGAS"),
            WaktuTerima = Waktu(b, "WAKTU_TERIMA"),
            CreateDate = Waktu(b, "CREATE_DATE"),
            LokasiId = Teks(b, "LOKASI_ID"),
            LokasiNama = Teks(b, "LOKASI_NAMA")
        };
    }

    /// <summary>
    /// Pindahkan paket ke lokasi baru.
    ///
    /// TIDAK dibatasi 24 jam seperti ubah dan hapus: kardus bisa saja baru
    /// dipindahkan beberapa hari kemudian, dan itu wajar. Batas 24 jam hanya
    /// untuk mengoreksi salah input data penerimaan.
    /// </summary>
    /// <summary>
    /// Pindahkan satu kardus - atau seluruh kardus bila <paramref name="nomorDus"/>
    /// bernilai 0, yang terjadi saat men-scan label lama tanpa akhiran nomor dus.
    ///
    /// TIDAK dibatasi 24 jam seperti ubah dan hapus: kardus bisa saja baru
    /// dipindahkan beberapa hari kemudian, dan itu wajar.
    /// </summary>
    public async Task<HasilPindah> PindahLokasiAsync(
        PaketPenerimaan paket, int nomorDus, Lokasi tujuan, string petugas, string ip,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paket.Id))
            throw new InvalidOperationException("Penerimaan belum tersimpan.");

        var semuaDus = await DaftarDusAsync(paket.Id, ct);

        var target = nomorDus > 0
            ? semuaDus.Where(d => d.NomorDus == nomorDus).ToList()
            : semuaDus;

        if (target.Count == 0)
            throw new InvalidOperationException($"Dus {nomorDus} tidak terdaftar pada {paket.NomorAntrian}.");

        // Semua kardus sasaran sudah di sini - bukan perpindahan.
        if (target.All(d => d.LokasiId == tujuan.Id))
        {
            return new HasilPindah(paket, nomorDus, target[0].LokasiNama, tujuan,
                semuaDus.Count(d => d.LokasiId == tujuan.Id), semuaDus.Count, true, false);
        }

        var asalNama = target[0].LokasiNama;
        var riwayatLengkap = true;

        foreach (var dus in target)
        {
            if (dus.LokasiId == tujuan.Id) continue;

            await _inlis.UpdateAsync(Skema.TabelDus, dus.Id, new Dictionary<string, object?>
            {
                [Skema.KolDusLokasiId] = tujuan.Id
            }, withTimestamp: true, ct);

            var tercatat = await CatatMutasiAsync(
                paket.Id, dus.LokasiId, tujuan.Id, petugas, ip, ct, dus.NomorDus);

            if (!tercatat) riwayatLengkap = false;

            dus.LokasiId = tujuan.Id;
            dus.LokasiNama = tujuan.Nama;
        }

        await SelaraskanLokasiPenerimaanAsync(paket, semuaDus, ct);

        return new HasilPindah(paket, nomorDus, asalNama, tujuan,
            semuaDus.Count(d => d.LokasiId == tujuan.Id), semuaDus.Count, false, riwayatLengkap);
    }

    /// <summary>
    /// Selaraskan LOKASI_ID di LETTER_ANTRIAN sebagai ringkasan: terisi bila
    /// semua kardus di tempat yang sama, dikosongkan bila tersebar.
    ///
    /// Dengan begitu tab Daftar Antrian tidak perlu menghitung ulang per baris
    /// untuk kasus normal, dan kolom kosong justru jadi penanda bahwa kardusnya
    /// terpisah - itu informasi yang berguna, bukan data hilang.
    /// </summary>
    private async Task SelaraskanLokasiPenerimaanAsync(
        PaketPenerimaan paket, List<DusPaket> semuaDus, CancellationToken ct)
    {
        var lokasiUnik = semuaDus.Select(d => d.LokasiId).Distinct().ToList();
        var ringkasan = lokasiUnik.Count == 1 ? lokasiUnik[0] : "";

        if (ringkasan == paket.LokasiId) return;

        await _inlis.UpdateAsync(Skema.TabelPenerimaan, paket.Id!, new Dictionary<string, object?>
        {
            [Skema.KolLokasiId] = ringkasan is { Length: > 0 } ? ringkasan : null
        }, withTimestamp: true, ct);

        paket.LokasiId = ringkasan;
        paket.LokasiNama = ringkasan.Length == 0
            ? ""
            : semuaDus.First(d => d.LokasiId == ringkasan).LokasiNama;
    }

    /// <summary>
    /// Tulis satu baris riwayat. Mengembalikan false bila gagal.
    ///
    /// Pemanggil sudah terlanjur mengubah data saat sampai ke sini, jadi
    /// kegagalan di sini tidak dilempar sebagai error - yang berubah tetap
    /// berubah, tinggal riwayatnya yang bolong.
    /// </summary>
    private async Task<bool> CatatMutasiAsync(
        string? antrianId, string? asalId, string tujuanId, string petugas, string ip,
        CancellationToken ct, int nomorDus = 0)
    {
        try
        {
            await _inlis.CreateAsync(Skema.TabelMutasi, new Dictionary<string, object?>
            {
                [Skema.KolMutasiAntrianId] = antrianId,
                [Skema.KolMutasiAsal] = asalId is { Length: > 0 } ? asalId : null,
                [Skema.KolMutasiTujuan] = tujuanId,
                [Skema.KolMutasiWaktu] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                [Skema.KolMutasiPetugas] = petugas,
                [Skema.KolMutasiIp] = ip,
                [Skema.KolMutasiNomorDus] = nomorDus > 0 ? nomorDus : null
            }, withTimestamp: false, ct);

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tandai penerimaan sebagai dihapus dengan mengisi DELETE_DATE.
    ///
    /// Bukan DELETE sungguhan: nomor antrian yang sudah tercetak dan tertempel
    /// di kardus harus tetap bisa ditelusuri. Baris yang DELETE_DATE-nya terisi
    /// tidak lagi muncul di daftar maupun pengecekan resi ganda.
    /// </summary>
    public async Task HapusAsync(PaketPenerimaan penerimaan, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(penerimaan.Id))
            throw new InvalidOperationException("Penerimaan belum tersimpan.");

        if (!penerimaan.MasihBisaDiubah)
            throw new InvalidOperationException("Batas 24 jam sudah lewat, data tidak bisa dihapus.");

        await _inlis.UpdateAsync(Skema.TabelPenerimaan, penerimaan.Id, new Dictionary<string, object?>
        {
            [Skema.KolDeleteDate] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        }, withTimestamp: true, ct);
    }

    /// <summary>
    /// Ubah penerimaan yang sudah tersimpan. Nomor antrian dan nomor resi tidak
    /// ikut diubah: keduanya sudah tercetak di label yang menempel di kardus.
    /// </summary>
    public async Task UbahAsync(PaketPenerimaan paket, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paket.Id))
            throw new InvalidOperationException("Penerimaan belum tersimpan.");

        if (!paket.MasihBisaDiubah)
            throw new InvalidOperationException("Batas 24 jam sudah lewat, data tidak bisa diubah.");

        await _inlis.UpdateAsync(Skema.TabelPenerimaan, paket.Id, new Dictionary<string, object?>
        {
            [Skema.KolPetugas] = paket.NamaPetugasPenerima,
            [Skema.KolWaktuTerima] = paket.WaktuTerima.ToString("yyyy-MM-dd HH:mm:ss"),
            [Skema.KolCaraDatang] = paket.Cara == CaraDatang.Langsung ? "langsung" : "ekspedisi",
            [Skema.KolEkspedisi] = paket.Cara == CaraDatang.Langsung ? null : paket.Ekspedisi,
            [Skema.KolJenisMediaId] = paket.JenisMediaId is { Length: > 0 } ? paket.JenisMediaId : null,
            [Skema.KolJumlahDus] = paket.JumlahDus,
            [Skema.KolJenisMediaId] = paket.JenisMediaId is { Length: > 0 } ? paket.JenisMediaId : null
        }, withTimestamp: true, ct);
    }

    /// <summary>
    /// Tambahkan dus susulan ke penerimaan yang sudah ada.
    ///
    /// Nomor antrian tidak berubah - dus susulan adalah bagian dari kiriman
    /// yang sama, jadi penomorannya melanjutkan yang sudah tercetak. Yang
    /// bertambah hanya JUMLAH_DUS, dan label yang dicetak cukup yang baru.
    /// </summary>
    public async Task<int> TambahDusAsync(
        PaketPenerimaan penerimaan, int tambahan, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(penerimaan.Id))
            throw new InvalidOperationException("Penerimaan belum tersimpan, tidak bisa ditambah dus.");

        if (tambahan < 1)
            throw new ArgumentOutOfRangeException(nameof(tambahan), "Jumlah dus tambahan minimal satu.");

        var sebelum = penerimaan.JumlahDus;
        var total = sebelum + tambahan;

        await _inlis.UpdateAsync(Skema.TabelPenerimaan, penerimaan.Id, new Dictionary<string, object?>
        {
            [Skema.KolJumlahDus] = total
        }, withTimestamp: true, ct);

        await BuatBarisDusAsync(penerimaan, sebelum + 1, total, ct);

        penerimaan.JumlahDus = total;
        return total;
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
                       t.{Skema.KolPetugas} AS PETUGAS,
                       t.{Skema.KolEkspedisi} AS EKSPEDISI,
                       t.{Skema.KolCaraDatang} AS CARA_DATANG,
                       t.{Skema.KolJumlahDus} AS JUMLAH_DUS,
                       m.{Skema.KolMediaNama} AS JENIS_MEDIA,
                       t.{Skema.KolJenisMediaId} AS JENIS_MEDIA_ID,
                       t.{Skema.KolLokasiId} AS LOKASI_ID,
                       lok.{Skema.KolLokasiNama} AS LOKASI_NAMA,
                       t.{Skema.KolCaraDatang} AS CARA_DATANG_T,
                       t.{Skema.KolCreateDate} AS CREATE_DATE,
                       t.{Skema.KolWaktuTerima} AS WAKTU_TERIMA
                FROM {Skema.TabelPenerimaan} t
                LEFT JOIN {Skema.TabelCollectionMedia} m
                       ON t.{Skema.KolJenisMediaId} = m.{Skema.KolMediaPk}
                LEFT JOIN {Skema.TabelLokasi} lok
                       ON t.{Skema.KolLokasiId} = lok.{Skema.KolLokasiPk}
                WHERE t.{Skema.KolDeleteDate} IS NULL
                  AND t.{Skema.KolWaktuTerima} >= {TanggalOracle(dari.Date)}
                  AND t.{Skema.KolWaktuTerima} < {TanggalOracle(sampai.Date.AddDays(1))}
                {filterCari}
                ORDER BY t.{Skema.KolWaktuTerima} DESC
            ) WHERE ROWNUM <= {maksBaris}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        return baris.Select(b => new PaketPenerimaan
        {
            Id = Teks(b, "ID"),
            LetterId = Teks(b, "LETTER_ID"),
            NomorResi = Teks(b, "NOMOR_RESI"),
            NomorAntrian = Teks(b, "NOMOR_ANTRIAN"),
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
            Status = StatusPaket.DiterimaSatpam
        }).ToList();
    }

    private static string TanggalOracle(DateTime waktu)
        => $"TO_DATE('{waktu:yyyy-MM-dd HH:mm:ss}', 'YYYY-MM-DD HH24:MI:SS')";

    /// <summary>Urutan terakhir pada bulan berjalan, ditambah satu.</summary>
    private async Task<int> UrutanBerikutnyaAsync(DateTime periode, CancellationToken ct)
    {
        var prefix = $"KCKR_{periode:MMyy}_";

        var sql = $"""
            SELECT {Skema.KolNomorAntrian} AS NOMOR
            FROM {Skema.TabelPenerimaan}
            WHERE {Skema.KolNomorAntrian} LIKE {InlisClient.KutipPola(prefix, depan: false)}
            """;

        var baris = await _inlis.GetAsync<Dictionary<string, JsonElement>>(sql, ct);

        var tertinggi = 0;
        foreach (var b in baris)
        {
            if (NomorAntrian.TryParse(Teks(b, "NOMOR"), out _, out var urut) && urut > tertinggi)
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
///   - tabel INLIS yang sudah ada, hanya DIBACA (letter, letter_detail,
///     jasa_pengiriman, penerbit)
///   - tabel penerimaan paket milik fitur ini, satu-satunya yang DITULIS
///
/// Aplikasi tidak pernah menulis ke letter. Lihat docs/SPEC.md.
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
    // Belum ada di INLIS. DDL-nya ada di docs/SPEC.md.

    public const string TabelPenerimaan = "letter_antrian";

    public const string KolPenerimaanId = "id";

    /// <summary>Referensi ke letter. NULL pada jalur manual.</summary>
    public const string KolLetterRef = "letter_id";

    public const string KolNomorAntrian = "nomor_antrian";
    public const string KolResi = "receipt_no";
    public const string KolEkspedisi = "ekspedisi";

    /// <summary>'ekspedisi' atau 'langsung'.</summary>
    public const string KolCaraDatang = "cara_datang";

    /// <summary>Banyaknya dus; menentukan berapa label dicetak.</summary>
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

    public const string KolPcPk = "id";
    public const string KolPcIp = "ip_address";
    public const string KolPcLokasiId = "lokasi_id";
    public const string KolPcAktif = "aktif";
    public const string KolPcLastSeen = "last_seen";

    public const string KolMutasiAntrianId = "letter_antrian_id";
    public const string KolMutasiAsal = "lokasi_asal_id";
    public const string KolMutasiTujuan = "lokasi_tujuan_id";
    public const string KolMutasiNomorDus = "nomor_dus";
    public const string KolMutasiWaktu = "waktu";
    public const string KolMutasiPetugas = "petugas";
    public const string KolMutasiIp = "ip_pc";

    // ---------- Dus ----------

    public const string TabelDus = "letter_antrian_dus";

    public const string KolDusPk = "id";
    public const string KolDusAntrianId = "letter_antrian_id";
    public const string KolDusNomor = "nomor_dus";
    public const string KolDusLokasiId = "lokasi_id";
    public const string KolSumber = "sumber";
    public const string KolStatus = "status";

    // Hanya terisi pada jalur manual - catatan satpam, belum terverifikasi.
    public const string KolPengirimManual = "pengirim_manual";
    public const string KolPerkiraanQty = "perkiraan_qty";
    public const string KolPerkiraanCopy = "perkiraan_copy";
}

/// <summary>Hasil satu kali scan di tab Pindah Lokasi.</summary>
/// <param name="NomorDus">0 berarti seluruh kardus, dari label lama tanpa akhiran.</param>
/// <param name="SudahDiSini">Tidak ada yang berpindah karena posisinya memang sudah di tujuan.</param>
public sealed record HasilPindah(
    PaketPenerimaan Paket,
    int NomorDus,
    string LokasiAsal,
    Lokasi Tujuan,
    int DusDiTujuan,
    int TotalDus,
    bool SudahDiSini,
    bool RiwayatLengkap);
