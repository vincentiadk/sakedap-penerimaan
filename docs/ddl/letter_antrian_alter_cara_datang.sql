-- =====================================================================
-- LETTER_ANTRIAN - penyesuaian untuk paket yang datang langsung
--
-- Untuk tabel yang SUDAH dibuat dengan DDL versi pertama. Kalau tabelnya
-- belum ada, pakai letter_antrian.sql saja - di sana semuanya sudah termasuk.
--
-- Perubahannya: paket bisa diserahkan langsung ke pos satpam tanpa lewat
-- jasa pengiriman, dan paket seperti itu tidak punya nomor resi.
-- =====================================================================


-- ---------------------------------------------------------------------
-- 1. Kolom cara datang
--
-- DEFAULT 'ekspedisi' membuat baris yang sudah ada langsung terisi, jadi
-- NOT NULL aman ditambahkan dalam satu perintah.
-- ---------------------------------------------------------------------
ALTER TABLE LETTER_ANTRIAN
    ADD (CARA_DATANG VARCHAR2(10) DEFAULT 'ekspedisi' NOT NULL);

ALTER TABLE LETTER_ANTRIAN
    ADD CONSTRAINT CK_LETTER_ANTRIAN_CARA
    CHECK (CARA_DATANG IN ('ekspedisi', 'langsung'));


-- ---------------------------------------------------------------------
-- 2. RECEIPT_NO boleh kosong
--
-- Paket yang diserahkan langsung tidak punya nomor resi sama sekali.
-- ---------------------------------------------------------------------
ALTER TABLE LETTER_ANTRIAN
    MODIFY (RECEIPT_NO NULL);


-- ---------------------------------------------------------------------
-- 3. Resi tetap wajib untuk kiriman ekspedisi
--
-- Longgarnya RECEIPT_NO hanya berlaku untuk paket yang datang langsung.
-- ---------------------------------------------------------------------
ALTER TABLE LETTER_ANTRIAN
    ADD CONSTRAINT CK_LETTER_ANTRIAN_RESI
    CHECK (CARA_DATANG = 'langsung' OR RECEIPT_NO IS NOT NULL);


-- ---------------------------------------------------------------------
-- 4. Verifikasi
-- ---------------------------------------------------------------------
-- DESC LETTER_ANTRIAN;
--
-- SELECT CONSTRAINT_NAME, CONSTRAINT_TYPE, SEARCH_CONDITION
--   FROM USER_CONSTRAINTS
--  WHERE TABLE_NAME = 'LETTER_ANTRIAN';
--
-- Harus muncul CK_LETTER_ANTRIAN_CARA dan CK_LETTER_ANTRIAN_RESI,
-- dan kolom RECEIPT_NO sudah Nullable.


-- ---------------------------------------------------------------------
-- Pembatalan, bila diperlukan
-- ---------------------------------------------------------------------
-- ALTER TABLE LETTER_ANTRIAN DROP CONSTRAINT CK_LETTER_ANTRIAN_RESI;
-- ALTER TABLE LETTER_ANTRIAN DROP CONSTRAINT CK_LETTER_ANTRIAN_CARA;
-- ALTER TABLE LETTER_ANTRIAN DROP COLUMN CARA_DATANG;
-- ALTER TABLE LETTER_ANTRIAN MODIFY (RECEIPT_NO NOT NULL);


-- =====================================================================
-- Tambahan berikutnya: jumlah dus
--
-- Satu kiriman bisa terdiri dari beberapa kardus, dan tiap kardus perlu
-- labelnya sendiri bertanda "DUS n DARI N".
-- =====================================================================

ALTER TABLE LETTER_ANTRIAN
    ADD (JUMLAH_DUS NUMBER(4) DEFAULT 1 NOT NULL);

ALTER TABLE LETTER_ANTRIAN
    ADD CONSTRAINT CK_LETTER_ANTRIAN_DUS
    CHECK (JUMLAH_DUS >= 1);

-- Pembatalan:
-- ALTER TABLE LETTER_ANTRIAN DROP CONSTRAINT CK_LETTER_ANTRIAN_DUS;
-- ALTER TABLE LETTER_ANTRIAN DROP COLUMN JUMLAH_DUS;


-- =====================================================================
-- Tambahan berikutnya: jenis media
--
-- Mengacu ke tabel referensi COLLECTIONMEDIA. Boleh NULL - satpam tidak
-- membuka paket, jadi jenis medianya kadang memang belum bisa ditentukan
-- di pos penerimaan dan baru dilengkapi Pegawai KCKR.
-- =====================================================================

ALTER TABLE LETTER_ANTRIAN
    ADD (JENIS_MEDIA_ID NUMBER(19));

ALTER TABLE LETTER_ANTRIAN
    ADD CONSTRAINT FK_LETTER_ANTRIAN_MEDIA
    FOREIGN KEY (JENIS_MEDIA_ID) REFERENCES COLLECTIONMEDIA (ID);

-- Kolom foreign key wajib diindeks di Oracle.
CREATE INDEX IX_LETTER_ANTRIAN_MEDIA ON LETTER_ANTRIAN (JENIS_MEDIA_ID);

-- Pembatalan:
-- DROP INDEX IX_LETTER_ANTRIAN_MEDIA;
-- ALTER TABLE LETTER_ANTRIAN DROP CONSTRAINT FK_LETTER_ANTRIAN_MEDIA;
-- ALTER TABLE LETTER_ANTRIAN DROP COLUMN JENIS_MEDIA_ID;


-- =====================================================================
-- Tambahan berikutnya: penanda hapus
--
-- Hapus tidak menghilangkan baris. Nomor antrian yang sudah tercetak dan
-- tertempel di kardus harus tetap bisa ditelusuri, jadi yang dilakukan
-- adalah mengisi DELETE_DATE. Baris yang DELETE_DATE-nya terisi dianggap
-- terhapus: tidak muncul di daftar maupun pengecekan resi ganda.
--
-- Batas hapus dan ubah adalah 24 jam sejak CREATE_DATE, dijaga aplikasi.
-- =====================================================================

ALTER TABLE LETTER_ANTRIAN
    ADD (DELETE_DATE DATE);

-- Daftar antrian selalu menyaring DELETE_DATE IS NULL lebih dulu.
CREATE INDEX IX_LETTER_ANTRIAN_HAPUS ON LETTER_ANTRIAN (DELETE_DATE);

-- Pembatalan:
-- DROP INDEX IX_LETTER_ANTRIAN_HAPUS;
-- ALTER TABLE LETTER_ANTRIAN DROP COLUMN DELETE_DATE;
