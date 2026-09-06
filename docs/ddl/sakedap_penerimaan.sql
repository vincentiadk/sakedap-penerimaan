-- =====================================================================
-- SAKEDAP - Penerimaan Paket SSKCKR (pos satpam Gedung E)
-- Skrip lengkap untuk database KOSONG. Oracle 11g.
--
-- Menggantikan seluruh berkas DDL lain di folder ini. Kalau tabel-tabel
-- ini sudah pernah dibuat, JANGAN pakai skrip ini - dia mengasumsikan
-- belum ada apa-apa dan akan gagal di CREATE TABLE pertama.
--
-- Oracle 11g tidak punya GENERATED AS IDENTITY, jadi tiap tabel memakai
-- sequence + trigger BEFORE INSERT. Trigger BEFORE UPDATE mengurus
-- UPDATE_DATE.
--
-- URUTAN PENTING: dijalankan dari atas ke bawah. LETTER_ANTRIAN merujuk
-- LETTER_ANTRIAN_LOKASI, dan LETTER_ANTRIAN_MUTASI merujuk keduanya.
--
--
-- PRASYARAT - tabel INLIS yang HARUS sudah ada dan hanya DIBACA:
--
--   LETTER            surat penyerahan; sumber data jalur SAKEDAP
--   LETTER_DETAIL     rincian judul/eksemplar
--   PENERBIT          nama pengirim yang sah
--   JASA_PENGIRIMAN   daftar ekspedisi
--   COLLECTIONMEDIAS  jenis media koleksi
--
-- Aplikasi TIDAK PERNAH menulis ke tabel-tabel itu.
--
--
-- KONSEP UTAMA: satu baris LETTER_ANTRIAN = satu KARDUS.
--
-- Kiriman berisi empat kardus menghasilkan EMPAT baris dengan empat nomor
-- antrian, diikat kolom KIRIMAN_ID. Alasannya kardus satu kiriman sering
-- berpindah tidak bersamaan, jadi posisinya harus bisa dilacak
-- sendiri-sendiri. Penjelasan lengkap di docs/SPEC.md.
-- =====================================================================


-- =====================================================================
-- 1. MASTER LOKASI
--
-- Dibuat paling awal karena LETTER_ANTRIAN merujuk ke sini.
--
-- Dinonaktifkan lewat AKTIF, tidak pernah dihapus: penerimaan lama tetap
-- merujuk lokasi yang mungkin sudah tidak dipakai lagi.
--
-- BRANCH_ID memisahkan lokasi antar satuan kerja: 37 = Perpusnas, dan tiap
-- perpustakaan provinsi punya nilainya sendiri. Aplikasi mengambil
-- BRANCH_ID milik user dari respons login INLIS, lalu hanya melihat lokasi
-- dengan BRANCH_ID itu. Tanpa pemisahan ini, PC di provinsi bisa
-- memindahkan paket ke gudang Perpusnas.
-- =====================================================================
CREATE TABLE LETTER_ANTRIAN_LOKASI (
    ID           NUMBER(19)                  NOT NULL,

    -- Satuan kerja pemilik lokasi ini. 37 = Perpusnas.
    BRANCH_ID    NUMBER(19)                  NOT NULL,

    NAMA         VARCHAR2(100)               NOT NULL,
    KETERANGAN   VARCHAR2(255),
    AKTIF        NUMBER(1)   DEFAULT 1       NOT NULL,
    CREATE_DATE  DATE,
    UPDATE_DATE  DATE,

    CONSTRAINT PK_LA_LOKASI       PRIMARY KEY (ID),

    -- Nama lokasi hanya unik DALAM satu satuan kerja. "Gudang Transit"
    -- boleh ada di Perpusnas dan di tiap provinsi sekaligus.
    CONSTRAINT UQ_LA_LOKASI_NAMA  UNIQUE (BRANCH_ID, NAMA),

    -- Bukan untuk mencegah duplikat - ID sudah primary key. Ini supaya
    -- LETTER_ANTRIAN_PC bisa memakai foreign key gabungan
    -- (BRANCH_ID, LOKASI_ID), yang menjamin sebuah PC tidak pernah
    -- dipetakan ke lokasi milik satuan kerja lain.
    CONSTRAINT UQ_LA_LOKASI_BRANCH UNIQUE (BRANCH_ID, ID),

    CONSTRAINT CK_LA_LOKASI_AKTIF CHECK (AKTIF IN (0, 1))
);

