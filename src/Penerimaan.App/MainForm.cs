using System.Media;
using Penerimaan.App.Config;
using Penerimaan.App.Models;
using Penerimaan.App.Services;

namespace Penerimaan.App;

public partial class MainForm : Form
{
    private readonly AppSettings _settings;
    private readonly PenerimaanRepository _repo;
    private readonly OutboxQueue _outbox;
    private readonly Pegawai _petugas;

    /// <summary>Label pilihan ekspedisi untuk paket yang diantar sendiri.</summary>
    private const string DatangLangsung = "Datang Langsung (tanpa ekspedisi)";

    /// <summary>Baris pertama dropdown jenis media - bukan pilihan yang sah.</summary>
    private const string TanpaMedia = "-- pilih jenis media --";

    /// <summary>
    /// Dipakai hanya bila JASA_PENGIRIMAN gagal dibaca. Bukan daftar resmi -
    /// daftar resminya ada di tabel itu.
    /// </summary>
    private static readonly string[] EkspedisiCadangan =
        { "J&T Express", "JNE", "SiCepat", "Pos Indonesia", "Kobra" };

    /// <summary>Paket yang sedang dikerjakan di layar. Null berarti form kosong.</summary>
    private PaketPenerimaan? _aktif;

    /// <summary>
    /// Kardus yang labelnya perlu dicetak. Berisi seluruh kardus untuk
    /// penerimaan baru, dan hanya kardus susulan setelah dus ditambahkan -
    /// label yang sudah tertempel di kardus tidak perlu dicetak lagi.
    /// </summary>
    private List<PaketPenerimaan> _cetak = new();

    /// <summary>
    /// Selama true, Tanggal Terima Paket terus mengikuti jam PC. Berubah jadi
    /// false begitu petugas mengubah sendiri nilainya - misalnya saat mencatat
    /// paket yang datang tadi pagi tapi baru sempat diinput siang.
    /// </summary>
    private bool _waktuIkutJam = true;

    /// <summary>Penanda bahwa pengisian datang dari kode, bukan dari petugas.</summary>
    private bool _sedangMengisiForm;

    private readonly System.Windows.Forms.Timer _jam = new() { Interval = 10_000 };

    /// <summary>Lokasi PC ini. Null berarti IP-nya belum dipetakan admin.</summary>
    private readonly Lokasi? _lokasiPc;

    private readonly string _ipLokal;

    /// <summary>
    /// Pesan error saat mencari lokasi, bila pencariannya memang gagal - beda
    /// dari sekadar tidak menemukan baris. Null berarti query berhasil.
    /// </summary>
    private readonly string? _galatLokasi;

