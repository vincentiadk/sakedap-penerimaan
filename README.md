# SAKEDAP - Penerimaan Barang

Aplikasi desktop Windows untuk penerimaan barang: scan barcode, kirim ke API
yang sudah ada, dan cetak label langsung ke printer barcode.

## Stack

- .NET 8 WinForms (`net8.0-windows`)
- Tanpa NuGet package sama sekali - semua pakai BCL, jadi `dotnet build` bisa jalan offline
- Installer: Inno Setup

## Struktur

```
src/Penerimaan.App/
  Program.cs                     entry point
  MainForm.cs                    logika layar utama
  MainForm.Designer.cs           layout (bisa dibuka di VS Designer)
  appsettings.json               URL API, nama printer, bahasa printer
  Config/AppSettings.cs          pembacaan konfigurasi
  Models/PenerimaanItem.cs       model data
  Services/
    RawPrinterHelper.cs          kirim data mentah ke spooler Windows (winspool.drv)
    LabelTemplate.cs             susunan label ZPL / TSPL
    ApiClient.cs                 pemanggilan API
    OutboxQueue.cs               antrian transaksi saat koneksi putus
installer/setup.iss              script Inno Setup
```

## Menjalankan

Butuh [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (belum
terpasang di PC ini).

```bash
dotnet run --project src/Penerimaan.App
```

## Build installer

1. Publish self-contained supaya PC target tidak perlu install runtime:

```bash
dotnet publish src/Penerimaan.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

2. Compile installer (butuh Inno Setup terpasang):

```bash
iscc installer/setup.iss
```

Hasilnya ada di `dist/SAKEDAP-Penerimaan-Setup-1.0.0.exe`.

## Yang perlu disesuaikan

- `appsettings.json` - isi `Api.BaseUrl` dengan URL API yang sebenarnya
- `Services/ApiClient.cs` - path endpoint (`api/barang/{kode}`, `api/penerimaan`)
  dan bentuk response masih contoh
- `Services/LabelTemplate.cs` - koordinat label diasumsikan 50x30 mm @203 dpi
- Autentikasi ke API belum ada. `ApiClient.SetToken()` sudah disiapkan, tapi
  layar login dan penyimpanan token belum dibuat

## Catatan printer

Data dikirim sebagai RAW ke spooler Windows, bukan lewat GDI/`PrintDocument`.
Ini penting: kalau lewat GDI, barcode dirender jadi bitmap sehingga buram dan
lambat. Konsekuensinya, driver printer di Windows tidak boleh mengubah data -
gunakan driver vendor atau "Generic / Text Only".

Nama printer di `appsettings.json` harus persis sama dengan yang tampil di
Control Panel > Devices and Printers. Dropdown di aplikasi sudah mengisinya
otomatis dan menyimpan pilihan operator.
