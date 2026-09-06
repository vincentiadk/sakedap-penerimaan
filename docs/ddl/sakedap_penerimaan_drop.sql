-- =====================================================================
-- SAKEDAP Penerimaan - HAPUS SEMUA objek buatan sakedap_penerimaan.sql
--
-- Untuk mengulang pemasangan dari nol. Setelah ini, jalankan
-- sakedap_penerimaan.sql lagi.
--
--
-- >>> SELURUH DATA PENERIMAAN HILANG PERMANEN. <<<
--
-- Termasuk nomor antrian yang sudah tercetak dan tertempel di kardus -
-- label itu jadi menunjuk baris yang tidak ada lagi. Kalau di database
-- sudah ada penerimaan sungguhan, cadangkan dulu:
--
--   CREATE TABLE LETTER_ANTRIAN_BACKUP AS SELECT * FROM LETTER_ANTRIAN;
--   CREATE TABLE LA_MUTASI_BACKUP      AS SELECT * FROM LETTER_ANTRIAN_MUTASI;
--
--
-- YANG TIDAK DISENTUH sama sekali - tabel inti INLIS:
--
--   LETTER, LETTER_DETAIL, PENERBIT, JASA_PENGIRIMAN, COLLECTIONMEDIAS
--
-- Aplikasi tidak pernah menulis ke sana, jadi tidak ada yang perlu dibersihkan.
--
--
-- CATATAN TEKNIS
--
-- Trigger dan index ikut terhapus bersama tabelnya - tidak perlu DROP
-- sendiri. Sequence TIDAK ikut, jadi harus dibuang terpisah; kalau
-- terlewat, sakedap_penerimaan.sql akan gagal di CREATE SEQUENCE dengan
-- ORA-00955 (name is already being used).
--
-- CASCADE CONSTRAINTS dipakai supaya foreign key antar tabel ini tidak
-- menghalangi, termasuk self-FK KIRIMAN_ID di LETTER_ANTRIAN. PURGE
-- melewati recycle bin supaya benar-benar bersih.
-- =====================================================================


-- ---------------------------------------------------------------------
-- Cara 1 - AMAN, bisa dijalankan berulang (disarankan)
--
-- Objek yang memang belum ada dilewati diam-diam, jadi blok ini tetap
-- selesai walau pemasangan sebelumnya gagal di tengah dan hanya sebagian
-- tabel yang sempat terbentuk.
--
-- ORA-00942 = table or view does not exist
-- ORA-02289 = sequence does not exist
-- ---------------------------------------------------------------------
BEGIN
    FOR t IN (
        SELECT 'LETTER_ANTRIAN_MUTASI' AS nama FROM DUAL UNION ALL
        SELECT 'LETTER_ANTRIAN_PC'            FROM DUAL UNION ALL
        SELECT 'LETTER_ANTRIAN'               FROM DUAL UNION ALL
        SELECT 'LETTER_ANTRIAN_LOKASI'        FROM DUAL
    ) LOOP
        BEGIN
            EXECUTE IMMEDIATE 'DROP TABLE ' || t.nama || ' CASCADE CONSTRAINTS PURGE';
            DBMS_OUTPUT.PUT_LINE('tabel dibuang   : ' || t.nama);
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE = -942 THEN
                    DBMS_OUTPUT.PUT_LINE('tabel tidak ada : ' || t.nama);
                ELSE
                    RAISE;
                END IF;
        END;
    END LOOP;

    FOR s IN (
        SELECT 'SEQ_LA_MUTASI' AS nama FROM DUAL UNION ALL
        SELECT 'SEQ_LA_PC'            FROM DUAL UNION ALL
        SELECT 'SEQ_LETTER_ANTRIAN'   FROM DUAL UNION ALL
        SELECT 'SEQ_LA_LOKASI'        FROM DUAL
    ) LOOP
        BEGIN
            EXECUTE IMMEDIATE 'DROP SEQUENCE ' || s.nama;
            DBMS_OUTPUT.PUT_LINE('sequence dibuang: ' || s.nama);
        EXCEPTION
            WHEN OTHERS THEN
                IF SQLCODE = -2289 THEN
                    DBMS_OUTPUT.PUT_LINE('sequence tdk ada: ' || s.nama);
                ELSE
                    RAISE;
                END IF;
        END;
    END LOOP;
END;
/

-- Aktifkan dulu kalau ingin melihat keluaran DBMS_OUTPUT di atas:
--   SQL*Plus / SQLcl : SET SERVEROUTPUT ON
--   SQL Developer    : View > DBMS Output, lalu pilih koneksinya


-- ---------------------------------------------------------------------
-- Cara 2 - perintah polos, bagi yang lebih suka eksplisit
--
-- Urutannya kebalikan dari pembuatan. Perintah untuk objek yang tidak ada
-- akan gagal dengan ORA-00942 / ORA-02289 - abaikan saja, lanjut ke
-- perintah berikutnya.
-- ---------------------------------------------------------------------
-- DROP TABLE LETTER_ANTRIAN_MUTASI CASCADE CONSTRAINTS PURGE;
-- DROP TABLE LETTER_ANTRIAN_PC     CASCADE CONSTRAINTS PURGE;
-- DROP TABLE LETTER_ANTRIAN        CASCADE CONSTRAINTS PURGE;
-- DROP TABLE LETTER_ANTRIAN_LOKASI CASCADE CONSTRAINTS PURGE;
--
-- DROP SEQUENCE SEQ_LA_MUTASI;
-- DROP SEQUENCE SEQ_LA_PC;
-- DROP SEQUENCE SEQ_LETTER_ANTRIAN;
-- DROP SEQUENCE SEQ_LA_LOKASI;


-- ---------------------------------------------------------------------
-- VERIFIKASI - ketiganya harus mengembalikan 0 baris
-- ---------------------------------------------------------------------

-- Tabel:
-- SELECT TABLE_NAME FROM USER_TABLES
--  WHERE TABLE_NAME LIKE 'LETTER_ANTRIAN%';

-- Sequence:
-- SELECT SEQUENCE_NAME FROM USER_SEQUENCES
--  WHERE SEQUENCE_NAME IN ('SEQ_LETTER_ANTRIAN', 'SEQ_LA_LOKASI',
--                          'SEQ_LA_PC', 'SEQ_LA_MUTASI');

-- Trigger - mestinya sudah ikut terhapus bersama tabelnya. Kalau masih ada
-- yang tersisa, buang manual dengan DROP TRIGGER <nama>:
-- SELECT TRIGGER_NAME, STATUS FROM USER_TRIGGERS
--  WHERE TRIGGER_NAME LIKE 'TRG_LA%'
--     OR TRIGGER_NAME LIKE 'TRG_LETTER_ANTRIAN%';

-- Recycle bin harus kosong dari tabel ini juga (PURGE sudah mengurusnya):
-- SELECT ORIGINAL_NAME, OBJECT_NAME FROM RECYCLEBIN
--  WHERE ORIGINAL_NAME LIKE 'LETTER_ANTRIAN%';


-- ---------------------------------------------------------------------
-- Setelah semuanya bersih, pasang ulang:
--
--   @sakedap_penerimaan.sql
--
-- Jangan lupa ganti IP di bagian 6 skrip itu dengan IP PC pos satpam
-- yang sebenarnya.
-- ---------------------------------------------------------------------
