# Kontrak API - Penerimaan Paket SSKCKR

Untuk tim backend SAKEDAP (`E:\SAKEDAP\api`, Laravel).

Dokumen ini mendefinisikan endpoint yang dibutuhkan aplikasi pos satpam Gedung E.
**Belum ada satu pun dari endpoint ini di API SAKEDAP saat ini.**

## Konteks arsitektur

API SAKEDAP bukan API yang punya database sendiri - dia proxy ke INLIS.
`QueryAPI::get($sql)` mengirim SQL sebagai query parameter lewat HTTP ke INLIS
dan mengembalikan hasilnya. Kolom balikan huruf besar semua (`ID`, `X_API_KEY`),
khas Oracle.

Endpoint yang sudah ada: `sso/login`, `isbn/*`, `penerbit/data`, `koleksi/*`.
Semuanya POST, kredensial di body, respons `{status, message, data}`.

## Yang dipakai aplikasi saat ini: INLIS langsung

Endpoint Laravel di bawah belum ada, jadi untuk sementara aplikasi bicara
langsung ke INLIS `Restful.aspx` - amplopnya berbeda, huruf besar di awal:
`{Status, Message, Data}`.

### `op=isloginvalid`

Verifikasi kredensial pegawai. Yang dibaca aplikasi dari `Data`:

| Field | Dipakai untuk |
|---|---|
| `Id` | Identitas pegawai; kosong berarti login ditolak |
| `Name` / `FullName` | Nama yang ditampilkan di header |
| `Nip` | Keterangan saja |
| `BranchId` | **Satuan kerja.** Menentukan lokasi mana yang terlihat, dan bentuk nomor antrian - lihat docs/SPEC.md |

**`BranchId` belum ada di respons saat ini; perubahannya masih ditunggu.**
Sampai terpasang, aplikasi jatuh ke `Lokasi.BranchIdOverride` di
`appsettings.local.json`. Respons API selalu menang begitu field-nya muncul,
jadi tidak ada konfigurasi yang perlu diubah saat perubahan itu naik.

Nilai `0` diperlakukan sama dengan tidak ada - itu bukan satuan kerja yang sah.

## Konvensi yang diikuti

- Semua endpoint **POST**
- Respons: `{"status": "Success"|"Failed", "message": "...", "data": {...}}`
- Aplikasi klien memeriksa `status` di body, bukan kode HTTP

## Perbedaan yang disengaja dari endpoint yang sudah ada

Endpoint lama mengirim `username` + `password` + `token` di **setiap** request.
Untuk endpoint ini, setelah login **hanya `token` yang dikirim ulang** -
password tidak ditahan di memori aplikasi maupun dikirim berulang kali.

Alasannya: aplikasi ini berjalan di PC pos satpam yang dipakai bergantian,
sehingga password yang mengendap di memori proses adalah risiko yang tidak perlu.
Kalau tim backend ingin tetap seragam dengan endpoint lama, beri tahu - sisi
klien tinggal menyesuaikan.

---

## 1. `POST api/pegawai/login`

Login petugas keamanan. Bukan `loginPenerbit` - petugas keamanan bukan penerbit,
jadi tidak punya baris di tabel `penerbit` maupun `x_api_key`.

**Request**

```json
{ "username": "satpam.gedunge", "password": "..." }
```

**Response sukses**

```json
{
  "status": "Success",
  "message": "",
  "data": {
    "id": "123",
    "nip": "19850101...",
    "nama": "Budi Santoso",
    "token": "<token sesi>"
  }
}
```

`nama` dipakai sebagai Nama Petugas Penerima di setiap penerimaan, jadi harus
nama orang yang bisa dipertanggungjawabkan - bukan nama akun bersama.

**Response gagal**: `status: "Failed"`, `message` diisi alasan.

---

## 2. `POST api/kckr/paket/cari`

Cari paket berdasarkan nomor resi yang sudah diisi penerbit di SAKEDAP.

**Request**