CREATE SEQUENCE SEQ_LA_LOKASI START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE OR REPLACE TRIGGER TRG_LA_LOKASI_BI
    BEFORE INSERT ON LETTER_ANTRIAN_LOKASI
    FOR EACH ROW
BEGIN
    IF :NEW.ID IS NULL THEN
        SELECT SEQ_LA_LOKASI.NEXTVAL INTO :NEW.ID FROM DUAL;
    END IF;

    IF :NEW.CREATE_DATE IS NULL THEN :NEW.CREATE_DATE := SYSDATE; END IF;
    IF :NEW.UPDATE_DATE IS NULL THEN :NEW.UPDATE_DATE := SYSDATE; END IF;
END;
/

CREATE OR REPLACE TRIGGER TRG_LA_LOKASI_BU
    BEFORE UPDATE ON LETTER_ANTRIAN_LOKASI
    FOR EACH ROW
BEGIN
    :NEW.UPDATE_DATE := SYSDATE;
END;
/


-- =====================================================================
-- 2. PEMETAAN PC KE LOKASI
--
-- Lokasi bukan data yang diketik petugas, melainkan atribut dari PC.
-- Aplikasi membaca IP-nya sendiri saat login, mencari lokasinya di sini,
-- lalu setiap scan memindahkan kardus ke lokasi itu.
--
-- Satu lokasi boleh punya beberapa PC, tapi satu IP hanya boleh menunjuk
-- satu lokasi DALAM satuan kerja yang sama - kalau tidak, aplikasi tidak
-- bisa menentukan dia di mana.
--
-- IP TIDAK unik lintas satuan kerja. Jaringan lokal hampir selalu memakai
-- rentang privat yang sama, jadi 192.168.1.10 sangat mungkin ada di
-- Perpusnas DAN di beberapa provinsi sekaligus. Karena itu keunikannya
-- (BRANCH_ID, IP_ADDRESS), bukan IP_ADDRESS saja.
--
-- PERHATIAN OPERASIONAL: seluruh mekanisme ini bergantung pada IP yang
-- tidak berubah. PC dengan DHCP biasa bisa berpindah IP dan lokasinya
-- ikut salah tanpa ada yang sadar. Tiap PC WAJIB memakai IP statis atau
-- DHCP reservation.
--
-- VARCHAR2(45) agar muat IPv6 kalau nanti dipakai.
-- =====================================================================
CREATE TABLE LETTER_ANTRIAN_PC (
    ID            NUMBER(19)                 NOT NULL,

    -- Satuan kerja pemilik PC ini. Wajib sama dengan BRANCH_ID lokasinya -
    -- dijaga FK_LA_PC_LOKASI di bawah.
    BRANCH_ID     NUMBER(19)                 NOT NULL,

    IP_ADDRESS    VARCHAR2(45)               NOT NULL,
    LOKASI_ID     NUMBER(19)                 NOT NULL,

    -- Supaya admin tahu ini PC yang mana, misal "PC pos satpam lantai 1".
    KETERANGAN    VARCHAR2(255),

    -- Diisi aplikasi setiap login. IP yang sudah lama tidak muncul di sini
    -- berarti PC-nya sudah tidak dipakai atau IP-nya berubah.
    LAST_SEEN     DATE,

    AKTIF         NUMBER(1)  DEFAULT 1       NOT NULL,
    CREATE_DATE   DATE,
    UPDATE_DATE   DATE,

    CONSTRAINT PK_LA_PC        PRIMARY KEY (ID),

    CONSTRAINT UQ_LA_PC_IP     UNIQUE (BRANCH_ID, IP_ADDRESS),

    CONSTRAINT CK_LA_PC_AKTIF  CHECK (AKTIF IN (0, 1)),

    -- Foreign key GABUNGAN, bukan hanya ke LOKASI_ID. Ini yang menjamin
    -- PC tidak bisa dipetakan ke lokasi milik satuan kerja lain - salah
    -- ketik BRANCH_ID langsung ditolak database, bukan baru ketahuan
    -- setelah paket terlanjur dipindahkan ke gudang instansi lain.
    CONSTRAINT FK_LA_PC_LOKASI FOREIGN KEY (BRANCH_ID, LOKASI_ID)
        REFERENCES LETTER_ANTRIAN_LOKASI (BRANCH_ID, ID)
);

