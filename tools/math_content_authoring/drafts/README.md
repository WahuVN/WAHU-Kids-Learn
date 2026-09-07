# Math Grade 2 — pool-6 shadow authoring

Thư mục này là **draft/shadow only** cho Request 009. Không được load trực tiếp từ runtime.

## Nội dung

- `pool6_specs_01.py` … `pool6_specs_04.py`: 201 câu tương lai `_04/_05/_06`, đúng 3 câu cho mỗi 67 skill.
- `generate_pool6_preview.py`: ghép runtime 201 hiện tại với draft thành preview deterministic 402 câu.
- `pool6_preview/lesson_catalog_pool6_preview.json`: 67 lesson × 6 practice IDs, 2 câu mỗi difficulty.
- `pool6_preview/question_bank_pool6_preview.json`: shadow bank 402 câu.

## Gate

Runtime `content_packs/math_grade2_v1/question_bank_v1.json` phải giữ 201 câu cho tới khi AI2 publish `REQUEST_009_READY=<sha>` trong `_WORK_CLAIMS/AI2_PARALLEL_NOW.md`.

Kiểm tra:

```powershell
python tools/math_content_authoring/drafts/generate_pool6_preview.py
python tools/math_content_validator/validate_math_pool6_draft.py
python -m unittest discover tests/MathContentDataSmoke -p "test*.py" -v
```

Draft validator chạy toàn bộ production semantic validator. Chỉ classifier `missing_choice_specific_diagnosis` cho prompt family mới được xử lý bằng fallback riêng; fallback bắt buộc nêu lựa chọn sai, kết luận đúng và giữ nguyên lời giải. Mọi lỗi production khác vẫn fail cứng.
