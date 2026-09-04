# Spesifikasi - Penerimaan Paket SSKCKR (Pos Satpam Gedung E)

Diturunkan dari [mockup-penerimaan-paket-kckr.pdf](mockup-penerimaan-paket-kckr.pdf)
(Kelompok Pusat Data Deposit, 30 halaman).

## Ruang lingkup aplikasi ini

Aplikasi di pos satpam Gedung E. Menulis data penerimaan ke SAKEDAP lewat API,
dan mencetak label nomor antrian ke printer label.

Yang **bukan** ruang lingkup aplikasi ini (ada di SAKEDAP web yang sudah ada):

- Daftar Antrian Paket di submenu Pengiriman Fisik
- Kolom "No. Antrian Paket" di Verifikasi Pengiriman & Daftar Dalam Pengiriman
- Notifikasi ke Pegawai KCKR
- Dashboard monitoring & audit trail

## Peralatan di pos satpam

1 set komputer, 1 barcode scanner, 1 printer label, kertas label.

## Aktor

| Aktor | Akses |
|---|---|
| Petugas Keamanan Gedung E | Penerimaan Paket: cari resi, catat penerimaan, dapat nomor antrian, cetak label |
| Pegawai Penerimaan Paket KCKR | Daftar Penerimaan: lihat data, pantau status/posisi, temu kembali paket |

## Dua tab

### Tab 1 - Penerimaan Paket

Satu kolom scan di atas: "Scan barcode untuk mencari nomor resi" + tombol CARI.

**Jalur A - resi ditemukan di SAKEDAP** (penerbit sudah mengisi resi):
data muncul otomatis - Ekspedisi, Resi, Nama Pelaksana Serah, Jumlah Judul,
Jumlah Eks. Petugas mengisi Nama Petugas Penerima + Tanggal/Waktu Terima.
Nomor antrian muncul otomatis dari sistem.

**Jalur B - resi tidak ditemukan**: form manual - Ekspedisi (dropdown, ada opsi
"lainnya"), Resi (+ tombol "Generate resi"), Jumlah Judul, Jumlah Eks,
Tanggal/Waktu Terima (default hari ini), Nama Petugas Penerima. Nomor antrian
otomatis.

SIMPAN -> halaman konfirmasi "Berhasil disimpan!" berisi ringkasan data +
tombol CETAK ANTRIAN.

### Tab 2 - Daftar Penerimaan

Tabel: No, Tanggal Terima, Nama Petugas, Nomor Antrian, Ekspedisi, Nomor Resi, Aksi.
Pencarian berdasarkan nomor antrian / nama petugas / nomor resi. Filter rentang
tanggal. Unduh Data ke Excel. Aksi per baris: Cetak | Hapus. Paginasi.

Catatan dari mockup:
- Data bisa dihapus dalam 24 jam
- Hapus = karantina (bukan hard delete)
- Nomor antrian yang dihapus di-skip, lanjut ke nomor berikutnya

## Label

Isi label (dari slide 13 & 15) hanya teks:

```
Nomor Resi: DEP0801111
KCKR_08_1335
```

Nomor antrian dicetak besar. **Tidak ada barcode/QR di desain.** Lihat Pertanyaan Terbuka.

## Data yang dicatat

- **Paket**: nomor resi, nomor antrian, nama penerbit/pengirim (Pelaksana Serah),
  jumlah judul, jumlah eksemplar, jenis paket/karya
- **Penerimaan**: tanggal & waktu, nama petugas keamanan, sumber penerimaan
  (integrasi SAKEDAP / manual), status
- **Lokasi**: lokasi transit, rak/area penyimpanan, posisi paket
- **Pendukung**: nama kurir, kontak kurir, foto/bukti penerimaan, catatan kondisi paket

## Status paket

| Status | Arti |
|---|---|
| Menunggu Penerimaan | Paket teridentifikasi tapi belum dicatat sebagai diterima |
| Diterima Satpam | Sudah diterima petugas keamanan dan tercatat di SAKEDAP |
| Di Lokasi Transit | Sudah diberi nomor antrian & label, ditempatkan di transit |
| Diterima Pegawai KCKR | Sudah diambil Pegawai KCKR untuk diproses |
| Selesai | Selesai proses penerimaan, tidak lagi di lokasi transit |

Aplikasi pos satpam hanya menyebabkan transisi sampai "Di Lokasi Transit".

## Keputusan