    public MainForm(AppSettings settings, PenerimaanRepository repo, Pegawai petugas,
        Lokasi? lokasiPc, string ipLokal, string? galatLokasi = null)
    {
        _settings = settings;
        _repo = repo;
        _petugas = petugas;
        _lokasiPc = lokasiPc;
        _ipLokal = ipLokal;
        _galatLokasi = galatLokasi;
        _outbox = new OutboxQueue();

        InitializeComponent();

        // Nama petugas penerima TIDAK diambil dari akun login: satu PC di pos
        // satpam dipakai bergantian beberapa petugas dengan satu akun aplikasi.
        // Jadi yang menerima paket diketik per transaksi.
        // Satuan kerja ikut ditampilkan: satu berkas installer dipakai di
        // Perpusnas maupun provinsi, jadi petugas perlu bisa memastikan
        // aplikasinya mengenali dia sebagai satuan kerja yang benar.
        // Sumber branch ikut disebut selama masih dari appsettings: keadaan
        // sementara yang tidak terlihat akan mengendap jadi permanen.
        lblAkun.Text = petugas.BranchId.Length == 0
            ? $"Akun: {petugas.Nama}"
            : petugas.BranchDariConfig
                ? $"Akun: {petugas.Nama}  (branch {petugas.BranchId} - dari appsettings)"
                : $"Akun: {petugas.Nama}  (branch {petugas.BranchId})";

        lblLokasi.Text = _lokasiPc is null ? "Lokasi: belum diatur" : $"Lokasi: {_lokasiPc.Nama}";
        lblLokasi.ForeColor = _lokasiPc is null ? Color.Firebrick : SystemColors.GrayText;

        SiapkanTabPindah();

        if (string.IsNullOrWhiteSpace(_settings.Printer.Name))
            _settings.Printer.Name = RawPrinterHelper.DefaultPrinter;

        cboPrinter.Items.AddRange(RawPrinterHelper.InstalledPrinters.Cast<object>().ToArray());
        cboPrinter.SelectedItem = _settings.Printer.Name;

        // Daftar sementara sampai JASA_PENGIRIMAN terbaca di OnShown. Kalau
        // tabel referensinya gagal dibaca, daftar ini yang tetap dipakai.
        IsiEkspedisi(EkspedisiCadangan);

        // Tab Daftar Antrian default menampilkan hari ini saja - rentang lebar
        // menarik banyak baris lewat sambungan INLIS yang tidak selalu cepat.
        dtpDari.Value = DateTime.Today;
        dtpSampai.Value = DateTime.Today;

        var tips = new ToolTip();
        tips.SetToolTip(btnHapus, "Hanya bisa dalam 24 jam sejak data dibuat.");
        tips.SetToolTip(lvDaftar, "Klik ganda untuk mengubah data.");

        // Tanggal terima mengikuti jam PC sampai petugas mengubahnya sendiri.
        dtpTerima.ValueChanged += dtpTerima_ValueChanged;
        _jam.Tick += (_, _) => SegarkanWaktu();
        _jam.Start();

        Kosongkan();
        UpdateStatus();
    }

    private void dtpTerima_ValueChanged(object? sender, EventArgs e)
    {
        if (_sedangMengisiForm) return;

        // Perubahan datang dari petugas: berhenti mengikuti jam supaya
        // nilai yang dia pilih tidak ditimpa beberapa detik kemudian.
        _waktuIkutJam = false;
    }

    private void SegarkanWaktu()
    {
        if (!_waktuIkutJam) return;

        // Penerimaan yang sudah tersimpan menampilkan waktu tersimpannya,
        // bukan jam sekarang.
        if (_aktif is { SiapCetak: true }) return;

        SetWaktuTerima(DateTime.Now);
    }

    /// <summary>Isi tanggal terima dari kode tanpa dianggap perubahan petugas.</summary>
    private void SetWaktuTerima(DateTime waktu)
    {
        _sedangMengisiForm = true;
        try
        {
            dtpTerima.Value = waktu;
        }
        finally
        {
            _sedangMengisiForm = false;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _jam.Stop();
        _jam.Dispose();
        base.OnFormClosed(e);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        txtScan.Focus();

        await MuatEkspedisiAsync();
        await MuatJenisMediaAsync();
        await FlushOutboxAsync();
    }

    /// <summary>
    /// Isi dropdown ekspedisi dari JASA_PENGIRIMAN. Kalau gagal, daftar
    /// cadangan yang sudah terpasang tetap dipakai - pos satpam harus bisa
    /// bekerja walau tabel referensi tidak terbaca.
    /// </summary>
    private async Task MuatEkspedisiAsync()
    {
        try
        {
            var daftar = await _repo.DaftarEkspedisiAsync();
            if (daftar.Count > 0) IsiEkspedisi(daftar);
        }
        catch (Exception ex)
        {
            Warn($"Daftar ekspedisi tidak bisa dimuat, memakai daftar bawaan: {ex.Message}");
        }
    }

    /// <summary>
    /// Dropdown ekspedisi sengaja tetap bisa diketik: ekspedisi yang belum
    /// terdaftar di JASA_PENGIRIMAN tetap harus bisa dicatat apa adanya.
    /// </summary>
    private void IsiEkspedisi(IEnumerable<string> nama)
    {
        var terpilih = cboEkspedisi.Text;

        cboEkspedisi.Items.Clear();
        cboEkspedisi.Items.Add(DatangLangsung);
        cboEkspedisi.Items.AddRange(nama.Cast<object>().ToArray());

        cboEkspedisi.Text = terpilih;
    }

    /// <summary>
    /// Isi dropdown jenis media dari tabel referensi. Kegagalan di sini tidak
    /// menghentikan pekerjaan: jenis media bersifat opsional, dan pos satpam
    /// tetap harus bisa mencatat paket walau tabel referensi tidak terbaca.
    /// </summary>
    private async Task MuatJenisMediaAsync()
    {
        try
        {
            var media = await _repo.DaftarMediaAsync();

            cboMedia.Items.Clear();
            cboMedia.Items.Add(TanpaMedia);
            cboMedia.Items.AddRange(media.Cast<object>().ToArray());
            cboMedia.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Warn($"Daftar jenis media tidak bisa dimuat: {ex.Message}");
        }
    }

    /// <summary>
    /// Scanner barcode berperilaku seperti keyboard: mengetik kode lalu menekan Enter.
    /// Jadi tidak perlu driver khusus - cukup tangkap Enter di kolom scan.
    /// </summary>
    private async void txtScan_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        await CariResiAsync();
    }

