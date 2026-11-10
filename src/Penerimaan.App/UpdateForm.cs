using Penerimaan.App.Services;

namespace Penerimaan.App;

/// <summary>
/// Tawaran pembaruan sebelum login. DialogResult.OK berarti installer sudah
/// dijalankan dan aplikasi harus segera ditutup; selain itu lanjut seperti
/// biasa dengan versi yang terpasang.
/// </summary>
public sealed class UpdateForm : Form
{
    private readonly UpdateChecker _checker;
    private readonly UpdateChecker.Rilis _rilis;
    private readonly ProgressBar bar;
    private readonly Label lblStatus;
    private readonly Button btnPerbarui;
    private readonly Button btnNanti;
    private CancellationTokenSource? _cts;

    public UpdateForm(UpdateChecker checker, UpdateChecker.Rilis rilis)
    {
        _checker = checker;
        _rilis = rilis;

        Text = "Pembaruan tersedia";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9F);
        ClientSize = new Size(460, 300);

        var lblJudul = new Label
        {
            Text = $"Versi {rilis.Versi} tersedia",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 14)
        };

        var lblVersi = new Label
        {
            Text = $"Terpasang: {UpdateChecker.VersiSekarang}",
            AutoSize = true,
            Location = new Point(16, 42),
            ForeColor = SystemColors.GrayText
        };

        var txtCatatan = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(16, 66),
            Size = new Size(428, 130),
            Text = string.IsNullOrWhiteSpace(rilis.Catatan)
                ? "(tanpa catatan rilis)"
                : rilis.Catatan.Replace("\r\n", "\n").Replace("\n", Environment.NewLine)
        };

        bar = new ProgressBar { Location = new Point(16, 206), Size = new Size(428, 16), Visible = false };

        lblStatus = new Label
        {
            Text = "Aplikasi akan tertutup sebentar lalu terbuka lagi. Data tertunda tidak hilang.",
            AutoSize = false,
            Size = new Size(428, 32),
            Location = new Point(16, 226),
            ForeColor = SystemColors.GrayText
        };

        btnPerbarui = new Button { Text = "Perbarui sekarang", Size = new Size(130, 28), Location = new Point(218, 262) };
        btnPerbarui.Click += async (_, _) => await PerbaruiAsync();

        btnNanti = new Button { Text = "Nanti", Size = new Size(90, 28), Location = new Point(354, 262) };
        btnNanti.Click += (_, _) =>
        {
            _cts?.Cancel();
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = btnPerbarui;
        CancelButton = btnNanti;
        Controls.AddRange([lblJudul, lblVersi, txtCatatan, bar, lblStatus, btnPerbarui, btnNanti]);
    }

    private async Task PerbaruiAsync()
    {
        btnPerbarui.Enabled = false;
        bar.Visible = true;
        lblStatus.ForeColor = SystemColors.GrayText;
        lblStatus.Text = "Mengunduh...";
        _cts = new CancellationTokenSource();

        try
        {
            var path = await _checker.UnduhAsync(_rilis, new Progress<int>(p => bar.Value = Math.Clamp(p, 0, 100)), _cts.Token);
            lblStatus.Text = "Menjalankan installer...";
            UpdateChecker.JalankanInstaller(path);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Ditutup lewat "Nanti".
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Permintaan izin admin (UAC) ditolak.
            Gagal("Pembaruan butuh izin administrator. Minta admin menyetujuinya, atau pilih Nanti.");
        }
        catch (Exception ex)
        {
            Gagal($"Pembaruan gagal: {ex.Message}");
        }
    }

    private void Gagal(string pesan)
    {
        bar.Visible = false;
        lblStatus.ForeColor = Color.Firebrick;
        lblStatus.Text = pesan;
        btnPerbarui.Enabled = true;
    }
}
