using Penerimaan.App.Config;

namespace Penerimaan.App;

/// <summary>
/// Pengaturan koneksi INLIS oleh admin: URL API dan token.
///
/// Muncul otomatis bila PC ini belum punya token (pemasangan baru, atau berkas
/// setting disalin dari PC lain sehingga DPAPI menolaknya), atau dengan
/// menjalankan: Penerimaan.exe --atur-koneksi
///
/// URL disimpan polos dan ditampilkan apa adanya - bukan rahasia. Token
/// disimpan terenkripsi dan tidak pernah ditampilkan kembali; kosongkan
/// kolomnya untuk mempertahankan token yang sudah ada.
/// </summary>
public sealed class KoneksiForm : Form
{
    private readonly AppSettings _settings;
    private readonly TextBox txtUrl;
    private readonly TextBox txtToken;
    private readonly Label lblStatus;
    private readonly bool _sudahAdaToken;

    public KoneksiForm(AppSettings settings)
    {
        _settings = settings;
        _sudahAdaToken = !string.IsNullOrWhiteSpace(settings.Inlis.Token);

        Text = "Koneksi INLIS";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(460, 300);

        var lblJudul = new Label
        {
            Text = _sudahAdaToken ? "Ubah koneksi INLIS" : "Koneksi INLIS belum diatur di PC ini",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 14)
        };

        var lblUrl = new Label { Text = "URL API", AutoSize = true, Location = new Point(16, 52) };
        txtUrl = new TextBox
        {
            Location = new Point(16, 72),
            Size = new Size(428, 23),
            Text = settings.Inlis.ApiUrl
        };

        var btnBawaan = new LinkLabel
        {
            Text = "Kembalikan ke bawaan",
            AutoSize = true,
            Location = new Point(16, 98),
            Enabled = !string.IsNullOrWhiteSpace(settings.Inlis.ApiUrlBawaan)
        };
        btnBawaan.LinkClicked += (_, _) => txtUrl.Text = settings.Inlis.ApiUrlBawaan;

        var lblToken = new Label { Text = "Token", AutoSize = true, Location = new Point(16, 126) };
        txtToken = new TextBox
        {
            Location = new Point(16, 146),
            Size = new Size(428, 23),
            UseSystemPasswordChar = true,
            PlaceholderText = _sudahAdaToken ? "Kosongkan untuk tetap memakai token yang tersimpan" : ""
        };

        var chkTampil = new CheckBox { Text = "Tampilkan", AutoSize = true, Location = new Point(16, 174) };
        chkTampil.CheckedChanged += (_, _) => txtToken.UseSystemPasswordChar = !chkTampil.Checked;

        var lblInfo = new Label
        {
            Text = "Token disimpan terenkripsi (DPAPI) dan hanya bisa dibuka di PC ini.",
            AutoSize = false,
            Size = new Size(428, 20),
            Location = new Point(16, 200),
            ForeColor = SystemColors.GrayText
        };

        lblStatus = new Label
        {
            AutoSize = false,
            Size = new Size(428, 34),
            Location = new Point(16, 222),
            ForeColor = Color.Firebrick
        };

        var btnSimpan = new Button { Text = "Simpan", Size = new Size(90, 28), Location = new Point(258, 260) };
        btnSimpan.Click += (_, _) => Simpan();

        var btnBatal = new Button
        {
            Text = "Batal",
            DialogResult = DialogResult.Cancel,
            Size = new Size(90, 28),
            Location = new Point(354, 260)
        };

        AcceptButton = btnSimpan;
        CancelButton = btnBatal;
        Controls.AddRange([lblJudul, lblUrl, txtUrl, btnBawaan, lblToken, txtToken, chkTampil,
            lblInfo, lblStatus, btnSimpan, btnBatal]);

        ActiveControl = string.IsNullOrWhiteSpace(txtUrl.Text) ? txtUrl : txtToken;
    }

    private void Simpan()
    {
        var url = txtUrl.Text.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.Query))
        {
            lblStatus.Text = "URL API harus alamat http/https lengkap, tanpa ?query.";
            return;
        }

        var token = txtToken.Text.Trim();
        if (token.Contains(' '))
        {
            lblStatus.Text = "Token tidak boleh mengandung spasi.";
            return;
        }
        if (token.Length == 0 && !_sudahAdaToken)
        {
            lblStatus.Text = "Token wajib diisi.";
            return;
        }

        // Token dikirim di query string; lewat http polos ia terbaca di jaringan.
        if (uri.Scheme == Uri.UriSchemeHttp
            && MessageBox.Show(this,
                "URL memakai http, bukan https. Token akan terkirim tanpa enkripsi di jaringan.\n\nTetap simpan?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            _settings.SimpanKoneksi(url, token.Length == 0 ? null : token);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lblStatus.Text = $"Gagal menyimpan ke {AppSettings.LocalDirectory}: {ex.Message}";
        }
    }
}
