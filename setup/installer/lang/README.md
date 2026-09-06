# INSTALLER LANGUAGE POLICY

## Baseline compile-safe

`WAHU_Kids_Learn.iss` hiện dùng `compiler:Default.isl` để tránh giả định Inno Setup cài sẵn một file Vietnamese `.isl` cụ thể.

Các chuỗi riêng của WAHU (task, lỗi prerequisite, launch text...) vẫn viết tiếng Việt trong `.iss`.

## Khi muốn Việt hóa toàn bộ wizard

Không sửa đường dẫn thành `compiler:Languages\Vietnamese.isl` nếu chưa xác minh file thật tồn tại trên toolchain release.

Quy trình đúng:
1. lấy bản dịch Vietnamese tương thích đúng Inno Setup major/minor từ nguồn được tin cậy;
2. kiểm license/attribution;
3. review các chuỗi quan trọng bằng người Việt;
4. lưu file đã duyệt trong `setup/installer/lang/`;
5. pin SHA-256 vào release manifest;
6. đổi `[Languages]` sang file local của dự án;
7. compile + smoke-test installer trên Win7 SP1.

Ví dụ sau khi có file đã duyệt:

```ini
[Languages]
Name: "vietnamese"; MessagesFile: "lang\Vietnamese.isl"
```

Không tải translation động trong lúc cài.
