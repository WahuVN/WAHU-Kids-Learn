# 18 — WINDOWS 7 COMPATIBILITY, SHA-2 READINESS & CODE SIGNING

Cập nhật: 2026-09-06
Trạng thái: V1 DESIGN LOCK — implementation/test pending.

## 1. Không đồng nhất `Win7 SP1` với `target-ready`

Phân loại compatibility V1:

```text
UNSUPPORTED_OS
WIN7_SP1_BASE
WIN7_SP1_SHA2_READINESS_UNKNOWN
WIN7_SP1_RUNTIME_COMPATIBLE
TARGET_SMOKE_VERIFIED
```

`TARGET_SMOKE_VERIFIED` mới là trạng thái được phép dùng để claim máy mục tiêu chạy ổn.

## 2. SHA-2 readiness

Microsoft yêu cầu nền Win7 SP1 cũ có:
- servicing stack update KB4490628;
- SHA-2 update KB4474419 (bản September 2019 hoặc mới hơn);
- restart sau prerequisite.

Hai KB là **evidence/prerequisite lịch sử**, không được dùng như phép kiểm duy nhất vĩnh viễn vì servicing/update state có thể đã được supersede.

### Preflight nên thu thập

```text
os_version
service_pack
net48_release
sha2_readiness = PASS | WARN | UNKNOWN
sha2_evidence[]
code_signature_self_test = PASS | FAIL | NOT_RUN
```

### Không làm
- không tự tải Windows Update;
- không tự cài KB từ URL lạ;
- không báo máy “hỏng” chỉ vì không tìm thấy đúng KB ID;
- không bỏ qua failure khi chính binary/release signature verification fail.

## 3. Signature self-test

Release nên có một binary/resource đã ký giống release policy để preflight/QA kiểm:
1. Windows có thể load app binary;
2. signature chain/digest được verify theo policy của target;
3. hash release đúng manifest.

Đây là evidence thực tế tốt hơn chỉ đọc danh sách hotfix.

## 4. Authenticode release policy

Baseline:
- file digest SHA-256;
- RFC3161 timestamp;
- timestamp digest SHA-256;
- verify sau ký;
- certificate/private key không nằm trong repo.

Pipeline:

```text
build unsigned
→ hash/pre-sign manifest
→ sign EXE/DLL cần publisher identity
→ verify signatures
→ build Inno installer
→ sign Setup + signed uninstaller
→ verify installer/uninstaller
→ final SHA256SUMS + release manifest
```

## 5. Inno Setup

Dùng Inno `SignTool`/signed-uninstaller support khi build machine đã cấu hình certificate/signing tool.

Không hard-code secret/certificate password trong `.iss`.

`.issig` nếu dùng chỉ là integrity/signature mechanism riêng của Inno cho file/source verification; không thay Authenticode publisher identity và không tự loại cảnh báo Unknown Publisher.

## 6. Unsigned development builds

Dev/QA nội bộ được phép unsigned nhưng phải có:
- `build_channel=dev`;
- banner Parent/Diagnostics;
- không phát nhầm thành production.

Production release gate yêu cầu signing status rõ `SIGNED_VERIFIED` hoặc explicit release exception đã ghi lý do.

## 7. Offline prerequisite package

Nếu bundle official .NET 4.8 redistributable:
- tải từ Microsoft trên máy build có Internet;
- pin filename + SHA-256 + source URL + retrieval date;
- kiểm redistribution terms;
- không tải động lúc cài.

Windows prerequisite/KB package chỉ bundle khi đã có policy/legal/source/hash cụ thể; mặc định V1 chỉ hướng dẫn phụ huynh/QA xử lý ngoài child installer.

## 8. Offline root-certificate trust

Một production signature có thể có digest/chữ ký đúng nhưng máy Win7 offline/stale vẫn không xây được publisher trust chain nếu root/intermediate state không đủ.

V1 rule:
- test **chính certificate chain dự kiến dùng production** trên disconnected target image;
- ghi riêng `signature_digest_valid` và `publisher_chain_trusted` thay vì gộp thành một boolean;
- không tự bật network/root auto-update từ child app;
- không bundle/import arbitrary root CA vào Trusted Root store từ child installer;
- nếu môi trường offline cần quản trị root trust, dùng quy trình admin/QA riêng có nguồn/hash/review; Microsoft có cơ chế CTL/SST cho disconnected environments.

Preflight/signature diagnostics phải test để network retrieval timeout của Windows không làm app treo dài.

## 9. Tests

- clean Win7 SP1 snapshot thiếu update nền;
- Win7 SP1 đã đủ SHA-2;
- Win7 SP1 x86/x64;
- signed binary verify;
- tampered binary reject/hash mismatch;
- expired signing certificate nhưng timestamp hợp lệ;
- installer/uninstaller publisher verification;
- production certificate chain on disconnected image;
- stale/missing trusted-root scenario phân biệt digest-valid vs chain-untrusted;
- certificate-validation network retrieval không làm preflight treo dài;
- offline install hoàn toàn.
