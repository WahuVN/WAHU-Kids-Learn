# AI07 Vietnamese input adapter

Module: `WAHU.TypingInput.VietnameseInputNormalizer`.

V1 integration for AI01:

1. Keep `TypingInput.rawKey` unchanged for telemetry/event payloads.
2. For a complete target/string comparison, call `VietnameseInputNormalizer.IsAccepted(target.displayText, rawTypedText)`.
3. If AI01 performs its own candidate matching, initialize `TypingTarget.acceptedInputs` from `GetAcceptedCandidates(displayText)`. This produces canonical Unicode, accent-insensitive V1, deterministic Telex, and deterministic VNI aliases.
4. For per-character comparison, use `Canonicalize` on both sides. Accent-insensitive progression should be handled against the no-diacritic target form from `RemoveVietnameseDiacritics`; do not mutate the display string.

Guarantees:
- Pure .NET Framework 4.8; no OS IME dependency.
- NFC/NFD input collapses to lowercase NFC.
- V1 always supports display-with-diacritics + user-input-without-diacritics.
- `RemoveVietnameseDiacritics(..., false)` can preserve `đ`; default V1 folds `đ -> d` for child-friendly acceptance.
- Telex/VNI support is deterministic alias compatibility, not an attempt to emulate every cursor/edit behavior of a full Vietnamese IME.

AI10 can add the project to the solution/integration shell when the shared merge lane is ready; AI07 intentionally does not edit the shared `.sln` or App project.