```json
{ "token": "...", "nomor_resi": "DEP0801111" }
```

**Response ketemu**

```json
{
  "status": "Success",
  "data": {
    "id": "...",
    "nomorResi": "DEP0801111",
    "ekspedisi": "J&T Express",
    "namaPelaksanaSerah": "PT Contoh Pustaka",
    "jumlahJudul": 3,
    "jumlahEks": 12
  }
}
```

**Response tidak ketemu**: `status: "Success"` dengan `data: null`.

Penting: resi tidak ketemu adalah **kondisi normal**, bukan error - petugas
lanjut mengisi manual. Jangan kembalikan `Failed` untuk kasus ini, karena
aplikasi akan menampilkannya sebagai kegagalan ke petugas.

---

## 3. `POST api/kckr/penerimaan/simpan`

Simpan penerimaan **dan terbitkan nomor antrian**.

**Request**

```json
{
  "token": "...",
  "nomor_resi": "DEP0801111",
  "ekspedisi": "J&T Express",
  "nama_pelaksana_serah": "PT Contoh Pustaka",
  "jumlah_judul": 3,
  "jumlah_eks": 12,
  "nama_petugas_penerima": "Budi Santoso",
  "waktu_terima": "2026-08-24 13:35:00",
  "lokasi_transit": "",
  "sumber": "sakedap"
}
```

`sumber` bernilai `"sakedap"` (resi ketemu) atau `"manual"` (petugas mengetik).

**Response**

```json
{
  "status": "Success",
  "data": {
    "id": "...",
    "nomorResi": "DEP0801111",
    "nomorAntrian": "KCKR_0826_1335",
    "status": "diterima_satpam"
  }
}
```

### Nomor antrian

Format: `KCKR_[bulan 2 digit][tahun 2 digit]_[urutan]`, contoh `KCKR_0826_1335`
untuk Agustus 2026 urutan 1335.

Diterbitkan server dari tabel `letter`. Klien **tidak pernah** menghasilkan nomor
sendiri - kolom nomor antrian di aplikasi read-only dan baru terisi dari respons
endpoint ini.

Tiga hal yang perlu dipastikan di sisi server:

1. **Penerbitan harus atomik.** Kalau nanti ada lebih dari satu pos penerimaan,
   dua request bersamaan tidak boleh mendapat nomor yang sama. Perlu penguncian
   di level DB, bukan `SELECT MAX()+1` lalu `INSERT` terpisah.

2. **Urutan reset per bulan atau berlanjut?** Contoh di mockup (`1335`) terlihat
   berlanjut, tapi karena bulan sudah ada di prefix, perlu ditegaskan.

3. **Nomor yang dihapus di-skip**, tidak dipakai ulang (sesuai catatan mockup:
   hapus = karantina, bukan hard delete, dan berlaku 24 jam).

---

## 4. Belum didefinisikan: Daftar Penerimaan (Tab 2)

Butuh endpoint list dengan pencarian (nomor antrian / nama petugas / nomor resi),
filter rentang tanggal, paginasi, ekspor Excel, cetak ulang label, dan hapus
(karantina). Akan ditambahkan setelah pertanyaan terbuka di
[SPEC.md](SPEC.md) terjawab.

---

## Catatan keamanan

`QueryAPI::get()` menyusun SQL sebagai string dan mengirimkannya ke INLIS.
Endpoint yang sudah ada menyisipkan nilai langsung ke SQL (`WHERE id = $userId`),
yang masih aman selama nilainya berasal dari hasil login internal.

Endpoint di dokumen ini berbeda: `nomor_resi` berasal dari **barcode scanner** -
input eksternal yang tidak terkendali. Menyisipkannya langsung ke string SQL
membuka SQL injection. Nomor resi harus divalidasi ketat (misal hanya
alfanumerik dan strip, panjang maksimum) atau di-escape sebelum masuk query.

Hal yang sama berlaku untuk `nama_pelaksana_serah` dan `ekspedisi` pada jalur
input manual.