-- Kolom foreign key wajib diindeks di Oracle. Kolom terdepannya BRANCH_ID
-- supaya sekaligus melayani pencarian "semua PC di satuan kerja ini".
CREATE INDEX IX_LA_PC_LOKASI ON LETTER_ANTRIAN_PC (BRANCH_ID, LOKASI_ID);

CREATE SEQUENCE SEQ_LA_PC START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE OR REPLACE TRIGGER TRG_LA_PC_BI
    BEFORE INSERT ON LETTER_ANTRIAN_PC
    FOR EACH ROW
BEGIN
    IF :NEW.ID IS NULL THEN
        SELECT SEQ_LA_PC.NEXTVAL INTO :NEW.ID FROM DUAL;
    END IF;

    IF :NEW.CREATE_DATE IS NULL THEN :NEW.CREATE_DATE := SYSDATE; END IF;
    IF :NEW.UPDATE_DATE IS NULL THEN :NEW.UPDATE_DATE := SYSDATE; END IF;
END;
/

CREATE OR REPLACE TRIGGER TRG_LA_PC_BU
    BEFORE UPDATE ON LETTER_ANTRIAN_PC
    FOR EACH ROW
BEGIN
    :NEW.UPDATE_DATE := SYSDATE;
END;
/


-- =====================================================================
-- 3. LETTER_ANTRIAN - satu baris per KARDUS
--
-- Tabel utama. Satu-satunya tabel yang ditulis aplikasi selain
-- LETTER_ANTRIAN_MUTASI.
--
-- Kenapa tabel sendiri dan bukan kolom tambahan di LETTER:
--
--   - Jalur manual tidak punya letter sama sekali. Baris LETTER berarti
--     ada penyerahan yang dideklarasikan penerbit; membuat baris palsu
--     hanya demi menampung nomor antrian akan mengotori data serah simpan.
--   - Satu letter bisa datang sebagai beberapa kardus, masing-masing butuh
--     nomor antrian dan labelnya sendiri.
--   - LETTER dipakai modul lain; menambah kolom di sana lebih berisiko.
--   - Aturan hapus/karantina 24 jam cukup berlaku di tabel ini.
-- =====================================================================
CREATE TABLE LETTER_ANTRIAN (
    ID               NUMBER(19)                        NOT NULL,

    -- Satuan kerja pemilik baris ini, dari respons login INLIS.
    -- 37 = Perpusnas. Menentukan siapa yang boleh melihatnya di tab Daftar
    -- Antrian, dan ikut membentuk nomor antrian - lihat NOMOR_ANTRIAN.
    BRANCH_ID        NUMBER(19)                        NOT NULL,

    -- Perpusnas  : KCKR_[bulan][tahun2digit]_[urutan]      -> KCKR_0826_1335
    -- Provinsi   : KCKR_[branch]_[bulan][tahun2digit]_[urutan]
    --                                                      -> KCKR_11_0826_1335
    --
    -- Perpusnas sengaja TANPA branch supaya nomor yang sudah beredar di sana
    -- tidak berubah bentuk. Provinsi memakai sisipan branch karena deret
    -- nomornya berjalan sendiri-sendiri per satuan kerja; tanpa sisipan itu
    -- dua satuan kerja akan menerbitkan nomor yang sama dan salah satunya
    -- ditolak UQ_LETTER_ANTRIAN_NOMOR.
    --
    -- Membedakannya cukup dari banyaknya ruas: tiga ruas berarti Perpusnas,
    -- empat ruas berarti provinsi.
    --
    -- Satu baris = SATU KARDUS. Kiriman berisi 4 kardus menghasilkan 4
    -- baris dengan 4 nomor antrian.
    NOMOR_ANTRIAN    VARCHAR2(30)                      NOT NULL,

    -- Pengikat kardus-kardus satu kiriman: diisi NOMOR_ANTRIAN kardus
    -- pertama, jadi kardus pertama menunjuk dirinya sendiri.
    --
    -- Tidak memakai LETTER_ID untuk ini: LETTER_ID NULL di jalur manual,
    -- dan paket yang diserahkan langsung bahkan tidak punya resi -
    -- keduanya akan meninggalkan kardus tanpa pengikat apa pun.
    KIRIMAN_ID       VARCHAR2(30)                      NOT NULL,

    -- Kardus ke berapa dalam kiriman ini, 1 sampai JUMLAH_DUS.
    NOMOR_DUS        NUMBER(4)     DEFAULT 1           NOT NULL,

    -- Banyaknya kardus dalam kiriman ini. Nilainya sama di seluruh baris
    -- sekiriman, dan dicetak di label sebagai "DUS n DARI N".
    JUMLAH_DUS       NUMBER(4)     DEFAULT 1           NOT NULL,

    -- NULL berarti jalur manual: resi belum terdaftar di SAKEDAP sehingga
    -- kardus ini belum bisa dihubungkan ke surat penyerahan mana pun.
    LETTER_ID        NUMBER(19),

    -- Boleh NULL: paket yang diserahkan langsung ke pos satpam tidak punya
    -- nomor resi sama sekali.
    RECEIPT_NO       VARCHAR2(100),

    -- 'ekspedisi' bila diantar jasa pengiriman, 'langsung' bila diserahkan
    -- sendiri ke pos satpam.
    CARA_DATANG      VARCHAR2(10)  DEFAULT 'ekspedisi' NOT NULL,

    -- Ekspedisi menurut pengamatan satpam, bukan relasi ke JASA_PENGIRIMAN.
    -- Di jalur manual sering tidak ada padanannya di tabel referensi.
    -- NULL bila CARA_DATANG = 'langsung'.
    EKSPEDISI        VARCHAR2(100),

    -- Diketik per transaksi. Satu PC pos satpam dipakai bergantian
    -- beberapa petugas dengan satu akun aplikasi, jadi ini bukan akun login.
    NAMA_PETUGAS     VARCHAR2(100)                     NOT NULL,

    WAKTU_TERIMA     DATE                              NOT NULL,

    -- Posisi kardus ini. NULL berarti belum pernah ditempatkan.
    LOKASI_ID        NUMBER(19),

    -- Jenis media koleksi. Boleh NULL: satpam tidak membuka paket, jadi
    -- kadang belum bisa ditentukan di pos penerimaan.
    JENIS_MEDIA_ID   NUMBER(19),

    SUMBER           VARCHAR2(10)  DEFAULT 'manual'    NOT NULL,

    -- Teks, bukan angka. Angka membuat arti sebuah baris bergantung pada
    -- urutan anggota enum StatusPaket di aplikasi: menyisipkan satu anggota
    -- baru diam-diam mengubah arti seluruh baris lama yang sudah tersimpan.
    -- Polanya sama dengan SUMBER dan CARA_DATANG di tabel ini.
    STATUS           VARCHAR2(20)  DEFAULT 'diterima_satpam' NOT NULL,

    -- Tiga kolom berikut HANYA terisi pada jalur manual, dan semuanya
    -- catatan satpam yang belum terverifikasi. Angka sebenarnya dihitung
    -- dari LETTER_DETAIL setelah paket dibuka Pegawai KCKR.
    --
    -- PERKIRAAN_QTY dan PERKIRAAN_COPY adalah angka SEKIRIMAN, bukan
    -- per kardus. Karena itu keduanya hanya boleh terisi di baris
    -- NOMOR_DUS = 1 - lihat CK_LETTER_ANTRIAN_QTY. Kalau ikut tersalin ke
    -- tiap kardus, SUM(PERKIRAAN_QTY) menghasilkan angka berlipat tanpa
    -- ada yang sadar.
    PENGIRIM_MANUAL  VARCHAR2(255),
    PERKIRAAN_QTY    NUMBER(10),
    PERKIRAAN_COPY   NUMBER(10),

    CREATE_DATE      DATE,
    UPDATE_DATE      DATE,

    -- Penanda hapus. Terisi berarti baris dianggap terhapus dan tidak lagi
    -- muncul di daftar. Bukan DELETE sungguhan: nomor antrian yang sudah
    -- tercetak dan tertempel di kardus harus tetap bisa ditelusuri.
    -- Batas hapus dan ubah 24 jam sejak CREATE_DATE, dijaga aplikasi.
    DELETE_DATE      DATE,

    CONSTRAINT PK_LETTER_ANTRIAN        PRIMARY KEY (ID),

    -- Jaring pengaman race condition: nomor antrian diterbitkan aplikasi
    -- dengan SELECT MAX lalu INSERT terpisah. Kalau nanti ada pos kedua,
    -- duplikat ditolak database, bukan diterima diam-diam.
    CONSTRAINT UQ_LETTER_ANTRIAN_NOMOR  UNIQUE (NOMOR_ANTRIAN),

    -- Satu kardus hanya boleh punya satu baris. Ini juga yang menjaga agar
    -- dus susulan tidak menghasilkan nomor dus kembar dalam satu kiriman.
    CONSTRAINT UQ_LETTER_ANTRIAN_KARDUS UNIQUE (KIRIMAN_ID, NOMOR_DUS),

    CONSTRAINT CK_LETTER_ANTRIAN_DUS    CHECK (JUMLAH_DUS >= 1),

    CONSTRAINT CK_LETTER_ANTRIAN_NODUS  CHECK (
        NOMOR_DUS >= 1 AND NOMOR_DUS <= JUMLAH_DUS
    ),

    -- Angka perkiraan berlaku sekiriman, jadi hanya kardus pertama yang
    -- menyimpannya. Dijaga database, bukan hanya disiplin aplikasi.
    CONSTRAINT CK_LETTER_ANTRIAN_QTY    CHECK (
        NOMOR_DUS = 1
        OR (PERKIRAAN_QTY IS NULL AND PERKIRAAN_COPY IS NULL)
    ),

    CONSTRAINT CK_LETTER_ANTRIAN_SUMBER CHECK (SUMBER IN ('sakedap', 'manual')),

    CONSTRAINT CK_LETTER_ANTRIAN_CARA   CHECK (CARA_DATANG IN ('ekspedisi', 'langsung')),

    -- Nilainya mengikuti tabel status di docs/SPEC.md. Aplikasi pos satpam
    -- hanya menerbitkan 'diterima_satpam' dan 'transit'; sisanya diisi
    -- SAKEDAP web setelah paket diambil Pegawai KCKR.
    CONSTRAINT CK_LETTER_ANTRIAN_STATUS CHECK (
        STATUS IN ('menunggu', 'diterima_satpam', 'transit',
                   'diterima_kckr', 'selesai')
    ),

    -- Paket yang diantar ekspedisi harus punya resi; yang diserahkan
    -- langsung boleh tanpa resi.
    CONSTRAINT CK_LETTER_ANTRIAN_RESI   CHECK (
        CARA_DATANG = 'langsung' OR RECEIPT_NO IS NOT NULL
    ),

    -- Kardus pertama menunjuk dirinya sendiri, jadi rujukannya selalu ada.
    -- Oracle memeriksa FK setelah baris masuk, sehingga INSERT yang merujuk
    -- dirinya sendiri diterima.
    CONSTRAINT FK_LETTER_ANTRIAN_KIRIM  FOREIGN KEY (KIRIMAN_ID)
        REFERENCES LETTER_ANTRIAN (NOMOR_ANTRIAN),

    -- Foreign key GABUNGAN, seperti di LETTER_ANTRIAN_PC: kardus tidak
    -- mungkin ditempatkan di lokasi milik satuan kerja lain.
    --
    -- LOKASI_ID boleh NULL, dan Oracle menganggap foreign key gabungan
    -- terpenuhi bila salah satu kolomnya NULL - jadi kardus yang belum
    -- ditempatkan tetap diterima.
    CONSTRAINT FK_LETTER_ANTRIAN_LOKASI FOREIGN KEY (BRANCH_ID, LOKASI_ID)
        REFERENCES LETTER_ANTRIAN_LOKASI (BRANCH_ID, ID),

    CONSTRAINT FK_LETTER_ANTRIAN_LETTER FOREIGN KEY (LETTER_ID)
        REFERENCES LETTER (LETTER_ID)
);