| Hal | Keputusan |
|---|---|
| Printer label | Zebra - perintah ZPL. Dukungan TSPL dibuang. |
| Sumber nomor antrian | Diterbitkan server dari tabel `letter`. |
| Format nomor antrian | `KCKR_[bulan][tahun 2 digit]_[urutan]`, contoh `KCKR_0826_1335` |

Konsekuensi dari nomor antrian yang diterbitkan server: **label tidak bisa
dicetak saat koneksi mati.** Antrian offline hanya menyelamatkan input petugas
agar tidak hilang; labelnya dicetak setelah data tersinkron. Format di deck
sendiri belum konsisten (`KCKR_08_16`, `KCKR_08_135`, `KCKR_08_1335`, dan
`A-0125` di tabel audit trail) - yang berlaku adalah format di tabel di atas.

## Perubahan skema INLIS yang dibutuhkan

Satu tabel baru, **`LETTER_ANTRIAN`**. Tabel `letter` tidak diubah sama sekali -
aplikasi ini hanya membacanya.

DDL lengkap untuk Oracle 11g ada di [ddl/letter_antrian.sql](ddl/letter_antrian.sql):
tabel, sequence, trigger BEFORE INSERT pengganti IDENTITY, trigger BEFORE UPDATE
untuk `update_date`, dan tiga index.

### Kenapa tabel sendiri, bukan kolom tambahan di letter

**Jalur manual tidak punya letter.** Baris `letter` berarti ada penyerahan yang
dideklarasikan penerbit. Saat resi belum terdaftar, deklarasi itu belum ada -
membuat baris `letter` palsu hanya demi menampung nomor antrian akan mengotori
data serah simpan.

**Satu letter bisa datang sebagai beberapa koli.** Masing-masing koli perlu
nomor antrian dan labelnya sendiri. Kolom yang menempel di `letter` hanya
memungkinkan satu nomor antrian per surat.

**Tidak mengubah tabel inti.** `letter` dipakai modul lain; menambah kolom di
sana berisiko lebih besar daripada membuat tabel baru.

**Hapus/karantina jadi sederhana.** Aturan 24 jam cukup berlaku di tabel ini.

`letter_id` NULL menandakan jalur manual. Kolom `pengirim_manual`,
`perkiraan_qty`, dan `perkiraan_copy` juga hanya terisi di jalur itu, dan
semuanya catatan satpam - bukan data terverifikasi.

**Kenapa nama pengirim tidak memakai `penerbit_id`.** `letter` menyimpan
`penerbit_id`, bukan nama. Pada jalur manual resi belum terdaftar sehingga
penerbit yang sah belum bisa ditentukan. Satpam juga tidak bisa memilih dari
daftar penerbit - yang dia lihat hanya nama pengirim di label paket, yang sering
berbeda dari nama penerbit terdaftar. Memaksa memilih dari daftar berarti
memaksa menebak, dan tebakan yang salah mengotori data.

**Kenapa jumlah judul/eks tidak ditulis ke `letter_detail`.** Angka sebenarnya
dihitung dari `SUM(letter_detail.quantity)` dan `SUM(letter_detail.copy)`, dan
baru bisa diketahui setelah paket dibuka. Satpam tidak membuka paket, jadi
angkanya perkiraan. Menyimpannya di kolom terpisah menjaga agar perkiraan tidak
tertukar dengan angka terverifikasi. Di layar pun labelnya berubah jadi
"Perkiraan Judul" dan "Perkiraan Eks" pada jalur manual.

Identitas penerbit dan jumlah sebenarnya tetap ditentukan Pegawai KCKR saat
paket dibuka.

## Pertanyaan terbuka

1. **Lokasi transit / rak tidak ada di form mockup**, padahal termasuk data yang
   dicatat dan jadi tujuan utama fitur (temu kembali). Di mana satpam menginput
   rak/posisi paket?

2. **Foto/bukti penerimaan** disebut sebagai data pendukung tapi tidak ada di
   mockup form. Perlu atau tidak?

3. **Semantik "Hapus = karantina"** dan batas 24 jam: perlu endpoint API yang
   sesuai, bukan DELETE biasa.

4. **Barcode di label**: desain hanya teks. Tapi tujuan fiturnya temu kembali &
   pelacakan. Menambahkan Code128 dari nomor antrian akan memungkinkan Pegawai
   KCKR men-scan label untuk mencari paket, bukan mengetik ulang. Rekomendasi:
   tambahkan.

5. **Autentikasi API** dan endpoint SAKEDAP belum diketahui.

6. **Ukuran kertas label** diasumsikan 50x30 mm @203 dpi di `appsettings.json`.
   Perlu dicocokkan dengan kertas dan model Zebra yang dipakai.