    private async void btnCari_Click(object? sender, EventArgs e) => await CariResiAsync();

    private async Task CariResiAsync()
    {
        var resi = txtScan.Text.Trim();
        if (resi.Length == 0) return;

        SetBusy(true);
        try
        {
            // Resi yang sudah pernah dicatat punya dua kemungkinan yang tidak
            // bisa dibedakan aplikasi: dus susulan dari kiriman yang sama, atau
            // kiriman lain yang kebetulan resinya sama. Petugas yang memutuskan.
            var sudahAda = await _repo.CariAntrianByResiAsync(resi);

            if (sudahAda.Count > 0)
            {
                using var dialog = new ResiTercatatForm(resi, sudahAda);

                if (dialog.ShowDialog(this) != DialogResult.OK ||
                    dialog.Tindakan == TindakanResiTercatat.Batal)
                {
                    Info("Dibatalkan.");
                    return;
                }

                if (dialog.Tindakan == TindakanResiTercatat.TambahDus)
                {
                    await TambahDusAsync(dialog.Terpilih, dialog.JumlahTambahan);
                    return;
                }
            }

            var kandidat = await _repo.CariResiAsync(resi);

            if (kandidat.Count == 0)
            {
                // Jalur B: resi tidak terdaftar di SAKEDAP, petugas isi manual.
                _aktif = new PaketPenerimaan { NomorResi = resi, Sumber = SumberData.Manual };
                Info("Nomor resi tidak ditemukan, silakan lengkapi data di bawah.");
            }
            else if (kandidat.Count == 1)
            {
                // Jalur A: penerbit sudah mengisi resi, data terisi otomatis.
                _aktif = kandidat[0];
                Info("Data ditemukan di SAKEDAP.");
            }
            else
            {
                // Resi tidak dijamin unik di letter. Petugas yang memilih -
                // dia memegang paketnya dan bisa mencocokkan nama pengirim.
                using var dialog = new PilihLetterForm(kandidat);

                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Terpilih is null)
                {
                    Info("Pemilihan dibatalkan.");
                    return;
                }

                _aktif = dialog.Terpilih;
                Info($"Dipilih surat penyerahan ID {_aktif.LetterId}.");
            }

            TampilkanKeForm(_aktif);
        }
        catch (Exception ex)
        {
            Warn($"Gagal mencari resi: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>
    /// Dus susulan dari kiriman yang sama: tiap kardus baru mendapat nomor
    /// antriannya sendiri, JUMLAH_DUS naik di seluruh kardus sekiriman, dan
    /// yang dicetak hanya label kardus yang baru.
    /// </summary>
    private async Task TambahDusAsync(PaketPenerimaan penerimaan, int tambahan)
    {
        try
        {
            var baru = await _repo.TambahDusAsync(penerimaan, tambahan, _ipLokal);

            if (baru.Count == 0)
            {
                Warn("Tidak ada dus yang ditambahkan.");
                return;
            }

            _aktif = baru[0];
            _cetak = baru;

            TampilkanKeForm(_aktif);
            btnSimpan.Enabled = false;
            btnCetak.Enabled = true;

            Info($"{baru.Count} dus ditambahkan ke kiriman {penerimaan.KirimanId} - "
               + $"kini {penerimaan.JumlahDus} dus. Cetak label DUS {baru[0].NomorDus} "
               + $"sampai {baru[^1].NomorDus}.");
        }
        catch (Exception ex)
        {
            Warn($"Gagal menambah dus: {ex.Message}");
        }
    }

    private async void btnSimpan_Click(object? sender, EventArgs e)
    {
        if (_aktif is null)
        {
            Warn("Belum ada paket. Scan atau ketik nomor resi terlebih dahulu.");
            return;
        }

        if (!AmbilDariForm(_aktif)) return;

        SetBusy(true);
        try
        {
            var hasil = await _repo.SimpanPenerimaanAsync(_aktif, _ipLokal);

            // Tiap kardus punya nomor antriannya sendiri; yang ditampilkan di
            // form adalah kardus pertama, dan yang dicetak seluruhnya.
            _aktif = hasil.Pertama;
            _cetak = hasil.Kardus;

            TampilkanKeForm(_aktif);

            if (hasil.Lengkap)
            {
                Info(hasil.Kardus.Count == 1
                    ? $"Berhasil disimpan. Nomor antrian: {_aktif.NomorAntrian}"
                    : $"Berhasil disimpan. {hasil.Kardus.Count} dus, nomor antrian "
                      + $"{_aktif.NomorAntrian} sampai {hasil.Kardus[^1].NomorAntrian}.");
            }
            else
            {
                // Sebagian kardus sudah masuk. Melaporkannya sebagai gagal
                // total akan membuat petugas menyimpan ulang, dan kiriman yang
                // sama tercatat dua kali.
                Warn(hasil.Peringatan!);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Koneksi bermasalah: simpan ke antrian agar input petugas tidak hilang.
            // Nomor antrian belum terbit, jadi label belum bisa dicetak.
            _outbox.Enqueue(_aktif);
            Warn("Koneksi bermasalah. Data disimpan sementara, label dicetak setelah tersinkron.");
            Kosongkan();
        }
        catch (Exception ex)
        {
            Warn($"Gagal menyimpan: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
            UpdateStatus();
        }
    }

    private void btnCetak_Click(object? sender, EventArgs e)
    {
        if (_cetak.Count == 0 || _aktif is null || !_aktif.SiapCetak)
        {
            Warn("Label baru bisa dicetak setelah penerimaan tersimpan dan nomor antrian terbit.");
            return;
        }

        Cetak(_cetak);
    }

    /// <summary>Dipakai tab Penerimaan maupun tombol Cetak Ulang di tab Daftar Antrian.</summary>
    private void Cetak(IReadOnlyList<PaketPenerimaan> kardus)
    {
        if (kardus.Count == 0) return;

        try
        {
            if (_settings.Printer.PakaiZpl)
            {
                var zpl = LabelTemplate.Build(kardus, _settings.Printer);
                RawPrinterHelper.SendString(_settings.Printer.Name, zpl);
            }
            else
            {
                // Printer biasa tidak mengerti ZPL - kirim data mentah ke sana
                // menghasilkan berkas rusak, jadi labelnya digambar.
                LabelRenderer.Print(kardus, _settings.Printer);
            }

            Info($"{kardus.Count} label {kardus[0].NomorAntrian} dikirim ke {_settings.Printer.Name}.");
        }
        catch (Exception ex)
        {
            Warn($"Gagal mencetak: {ex.Message}");
        }
    }

    /// <summary>
    /// Paket yang diserahkan langsung biasanya tidak punya resi untuk discan,
    /// jadi harus ada jalan masuk yang tidak lewat pencarian.
    /// </summary>
    private void btnTanpaResi_Click(object? sender, EventArgs e)
    {
        Kosongkan();
        _aktif = new PaketPenerimaan { Sumber = SumberData.Manual, Cara = CaraDatang.Langsung };
        TampilkanKeForm(_aktif);
        cboEkspedisi.SelectedItem = DatangLangsung;
        Info("Input tanpa resi. Lengkapi data di bawah.");
        txtPelaksanaSerah.Focus();
    }

    private void btnBaru_Click(object? sender, EventArgs e)
    {
        Kosongkan();
        txtScan.Focus();
    }

    private void TampilkanKeForm(PaketPenerimaan paket)
    {
        txtResi.Text = paket.NomorResi;
        cboEkspedisi.Text = paket.Ekspedisi;
        txtPelaksanaSerah.Text = paket.NamaPelaksanaSerah;
        numJudul.Value = Math.Clamp(paket.JumlahJudul, 0, (int)numJudul.Maximum);
        numEks.Value = Math.Clamp(paket.JumlahEks, 0, (int)numEks.Maximum);
        numDus.Value = Math.Clamp(paket.JumlahDus, 1, (int)numDus.Maximum);
        // Data yang sudah tersimpan menampilkan waktu aslinya; yang masih baru
        // tetap mengikuti jam PC.
        if (paket.SiapCetak)
        {
            _waktuIkutJam = false;
            SetWaktuTerima(paket.WaktuTerima);
        }
        else
        {
            SetWaktuTerima(_waktuIkutJam ? DateTime.Now : dtpTerima.Value);
        }

        txtAntrian.Text = paket.NomorAntrian;

        // Jalur SAKEDAP: data paket datang dari server, jangan diubah petugas.
        var dariSakedap = paket.Sumber == SumberData.Sakedap;

        // Ekspedisi tetap bisa dipilih walau data dari SAKEDAP, kalau letter-nya
        // tidak punya jasa_pengiriman_id - paket bisa saja diantar langsung.
        cboEkspedisi.Enabled = !dariSakedap || paket.Ekspedisi.Length == 0;
        txtPelaksanaSerah.ReadOnly = dariSakedap;

        // Jalur SAKEDAP menampilkan nama penerbit terdaftar; jalur manual
        // meminta nama pengirim apa adanya dari label paket, karena satpam
        // tidak tahu nama penerbit yang sah.
        txtPelaksanaSerah.PlaceholderText = dariSakedap
            ? ""
            : "nama pengirim sesuai tertulis di paket";

        // Di jalur manual angkanya perkiraan satpam, bukan hasil pemeriksaan
        // isi paket. Labelnya dibedakan supaya tidak dibaca sebagai angka pasti.
        lblJudul.Text = dariSakedap ? "Jumlah Judul" : "Perkiraan Judul";
        lblEks.Text = dariSakedap ? "Jumlah Eks" : "Perkiraan Eks";
        numJudul.Enabled = !dariSakedap;
        numEks.Enabled = !dariSakedap;

        // Jumlah dus selalu bisa diisi: yang menghitung fisik kardus adalah
        // satpam di depan paket, bukan data deklarasi penerbit di SAKEDAP.
        numDus.Enabled = true;

        btnSimpan.Enabled = !paket.SiapCetak;
        btnCetak.Enabled = paket.SiapCetak;
        lblSumber.Text = dariSakedap ? "Sumber: integrasi SAKEDAP" : "Sumber: input manual";
    }

    /// <summary>Baca isian form ke objek paket. Mengembalikan false bila ada yang belum valid.</summary>
    private bool AmbilDariForm(PaketPenerimaan paket)
    {
        if (cboEkspedisi.Text.Trim().Length == 0)
        {
            Warn("Ekspedisi wajib dipilih.");
            cboEkspedisi.Focus();
            return false;
        }

        if (cboMedia.SelectedItem is not JenisMedia)
        {
            Warn(cboMedia.Items.Count <= 1
                ? "Daftar jenis media kosong. Periksa koneksi lalu buka ulang aplikasi."
                : "Jenis media wajib dipilih.");
            cboMedia.Focus();
            return false;
        }

        if (txtPetugas.Text.Trim().Length == 0)
        {
            Warn("Nama petugas penerima wajib diisi.");
            txtPetugas.Focus();
            return false;
        }

        var langsung = cboEkspedisi.Text.Trim() == DatangLangsung;

        if (!langsung && txtResi.Text.Trim().Length == 0)
        {
            Warn("Nomor resi wajib diisi untuk paket yang diantar ekspedisi.");
            txtResi.Focus();
            return false;
        }

        paket.Cara = langsung ? CaraDatang.Langsung : CaraDatang.Ekspedisi;
        paket.NomorResi = txtResi.Text.Trim();
        paket.Ekspedisi = langsung ? "" : cboEkspedisi.Text.Trim();
        paket.NamaPelaksanaSerah = txtPelaksanaSerah.Text.Trim();
        paket.JumlahJudul = (int)numJudul.Value;
        paket.JumlahEks = (int)numEks.Value;
        paket.JumlahDus = (int)numDus.Value;

        // Sudah divalidasi di atas, jadi pasti JenisMedia.
        var media = (JenisMedia)cboMedia.SelectedItem!;
        paket.JenisMediaId = media.Id;
        paket.JenisMedia = media.Nama;
        paket.NamaPetugasPenerima = txtPetugas.Text.Trim();
        // Kalau masih mengikuti jam PC, pakai detik saat SIMPAN ditekan -
        // bukan nilai terakhir yang kebetulan tampil di layar.
        paket.WaktuTerima = _waktuIkutJam ? DateTime.Now : dtpTerima.Value;

        // Lokasi awal adalah tempat PC ini berada - satpam tidak memilihnya.
        if (_lokasiPc is not null)
        {
            paket.LokasiId = _lokasiPc.Id;
            paket.LokasiNama = _lokasiPc.Nama;
        }
        paket.Status = StatusPaket.DiterimaSatpam;
        return true;
    }

    private void Kosongkan()
    {
        _aktif = null;
        _cetak = new List<PaketPenerimaan>();
        txtScan.Clear();
        txtResi.Clear();
        cboEkspedisi.Text = "";
        txtPelaksanaSerah.Clear();
        numJudul.Value = 0;
        numEks.Value = 0;
        numDus.Value = 1;
        if (cboMedia.Items.Count > 0) cboMedia.SelectedIndex = 0;

        _waktuIkutJam = true;
        SetWaktuTerima(DateTime.Now);
        txtAntrian.Clear();

        // txtPetugas sengaja TIDAK dikosongkan: dalam satu shift, petugas yang
        // sama memproses banyak paket berturut-turut. Mengetik ulang namanya
        // setiap kali hanya memperlambat dan mengundang salah ketik.

        cboEkspedisi.Enabled = true;
        txtPelaksanaSerah.ReadOnly = false;
        numJudul.Enabled = true;
        numEks.Enabled = true;
        btnSimpan.Enabled = false;
        btnCetak.Enabled = false;
        lblSumber.Text = "";
    }

    private async void btnKirimTertunda_Click(object? sender, EventArgs e) => await FlushOutboxAsync();

    private async Task FlushOutboxAsync()
    {
        if (_outbox.Count == 0) return;

        SetBusy(true);
        try
        {
            var terkirim = await _outbox.FlushAsync(_repo, _ipLokal);
            if (terkirim.Count > 0)
            {
                var nomor = string.Join(", ", terkirim.Select(p => p.NomorAntrian));
                Info($"{terkirim.Count} penerimaan tertunda tersimpan. Nomor antrian: {nomor}.");
            }
        }
        finally
        {
            SetBusy(false);
            UpdateStatus();
        }
    }

    private void cboPrinter_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cboPrinter.SelectedItem is not string nama) return;

        _settings.Printer.Name = nama;
        _settings.Save();
    }

    private void UpdateStatus() =>
        lblOutbox.Text = _outbox.Count == 0
            ? "Semua data terkirim"
            : $"Tertunda: {_outbox.Count}";

    private void SetBusy(bool busy)
    {
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        txtScan.Enabled = !busy;
        btnCari.Enabled = !busy;
    }

    private void Info(string pesan) => lblStatus.Text = pesan;

    private void Warn(string pesan)
    {
        lblStatus.Text = pesan;
        SystemSounds.Beep.Play();
    }

}