CREATE SEQUENCE SEQ_LETTER_ANTRIAN START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

-- Semua pengisian memakai IF NULL supaya nilai yang dikirim aplikasi tidak
-- ditimpa - INLIS mengirim CREATE_DATE/UPDATE_DATE sendiri.
CREATE OR REPLACE TRIGGER TRG_LETTER_ANTRIAN_BI
    BEFORE INSERT ON LETTER_ANTRIAN
    FOR EACH ROW
BEGIN
    IF :NEW.ID IS NULL THEN
        SELECT SEQ_LETTER_ANTRIAN.NEXTVAL INTO :NEW.ID FROM DUAL;
    END IF;

    IF :NEW.CREATE_DATE IS NULL THEN :NEW.CREATE_DATE := SYSDATE; END IF;
    IF :NEW.UPDATE_DATE IS NULL THEN :NEW.UPDATE_DATE := SYSDATE; END IF;
END;
/

CREATE OR REPLACE TRIGGER TRG_LETTER_ANTRIAN_BU
    BEFORE UPDATE ON LETTER_ANTRIAN
    FOR EACH ROW
BEGIN
    :NEW.UPDATE_DATE := SYSDATE;
END;
/

-- Tab Daftar Antrian selalu menyaring satuan kerja DULU, baru rentang
-- tanggal, lalu mengurutkan menurun pada kolom tanggal yang sama. Kolom
-- terdepan BRANCH_ID karena penyaring itu yang paling menyempitkan.
CREATE INDEX IX_LETTER_ANTRIAN_WAKTU  ON LETTER_ANTRIAN (BRANCH_ID, WAKTU_TERIMA);

