# 13 — DEVELOPMENT ENVIRONMENT

## 1. Dev OS

Máy dev không cần là Win7.

Khuyến nghị Windows 10/11 để dùng toolchain hiện đại, nhưng build target vẫn .NET Framework 4.8/x86 và test final trên Win7 SP1 thật/VM + máy thật.

## 2. Toolchain

- Visual Studio 2022 với `.NET desktop development`.
- .NET Framework 4.8 Developer/Targeting Pack.
- Git.
- Inno Setup 7.1.x.
- Python 3.x cho content/build tools nếu cần.
- image optimizer deterministic nếu được pin.
- Windows SDK/SignTool phù hợp để Authenticode SHA-256 + RFC3161 timestamp production release.
- UI Automation/accessibility inspection tool phù hợp cho manual smoke test.

Signing certificate/private key/password không nằm trong repo hoặc build script plaintext.

## 3. Repo conventions

- UTF-8.
- line endings nhất quán.
- Vietnamese comments/docs có dấu.
- không commit generated cache/log/user DB.
- commit content source + manifests + validators.
- generated release artifact có checksum.

## 4. Solution structure dự kiến

```text
WAHUKidsLearn.sln
src/
  App/
  Core/
  Data/
  Content/
  Game/
  Parent/
  Diagnostics/
tools/
  ContentValidator/
  PackBuilder/
  AssetBuilder/
  LearnerSimulator/
tests/
setup/
content_packs/
assets/
research/
```

## 5. Build commands policy

Có script một nút:

```text
build_dev
validate_content
run_tests
build_release_x86
build_installer
verify_release
```

Không yêu cầu dev nhớ chuỗi lệnh dài.

## 6. Dependency audit

Trước release:
- list NuGet transitive deps;
- license review;
- check x86 native binaries;
- verify hash;
- ensure no accidental WebView/runtime package.

## 7. Reproducibility

Pin:
- NuGet version;
- tool versions;
- content schema;
- installer version;
- asset build options.

Không bắt buộc byte-identical ngay V1 nếu toolchain cản, nhưng release phải traceable.