#nullable disable

namespace Penerimaan.App;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        tabs = new TabControl();
        tabPenerimaan = new TabPage();
        tabDaftar = new TabPage();
        tabPindah = new TabPage();
        lblLokasiPindah = new Label();
        txtScanPindah = new TextBox();
        lblHasilPindah = new Label();
        lblJumlahPindah = new Label();

        txtScan = new TextBox();
        btnCari = new Button();
        btnTanpaResi = new Button();
        grpPaket = new GroupBox();
        lblResi = new Label();
        txtResi = new TextBox();
        lblEkspedisi = new Label();
        cboEkspedisi = new ComboBox();
        lblMedia = new Label();
        cboMedia = new ComboBox();
        lblPelaksanaSerah = new Label();
        txtPelaksanaSerah = new TextBox();
        lblJudul = new Label();
        numJudul = new NumericUpDown();
        lblEks = new Label();
        numEks = new NumericUpDown();
        lblDus = new Label();
        numDus = new NumericUpDown();
        lblPetugas = new Label();
        txtPetugas = new TextBox();
        lblTerima = new Label();
        dtpTerima = new DateTimePicker();
        lblAntrian = new Label();
        txtAntrian = new TextBox();
        lblSumber = new Label();
        btnSimpan = new Button();
        btnCetak = new Button();
        btnBaru = new Button();

        lblDari = new Label();
        dtpDari = new DateTimePicker();
        lblSampai = new Label();
        dtpSampai = new DateTimePicker();
        txtCari = new TextBox();
        btnMuat = new Button();
        lvDaftar = new ListView();
        colNo = new ColumnHeader();
        colTanggal = new ColumnHeader();
        colPetugas = new ColumnHeader();
        colAntrian = new ColumnHeader();
        colEkspedisi = new ColumnHeader();
        colMedia = new ColumnHeader();
        colLokasi = new ColumnHeader();
        colResi = new ColumnHeader();
        btnCetakUlang = new Button();
        btnUnduh = new Button();
        btnHapus = new Button();

        lblPrinter = new Label();
        cboPrinter = new ComboBox();
        btnKirimTertunda = new Button();
        lblAkun = new Label();
        lblLokasi = new Label();
        statusStrip = new StatusStrip();
        lblStatus = new ToolStripStatusLabel();
        lblOutbox = new ToolStripStatusLabel();

        tabs.SuspendLayout();
        tabPenerimaan.SuspendLayout();
        tabDaftar.SuspendLayout();
        tabPindah.SuspendLayout();
        grpPaket.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numJudul).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numEks).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numDus).BeginInit();
        statusStrip.SuspendLayout();
        SuspendLayout();

        // ---------- TabControl ----------
        tabs.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tabs.Controls.Add(tabPenerimaan);
        tabs.Controls.Add(tabDaftar);
        tabs.Controls.Add(tabPindah);
        tabs.Location = new Point(12, 12);
        tabs.Name = "tabs";
        tabs.SelectedIndex = 0;
        tabs.Size = new Size(876, 470);
        tabs.TabIndex = 0;

        tabPenerimaan.Controls.Add(btnBaru);
        tabPenerimaan.Controls.Add(btnCetak);
        tabPenerimaan.Controls.Add(btnSimpan);
        tabPenerimaan.Controls.Add(grpPaket);
        tabPenerimaan.Controls.Add(btnTanpaResi);
        tabPenerimaan.Controls.Add(btnCari);
        tabPenerimaan.Controls.Add(txtScan);
        tabPenerimaan.Location = new Point(4, 29);
        tabPenerimaan.Name = "tabPenerimaan";
        tabPenerimaan.Padding = new Padding(3);
        tabPenerimaan.Size = new Size(868, 437);
        tabPenerimaan.TabIndex = 0;
        tabPenerimaan.Text = "Penerimaan Paket";
        tabPenerimaan.UseVisualStyleBackColor = true;

        tabDaftar.Controls.Add(btnHapus);
        tabDaftar.Controls.Add(btnUnduh);
        tabDaftar.Controls.Add(btnCetakUlang);
        tabDaftar.Controls.Add(lvDaftar);
        tabDaftar.Controls.Add(btnMuat);
        tabDaftar.Controls.Add(txtCari);
        tabDaftar.Controls.Add(dtpSampai);
        tabDaftar.Controls.Add(lblSampai);
        tabDaftar.Controls.Add(dtpDari);
        tabDaftar.Controls.Add(lblDari);
        tabDaftar.Location = new Point(4, 29);
        tabDaftar.Name = "tabDaftar";
        tabDaftar.Padding = new Padding(3);
        tabDaftar.Size = new Size(868, 437);
        tabDaftar.TabIndex = 1;
        tabDaftar.Text = "Daftar Antrian";
        tabDaftar.UseVisualStyleBackColor = true;

        // ---------- Tab 3: Pindah Lokasi ----------
        tabPindah.Controls.Add(lblJumlahPindah);
        tabPindah.Controls.Add(lblHasilPindah);
        tabPindah.Controls.Add(txtScanPindah);
        tabPindah.Controls.Add(lblLokasiPindah);
        tabPindah.Location = new Point(4, 29);
        tabPindah.Name = "tabPindah";
        tabPindah.Padding = new Padding(3);
        tabPindah.Size = new Size(868, 437);
        tabPindah.TabIndex = 2;
        tabPindah.Text = "Pindah Lokasi";
        tabPindah.UseVisualStyleBackColor = true;

        lblLokasiPindah.Font = new Font("Segoe UI", 11F);
        lblLokasiPindah.Location = new Point(24, 24);
        lblLokasiPindah.Name = "lblLokasiPindah";
        lblLokasiPindah.Size = new Size(816, 32);
        lblLokasiPindah.TextAlign = ContentAlignment.MiddleCenter;

        // Kotak scan dibuat besar: petugas men-scan sambil berdiri memegang
        // kardus, bukan duduk menatap layar dari dekat.
        txtScanPindah.Font = new Font("Segoe UI", 16F);
        txtScanPindah.Location = new Point(154, 68);
        txtScanPindah.Name = "txtScanPindah";
        txtScanPindah.PlaceholderText = "Scan barcode label di sini";
        txtScanPindah.Size = new Size(556, 39);
        txtScanPindah.TabIndex = 0;
        txtScanPindah.TextAlign = HorizontalAlignment.Center;
        txtScanPindah.KeyDown += txtScanPindah_KeyDown;

        lblHasilPindah.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        lblHasilPindah.Location = new Point(24, 136);
        lblHasilPindah.Name = "lblHasilPindah";
        lblHasilPindah.Size = new Size(816, 210);
        lblHasilPindah.TextAlign = ContentAlignment.MiddleCenter;

        lblJumlahPindah.ForeColor = SystemColors.GrayText;
        lblJumlahPindah.Location = new Point(24, 372);
        lblJumlahPindah.Name = "lblJumlahPindah";
        lblJumlahPindah.Size = new Size(816, 28);
        lblJumlahPindah.Text = "Dipindahkan sesi ini: 0";
        lblJumlahPindah.TextAlign = ContentAlignment.MiddleCenter;

        // ---------- Tab 1: Penerimaan ----------
        txtScan.Font = new Font("Segoe UI", 12F);
        txtScan.Location = new Point(16, 16);
        txtScan.Name = "txtScan";
        txtScan.PlaceholderText = "Scan barcode untuk mencari nomor resi";
        txtScan.Size = new Size(566, 29);
        txtScan.TabIndex = 0;
        txtScan.KeyDown += txtScan_KeyDown;

        btnCari.Location = new Point(594, 15);
        btnCari.Name = "btnCari";
        btnCari.Size = new Size(100, 31);
        btnCari.TabIndex = 1;
        btnCari.Text = "CARI";
        btnCari.UseVisualStyleBackColor = true;
        btnCari.Click += btnCari_Click;

        // Jalan masuk untuk paket yang diserahkan langsung: tidak ada resi
        // untuk discan, jadi tidak bisa lewat pencarian.
        btnTanpaResi.Location = new Point(700, 15);
        btnTanpaResi.Name = "btnTanpaResi";
        btnTanpaResi.Size = new Size(128, 31);
        btnTanpaResi.TabIndex = 2;
        btnTanpaResi.Text = "TANPA RESI";
        btnTanpaResi.UseVisualStyleBackColor = true;
        btnTanpaResi.Click += btnTanpaResi_Click;

        grpPaket.Controls.Add(lblResi);
        grpPaket.Controls.Add(txtResi);
        grpPaket.Controls.Add(lblEkspedisi);
        grpPaket.Controls.Add(cboEkspedisi);
        grpPaket.Controls.Add(lblMedia);
        grpPaket.Controls.Add(cboMedia);
        grpPaket.Controls.Add(lblPelaksanaSerah);
        grpPaket.Controls.Add(txtPelaksanaSerah);
        grpPaket.Controls.Add(lblJudul);
        grpPaket.Controls.Add(numJudul);
        grpPaket.Controls.Add(lblEks);
        grpPaket.Controls.Add(numEks);
        grpPaket.Controls.Add(lblDus);
        grpPaket.Controls.Add(numDus);
        grpPaket.Controls.Add(lblPetugas);
        grpPaket.Controls.Add(txtPetugas);
        grpPaket.Controls.Add(lblTerima);
        grpPaket.Controls.Add(dtpTerima);
        grpPaket.Controls.Add(lblAntrian);
        grpPaket.Controls.Add(txtAntrian);
        grpPaket.Controls.Add(lblSumber);
        grpPaket.Location = new Point(16, 60);
        grpPaket.Name = "grpPaket";
        grpPaket.Size = new Size(812, 300);
        grpPaket.TabIndex = 2;
        grpPaket.TabStop = false;
        grpPaket.Text = "Data Penerimaan";

        lblResi.AutoSize = true;
        lblResi.Location = new Point(20, 36);
        lblResi.Name = "lblResi";
        lblResi.Size = new Size(40, 20);
        lblResi.Text = "Resi *";

        txtResi.Location = new Point(200, 33);
        txtResi.Name = "txtResi";
        txtResi.Size = new Size(260, 27);
        txtResi.TabIndex = 1;

        lblEkspedisi.AutoSize = true;
        lblEkspedisi.Location = new Point(20, 72);
        lblEkspedisi.Name = "lblEkspedisi";
        lblEkspedisi.Size = new Size(78, 20);
        lblEkspedisi.Text = "Ekspedisi *";

        cboEkspedisi.Location = new Point(200, 69);
        cboEkspedisi.Name = "cboEkspedisi";
        cboEkspedisi.Size = new Size(260, 28);
        cboEkspedisi.TabIndex = 3;

        // Diisi dari tabel referensi collectionmedia saat aplikasi dibuka.
        lblMedia.AutoSize = true;
        lblMedia.Location = new Point(490, 72);
        lblMedia.Name = "lblMedia";
        lblMedia.Size = new Size(82, 20);
        lblMedia.Text = "Jenis Media *";

        cboMedia.DropDownStyle = ComboBoxStyle.DropDownList;
        cboMedia.Location = new Point(580, 69);
        cboMedia.Name = "cboMedia";
        cboMedia.Size = new Size(210, 28);
        cboMedia.TabIndex = 4;

        lblPelaksanaSerah.AutoSize = true;
        lblPelaksanaSerah.Location = new Point(20, 108);
        lblPelaksanaSerah.Name = "lblPelaksanaSerah";
        lblPelaksanaSerah.Size = new Size(140, 20);
        lblPelaksanaSerah.Text = "Nama Pelaksana Serah";

        txtPelaksanaSerah.Location = new Point(200, 105);
        txtPelaksanaSerah.Name = "txtPelaksanaSerah";
        txtPelaksanaSerah.Size = new Size(400, 27);
        txtPelaksanaSerah.TabIndex = 5;

        lblJudul.AutoSize = true;
        lblJudul.Location = new Point(20, 144);
        lblJudul.Name = "lblJudul";
        lblJudul.Size = new Size(88, 20);
        lblJudul.Text = "Jumlah Judul";

        numJudul.Location = new Point(200, 142);
        numJudul.Maximum = 99999;
        numJudul.Name = "numJudul";
        numJudul.Size = new Size(100, 27);
        numJudul.TabIndex = 7;

        lblEks.AutoSize = true;
        lblEks.Location = new Point(330, 144);
        lblEks.Name = "lblEks";
        lblEks.Size = new Size(78, 20);
        lblEks.Text = "Jumlah Eks";

        numEks.Location = new Point(430, 142);
        numEks.Maximum = 99999;
        numEks.Name = "numEks";
        numEks.Size = new Size(100, 27);
        numEks.TabIndex = 9;

        // Menentukan berapa label dicetak - satu label per dus.
        lblDus.AutoSize = true;
        lblDus.Location = new Point(560, 144);
        lblDus.Name = "lblDus";
        lblDus.Size = new Size(82, 20);
        lblDus.Text = "Jumlah Dus *";

        numDus.Location = new Point(660, 142);
        numDus.Maximum = 999;
        numDus.Minimum = 1;
        numDus.Name = "numDus";
        numDus.Size = new Size(80, 27);
        numDus.TabIndex = 10;
        numDus.Value = 1;

        lblPetugas.AutoSize = true;
        lblPetugas.Location = new Point(20, 182);
        lblPetugas.Name = "lblPetugas";
        lblPetugas.Size = new Size(170, 20);
        lblPetugas.Text = "Nama Petugas Penerima *";

        txtPetugas.Location = new Point(200, 179);
        txtPetugas.Name = "txtPetugas";
        txtPetugas.Size = new Size(260, 27);
        txtPetugas.TabIndex = 11;

        lblTerima.AutoSize = true;
        lblTerima.Location = new Point(20, 218);
        lblTerima.Name = "lblTerima";
        lblTerima.Size = new Size(140, 20);
        lblTerima.Text = "Tanggal Terima Paket *";

        dtpTerima.CustomFormat = "dd/MM/yyyy HH:mm";
        dtpTerima.Format = DateTimePickerFormat.Custom;
        dtpTerima.Location = new Point(200, 215);
        dtpTerima.Name = "dtpTerima";
        dtpTerima.Size = new Size(260, 27);
        dtpTerima.TabIndex = 13;

        lblAntrian.AutoSize = true;
        lblAntrian.Location = new Point(20, 256);
        lblAntrian.Name = "lblAntrian";
        lblAntrian.Size = new Size(100, 20);
        lblAntrian.Text = "Nomor Antrian";

        txtAntrian.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        txtAntrian.Location = new Point(200, 251);
        txtAntrian.Name = "txtAntrian";
        txtAntrian.PlaceholderText = "otomatis dari sistem";
        txtAntrian.ReadOnly = true;
        txtAntrian.Size = new Size(260, 27);
        txtAntrian.TabIndex = 15;

        lblSumber.AutoSize = true;
        lblSumber.ForeColor = SystemColors.GrayText;
        lblSumber.Location = new Point(480, 256);
        lblSumber.Name = "lblSumber";
        lblSumber.Size = new Size(0, 20);

        btnSimpan.Location = new Point(508, 372);
        btnSimpan.Name = "btnSimpan";
        btnSimpan.Size = new Size(100, 34);
        btnSimpan.TabIndex = 3;
        btnSimpan.Text = "SIMPAN";
        btnSimpan.UseVisualStyleBackColor = true;
        btnSimpan.Click += btnSimpan_Click;

        btnCetak.Location = new Point(616, 372);
        btnCetak.Name = "btnCetak";
        btnCetak.Size = new Size(140, 34);
        btnCetak.TabIndex = 4;
        btnCetak.Text = "CETAK ANTRIAN";
        btnCetak.UseVisualStyleBackColor = true;
        btnCetak.Click += btnCetak_Click;

        btnBaru.Location = new Point(764, 372);
        btnBaru.Name = "btnBaru";
        btnBaru.Size = new Size(64, 34);
        btnBaru.TabIndex = 5;
        btnBaru.Text = "Baru";
        btnBaru.UseVisualStyleBackColor = true;
        btnBaru.Click += btnBaru_Click;

        // ---------- Tab 2: Daftar Antrian ----------
        lblDari.AutoSize = true;
        lblDari.Location = new Point(16, 20);
        lblDari.Name = "lblDari";
        lblDari.Size = new Size(35, 20);
        lblDari.Text = "Dari";

        dtpDari.Format = DateTimePickerFormat.Short;
        dtpDari.Location = new Point(60, 16);
        dtpDari.Name = "dtpDari";
        dtpDari.Size = new Size(140, 27);
        dtpDari.TabIndex = 0;

        lblSampai.AutoSize = true;
        lblSampai.Location = new Point(212, 20);
        lblSampai.Name = "lblSampai";
        lblSampai.Size = new Size(52, 20);
        lblSampai.Text = "Sampai";

        dtpSampai.Format = DateTimePickerFormat.Short;
        dtpSampai.Location = new Point(272, 16);
        dtpSampai.Name = "dtpSampai";
        dtpSampai.Size = new Size(140, 27);
        dtpSampai.TabIndex = 1;

        txtCari.Location = new Point(424, 16);
        txtCari.Name = "txtCari";
        txtCari.PlaceholderText = "Cari nomor antrian / petugas / resi";
        txtCari.Size = new Size(300, 27);
        txtCari.TabIndex = 2;
        txtCari.KeyDown += txtCari_KeyDown;

        btnMuat.Location = new Point(732, 15);
        btnMuat.Name = "btnMuat";
        btnMuat.Size = new Size(100, 29);
        btnMuat.TabIndex = 3;
        btnMuat.Text = "TAMPILKAN";
        btnMuat.UseVisualStyleBackColor = true;
        btnMuat.Click += btnMuat_Click;

        lvDaftar.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        lvDaftar.Columns.AddRange(new[] { colNo, colTanggal, colPetugas, colAntrian, colEkspedisi, colMedia, colLokasi, colResi });
        lvDaftar.FullRowSelect = true;
        lvDaftar.Location = new Point(16, 56);
        lvDaftar.MultiSelect = false;
        lvDaftar.Name = "lvDaftar";
        lvDaftar.Size = new Size(816, 320);
        lvDaftar.TabIndex = 4;
        lvDaftar.UseCompatibleStateImageBehavior = false;
        lvDaftar.View = View.Details;
        lvDaftar.SelectedIndexChanged += lvDaftar_SelectedIndexChanged;
        lvDaftar.DoubleClick += lvDaftar_DoubleClick;

        colNo.Text = "No";
        colNo.Width = 50;
        colTanggal.Text = "Tanggal Terima";
        colTanggal.Width = 130;
        colPetugas.Text = "Nama Petugas";
        colPetugas.Width = 130;
        colAntrian.Text = "Nomor Antrian";
        colAntrian.Width = 160;
        colEkspedisi.Text = "Ekspedisi";
        colEkspedisi.Width = 120;
        colMedia.Text = "Jenis Media";
        colMedia.Width = 110;
        colLokasi.Text = "Lokasi";
        colLokasi.Width = 140;
        colResi.Text = "Nomor Resi";
        colResi.Width = 140;

        btnCetakUlang.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnCetakUlang.Enabled = false;
        btnCetakUlang.Location = new Point(16, 388);
        btnCetakUlang.Name = "btnCetakUlang";
        btnCetakUlang.Size = new Size(120, 32);
        btnCetakUlang.TabIndex = 5;
        btnCetakUlang.Text = "Cetak Ulang";
        btnCetakUlang.UseVisualStyleBackColor = true;
        btnCetakUlang.Click += btnCetakUlang_Click;

        btnHapus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        btnHapus.Enabled = false;
        btnHapus.Location = new Point(144, 388);
        btnHapus.Name = "btnHapus";
        btnHapus.Size = new Size(100, 32);
        btnHapus.TabIndex = 6;
        btnHapus.Text = "Hapus";
        btnHapus.UseVisualStyleBackColor = true;
        btnHapus.Click += btnHapus_Click;

        btnUnduh.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnUnduh.Location = new Point(672, 388);
        btnUnduh.Name = "btnUnduh";
        btnUnduh.Size = new Size(160, 32);
        btnUnduh.TabIndex = 7;
        btnUnduh.Text = "Unduh Data";
        btnUnduh.UseVisualStyleBackColor = true;
        btnUnduh.Click += btnUnduh_Click;

        // ---------- Bar bawah ----------
        lblPrinter.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblPrinter.AutoSize = true;
        lblPrinter.Location = new Point(16, 496);
        lblPrinter.Name = "lblPrinter";
        lblPrinter.Size = new Size(50, 20);
        lblPrinter.Text = "Printer";

        cboPrinter.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        cboPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPrinter.Location = new Point(72, 492);
        cboPrinter.Name = "cboPrinter";
        cboPrinter.Size = new Size(380, 28);
        cboPrinter.TabIndex = 1;
        cboPrinter.SelectedIndexChanged += cboPrinter_SelectedIndexChanged;

        btnKirimTertunda.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnKirimTertunda.Location = new Point(748, 490);
        btnKirimTertunda.Name = "btnKirimTertunda";
        btnKirimTertunda.Size = new Size(140, 32);
        btnKirimTertunda.TabIndex = 2;
        btnKirimTertunda.Text = "Kirim Tertunda";
        btnKirimTertunda.UseVisualStyleBackColor = true;
        btnKirimTertunda.Click += btnKirimTertunda_Click;

        // Menandakan akun aplikasi yang dipakai, BUKAN petugas yang menerima
        // paket - satu PC dipakai bergantian beberapa petugas.
        lblAkun.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblAkun.AutoSize = true;
        lblAkun.ForeColor = SystemColors.GrayText;
        lblAkun.Location = new Point(468, 496);
        lblAkun.Name = "lblAkun";
        lblAkun.Size = new Size(60, 20);

        // Lokasi PC ditampilkan permanen supaya petugas selalu tahu dia
        // sedang "berdiri" di lokasi mana saat men-scan.
        lblLokasi.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblLokasi.AutoSize = true;
        lblLokasi.ForeColor = SystemColors.GrayText;
        lblLokasi.Location = new Point(468, 470);
        lblLokasi.Name = "lblLokasi";
        lblLokasi.Size = new Size(60, 20);

        statusStrip.Items.AddRange(new ToolStripItem[] { lblStatus, lblOutbox });
        statusStrip.Location = new Point(0, 538);
        statusStrip.Name = "statusStrip";
        statusStrip.Size = new Size(900, 26);
        statusStrip.TabIndex = 3;

        lblStatus.Name = "lblStatus";
        lblStatus.Spring = true;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.Text = "Siap.";

        lblOutbox.Name = "lblOutbox";
        lblOutbox.Text = "";

        // ---------- MainForm ----------
        AutoScaleDimensions = new SizeF(8F, 20F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(900, 564);
        Controls.Add(statusStrip);
        Controls.Add(btnKirimTertunda);
        Controls.Add(lblAkun);
        Controls.Add(lblLokasi);
        Controls.Add(cboPrinter);
        Controls.Add(lblPrinter);
        Controls.Add(tabs);
        MinimumSize = new Size(916, 610);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SAKEDAP - Penerimaan Paket SSKCKR";

        tabs.ResumeLayout(false);
        tabPenerimaan.ResumeLayout(false);
        tabPenerimaan.PerformLayout();
        tabDaftar.ResumeLayout(false);
        tabDaftar.PerformLayout();
        tabPindah.ResumeLayout(false);
        tabPindah.PerformLayout();
        grpPaket.ResumeLayout(false);
        grpPaket.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numJudul).EndInit();
        ((System.ComponentModel.ISupportInitialize)numEks).EndInit();
        ((System.ComponentModel.ISupportInitialize)numDus).EndInit();
        statusStrip.ResumeLayout(false);
        statusStrip.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    private TabControl tabs;
    private TabPage tabPenerimaan;
    private TabPage tabDaftar;
    private TabPage tabPindah;
    private Label lblLokasiPindah;
    private TextBox txtScanPindah;
    private Label lblHasilPindah;
    private Label lblJumlahPindah;

    private TextBox txtScan;
    private Button btnCari;
    private Button btnTanpaResi;
    private GroupBox grpPaket;
    private Label lblResi;
    private TextBox txtResi;
    private Label lblEkspedisi;
    private ComboBox cboEkspedisi;
    private Label lblMedia;
    private ComboBox cboMedia;
    private Label lblPelaksanaSerah;
    private TextBox txtPelaksanaSerah;
    private Label lblJudul;
    private NumericUpDown numJudul;
    private Label lblEks;
    private NumericUpDown numEks;
    private Label lblDus;
    private NumericUpDown numDus;
    private Label lblPetugas;
    private TextBox txtPetugas;
    private Label lblTerima;
    private DateTimePicker dtpTerima;
    private Label lblAntrian;
    private TextBox txtAntrian;
    private Label lblSumber;
    private Button btnSimpan;
    private Button btnCetak;
    private Button btnBaru;

    private Label lblDari;
    private DateTimePicker dtpDari;
    private Label lblSampai;
    private DateTimePicker dtpSampai;
    private TextBox txtCari;
    private Button btnMuat;
    private ListView lvDaftar;
    private ColumnHeader colNo;
    private ColumnHeader colTanggal;
    private ColumnHeader colPetugas;
    private ColumnHeader colAntrian;
    private ColumnHeader colEkspedisi;
    private ColumnHeader colMedia;
    private ColumnHeader colLokasi;
    private ColumnHeader colResi;
    private Button btnCetakUlang;
    private Button btnUnduh;
    private Button btnHapus;

    private Label lblPrinter;
    private ComboBox cboPrinter;
    private Button btnKirimTertunda;
    private Label lblAkun;
    private Label lblLokasi;
    private StatusStrip statusStrip;
    private ToolStripStatusLabel lblStatus;
    private ToolStripStatusLabel lblOutbox;
}