-- Pencarian resi, dan pengecekan kiriman yang sudah pernah dicatat -
-- keduanya juga selalu dibatasi satuan kerja.
CREATE INDEX IX_LETTER_ANTRIAN_RESI   ON LETTER_ANTRIAN (BRANCH_ID, RECEIPT_NO);

-- Kolom foreign key wajib diindeks di Oracle. Tanpa ini, DML pada tabel
-- induk dapat mengambil kunci tingkat tabel di LETTER_ANTRIAN.
CREATE INDEX IX_LETTER_ANTRIAN_LETTER ON LETTER_ANTRIAN (LETTER_ID);
CREATE INDEX IX_LETTER_ANTRIAN_LOKASI ON LETTER_ANTRIAN (BRANCH_ID, LOKASI_ID);
CREATE INDEX IX_LETTER_ANTRIAN_MEDIA  ON LETTER_ANTRIAN (JENIS_MEDIA_ID);

-- Daftar antrian selalu menyaring DELETE_DATE IS NULL lebih dulu.
CREATE INDEX IX_LETTER_ANTRIAN_HAPUS  ON LETTER_ANTRIAN (DELETE_DATE);

-- Catatan index yang SENGAJA TIDAK dibuat:
--
--   NOMOR_ANTRIAN - UQ_LETTER_ANTRIAN_NOMOR sudah membuat index uniknya,
--   dan itu yang dipakai aplikasi saat mencari nomor urut terakhir bulan
--   berjalan (LIKE 'KCKR_0826_%') maupun saat scan barcode.
--
--   KIRIMAN_ID - UQ_LETTER_ANTRIAN_KARDUS (KIRIMAN_ID, NOMOR_DUS) sudah
--   menaruhnya di kolom terdepan, jadi index itu sekaligus memenuhi
--   kewajiban mengindeks kolom foreign key dan melayani pencarian
--   "semua kardus sekiriman".
--
-- Pencarian bebas di tab Daftar Antrian memakai UPPER(kolom) LIKE '%kata%'.
-- Wildcard di depan membuat index tidak terpakai, dan itu diterima:
-- pencarian selalu dibatasi rentang tanggal lebih dulu.


