# 07 — AUDIO, VOICE & MICROPHONE SETUP

## 1. Priorities

```text
instructional voice > important signal > SFX > music
```

Music không được đè voice.

## 2. V1 audio defaults

- Voice ON.
- SFX ON, calm.
- Music OFF mặc định.
- Replay button luôn cùng vị trí.
- Không giới hạn số lần nghe lại; attempt metadata ghi nghe lại nếu cần phân tích modality.

## 3. Format baseline

SFX:
- PCM WAV;
- ngắn;
- preload nhỏ.

English word/phrase voice:
- mono PCM WAV để zero-extra-codec risk;
- sample rate/bit depth benchmark theo clarity + size;
- normalize loudness tương đối giữa clips.

Nếu dung lượng voice quá lớn:
- benchmark **NAudio 2.2.1 / verified 2.x** + compressed format;
- không auto-upgrade NAudio 3 vì nhánh đó không còn target .NET Framework;
- không đổi codec chỉ vì file nhỏ hơn nếu làm deployment dễ lỗi.

BCL `SoundPlayer` là WAV-only. Với clip sắp dùng, preload bounded/async (`LoadAsync` hoặc abstraction tương đương) để tránh first-play load chặn input/UI.

## 4. Audio cache

- cache bounded theo profile;
- current/next prompt preload;
- không preload cả pack;
- dispose stream handles;
- missing clip → safe fallback text/parent diagnostic, không crash.

## 5. Microphone

Mic optional.

V1 speaking practice:
- record;
- play model;
- play child recording;
- optional self-rating icon.

Không chấm pronunciation AI giả.

## 6. Privacy

Recording mặc định có thể là ephemeral:
- lưu temp cho playback;
- xóa khi lesson kết thúc nếu parent không bật lưu.

Nếu lưu:
- local-only;
- Parent Mode toggle;
- retention limit rõ.

## 7. Audio failure

Nếu không có sound device:
- app vẫn mở;
- English listening lesson được scheduler tránh hoặc chuyển activity phù hợp;
- Parent dashboard báo audio unavailable.

## 8. SFX psychology

Đúng:
- click nhỏ;
- completion tone ấm;
- milestone ngắn.

Sai:
- buzzer lớn;
- fail sound chói;
- layered coin spam;
- random reward sound;
- âm thanh chạy khi trẻ đang nghe English target.