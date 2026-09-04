-- =====================================================================
-- LETTER_ANTRIAN_DUS - posisi tiap kardus
--
-- Target: Oracle 11g.
--
-- Kenapa per kardus, bukan per penerimaan:
--
-- Satu surat penyerahan bisa berisi 150 judul yang dipacking jadi 3 kardus
-- dengan satu nomor resi. Ketiga kardus itu BISA TERPISAH lokasinya -
-- misalnya dua naik duluan, satu menyusul.
--
-- Kalau posisi hanya disimpan di tingkat penerimaan, men-scan satu kardus
-- akan menandai ketiganya sudah pindah. Datanya jadi salah persis di titik
-- yang paling dibutuhkan untuk temu kembali.
--
-- Karena itu barcode di label memuat nomor dus: KCKR_0826_1335-2.
-- Teks besar di label tetap nomor antrian saja.
--
-- Jalankan SETELAH letter_antrian_lokasi.sql.
-- =====================================================================


-- ---------------------------------------------------------------------
-- 1. Tabel
-- ---------------------------------------------------------------------
CREATE TABLE LETTER_ANTRIAN_DUS (
    ID                 NUMBER(19)   NOT NULL,
    LETTER_ANTRIAN_ID  NUMBER(19)   NOT NULL,

    -- 1 sampai JUMLAH_DUS di LETTER_ANTRIAN.
    NOMOR_DUS          NUMBER(4)    NOT NULL,

    -- Posisi kardus ini. NULL berarti belum pernah ditempatkan.
    LOKASI_ID          NUMBER(19),

    CREATE_DATE        DATE,
    UPDATE_DATE        DATE,

    CONSTRAINT PK_LA_DUS        PRIMARY KEY (ID),

    -- Satu kardus hanya boleh punya satu baris. Ini juga yang menjaga
    -- agar penambahan dus susulan tidak menghasilkan nomor kembar.
    CONSTRAINT UQ_LA_DUS_NOMOR  UNIQUE (LETTER_ANTRIAN_ID, NOMOR_DUS),

    CONSTRAINT CK_LA_DUS_NOMOR  CHECK (NOMOR_DUS >= 1),

    CONSTRAINT FK_LA_DUS_ANTRIAN FOREIGN KEY (LETTER_ANTRIAN_ID)
        REFERENCES LETTER_ANTRIAN (ID),

    CONSTRAINT FK_LA_DUS_LOKASI  FOREIGN KEY (LOKASI_ID)
        REFERENCES LETTER_ANTRIAN_LOKASI (ID)
);

-- Kolom foreign key wajib diindeks di Oracle. Index unik dari
-- UQ_LA_DUS_NOMOR sudah menutupi pencarian per penerimaan.
CREATE INDEX IX_LA_DUS_LOKASI ON LETTER_ANTRIAN_DUS (LOKASI_ID);

CREATE SEQUENCE SEQ_LA_DUS START WITH 1 INCREMENT BY 1 NOCACHE NOCYCLE;

CREATE OR REPLACE TRIGGER TRG_LA_DUS_BI
    BEFORE INSERT ON LETTER_ANTRIAN_DUS
    FOR EACH ROW
BEGIN
    IF :NEW.ID IS NULL THEN
        SELECT SEQ_LA_DUS.NEXTVAL INTO :NEW.ID FROM DUAL;
    END IF;

    IF :NEW.CREATE_DATE IS NULL THEN :NEW.CREATE_DATE := SYSDATE; END IF;
    IF :NEW.UPDATE_DATE IS NULL THEN :NEW.UPDATE_DATE := SYSDATE; END IF;
END;
/

CREATE OR REPLACE TRIGGER TRG_LA_DUS_BU
    BEFORE UPDATE ON LETTER_ANTRIAN_DUS
    FOR EACH ROW
BEGIN
    :NEW.UPDATE_DATE := SYSDATE;
END;
/


-- ---------------------------------------------------------------------
-- 2. Data untuk penerimaan yang sudah terlanjur ada
--
-- Membuat satu baris dus per kardus, memakai posisi penerimaannya sebagai
-- posisi awal. Aman dijalankan berulang - yang sudah ada tidak diduplikasi.
-- ---------------------------------------------------------------------
INSERT INTO LETTER_ANTRIAN_DUS (LETTER_ANTRIAN_ID, NOMOR_DUS, LOKASI_ID)
SELECT t.ID, n.NOMOR, t.LOKASI_ID
  FROM LETTER_ANTRIAN t
  JOIN (SELECT LEVEL AS NOMOR FROM DUAL CONNECT BY LEVEL <= 999) n
    ON n.NOMOR <= t.JUMLAH_DUS
 WHERE t.DELETE_DATE IS NULL
   AND NOT EXISTS (SELECT 1 FROM LETTER_ANTRIAN_DUS d
                    WHERE d.LETTER_ANTRIAN_ID = t.ID
                      AND d.NOMOR_DUS = n.NOMOR);

COMMIT;


-- ---------------------------------------------------------------------
-- 3. Verifikasi
-- ---------------------------------------------------------------------
-- Jumlah baris dus harus sama dengan JUMLAH_DUS tiap penerimaan:
--
-- SELECT t.NOMOR_ANTRIAN, t.JUMLAH_DUS, COUNT(d.ID) AS BARIS_DUS
--   FROM LETTER_ANTRIAN t
--   LEFT JOIN LETTER_ANTRIAN_DUS d ON d.LETTER_ANTRIAN_ID = t.ID
--  WHERE t.DELETE_DATE IS NULL
--  GROUP BY t.NOMOR_ANTRIAN, t.JUMLAH_DUS
-- HAVING COUNT(d.ID) <> t.JUMLAH_DUS;
--
-- Paket yang kardusnya tersebar di beberapa lokasi:
--
-- SELECT t.NOMOR_ANTRIAN, COUNT(DISTINCT d.LOKASI_ID) AS JUMLAH_LOKASI
--   FROM LETTER_ANTRIAN t
--   JOIN LETTER_ANTRIAN_DUS d ON d.LETTER_ANTRIAN_ID = t.ID
--  WHERE t.DELETE_DATE IS NULL
--  GROUP BY t.NOMOR_ANTRIAN
-- HAVING COUNT(DISTINCT d.LOKASI_ID) > 1;


-- ---------------------------------------------------------------------
-- Pembatalan, bila diperlukan
-- ---------------------------------------------------------------------
-- DROP TABLE LETTER_ANTRIAN_DUS;
-- DROP SEQUENCE SEQ_LA_DUS;
