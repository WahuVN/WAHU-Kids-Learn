# 14 — RUNTIME DIRECTORY LAYOUT

## 1. Installed application

Per-user default:

```text
%LOCALAPPDATA%\Programs\WAHU Kids Learn\
  WAHUKidsLearn.exe
  WAHUKidsLearn.exe.config
  WAHU.Data.dll
  System.Data.SQLite.dll
  e_sqlite3.dll
  data\
    schema\
      001_initial.sql
  assets\
    ui\
    sprites\
    instructional\
  content_builtin\
    math_grade2\
    english_grade2\
  licenses\
  release_manifest.json
  unins*.exe
```

Không chứa learner DB ở đây.

## 2. User data

```text
%LOCALAPPDATA%\WAHU Kids Learn\
  config\
    app_settings.json
    hardware_profile.json
  data\
    learning.db
  content_user\
    <pack_id>\<version>\
  cache\
    images\
    indexes\
  logs\
  backups\
  temp\
  recovery\
```

## 3. Export path

Manual backup/diagnostics có thể mặc định:

```text
%USERPROFILE%\Documents\WAHU Kids Learn\
```

nhưng luôn cho parent chọn đường dẫn.

## 4. Portable mode

```text
WAHU-Kids-Portable\
  WAHUKidsLearn.exe
  ...runtime...
  UserData\
    config\
    data\
    cache\
    logs\
    backups\
```

## 5. Path rules

- hỗ trợ Unicode path;
- không assume drive C:;
- no temp extraction to current working directory;
- normalize path trước validation;
- atomic replace dùng cùng volume khi cần;
- cache/temp có thể xóa;
- learner data/backups không được tự cleanup như cache.

## 6. Permissions

Per-user install/data giúp current user có quyền cần thiết mà không admin.

Không ACL trick phức tạp V1.

## 7. Cleanup

Safe to delete:
- cache;
- temp cũ;
- derived indexes.

Do not auto-delete:
- learning.db;
- verified backups;
- parent notes;
- content source selected by parent.