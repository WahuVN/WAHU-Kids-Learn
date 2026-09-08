# INSTALLER BUILD NOTES

## Compiler

Baseline: Inno Setup 7.1.x.

## Inputs

App publish directory expected:

`D:\APP HOC TAP\build\win7_x86\publish\`

Main executable expected:

`WAHUKidsLearn.exe`

## Build without bundled .NET

```bat
ISCC.exe /DAppVersion=0.1.0 WAHU_Kids_Learn.iss
```

Nếu target PC chưa có .NET Framework 4.8, installer dừng với thông báo tiếng Việt. Không download ngầm.

## Build one-package with official .NET 4.8 offline redistributable

Chỉ dùng file redistributable lấy từ Microsoft, giữ nguyên file/hash/license tương ứng.

```bat
ISCC.exe /DAppVersion=0.1.0 /DNet48Redist="D:\prerequisites\ndp48-x86-x64-allos-enu.exe" WAHU_Kids_Learn.iss
```

Khi thiếu .NET, installer tạm extract redistributable, gọi elevation `runas`, chờ cài xong rồi kiểm lại Release key.

Không commit redistributable vào repo nếu quyền phân phối/chính sách repo chưa được xác nhận.

## Portable All-in-One cho máy Win7 thiếu .NET

Artifact này dành cho trường hợp muốn tải **một EXE duy nhất** thay vì tự tải/cài .NET trước:

`WAHU-Kids-Learn-Portable-AllInOne-win7-x86-<version>.exe`

Luồng chạy:

1. Bung payload portable vào thư mục `WAHU-Kids-Learn-Portable` cạnh file All-in-One.
2. Nếu máy đã có .NET Framework 4.8 thì mở app portable ngay.
3. Nếu thiếu .NET 4.8 thì dùng bản **Microsoft .NET Framework 4.8 Offline Installer** đã nhúng trong EXE; không tải prerequisite từ Internet lúc chạy.
4. Trước khi chạy prerequisite, bootstrapper kiểm SHA-256 đã pin. Pipeline build còn kiểm Authenticode phải `Valid` và signer phải là `Microsoft Corporation`.
5. Nếu .NET yêu cầu restart (3010), bootstrapper đăng ký `RunOnce` cho đúng app portable để mở lại một lần sau reboot.

Nguồn prerequisite được pipeline pin:

- URL Microsoft: `https://download.microsoft.com/download/f/3/a/f3a6af84-da23-40a5-8d1c-49cc10c8e76f/NDP48-x86-x64-AllOS-ENU.exe`
- Filename: `ndp48-x86-x64-allos-enu.exe`
- Bytes: `121346568`
- SHA-256: `0A3A390C47E639D0F7FC65B21195FEE6B7F65B066F80F70C60FAB191D14B7E40`
- Expected signer: `Microsoft Corporation`

Build prerequisite cache (file nằm trong `build/`, không commit binary vào Git):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build\Get-Net48OfflineRedistributable.ps1
```

Build full release kèm All-in-One:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\build\Build-SetupArtifacts.ps1 `
  -Configuration Release `
  -AppVersion 0.1.90-dev `
  -CompileInstaller -RequireInstaller `
  -CompileAllInOne `
  -Net48RedistPath .\build\prerequisites\ndp48-x86-x64-allos-enu.exe
```

`Test-AllInOneE2E.ps1` kiểm extraction, marker portable, bootstrap/schema/integrity, không tạo uninstaller và không sửa DB learner installed hiện hữu. Nhánh thực sự **thiếu .NET 4.8** vẫn phải target-test trên snapshot Windows 7 SP1 sạch trước khi claim `TARGET_SMOKE_VERIFIED`.

Đây là **portable bootstrapper**, không phải .NET runtime self-contained: nếu máy thiếu .NET 4.8 thì runtime Microsoft vẫn được cài vào Windows (cần UAC/admin), nhưng người dùng không phải tự tải/cài prerequisite và không cần Internet để tải .NET.

## Release checks

- Compile installer không warning nghiêm trọng.
- Test Win7 SP1 x86.
- Test Win7 SP1 x64 chạy app x86.
- Test machine có .NET 4.8.
- Test machine thiếu .NET 4.8.
- Test non-admin user.
- Test update giữ nguyên `%LOCALAPPDATA%\WAHU Kids Learn\data\learning.db`.
- Test uninstall giữ learner data mặc định.
- Test path có dấu/space.
- Hash installer vào `SHA256SUMS.txt`.
- Test Win7 legacy SHA-2 readiness state (clean SP1 vs updated image).
- Production: verify Authenticode SHA-256 + RFC3161 timestamp của app/installer.
- Nếu Inno `SignTool` được bật, verify signed uninstaller.
- Không lưu certificate private key/password trong repo.

## Language

Baseline `.iss` dùng `compiler:Default.isl` để compile ổn định. Không giả định Inno Setup có sẵn `Vietnamese.isl`.

Muốn Việt hóa toàn wizard: vendor một translation đã kiểm version/license vào `setup/installer/lang/`, hash và smoke-test. Xem `lang/README.md`.

## Licensing

Trước commercial release, kiểm tra license hiện hành của Inno Setup và các redistributable/dependency dùng trong gói phát hành.