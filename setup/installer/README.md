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