-- =====================================================================
-- 4. RIWAYAT PERPINDAHAN
--
-- Baris pertama sebuah kardus ditulis saat penerimaan disimpan, dengan
-- LOKASI_ASAL_ID kosong - sebelum itu kardus memang belum ada di mana pun.
-- Perpindahan berikutnya menambah baris dengan asal dan tujuan terisi.
--
-- Tidak ada NOMOR_DUS di sini: satu baris LETTER_ANTRIAN sudah berarti
-- satu kardus, jadi LETTER_ANTRIAN_ID sendiri sudah menunjuk kardusnya.
-- =====================================================================
CREATE TABLE LETTER_ANTRIAN_MUTASI (
    ID                 NUMBER(19)            NOT NULL,
    LETTER_ANTRIAN_ID  NUMBER(19)            NOT NULL,

    LOKASI_ASAL_ID     NUMBER(19),
    LOKASI_TUJUAN_ID   NUMBER(19)            NOT NULL,

    WAKTU              DATE                  NOT NULL,
    PETUGAS            VARCHAR2(100),
    IP_PC              VARCHAR2(45),

    CREATE_DATE        DATE,

    CONSTRAINT PK_LA_MUTASI          PRIMARY KEY (ID),

    CONSTRAINT FK_LA_MUTASI_ANTRIAN  FOREIGN KEY (LETTER_ANTRIAN_ID)
        REFERENCES LETTER_ANTRIAN (ID),

    CONSTRAINT FK_LA_MUTASI_ASAL     FOREIGN KEY (LOKASI_ASAL_ID)
        REFERENCES LETTER_ANTRIAN_LOKASI (ID),

    CONSTRAINT FK_LA_MUTASI_TUJUAN   FOREIGN KEY (LOKASI_TUJUAN_ID)
        REFERENCES LETTER_ANTRIAN_LOKASI (ID)
);

