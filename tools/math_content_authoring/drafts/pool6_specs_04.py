# -*- coding: utf-8 -*-
"""Draft pool-6 specs: skills 61-67."""


def build(g):
    nq, mc = g.nq, g.mc
    return {
        "MEASUREMENT_REAL_WORLD_ONE_STEP": [
            nq("Một sợi dây dài 4 m, nối thêm đoạn 2 m. Tổng chiều dài là bao nhiêu mét?", 6, "Hai số đo cùng đơn vị mét nên 4 + 2 = 6 m.", unit="m"),
            nq("Can có 9 l nước, rót ra 4 l. Còn lại bao nhiêu lít?", 5, "Rót ra làm lượng nước giảm: 9 - 4 = 5 l.", unit="l"),
            nq("Bao A nặng 7 kg, bao B nặng 2 kg. Minh cộng được 9 nhưng ghi đơn vị l. Tổng khối lượng đúng là bao nhiêu kg?", 9, "Hai đại lượng đều là khối lượng nên 7 + 2 = 9 và phải giữ đơn vị kg.", unit="kg"),
        ],
        "DATA_COLLECT_CLASSIFY_COUNT": [
            nq("Các thẻ màu là: xanh, đỏ, xanh, vàng, xanh, đỏ. Có bao nhiêu thẻ xanh?", 3, "Phân loại theo màu rồi đếm được ba thẻ xanh."),
            nq("Danh sách đồ dùng: thước, bút, sách, bút, bút, thước. Có bao nhiêu chiếc thước?", 2, "Lọc các mục 'thước' rồi đếm được 2."),
            nq("Khảo sát món quả yêu thích ghi: cam, táo, chuối, cam, cam, táo, chuối. Có bao nhiêu bạn chọn táo?", 2, "Trong danh sách có hai lần xuất hiện của 'táo'."),
        ],
        "PICTOGRAPH_READ_DESCRIBE": [
            nq("Biểu đồ tranh có 6 hình bông hoa cho nhóm B; chú giải 1 hình = 1 bạn. Nhóm B có bao nhiêu bạn?", 6, "Mỗi hình đại diện 1 bạn nên 6 hình biểu diễn 6 bạn."),
            nq("Một hàng biểu đồ có 4 biểu tượng quả bóng; chú giải 1 biểu tượng = 2 quả. Hàng đó biểu diễn bao nhiêu quả?", 8, "Bốn biểu tượng, mỗi biểu tượng 2 quả: 4 × 2 = 8 quả."),
            nq("Biểu đồ có 3 biểu tượng, chú giải 1 biểu tượng = 2 quyển. Lan chỉ đếm hình và nói có 3 quyển. Số quyển thực sự là bao nhiêu?", 6, "Phải dùng chú giải: 3 biểu tượng × 2 quyển = 6 quyển."),
        ],
        "PICTOGRAPH_SIMPLE_INFERENCE": [
            nq("Biểu đồ có nhóm A 7 biểu tượng, nhóm B 5 biểu tượng; mỗi biểu tượng = 1 bạn. A nhiều hơn B bao nhiêu bạn?", 2, "7 - 5 = 2 bạn."),
            mc("Biểu đồ có nhóm Đỏ 4 biểu tượng, Xanh 8 biểu tượng, Vàng 6 biểu tượng; chú giải như nhau. Nhóm nào nhiều nhất?", "Xanh", ["Đỏ", "Vàng", "Cả ba bằng nhau"], "Xanh có 8 biểu tượng, nhiều hơn 6 và 4."),
            nq("Biểu đồ có 3 biểu tượng cho A và 6 biểu tượng cho B; 1 biểu tượng = 2 vật. B nhiều hơn A bao nhiêu vật?", 6, "B có 12 vật, A có 6 vật; 12 - 6 = 6."),
        ],
        "EVENT_POSSIBLE": [
            mc("Một vòng quay có các số 1, 2, 3, 4. Kim dừng ở số 3 là sự kiện gì?", "có thể", ["chắc chắn", "không thể", "luôn xảy ra"], "Số 3 có trên vòng quay nên có thể xuất hiện, nhưng còn các số khác nên không chắc chắn."),
            mc("Trong túi có thẻ tròn và thẻ vuông. Lấy ngẫu nhiên một thẻ tròn là sự kiện gì?", "có thể", ["chắc chắn", "không thể", "không có kết quả"], "Trong túi có thẻ tròn nên sự kiện có thể xảy ra, nhưng còn loại thẻ khác nên không chắc chắn."),
            mc("Hộp có thẻ số 2, 4, 6. Bình nói rút được số 4 chắc chắn xảy ra. Phân loại đúng là gì?", "có thể", ["chắc chắn", "không thể", "luôn sai"], "Thẻ 4 có trong hộp nên có thể rút được, nhưng còn thẻ 2 và 6 nên không chắc chắn."),
        ],
        "EVENT_CERTAIN": [
            mc("Một hộp chỉ chứa các thẻ màu xanh. Rút một thẻ xanh là sự kiện gì?", "chắc chắn", ["có thể nhưng không chắc", "không thể", "không xác định"], "Mọi thẻ trong hộp đều xanh nên rút thẻ nào cũng được màu xanh."),
            mc("Xúc xắc chuẩn có các mặt từ 1 đến 6. Kết quả gieo nhỏ hơn 7 là sự kiện gì?", "chắc chắn", ["không thể", "chỉ có thể", "luôn sai"], "Mọi mặt 1 đến 6 đều nhỏ hơn 7 nên sự kiện chắc chắn xảy ra."),
            mc("Hộp chỉ có thẻ ghi A hoặc B. Mai nói rút được A hoặc B chỉ là sự kiện có thể. Phân loại chính xác hơn là gì?", "chắc chắn", ["không thể", "có thể nhưng không chắc", "không có kết quả"], "Mọi thẻ đều là A hoặc B nên bất kỳ lần rút nào cũng thỏa điều kiện."),
        ],
        "EVENT_IMPOSSIBLE": [
            mc("Vòng quay chỉ có các số 1, 2, 3. Kim dừng ở số 6 là sự kiện gì?", "không thể", ["có thể", "chắc chắn", "luôn đúng"], "Số 6 không có trên vòng quay nên không thể xuất hiện."),
            mc("Trong túi chỉ có thẻ đỏ. Lấy được một thẻ xanh là sự kiện gì?", "không thể", ["có thể", "chắc chắn", "bằng nhau"], "Không có thẻ xanh trong túi nên sự kiện không thể xảy ra."),
            mc("Hộp chỉ chứa thẻ số 2 và 5. An nói vẫn có thể rút được số 8. Phân loại đúng là gì?", "không thể", ["có thể", "chắc chắn", "luôn xảy ra"], "Số 8 không xuất hiện trên bất kỳ thẻ nào trong hộp nên không có kết quả hợp lệ làm sự kiện xảy ra."),
        ],
    }
