from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACK_ROOT = ROOT / "content_packs" / "math_grade2_v1"
EVENT_PATH = PACK_ROOT / "game_events_v1.json"
MANIFEST_PATH = PACK_ROOT / "manifest.json"

EVENT_SPECS = [
    {
        "id": "m2_evt_number_sign_rescue_01",
        "kind": "quick_rescue",
        "title_vi": "Sửa biển số trong Rừng Toán",
        "intro_vi": "Một cơn gió làm ba biển số trong Rừng Toán bị lộn xộn. Con giúp đặt lại từng biển để con đường dễ nhìn nhé.",
        "completion_vi": "Ba biển số đã về đúng chỗ. Con đường trong Rừng Toán lại rõ ràng rồi.",
        "target_lesson_id": "m2_ls_num_count_read_write_0_1000",
        "target_skill_id": "NUM_COUNT_READ_WRITE_0_1000",
        "question_count": 3,
        "checkpoint_nouns_vi": ["biển số đầu tiên", "biển số thứ hai", "biển số cuối cùng"],
        "theme": "forest_path",
        "repair_copy_vi": "Mình xem từng hàng của số rồi sửa một bước nhỏ nhé.",
        "break_copy_vi": "Con có thể dừng ở đây. Những biển đã đặt đúng đã được lưu để lần sau mình tiếp tục.",
        "reward_presentation": "garden_progress",
    },
    {
        "id": "m2_evt_hundred_station_restore_01",
        "kind": "quick_rescue",
        "title_vi": "Khôi phục các trạm trăm",
        "intro_vi": "Ba trạm trong khu vườn bị mờ nhãn. Con nhận ra các số tròn trăm để gắn lại nhãn cho từng trạm nhé.",
        "completion_vi": "Các trạm trăm đã có nhãn rõ ràng và khu vườn lại dễ tìm đường.",
        "target_lesson_id": "m2_ls_num_full_hundreds_recognize",
        "target_skill_id": "NUM_FULL_HUNDREDS_RECOGNIZE",
        "question_count": 3,
        "checkpoint_nouns_vi": ["trạm Mầm", "trạm Lá", "trạm Hoa"],
        "theme": "hundred_station",
        "repair_copy_vi": "Mình nhìn hai chữ số cuối trước rồi chọn lại nhé.",
        "break_copy_vi": "Con nghỉ cũng được. Nhãn của các trạm đã sửa vẫn được giữ nguyên để lần sau mình tiếp tục.",
        "reward_presentation": "garden_progress",
    },
    {
        "id": "m2_evt_number_path_reconnect_01",
        "kind": "quick_rescue",
        "title_vi": "Nối lại đường số",
        "intro_vi": "Ba đoạn trên đường số đang thiếu mốc nối. Con tìm số liền trước hoặc liền sau để nối đường cho đúng nhé.",
        "completion_vi": "Ba đoạn đã nối đúng thứ tự. Đường số lại liền mạch rồi.",
        "target_lesson_id": "m2_ls_num_predecessor_successor",
        "target_skill_id": "NUM_PREDECESSOR_SUCCESSOR",
        "question_count": 3,
        "checkpoint_nouns_vi": ["đoạn đầu", "đoạn giữa", "đoạn cuối"],
        "theme": "number_path",
        "repair_copy_vi": "Mình thử bớt 1 hoặc thêm 1 rồi kiểm tra lại nhé.",
        "break_copy_vi": "Mình có thể dừng tại đoạn này. Các đoạn đã nối đúng đã được lưu để lần sau mình nối tiếp.",
        "reward_presentation": "garden_progress",
    },
    {
        "id": "m2_evt_place_value_store_sort_01",
        "kind": "quick_rescue",
        "title_vi": "Sắp đúng kho hàng",
        "intro_vi": "Các thẻ trăm, chục và đơn vị trong kho bị xếp lẫn. Con giúp đặt chúng vào đúng ngăn để kho gọn lại nhé.",
        "completion_vi": "Các thẻ đã về đúng ngăn trăm, chục và đơn vị. Kho hàng đã gọn lại rồi.",
        "target_lesson_id": "m2_ls_place_value_hundreds_tens_ones",
        "target_skill_id": "PLACE_VALUE_HUNDREDS_TENS_ONES",
        "question_count": 3,
        "checkpoint_nouns_vi": ["ngăn hàng trăm", "ngăn hàng chục", "ngăn hàng đơn vị"],
        "theme": "place_value_workshop",
        "repair_copy_vi": "Mình nhìn vị trí của chữ số rồi xác định hàng trước nhé.",
        "break_copy_vi": "Nếu muốn nghỉ, con cứ dừng ở đây. Các ngăn đã sắp đúng sẽ được giữ nguyên cho lần sau.",
        "reward_presentation": "garden_progress",
    },
    {
        "id": "m2_evt_expanded_number_machine_repair_01",
        "kind": "quick_rescue",
        "title_vi": "Sửa máy ghép số",
        "intro_vi": "Máy ghép số đang tách các hàng chưa đúng. Con giúp ghép lại phần trăm, chục và đơn vị để máy chạy êm nhé.",
        "completion_vi": "Ba bộ phận đã ghép đúng giá trị từng hàng. Máy ghép số lại chạy êm rồi.",
        "target_lesson_id": "m2_ls_num_expanded_form_hto",
        "target_skill_id": "NUM_EXPANDED_FORM_HTO",
        "question_count": 3,
        "checkpoint_nouns_vi": ["bộ phận trăm", "bộ phận chục", "bộ phận đơn vị"],
        "theme": "number_machine",
        "repair_copy_vi": "Mình tách số thành giá trị của từng hàng rồi kiểm tra lại nhé.",
        "break_copy_vi": "Con có thể nghỉ sau bộ phận này. Những phần đã ghép đúng đã được lưu để lần sau mình làm tiếp.",
        "reward_presentation": "garden_progress",
    },
]


def build() -> dict:
    return {
        "schema_version": 1,
        "catalog_id": "math_grade2_game_events_v1",
        "language": "vi",
        "events": [dict(item) for item in EVENT_SPECS],
    }


def render(catalog: dict) -> str:
    return json.dumps(catalog, ensure_ascii=False, indent=2) + "\n"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def update_manifest(event_path: Path = EVENT_PATH, manifest_path: Path = MANIFEST_PATH) -> None:
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    files = manifest.get("files")
    if not isinstance(files, list):
        raise SystemExit("Manifest files list is missing.")
    entry = {"path": "game_events_v1.json", "sha256": sha256(event_path)}
    matches = [i for i, item in enumerate(files) if isinstance(item, dict) and item.get("path") == entry["path"]]
    if len(matches) > 1:
        raise SystemExit("Manifest has duplicate game_events_v1.json entries.")
    if matches:
        files[matches[0]] = entry
    else:
        files.append(entry)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=4) + "\n", encoding="utf-8", newline="\n")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=EVENT_PATH)
    parser.add_argument("--no-manifest", action="store_true")
    args = parser.parse_args()
    catalog = build()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(render(catalog), encoding="utf-8", newline="\n")
    if not args.no_manifest:
        if args.output.resolve() != EVENT_PATH.resolve():
            raise SystemExit("Manifest may only be updated for the production event path; use --no-manifest for fixtures.")
        update_manifest(args.output, MANIFEST_PATH)
    print(f"WROTE game_events={len(catalog['events'])} path={args.output}")


if __name__ == "__main__":
    main()