-- Penelusuran selalu dimulai dari satu kardus, diurutkan menurut waktu.
CREATE INDEX IX_LA_MUTASI_ANTRIAN ON LETTER_ANTRIAN_MUTASI (LETTER_ANTRIAN_ID, WAKTU);

-- Kolom foreign key wajib diindeks di Oracle.
CREATE INDEX IX_LA_MUTASI_ASAL    ON LETTER_ANTRIAN_MUTASI (LOKASI_ASAL_ID);
CREATE INDEX IX_LA_MUTASI_TUJUAN  ON LETTER_ANTRIAN_MUTASI (LOKASI_TUJUAN_ID);

CREATE SEQUENCE SEQ_LA_MUTASI START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE OR REPLACE TRIGGER TRG_LA_MUTASI_BI
    BEFORE INSERT ON LETTER_ANTRIAN_MUTASI
    FOR EACH ROW
BEGIN
    IF :NEW.ID IS NULL THEN
        SELECT SEQ_LA_MUTASI.NEXTVAL INTO :NEW.ID FROM DUAL;
    END IF;

    IF :NEW.CREATE_DATE IS NULL THEN :NEW.CREATE_DATE := SYSDATE; END IF;
END;
/


-- =====================================================================
-- 5. FOREIGN KEY JENIS MEDIA - PERIKSA NAMA TABELNYA DULU
--
-- Dipisah ke sini dengan sengaja. Nama tabel jenis media BELUM TERVERIFIKASI
-- dan sumbernya bertentangan: kode aplikasi memakai COLLECTIONMEDIAS
-- (jamak), DDL versi lama memakai COLLECTIONMEDIA (tunggal).
--
-- Kalau ditaruh di dalam CREATE TABLE di bagian 3 dan namanya salah,
-- SELURUH tabel utama gagal dibuat. Di sini, yang gagal hanya satu perintah
-- dan sisa skripnya tetap jadi.
--
-- Periksa dulu mana yang benar:
--
--   SELECT TABLE_NAME FROM ALL_TABLES WHERE TABLE_NAME LIKE 'COLLECTIONMEDIA%';
--
-- Lalu jalankan yang cocok, dan samakan Skema.TabelCollectionMedia di
-- src/Penerimaan.App/Services/PenerimaanRepository.cs dengan hasilnya.
-- =====================================================================

ALTER TABLE LETTER_ANTRIAN
    ADD CONSTRAINT FK_LETTER_ANTRIAN_MEDIA
    FOREIGN KEY (JENIS_MEDIA_ID) REFERENCES COLLECTIONMEDIAS (ID);

-- Kalau ternyata namanya tunggal, pakai yang ini:
-- ALTER TABLE LETTER_ANTRIAN
--     ADD CONSTRAINT FK_LETTER_ANTRIAN_MEDIA
--     FOREIGN KEY (JENIS_MEDIA_ID) REFERENCES COLLECTIONMEDIA (ID);


-- =====================================================================
-- 6. DATA AWAL
--
-- WAJIB DISESUAIKAN. Aplikasi mencari lokasi PC dari IP-nya sendiri; kalau
-- tidak ketemu di LETTER_ANTRIAN_PC, tab Pindah Lokasi mati dan penerimaan
-- tersimpan tanpa posisi.
-- =====================================================================

-- Contoh di bawah ini untuk BRANCH_ID 37 (Perpusnas). Untuk perpustakaan
-- provinsi, ganti angka 37 di SELURUH perintah di bagian ini dengan
-- BRANCH_ID satuan kerja yang bersangkutan.
--
-- Cocokkan dengan BRANCH_ID yang dipakai user di INLIS - itu yang dibaca
-- aplikasi saat login. Salah angka = lokasi tidak akan pernah ketemu.

