using System.Media;
using Penerimaan.App.Models;
using Penerimaan.App.Services;

namespace Penerimaan.App;

/// <summary>
/// Login petugas keamanan. Nama petugas yang login dipakai sebagai
/// "Nama Petugas Penerima" di setiap penerimaan, jadi jejak auditnya jelas.
/// </summary>
public partial class LoginForm : Form
{
    private readonly PenerimaanRepository _repo;

    public Pegawai? Petugas { get; private set; }

    public LoginForm(PenerimaanRepository repo, Lokasi? lokasi, string ip)
    {
        _repo = repo;
        InitializeComponent();

        // Lokasi ditampilkan sejak layar masuk supaya petugas langsung sadar
        // kalau PC ini belum dipetakan, sebelum sempat bekerja.
        if (lokasi is null)
        {
            lblLokasi.Text = $"Lokasi belum diatur untuk IP {(ip.Length == 0 ? "(tidak terdeteksi)" : ip)}";
            lblLokasi.ForeColor = Color.Firebrick;
        }
        else
        {
            lblLokasi.Text = $"Lokasi: {lokasi.Nama}";
            lblLokasi.ForeColor = SystemColors.ControlText;
        }
    }

    private async void btnLogin_Click(object? sender, EventArgs e) => await LoginAsync();

    private async void txtPassword_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        await LoginAsync();
    }

    private async Task LoginAsync()
    {
        var username = txtUsername.Text.Trim();
        var password = txtPassword.Text;

        if (username.Length == 0 || password.Length == 0)
        {
            Warn("Username dan password wajib diisi.");
            return;
        }

        SetBusy(true);
        try
        {
            Petugas = await _repo.LoginAsync(username, password);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException ex)
        {
            Warn(ex.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Warn("Tidak bisa menghubungi server INLIS/SAKEDAP. Periksa koneksi jaringan.");
        }
        catch (Exception ex)
        {
            Warn($"Gagal login: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        txtUsername.Enabled = !busy;
        txtPassword.Enabled = !busy;
        btnLogin.Enabled = !busy;
    }

    private void Warn(string pesan)
    {
        lblPesan.Text = pesan;
        SystemSounds.Beep.Play();
    }
}
