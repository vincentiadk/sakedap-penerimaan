-- =====================================================================
-- LETTER_ANTRIAN - antrian penerimaan paket SSKCKR di pos satpam Gedung E
--
-- Target: Oracle 11g (tanpa GENERATED AS IDENTITY, jadi pakai sequence
--         dan trigger BEFORE INSERT).
--
-- Tabel LETTER TIDAK diubah sama sekali. Aplikasi pos satpam hanya
-- membaca LETTER, LETTER_DETAIL, JASA_PENGIRIMAN, dan PENERBIT;
-- satu-satunya tabel yang ditulis adalah LETTER_ANTRIAN.
--
-- Alasan memakai tabel terpisah ada di docs/SPEC.md.
-- =====================================================================


-- ---------------------------------------------------------------------
-- 1. Tabel
-- ---------------------------------------------------------------------
CREATE TABLE LETTER_ANTRIAN (
    ID               NUMBER(19)                        NOT NULL,

    -- Format KCKR_[bulan][tahun2digit]_[urutan], contoh KCKR_0826_1335
    NOMOR_ANTRIAN    VARCHAR2(30)                      NOT NULL,

    -- NULL berarti jalur manual: resi belum terdaftar di SAKEDAP sehingga
    -- paket ini belum bisa dihubungkan ke surat penyerahan mana pun.
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
    LOKASI_TRANSIT   VARCHAR2(100),

    -- Jenis media koleksi, mengacu ke tabel referensi COLLECTIONMEDIA.
    -- Boleh NULL: satpam tidak membuka paket, jadi kadang belum bisa
    -- ditentukan di pos penerimaan.
    JENIS_MEDIA_ID   NUMBER(19),

    -- Banyaknya kardus dalam satu kiriman. Menentukan berapa label dicetak:
    -- tiap dus mendapat labelnya sendiri bertanda "DUS n DARI N".
    JUMLAH_DUS       NUMBER(4)     DEFAULT 1           NOT NULL,

    SUMBER           VARCHAR2(10)  DEFAULT 'manual'    NOT NULL,
    STATUS           NUMBER(2)     DEFAULT 1           NOT NULL,

    -- Tiga kolom berikut HANYA terisi pada jalur manual, dan semuanya
    -- catatan satpam yang belum terverifikasi. Angka sebenarnya dihitung
    -- dari LETTER_DETAIL setelah paket dibuka Pegawai KCKR.
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

    CONSTRAINT CK_LETTER_ANTRIAN_SUMBER CHECK (SUMBER IN ('sakedap', 'manual')),

    CONSTRAINT CK_LETTER_ANTRIAN_CARA   CHECK (CARA_DATANG IN ('ekspedisi', 'langsung')),

    CONSTRAINT CK_LETTER_ANTRIAN_DUS    CHECK (JUMLAH_DUS >= 1),

    -- Paket yang diantar ekspedisi harus punya resi; yang diserahkan
    -- langsung boleh tanpa resi.
    CONSTRAINT CK_LETTER_ANTRIAN_RESI   CHECK (
        CARA_DATANG = 'langsung' OR RECEIPT_NO IS NOT NULL
    ),

    CONSTRAINT FK_LETTER_ANTRIAN_LETTER FOREIGN KEY (LETTER_ID)
        REFERENCES LETTER (LETTER_ID),

    CONSTRAINT FK_LETTER_ANTRIAN_MEDIA  FOREIGN KEY (JENIS_MEDIA_ID)
        REFERENCES COLLECTIONMEDIA (ID)
);


-- ---------------------------------------------------------------------
-- 2. Sequence
--
-- NOCACHE dipilih karena volume penerimaan rendah (puluhan per hari) dan
-- nomor yang melompat setelah instance restart lebih merepotkan untuk
-- ditelusuri daripada keuntungan performanya.
-- ---------------------------------------------------------------------
CREATE SEQUENCE SEQ_LETTER_ANTRIAN
    START WITH 1
    INCREMENT BY 1
    NOCACHE
    NOCYCLE;


-- ---------------------------------------------------------------------
-- 3. Trigger pengganti IDENTITY
--
-- Semua pengisian memakai IF NULL supaya nilai yang dikirim aplikasi
-- tidak ditimpa - INLIS mengirim create_date/update_date sendiri.
-- ---------------------------------------------------------------------
CREATE OR REPLACE TRIGGER TRG_LETTER_ANTRIAN_BI
    BEFORE INSERT ON LETTER_ANTRIAN
    FOR EACH ROW
BEGIN
    IF :NEW.ID IS NULL THEN
        SELECT SEQ_LETTER_ANTRIAN.NEXTVAL INTO :NEW.ID FROM DUAL;
    END IF;

    IF :NEW.CREATE_DATE IS NULL THEN
        :NEW.CREATE_DATE := SYSDATE;
    END IF;

    IF :NEW.UPDATE_DATE IS NULL THEN
        :NEW.UPDATE_DATE := SYSDATE;
    END IF;
END;
/

CREATE OR REPLACE TRIGGER TRG_LETTER_ANTRIAN_BU
    BEFORE UPDATE ON LETTER_ANTRIAN
    FOR EACH ROW
BEGIN
    :NEW.UPDATE_DATE := SYSDATE;
END;
/


-- ---------------------------------------------------------------------
-- 4. Index
-- ---------------------------------------------------------------------

-- Tab Daftar Antrian selalu menyaring rentang tanggal lalu mengurutkan
-- menurun pada kolom yang sama.
CREATE INDEX IX_LETTER_ANTRIAN_WAKTU  ON LETTER_ANTRIAN (WAKTU_TERIMA);

-- Pencarian resi, dan pengecekan paket yang sudah pernah dicatat.
CREATE INDEX IX_LETTER_ANTRIAN_RESI   ON LETTER_ANTRIAN (RECEIPT_NO);

-- Kolom foreign key wajib diindeks di Oracle. Tanpa ini, DML pada tabel
-- induk LETTER dapat mengambil kunci tingkat tabel di LETTER_ANTRIAN.
CREATE INDEX IX_LETTER_ANTRIAN_LETTER ON LETTER_ANTRIAN (LETTER_ID);
CREATE INDEX IX_LETTER_ANTRIAN_MEDIA  ON LETTER_ANTRIAN (JENIS_MEDIA_ID);

-- Daftar antrian selalu menyaring DELETE_DATE IS NULL lebih dulu.
CREATE INDEX IX_LETTER_ANTRIAN_HAPUS  ON LETTER_ANTRIAN (DELETE_DATE);

-- Catatan: UQ_LETTER_ANTRIAN_NOMOR sudah membuat index unik pada
-- NOMOR_ANTRIAN, dan itu dipakai saat aplikasi mencari nomor urut
-- terakhir bulan berjalan (LIKE 'KCKR_0826_%'). Tidak perlu index lagi.
--
-- Pencarian bebas di tab Daftar Antrian memakai UPPER(kolom) LIKE '%kata%'.
-- Wildcard di depan membuat index tidak terpakai, dan itu diterima:
-- pencarian selalu dibatasi rentang tanggal lebih dulu.


-- ---------------------------------------------------------------------
-- 5. Verifikasi
-- ---------------------------------------------------------------------
-- DESC LETTER_ANTRIAN;
-- SELECT INDEX_NAME, UNIQUENESS FROM USER_INDEXES WHERE TABLE_NAME = 'LETTER_ANTRIAN';
-- SELECT CONSTRAINT_NAME, CONSTRAINT_TYPE FROM USER_CONSTRAINTS WHERE TABLE_NAME = 'LETTER_ANTRIAN';
