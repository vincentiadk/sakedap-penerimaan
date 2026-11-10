; Inno Setup script - build dengan: iscc installer\setup.iss
; Download Inno Setup: https://jrsoftware.org/isdl.php

#define AppName "SAKEDAP Penerimaan"
; Versi diisi GitHub Actions dari tag rilis: iscc /DAppVersion=1.2.3 ...
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "SAKEDAP"
#define AppExeName "Penerimaan.exe"

[Setup]
AppId={{8B3C1A64-7E42-4C0B-9E3F-2A15D7C9F001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\SAKEDAP\Penerimaan
DefaultGroupName=SAKEDAP
OutputDir=..\dist
OutputBaseFilename=SAKEDAP-Penerimaan-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Butuh admin karena install ke Program Files
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
; Pembaruan otomatis menjalankan installer ini saat aplikasi masih terbuka.
; Installer menutupnya lewat Restart Manager; menjalankan ulang ditangani
; entri [Run] khusus mode senyap di bawah.
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}

[Languages]
Name: "id"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Buat shortcut di Desktop"; GroupDescription: "Shortcut:"

[Dirs]
; Setting per mesin (token INLIS terenkripsi DPAPI, branch, printer). Harus
; bisa ditulis user biasa karena aplikasi berjalan tanpa admin. Sengaja tidak
; dihapus saat uninstall, supaya pasang ulang tidak perlu mengisi token lagi.
Name: "{commonappdata}\SAKEDAP\Penerimaan"; Permissions: users-modify; Flags: uninsneveruninstall

[InstallDelete]
; Format lama: token polos di samping exe. Aplikasi sudah memigrasikannya ke
; ProgramData terenkripsi saat pertama dijalankan; sisa berkasnya dibuang di sini.
Type: files; Name: "{app}\appsettings.local.json"

[Files]
; Sumber = hasil dotnet publish (self-contained, lihat README)
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Jalankan {#AppName}"; Flags: nowait postinstall skipifsilent
; Setelah pembaruan otomatis (/SILENT), buka kembali aplikasinya.
Filename: "{app}\{#AppExeName}"; Flags: nowait runasoriginaluser; Check: WizardSilent
