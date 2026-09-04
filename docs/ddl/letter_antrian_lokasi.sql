-- =====================================================================
-- Pelacakan lokasi paket SSKCKR
--
-- Target: Oracle 11g (tanpa GENERATED AS IDENTITY, jadi sequence + trigger).
--
-- Konsep: lokasi bukan data yang diketik petugas, melainkan atribut dari PC.
-- Aplikasi membaca IP-nya sendiri saat login, mencari lokasinya di
-- LETTER_ANTRIAN_PC, lalu setiap scan memindahkan paket ke lokasi itu.
--
-- Isi berkas ini:
--   1. LETTER_ANTRIAN_LOKASI   master lokasi
--   2. LETTER_ANTRIAN_PC       pemetaan IP PC ke lokasi
--   3. LETTER_ANTRIAN.LOKASI_ID  kolom posisi terakhir
--   4. LETTER_ANTRIAN_MUTASI   riwayat perpindahan
--
-- Analisis lengkap dan keputusan yang masih terbuka ada di docs/SPEC.md.
-- =====================================================================


-- ---------------------------------------------------------------------
-- 1. Master lokasi
--
-- Dinonaktifkan lewat AKTIF, tidak pernah dihapus: penerimaan lama tetap
-- merujuk lokasi yang mungkin sudah tidak dipakai lagi.
-- ---------------------------------------------------------------------
CREATE TABLE LETTER_ANTRIAN_LOKASI (
    ID           NUMBER(19)                  NOT NULL,
    NAMA         VARCHAR2(100)               NOT NULL,
    KETERANGAN   VARCHAR2(255),
    AKTIF        NUMBER(1)   DEFAULT 1       NOT NULL,
    CREATE_DATE  DATE,
    UPDATE_DATE  DATE,

    CONSTRAINT PK_LA_LOKASI       PRIMARY KEY (ID),
    CONSTRAINT UQ_LA_LOKASI_NAMA  UNIQUE (NAMA),
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


-- ---------------------------------------------------------------------
-- 2. Pemetaan PC ke lokasi
--
-- Satu lokasi boleh punya beberapa PC, tapi satu IP hanya boleh menunjuk
-- satu lokasi - kalau tidak, aplikasi tidak bisa menentukan dia di mana.
--
-- PERHATIAN OPERASIONAL: seluruh mekanisme ini bergantung pada IP yang
-- tidak berubah. PC dengan DHCP biasa bisa berpindah IP dan lokasinya
-- ikut salah tanpa ada yang sadar. Tiap PC wajib memakai IP statis atau
-- DHCP reservation.
--
-- VARCHAR2(45) agar muat IPv6 kalau nanti dipakai.
-- ---------------------------------------------------------------------
CREATE TABLE LETTER_ANTRIAN_PC (
    ID            NUMBER(19)                 NOT NULL,
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
    CONSTRAINT UQ_LA_PC_IP     UNIQUE (IP_ADDRESS),
    CONSTRAINT CK_LA_PC_AKTIF  CHECK (AKTIF IN (0, 1)),
    CONSTRAINT FK_LA_PC_LOKASI FOREIGN KEY (LOKASI_ID)
        REFERENCES LETTER_ANTRIAN_LOKASI (ID)
);

-- Kolom foreign key wajib diindeks di Oracle.
CREATE INDEX IX_LA_PC_LOKASI ON LETTER_ANTRIAN_PC (LOKASI_ID);

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


-- ---------------------------------------------------------------------
-- 3. Posisi terakhir di LETTER_ANTRIAN
--
-- Menggantikan LOKASI_TRANSIT yang bertipe teks bebas. Kolom lama sengaja
-- TIDAK di-drop di sini: kalau sudah ada data terisi, pindahkan dulu.
-- Lihat bagian 5.
-- ---------------------------------------------------------------------
ALTER TABLE LETTER_ANTRIAN
    ADD (LOKASI_ID NUMBER(19));

ALTER TABLE LETTER_ANTRIAN
    ADD CONSTRAINT FK_LETTER_ANTRIAN_LOKASI
    FOREIGN KEY (LOKASI_ID) REFERENCES LETTER_ANTRIAN_LOKASI (ID);

CREATE INDEX IX_LETTER_ANTRIAN_LOKASI ON LETTER_ANTRIAN (LOKASI_ID);


-- ---------------------------------------------------------------------
-- 4. Riwayat perpindahan
--
-- WAJIB dibuat: aplikasi selalu mencatat riwayat, tidak ada lagi pilihan
-- untuk mematikannya.
--
-- Baris pertama sebuah paket ditulis saat penerimaan disimpan, dengan
-- LOKASI_ASAL_ID kosong - sebelum itu paket memang belum ada di mana pun.
-- Perpindahan berikutnya menambah baris dengan asal dan tujuan terisi.
--
-- Riwayat berguna saat paket sulit ditelusuri: bisa dilihat kapan berpindah,
-- dari mana ke mana, dan lewat PC siapa.
--
-- NOMOR_DUS sengaja disiapkan tapi boleh NULL. NULL berarti perpindahan
-- berlaku untuk seluruh penerimaan - itu perilaku sekarang, karena semua
-- label dalam satu penerimaan memuat barcode yang sama. Kalau nanti
-- diputuskan pelacakan per dus, barcode harus memuat nomor dus juga dan
-- kolom ini mulai terisi, tanpa perlu mengubah struktur tabel.
-- ---------------------------------------------------------------------
CREATE TABLE LETTER_ANTRIAN_MUTASI (
    ID                 NUMBER(19)            NOT NULL,
    LETTER_ANTRIAN_ID  NUMBER(19)            NOT NULL,

    LOKASI_ASAL_ID     NUMBER(19),
    LOKASI_TUJUAN_ID   NUMBER(19)            NOT NULL,

    NOMOR_DUS          NUMBER(4),

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

-- Penelusuran selalu dimulai dari satu penerimaan, diurutkan menurut waktu.
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

    IF :NEW.WAKTU IS NULL       THEN :NEW.WAKTU := SYSDATE;       END IF;
    IF :NEW.CREATE_DATE IS NULL THEN :NEW.CREATE_DATE := SYSDATE; END IF;
END;
/


-- ---------------------------------------------------------------------
-- 5. Data awal dan pemindahan data lama
-- ---------------------------------------------------------------------

-- Sesuaikan dengan lokasi yang benar-benar ada.
INSERT INTO LETTER_ANTRIAN_LOKASI (NAMA, KETERANGAN)
     VALUES ('Lobi Gedung E', 'Pos penerimaan paket');

INSERT INTO LETTER_ANTRIAN_LOKASI (NAMA, KETERANGAN)
     VALUES ('Ruang Transit 8E', 'Ruang transit lantai 8');

-- Contoh pemetaan PC. Ganti IP-nya dengan IP statis PC yang sebenarnya.
-- INSERT INTO LETTER_ANTRIAN_PC (IP_ADDRESS, LOKASI_ID, KETERANGAN)
--      VALUES ('192.168.10.50',
--              (SELECT ID FROM LETTER_ANTRIAN_LOKASI WHERE NAMA = 'Lobi Gedung E'),
--              'PC pos satpam Gedung E');

-- Kalau LOKASI_TRANSIT lama sudah ada isinya, cocokkan dulu ke master
-- sebelum kolom lamanya dibuang:
-- UPDATE LETTER_ANTRIAN t
--    SET t.LOKASI_ID = (SELECT l.ID FROM LETTER_ANTRIAN_LOKASI l
--                        WHERE UPPER(l.NAMA) = UPPER(TRIM(t.LOKASI_TRANSIT)))
--  WHERE t.LOKASI_TRANSIT IS NOT NULL;
--
-- Periksa sisa yang tidak cocok sebelum melanjutkan:
-- SELECT DISTINCT LOKASI_TRANSIT FROM LETTER_ANTRIAN
--  WHERE LOKASI_TRANSIT IS NOT NULL AND LOKASI_ID IS NULL;
--
-- Baru setelah itu:
-- ALTER TABLE LETTER_ANTRIAN DROP COLUMN LOKASI_TRANSIT;

COMMIT;


-- ---------------------------------------------------------------------
-- 6. Verifikasi
-- ---------------------------------------------------------------------
-- SELECT p.IP_ADDRESS, l.NAMA, p.AKTIF
--   FROM LETTER_ANTRIAN_PC p
--   JOIN LETTER_ANTRIAN_LOKASI l ON p.LOKASI_ID = l.ID;


-- ---------------------------------------------------------------------
-- Pembatalan, bila diperlukan
-- ---------------------------------------------------------------------
-- DROP TABLE LETTER_ANTRIAN_MUTASI;
-- DROP SEQUENCE SEQ_LA_MUTASI;
-- DROP INDEX IX_LETTER_ANTRIAN_LOKASI;
-- ALTER TABLE LETTER_ANTRIAN DROP CONSTRAINT FK_LETTER_ANTRIAN_LOKASI;
-- ALTER TABLE LETTER_ANTRIAN DROP COLUMN LOKASI_ID;
-- DROP TABLE LETTER_ANTRIAN_PC;
-- DROP SEQUENCE SEQ_LA_PC;
-- DROP TABLE LETTER_ANTRIAN_LOKASI;
-- DROP SEQUENCE SEQ_LA_LOKASI;
