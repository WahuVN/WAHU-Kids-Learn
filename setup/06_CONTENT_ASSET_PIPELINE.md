# 06 — CONTENT & ASSET BUILD/INSTALL PIPELINE

## 1. Rule số 1

Child Runtime chỉ load content state `VERIFIED`.

`DRAFT`, `AUTO_VALIDATED`, `HUMAN_REVIEWED`, `HOLD`, `REJECTED` không được phát cho trẻ.

## 2. Build pipeline

```text
source/provenance
→ curriculum map
→ generator/template
→ deterministic answer validation
→ language/range/unit validation
→ asset semantic validation
→ duplicate/ambiguity detection
→ human review where required
→ VERIFIED
→ pack build
→ checksums
→ runtime smoke test
```

## 3. Asset source strategy

### Math/instructional
- SVG/procedural source of truth;
- deterministic geometry/data;
- build-time render PNG nếu static;
- procedural GDI+ nếu số liệu động.

### English semantic images
- image phải một nghĩa mục tiêu rõ;
- human semantic review;
- no text leakage khi task nghe → ảnh;
- provenance/license metadata.

### Character/world
- sprite sheet PNG;
- low frame-count animation;
- optional decorative asset separate from instructional.

## 4. Runtime asset formats V1

Preferred:
- PNG 32-bit/24-bit;
- sprite sheets;
- WAV PCM short clips;
- JSON manifests.

Avoid core:
- video;
- Lottie;
- animated SVG;
- WebP nếu decoder phải kéo dependency đáng kể;
- huge transparent layers.

## 5. Build-time SVG

SVG.NET nếu dùng chỉ ở tool/build pipeline:
- verify SVG manifest;
- render target sizes;
- compare expected dimensions;
- output PNG;
- hash output;
- child runtime không cần renderer SVG.

## 6. Pack layout

```text
pack_root/
  manifest.json
  curriculum_map.json
  skills.json
  lessons/
  questions/
  images/
  audio/
  atlas/
  checksums.json
  provenance.json
```

## 7. Runtime install

External pack import:
1. copy archive/temp;
2. enforce max compressed size;
3. inspect entries before extraction;
4. reject absolute path / `..` traversal / invalid filename;
5. enforce max file count/uncompressed size;
6. extract temp;
7. schema validation;
8. checksum validation;
9. content validator;
10. parent confirmation;
11. atomic move into content store;
12. mark active only after smoke load.

## 8. Pack rollback

Active pack switch là atomic metadata change.

Không sửa pack in-place.

Mỗi version ở directory riêng.

## 9. Cache

Derived cache (rendered thumbnails/index) có thể xóa và rebuild.

Không backup cache.

## 10. Copyright/license

Mỗi non-original asset phải có:
- source URL;
- author nếu yêu cầu;
- license;
- attribution text nếu cần;
- redistribution permission.

Không lấy Pinterest/TPT/random Google image làm production source.

## 11. Adversarial import tests

Content importer phải có fixture/test cho:
- zip-slip `../` và absolute paths;
- duplicate path sau normalize/case-fold hợp filesystem target;
- executable/script payload;
- archive khai báo nhỏ nhưng giải nén cực lớn;
- quá nhiều file nhỏ;
- malformed/corrupt archive;
- nested archive nếu policy cấm;
- symlink/reparse payload nếu extractor có thể tạo;
- Unicode filename/path edge cases;
- manifest/checksum mismatch.

Validator phải inspect limits/path trước extraction khi format cho phép; extract chỉ vào temp unique directory rồi mới atomic move.