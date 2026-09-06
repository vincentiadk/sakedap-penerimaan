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

| Status | `LETTER_ANTRIAN.STATUS` | Arti |
|---|---|---|
| Menunggu Penerimaan | `menunggu` | Paket teridentifikasi tapi belum dicatat sebagai diterima |
| Diterima Satpam | `diterima_satpam` | Sudah diterima petugas keamanan dan tercatat di SAKEDAP |
| Di Lokasi Transit | `transit` | Sudah diberi nomor antrian & label, ditempatkan di transit |
| Diterima Pegawai KCKR | `diterima_kckr` | Sudah diambil Pegawai KCKR untuk diproses |
| Selesai | `selesai` | Selesai proses penerimaan, tidak lagi di lokasi transit |

Aplikasi pos satpam hanya menyebabkan transisi sampai "Di Lokasi Transit".

Disimpan sebagai teks, bukan angka, supaya arti sebuah baris tidak bergantung
pada urutan anggota enum `StatusPaket` di kode aplikasi. Dijaga CHECK constraint
`CK_LETTER_ANTRIAN_STATUS`, dan dipetakan di `StatusPaketDb`.

## Keputusan

| Hal | Keputusan |
|---|---|
| Printer label | Zebra - perintah ZPL. Dukungan TSPL dibuang. |
| Sumber nomor antrian | Diterbitkan server dari tabel `letter`. |
| Format nomor antrian | Perpusnas `KCKR_[bulan][tahun 2 digit]_[urutan]` → `KCKR_0826_1335`; provinsi menyisipkan branch → `KCKR_11_0826_1335` |

Konsekuensi dari nomor antrian yang diterbitkan server: **label tidak bisa
dicetak saat koneksi mati.** Antrian offline hanya menyelamatkan input petugas
agar tidak hilang; labelnya dicetak setelah data tersinkron. Format di deck
sendiri belum konsisten (`KCKR_08_16`, `KCKR_08_135`, `KCKR_08_1335`, dan
`A-0125` di tabel audit trail) - yang berlaku adalah format di tabel di atas.

## Perubahan skema INLIS yang dibutuhkan

Empat tabel baru. Tabel `letter` tidak diubah sama sekali - aplikasi ini hanya
membacanya, bersama `letter_detail`, `penerbit`, `jasa_pengiriman`, dan
`collectionmedias`.

| Tabel | Isi |
|---|---|
| `LETTER_ANTRIAN` | Satu baris per kardus: nomor antrian, data kiriman, posisi |
| `LETTER_ANTRIAN_LOKASI` | Master lokasi, per satuan kerja |
| `LETTER_ANTRIAN_PC` | Pemetaan IP PC ke lokasi, per satuan kerja |
| `LETTER_ANTRIAN_MUTASI` | Riwayat perpindahan |

### Pemisahan per satuan kerja (`BRANCH_ID`)

Aplikasi ini dipakai di Perpusnas (`BRANCH_ID = 37`) maupun di perpustakaan
provinsi, yang masing-masing punya `BRANCH_ID` sendiri. Lokasi dan pemetaan PC
karena itu dipisah per satuan kerja.

`BRANCH_ID` diambil dari respons login INLIS (`op=isloginvalid`) milik user yang
sedang masuk, jadi satu berkas installer yang sama bisa dipasang di mana pun
tanpa penyesuaian - dan petugas tidak bisa keliru memilih satuan kerja, karena
memang tidak memilih.

Field-nya bernama **`BranchId`**, dibaca dari `data` pada respons `isloginvalid`.

**Status per September 2026: perubahan API-nya belum terpasang.** Sampai
selesai, nilainya diisi lewat `Lokasi.BranchIdOverride` di
`appsettings.local.json` (berkas per-mesin, tidak ikut repositori). Respons API
selalu menang bila memuatnya, jadi begitu perubahan itu naik, nilai cadangan
otomatis tidak terpakai dan barisnya bisa dibuang tanpa mengubah apa pun -
tidak perlu menyentuh konfigurasi PC mana pun. Selama masih memakai cadangan,
header aplikasi menuliskan "dari appsettings" di samping nomor branch.

`BranchIdOverride` sengaja tidak diberi nilai bawaan `37`. Kalau diberi, PC
provinsi yang dipasang tanpa menyunting berkas ini akan diam-diam menulis
sebagai Perpusnas - datanya masuk ke lingkup instansi lain dan nomor antriannya
berebut dengan deret Perpusnas.

Akibatnya urutannya berubah: **lokasi baru dicari setelah login**, bukan
sebelumnya seperti dulu. Layar masuk hanya menampilkan IP PC.

