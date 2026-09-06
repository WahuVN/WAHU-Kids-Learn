# 09 — SECURITY & PRIVACY SETUP

## 1. Threat model V1

App chạy trên máy gia đình cũ, offline-first.

Ưu tiên phòng:
- corrupt/malicious content pack;
- path traversal/archive bomb;
- accidental data deletion;
- child vào Parent Mode;
- dependency tampering;
- crash giữa DB write.

Không xây hệ thống enterprise/network security không cần thiết.

## 2. Network

V1 vẫn offline-first và không có network chung cho Child Mode:
- không mở port;
- không local web server;
- không analytics endpoint;
- không ads SDK;
- không auto-download lesson/content pack cho Child Mode;
- **chỉ updater installed-mode** được outbound HTTPS tới release feed khóa cứng `WahuVN/WAHU-Kids-Learn`.

Updater không gửi learner data/nickname/attempt/diagnostics lên GitHub. Request chỉ lấy release manifest và installer. Mất mạng/TLS/GitHub lỗi phải fail-safe: app vẫn học offline, không hiện lỗi kỹ thuật cho trẻ và chỉ throttle lần check kế tiếp.

Update trust:
- SHA-256 installer luôn bắt buộc;
- URL/owner/repo/channel/size được validate fail-closed;
- stable production bắt buộc Authenticode/cache-only trust trước khi chạy installer;
- updater helper chạy ngoài process app để không tự ghi đè executable đang sử dụng;
- learner DB được verified-backup trước khi cài staged update.

## 3. Parent PIN

Không lưu plaintext.

Dùng:
- random salt per profile/install;
- PBKDF2-HMAC-SHA256 nếu framework implementation đã verify;
- iteration được benchmark để khoảng trễ chấp nhận được trên PC thật;
- lưu algorithm + iterations + salt + hash.

Không dùng security question của trẻ.

## 4. Sensitive data minimization

Không cần lưu:
- địa chỉ;
- số điện thoại;
- email trẻ;
- location;
- ad ID;
- contact list;
- browser history.

Nickname là đủ.

## 5. Content import security

Reject:
- `..` path;
- absolute paths;
- executable/script trong pack;
- symlink/reparse payload nếu extractor hỗ trợ;
- quá nhiều files;
- uncompressed size vượt limit;
- duplicate normalized path;
- invalid UTF filename;
- manifest/hash mismatch.

Extract vào temp unique directory rồi mới validate/move.

## 6. File trust

Built-in release có SHA-256 manifest.

External content:
- hash + schema + validator + Parent approval.

Future signature có thể thêm nhưng V1 không giả rằng checksum = publisher identity.

## 7. Data deletion

Delete profile:
- Parent PIN;
- explicit confirmation;
- backup/export option trước;
- không undo giả.

Uninstall không xóa learner data mặc định.

## 8. Log privacy

Diagnostics không ghi:
- nickname nếu không cần;
- raw voice;
- free-text parent note;
- full answer text nếu question ID đủ.

## 9. Child Mode escape

Parent Mode:
- hidden/small parent affordance;
- Ctrl+Shift+P optional;
- luôn yêu cầu PIN nếu đã bật.

Không dùng câu hỏi kiến thức để khóa Parent Mode.

## 10. Retention/minimization policy

Mỗi field dữ liệu động phải có mục đích rõ. Không thu/lưu dữ liệu “để sau này có thể dùng”.

- raw mic recording mặc định ephemeral;
- diagnostics aggregate, không raw voice/free-text nếu không cần;
- temp/import artifacts có cleanup TTL;
- manual backup giữ theo Parent intent;
- profile deletion flow nêu rõ backup nào còn tồn tại.

## 11. Archive bomb/path traversal tests

Ngoài schema/hash validation phải test:
- compression ratio/uncompressed ceiling;
- file count ceiling;
- `..`/absolute/normalized duplicate paths;
- nested archive policy;
- malformed archive;
- executable/script entry;
- temp-only extraction trước atomic install.