INSERT INTO LETTER_ANTRIAN_LOKASI (BRANCH_ID, NAMA, KETERANGAN) VALUES
    (37, 'Pos Satpam Gedung E', 'Titik penerimaan paket SSKCKR');

INSERT INTO LETTER_ANTRIAN_LOKASI (BRANCH_ID, NAMA, KETERANGAN) VALUES
    (37, 'Gudang Transit KCKR', 'Penyimpanan sementara sebelum paket dibuka');

-- >>> GANTI IP DI BAWAH INI dengan IP statis PC pos satpam yang sebenarnya.
--     Cek dengan "ipconfig" di PC tersebut. Salah IP = lokasi tidak ketemu.
--
-- BRANCH_ID diambil dari baris lokasinya, bukan diketik ulang - supaya
-- tidak mungkin melenceng dari lokasi yang ditunjuk.
INSERT INTO LETTER_ANTRIAN_PC (BRANCH_ID, IP_ADDRESS, LOKASI_ID, KETERANGAN)
SELECT BRANCH_ID, '192.168.1.10', ID, 'PC pos satpam Gedung E'
  FROM LETTER_ANTRIAN_LOKASI
 WHERE BRANCH_ID = 37
   AND NAMA = 'Pos Satpam Gedung E';

COMMIT;


-- =====================================================================
-- 7. VERIFIKASI
-- =====================================================================

-- Keempat tabel harus muncul:
--
-- SELECT TABLE_NAME FROM USER_TABLES
--  WHERE TABLE_NAME LIKE 'LETTER_ANTRIAN%'
--  ORDER BY TABLE_NAME;
--
-- Harus ada: LETTER_ANTRIAN, LETTER_ANTRIAN_LOKASI,
--            LETTER_ANTRIAN_MUTASI, LETTER_ANTRIAN_PC


-- Semua constraint harus ENABLED dan VALIDATED:
--
-- SELECT CONSTRAINT_NAME, CONSTRAINT_TYPE, STATUS, VALIDATED
--   FROM USER_CONSTRAINTS
--  WHERE TABLE_NAME LIKE 'LETTER_ANTRIAN%'
--    AND CONSTRAINT_NAME NOT LIKE 'SYS_%'
--  ORDER BY TABLE_NAME, CONSTRAINT_TYPE;


-- Trigger harus ENABLED - kalau tidak, ID tidak terisi dan INSERT gagal:
--
-- SELECT TRIGGER_NAME, STATUS FROM USER_TRIGGERS
--  WHERE TABLE_NAME LIKE 'LETTER_ANTRIAN%';


-- Pemetaan PC sudah benar:
--
-- SELECT p.IP_ADDRESS, l.NAMA, p.AKTIF
--   FROM LETTER_ANTRIAN_PC p
--   JOIN LETTER_ANTRIAN_LOKASI l ON l.ID = p.LOKASI_ID;


-- Setelah aplikasi dipakai - kiriman yang kardusnya belum lengkap
-- (mestinya kosong; kalau ada, penyimpanan pernah gagal di tengah):
--
-- SELECT KIRIMAN_ID, MAX(JUMLAH_DUS) AS SEHARUSNYA, COUNT(*) AS ADA
--   FROM LETTER_ANTRIAN
--  WHERE DELETE_DATE IS NULL
--  GROUP BY KIRIMAN_ID
-- HAVING COUNT(*) <> MAX(JUMLAH_DUS);


-- Kiriman yang kardusnya tersebar di beberapa lokasi - bukan error,
-- justru inilah yang ingin terlihat:
--
-- SELECT KIRIMAN_ID, COUNT(DISTINCT LOKASI_ID) AS JUMLAH_LOKASI
--   FROM LETTER_ANTRIAN
--  WHERE DELETE_DATE IS NULL
--  GROUP BY KIRIMAN_ID
-- HAVING COUNT(DISTINCT LOKASI_ID) > 1;


-- =====================================================================
-- 8. PEMBATALAN
--
-- Ada di berkas terpisah: sakedap_penerimaan_drop.sql
--
-- Membuang keempat tabel beserta trigger dan sequence-nya, supaya
-- pemasangan bisa diulang dari nol. Sengaja dipisah agar perintah
-- penghapusan tidak pernah ikut terseleksi saat menjalankan skrip ini.
-- =====================================================================