Tiga hal yang dijaga database, bukan sekadar disiplin aplikasi:

- `UQ_LA_PC_IP` adalah `(BRANCH_ID, IP_ADDRESS)`, bukan `IP_ADDRESS` saja.
  Jaringan lokal hampir selalu memakai rentang privat yang sama, jadi
  `192.168.1.10` wajar ada di Perpusnas dan di beberapa provinsi sekaligus.
- `FK_LA_PC_LOKASI` adalah foreign key gabungan `(BRANCH_ID, LOKASI_ID)`, jadi
  sebuah PC tidak mungkin dipetakan ke lokasi milik satuan kerja lain.
- `FK_LETTER_ANTRIAN_LOKASI` juga gabungan, sehingga kardus tidak bisa
  ditempatkan di lokasi instansi lain.

`LETTER_ANTRIAN.BRANCH_ID` menyaring seluruh pembacaan: tab Daftar Antrian,
pencarian barcode, dan pengecekan resi ganda. Penyaringnya dipasang di dalam
repository, bukan diserahkan ke tiap pemanggil, supaya tidak ada jalur baca
yang bisa lupa memasangnya.

### Deret nomor antrian per satuan kerja

Tiap satuan kerja punya deret nomornya sendiri, karena `UQ_LETTER_ANTRIAN_NOMOR`
unik secara global - tanpa pembeda, dua satuan kerja akan menerbitkan
`KCKR_0926_42` yang sama dan salah satunya ditolak database.

Perpusnas sengaja **tanpa** sisipan branch supaya nomor yang sudah beredar dan
tertempel di kardus di sana tidak berubah bentuk. Provinsi memakai sisipan.
Keduanya dibedakan dari banyaknya ruas - tiga ruas Perpusnas, empat ruas
provinsi - dan itu tidak ambigu karena periode selalu 4 digit.

Pencarian urutan terakhir memakai `LIKE` dengan awalan yang sudah membedakan
satuan kerja, dan garis bawahnya di-escape (`KutipPola`). Tanpa escape, `_`
adalah wildcard `LIKE` dan awalan Perpusnas bisa ikut menangkap nomor provinsi.

DDL lengkap untuk Oracle 11g ada di
[ddl/sakedap_penerimaan.sql](ddl/sakedap_penerimaan.sql) - satu berkas berisi
keempat tabel, sequence, trigger BEFORE INSERT pengganti IDENTITY, trigger
BEFORE UPDATE untuk `update_date`, index, dan data awal. Dijalankan berurutan
dari atas ke bawah pada database yang belum punya tabel-tabel ini.

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

### Satu baris = satu KARDUS

Kiriman berisi empat kardus menghasilkan **empat baris** `LETTER_ANTRIAN`
dengan empat nomor antrian, bukan satu baris dengan `jumlah_dus = 4`.

Alasannya kardus satu kiriman sering berpindah tidak bersamaan - dua naik
duluan, dua menyusul - jadi posisinya harus bisa dilacak sendiri-sendiri.
Karena tiap kardus sudah punya barisnya sendiri, `lokasi_id` di baris itu
sudah cukup: tidak perlu tabel posisi terpisah, dan barcode di label tidak
perlu akhiran penanda kardus.

Pengikatnya kolom `kiriman_id`, diisi **nomor antrian kardus pertama**.
Tidak memakai `letter_id` untuk ini karena `letter_id` NULL di jalur manual,
dan paket yang diserahkan langsung bahkan tidak punya resi - keduanya akan
meninggalkan kardus tanpa pengikat apa pun.

Harga yang dibayar, dan bagaimana ditanganinya:

| Konsekuensi | Penanganan |
|---|---|
| Data kiriman berulang di tiap baris | Ubah dan hapus bekerja atas seluruh kiriman, tidak pernah satu baris |
| `perkiraan_qty`/`perkiraan_copy` bisa terjumlah berlipat | Hanya boleh terisi di `nomor_dus = 1`, dijaga `CK_LETTER_ANTRIAN_QTY` |
| Satu resi sah punya banyak baris | Deteksi resi ganda mengelompokkan per `kiriman_id`, bukan per baris |
| Daftar Antrian menampilkan satu baris per kardus | Ada kolom Dus (`2/4`), dan statusnya menyebut jumlah dus dan jumlah kiriman |
| Nomor antrian dus susulan tidak bersambung | Wajar - yang mengikat `kiriman_id`, bukan urutan nomor |

Bentuk tabelnya ada di [ddl/sakedap_penerimaan.sql](ddl/sakedap_penerimaan.sql).

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
