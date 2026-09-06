# 03 — FIRST RUN & CHILD-FRIENDLY CALIBRATION

## 1. Mục tiêu

First run phải tạo môi trường học ổn định mà không biến thành wizard kỹ thuật dài cho trẻ.

Parent/installer làm phần kỹ thuật; trẻ chỉ thấy calibration nhẹ.

## 2. Technical first-run

Chạy trước Child UI:

```text
OS/SP1 check
→ legacy SHA-2/signature readiness probe
→ framework check
→ writable data path
→ free disk
→ SQLite open + migration
→ content hash verification
→ audio device probe
→ display/DPI probe
→ benchmark
→ choose profile
→ save hardware_profile.json
```

Nếu lỗi → Parent/Recovery screen, không đưa lỗi stack trace cho trẻ.

## 3. Hardware profile fields

```text
schema_version
created_at_utc
os_version
service_pack
process_arch=x86
os_arch
cpu_name
logical_cores
ram_total_mb
screen_width
screen_height
system_dpi
primary_refresh_hz_if_known
gpu_name_if_available
free_disk_mb
net_framework_release
legacy_sha2_readiness
sha2_evidence
code_signature_self_test
stopwatch_high_resolution
high_contrast_enabled
audio_output_available
microphone_available
benchmark_profile
benchmark_version
```

Không upload.

## 4. Child calibration

Tên hiển thị: nickname tùy chọn.

Calibration gồm:
- chọn mức âm lượng qua nút nghe thử;
- 3–5 thao tác click đơn giản để đo input/target size;
- nếu game dùng drag: có thể thử 1 drag đơn giản, nhưng calibration **không được phụ thuộc drag**; luôn có click-select/click-target hoặc non-drag equivalent;
- 3–6 câu warm-up cực ngắn từ baseline;
- đo response-time baseline cá nhân sơ bộ;
- kiểm xem instruction chữ có quá nhỏ không.

Không gọi:
- test IQ;
- kiểm tra thông minh;
- xếp hạng;
- thi đầu vào.

## 5. Không khóa profile từ vài câu đầu

Calibration chỉ tạo prior mềm.

Mastery thật phải cập nhật qua nhiều bằng chứng, delayed recall và nhiều representation.

## 6. Response-time baseline

Không so trẻ với population ngay.

Lưu median/robust range theo:
- subject;
- input type;
- question complexity band;
- representation.

Baseline tự cập nhật chậm sau các attempt tin cậy.

## 7. Motion default

- Benchmark NORMAL → Motion Normal nhưng decorative off trong question.
- Benchmark LOW → Motion Reduced mặc định.
- Parent có thể chọn Minimal bất cứ lúc nào.

## 8. Audio default

- Voice: ON.
- SFX: calm/ON.
- Music: OFF mặc định V1 để giảm tải và không đè voice.
- Mic: OFF cho tới khi parent bật/lesson cần.

## 9. Session onboarding

Lần đầu trẻ chỉ cần thấy:
1. avatar/companion;
2. nút “Bắt đầu”; 
3. một nhiệm vụ thành công ngắn;
4. hướng dẫn qua hành động, không tutorial dài.

## 10. Recalibration

Có thể chạy lại từ Parent Mode khi:
- đổi màn hình;
- đổi chuột;
- app vừa restore;
- performance thay đổi rõ;
- parent muốn giảm motion/font lớn hơn.

Không reset mastery khi recalibrate hardware/UI.