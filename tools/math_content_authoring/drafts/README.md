# Math Grade 2 — pool-6 shadow authoring

Thư mục này lưu **pool-6 authored expansion source** từng được phát triển ở shadow lane và nay đã được promote vào production authoring sau Request 009.

## Nội dung

- `pool6_specs_01.py` … `pool6_specs_04.py`: 201 câu tương lai `_04/_05/_06`, đúng 3 câu cho mỗi 67 skill.
- `generate_pool6_preview.py`: ghép runtime 201 hiện tại với draft thành preview deterministic 402 câu.
- `pool6_preview/lesson_catalog_pool6_preview.json`: 67 lesson × 6 practice IDs, 2 câu mỗi difficulty.
- `pool6_preview/question_bank_pool6_preview.json`: shadow bank 402 câu.

## Gate

Request 009 đã publish tại `05cdb2a`. Production `generate_grade2_content.py` nạp các spec `_04/_05/_06` ở đây và sinh runtime **402 câu**; preview sau publish phải là exact mirror của runtime 402.

Kiểm tra:

```powershell
python tools/math_content_authoring/drafts/generate_pool6_preview.py
python tools/math_content_validator/validate_math_pool6_draft.py
python -m unittest discover tests/MathContentDataSmoke -p "test*.py" -v
```

Production validator hiện hiểu cả structured diagnosis và explicit wrong→correct contrast; **402/402 runtime questions phải pass trực tiếp**. Pool-6 validator tiếp tục khóa deterministic rebuild, 67×6, `_01..06`, 2 câu/difficulty và preview/runtime parity.
