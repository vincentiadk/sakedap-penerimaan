namespace Penerimaan.App;

/// <summary>
/// Membuat isi form ikut membesar saat jendela diperbesar/dimaksimalkan.
///
/// Anchor bawaan WinForms hanya menggeser dan merentangkan kontrol - tombol,
/// kotak isian, dan hurufnya tetap berukuran kecil di layar besar. Di pos
/// satpam aplikasi biasanya dibuka penuh layar dan dibaca dari jarak berdiri,
/// jadi seluruh posisi, ukuran, dan huruf diskalakan mengikuti ukuran jendela.
///
/// Ukuran awal dari designer menjadi patokan (faktor 1). Posisi dan lebar
/// mengikuti lebar induknya, posisi dan tinggi mengikuti tinggi induknya;
/// huruf memakai faktor terkecil dari keduanya supaya teks tidak terpotong.
/// </summary>
internal sealed class SkalaTampilan
{
    private sealed record Awal(Rectangle Bounds, Size KlienInduk, Font Font);

    private readonly Form _form;
    private readonly Size _klienAwal;
    private readonly Dictionary<Control, Awal> _awal = new();
    private readonly Dictionary<ColumnHeader, int> _kolom = new();
    private readonly Dictionary<(string, float, FontStyle, GraphicsUnit), Font> _fontCache = new();
    private float _faktorFont = 1f;

    /// <summary>Panggil setelah form selesai diskalakan DPI (mis. di OnLoad).</summary>
    public SkalaTampilan(Form form)
    {
        _form = form;
        _klienAwal = form.ClientSize;
        Rekam(form);

        _form.Resize += (_, _) => TerapkanForm();
    }

    private void Rekam(Control induk)
    {
        foreach (Control c in induk.Controls)
        {
            _awal[c] = new Awal(c.Bounds, induk.ClientSize, c.Font);

            if (c is ListView lv)
            {
                foreach (ColumnHeader k in lv.Columns) _kolom[k] = k.Width;
                lv.Resize += (_, _) => AturKolom(lv);
            }

            // Posisi kini dihitung di sini; anchor lama hanya akan bertabrakan.
            if (Diatur(c)) c.Anchor = AnchorStyles.Top | AnchorStyles.Left;

            if (c.Controls.Count > 0)
            {
                // Tiap wadah mengatur ulang anaknya sendiri begitu ukurannya
                // berubah - termasuk TabPage yang diukur ulang oleh TabControl.
                var wadah = c;
                wadah.Resize += (_, _) => AturAnak(wadah);
                Rekam(c);
            }
        }
    }

    /// <summary>TabPage dan kontrol ber-Dock sudah diatur oleh induknya.</summary>
    private static bool Diatur(Control c) => c is not TabPage && c.Dock == DockStyle.None;

    private void TerapkanForm()
    {
        if (_form.WindowState == FormWindowState.Minimized) return;

        var klien = _form.ClientSize;
        if (klien.Width <= 0 || klien.Height <= 0) return;

        var faktor = Math.Clamp(Math.Min(
            (float)klien.Width / _klienAwal.Width,
            (float)klien.Height / _klienAwal.Height), 1f, 4f);

        // Huruf dulu: tinggi tab, kotak isian, dan status bar bergantung pada
        // huruf, dan posisi anak dihitung dari ukuran wadah yang sudah final.
        if (Math.Abs(faktor - _faktorFont) > 0.01f)
        {
            _faktorFont = faktor;
            foreach (var (c, a) in _awal) c.Font = FontSkala(a.Font, faktor);
        }

        AturAnak(_form);
    }

    private void AturAnak(Control induk)
    {
        if (induk.ClientSize.Width <= 0 || induk.ClientSize.Height <= 0) return;

        foreach (Control c in induk.Controls)
        {
            if (!_awal.TryGetValue(c, out var a) || !Diatur(c)) continue;

            var fx = (float)induk.ClientSize.Width / a.KlienInduk.Width;
            var fy = (float)induk.ClientSize.Height / a.KlienInduk.Height;

            c.SetBounds(
                (int)Math.Round(a.Bounds.X * fx),
                (int)Math.Round(a.Bounds.Y * fy),
                (int)Math.Round(a.Bounds.Width * fx),
                (int)Math.Round(a.Bounds.Height * fy));
        }
    }

    private void AturKolom(ListView lv)
    {
        if (!_awal.TryGetValue(lv, out var a) || a.Bounds.Width <= 0) return;

        var fx = (float)lv.Width / a.Bounds.Width;
        foreach (ColumnHeader k in lv.Columns)
            if (_kolom.TryGetValue(k, out var lebar))
                k.Width = (int)Math.Round(lebar * fx);
    }

    /// <summary>Font disimpan ulang supaya resize berulang tidak menumpuk handle GDI.</summary>
    private Font FontSkala(Font asal, float faktor)
    {
        // Dibulatkan ke 0,25 pt: perbedaan lebih kecil tidak terlihat, dan
        // jumlah font yang tersimpan tetap sedikit.
        var ukuran = MathF.Round(asal.Size * faktor * 4f) / 4f;
        var kunci = (asal.FontFamily.Name, ukuran, asal.Style, asal.Unit);

        if (!_fontCache.TryGetValue(kunci, out var font))
        {
            font = new Font(asal.FontFamily, ukuran, asal.Style, asal.Unit);
            _fontCache[kunci] = font;
        }

        return font;
    }
}
