# -*- coding: utf-8 -*-
"""Draft pool-6 specs: skills 01-20."""


def build(g):
    nq, mc, tf, eq = g.nq, g.mc, g.tf, g.eq
    return {
        "NUM_COUNT_READ_WRITE_0_1000": [
            nq("Số gồm 8 trăm, 2 chục và 5 đơn vị được viết thế nào?", 825, "8 trăm là 800, 2 chục là 20 và 5 đơn vị là 5; ghép đúng vị trí được 825."),
            mc("Số 603 được đọc đúng theo cách nào?", "sáu trăm linh ba", ["sáu trăm ba mươi", "sáu mươi ba", "ba trăm linh sáu"], "603 có 6 trăm, 0 chục và 3 đơn vị nên phải giữ hàng chục bằng 0 khi đọc."),
            nq("Tấm thẻ ghi 9 trăm, 0 chục, 6 đơn vị nhưng bạn Hoa viết 960. Hãy viết lại số đúng.", 906, "0 chục phải nằm ở hàng chục; 900 + 0 + 6 = 906."),
        ],
        "NUM_FULL_HUNDREDS_RECOGNIZE": [
            mc("Trong bốn số 300, 330, 303, 333, số nào là số tròn trăm?", "300", ["330", "303", "333"], "300 có hàng chục và hàng đơn vị đều bằng 0 nên là số tròn trăm."),
            tf("900 có đúng 9 trăm và không có thêm chục hay đơn vị.", True, "900 = 9 trăm, 0 chục, 0 đơn vị nên mệnh đề đúng."),
            mc("Một hộp ghi 5 trăm đầy đủ, không thêm chục hoặc đơn vị. Nhãn nào khớp?", "500", ["550", "505", "50"], "Năm trăm đầy đủ được viết 500; hai chữ số cuối đều phải bằng 0."),
        ],
        "NUM_PREDECESSOR_SUCCESSOR": [
            nq("Số đứng ngay trước 700 là số nào?", 699, "Số liền trước nhỏ hơn số đã cho đúng 1; 700 - 1 = 699."),
            nq("Số đứng ngay sau 849 là số nào?", 850, "Số liền sau lớn hơn đúng 1; 849 + 1 = 850."),
            nq("Minh điền số 600 vào ô ngay sau 598 trên dãy số liên tiếp. Số nào mới đúng?", 599, "Dãy số liên tiếp tăng từng 1 đơn vị, nên sau 598 là 599."),
        ],
        "PLACE_VALUE_HUNDREDS_TENS_ONES": [
            nq("Số 731 có 3 chục. Giá trị của chữ số 3 trong số này là bao nhiêu?", 30, "Chữ số 3 đứng ở hàng chục nên có giá trị 30."),
            nq("Số 905 có chữ số hàng đơn vị là mấy?", 5, "905 gồm 9 trăm, 0 chục và 5 đơn vị."),
            nq("Bạn Linh nói trong 286, chữ số 8 có giá trị 800. Giá trị đúng của chữ số 8 là bao nhiêu?", 80, "Trong 286, chữ số 8 nằm ở hàng chục nên có giá trị 80, không phải 800."),
        ],
        "NUM_EXPANDED_FORM_HTO": [
            mc("Chọn cách khai triển đúng của 641.", "600 + 40 + 1", ["600 + 4 + 1", "60 + 40 + 1", "600 + 40 + 10"], "641 có 6 trăm, 4 chục, 1 đơn vị nên bằng 600 + 40 + 1."),
            nq("Ghép các giá trị 700 + 9 thành số có ba chữ số nào?", 709, "Không có chục nên hàng chục là 0; 700 + 9 = 709."),
            mc("Bạn Nam khai triển 480 thành 400 + 8. Phương án nào sửa đúng?", "480 = 400 + 80", ["480 = 40 + 80", "480 = 400 + 8", "480 = 400 + 70"], "480 có 4 trăm, 8 chục và 0 đơn vị nên phần chục phải là 80."),
        ],
        "NUMBER_RAY_FILL": [
            nq("Tia số có các mốc 100, 200, 300, __, 500. Mốc còn trống là số nào?", 400, "Các mốc tăng đều 100 nên sau 300 là 400."),
            nq("Trên tia số, 420, 440, __, 480 cách đều nhau. Điền mốc còn thiếu.", 460, "Mỗi bước tăng 20: 420, 440, 460, 480."),
            nq("Bắt đầu ở 250 trên tia số, mỗi bước sang phải tăng 50. Sau 4 bước em đứng ở số nào?", 450, "250 → 300 → 350 → 400 → 450, nên bốn bước đến 450."),
        ],
        "NUM_COMPARE_0_1000": [
            mc("Chọn dấu thích hợp cho 709 __ 790.", "<", [">", "=", "không thể so sánh"], "Cùng 7 trăm nhưng 0 chục nhỏ hơn 9 chục nên 709 < 790."),
            mc("So sánh 836 __ 829 bằng dấu nào?", ">", ["<", "=", "không đủ dữ kiện"], "Cùng 8 trăm; 3 chục lớn hơn 2 chục nên 836 > 829."),
            mc("Kho A có 508 hộp, kho B có 580 hộp. Quan hệ nào mô tả đúng hai số lượng?", "508 < 580", ["508 > 580", "508 = 580", "chưa thể kết luận"], "Hai số cùng 5 trăm nhưng 0 chục nhỏ hơn 8 chục, nên 508 < 580."),
        ],
        "NUM_MIN_MAX_UP_TO_4": [
            nq("Trong 612, 621, 216, 261, số nhỏ nhất là số nào?", 216, "Hai số bắt đầu bằng 2 nhỏ hơn các số bắt đầu bằng 6; giữa 216 và 261 thì 216 nhỏ hơn."),
            nq("Trong 808, 880, 800, 818, số lớn nhất là số nào?", 880, "Cùng 8 trăm; 880 có hàng chục 8 lớn nhất nên là số lớn nhất."),
            nq("Một trò chơi yêu cầu đặt thẻ nhỏ nhất vào ô đầu. Các thẻ là 657, 675, 756, 765. Sau khi loại hai thẻ 7 trăm, em nên đặt thẻ nào vào ô đầu?", 657, "Hai thẻ 756 và 765 có 7 trăm nên lớn hơn hai thẻ 6 trăm. Giữa 657 và 675, 5 chục nhỏ hơn 7 chục nên thẻ nhỏ nhất là 657."),
        ],
        "NUM_SORT_UP_TO_4": [
            mc("Xếp 410, 401, 140, 104 theo thứ tự từ bé đến lớn.", "104, 140, 401, 410", ["410, 401, 140, 104", "104, 401, 140, 410", "140, 104, 410, 401"], "So hàng trăm trước rồi hàng chục, đơn vị: 104 < 140 < 401 < 410."),
            mc("Xếp 630, 603, 360, 306 theo thứ tự từ lớn đến bé.", "630, 603, 360, 306", ["306, 360, 603, 630", "630, 360, 603, 306", "603, 630, 306, 360"], "Các số 6 trăm đứng trước các số 3 trăm; trong từng cặp tiếp tục so hàng chục."),
            mc("Bốn thẻ 202, 220, 200, 222 cần đặt tăng dần. Dãy nào hoàn chỉnh đúng?", "200, 202, 220, 222", ["222, 220, 202, 200", "200, 220, 202, 222", "202, 200, 222, 220"], "Cùng 2 trăm nên so hàng chục rồi đơn vị: 200 < 202 < 220 < 222."),
        ],
        "ESTIMATE_OBJECTS_BY_TENS": [
            nq("Có khoảng 4 nhóm chục que tính. Ước lượng số que là khoảng bao nhiêu?", 40, "Bốn nhóm 10 tương ứng khoảng 40 que."),
            nq("Một rổ nhìn có khoảng 9 chục hạt. Nói số lượng gần đúng theo chục.", 90, "9 chục là khoảng 90 hạt."),
            mc("Một hộp có 4 nhóm đủ 10 nút áo và thêm khoảng 6 nút rời. Nếu chỉ báo số lượng gần đúng theo chục, em nên nói khoảng bao nhiêu nút?", "khoảng 50", ["khoảng 40", "khoảng 60", "khoảng 46"], "4 nhóm đủ 10 là 40; thêm khoảng 6 thành khoảng 46. Vì 46 gần 50 hơn 40 nên nói khoảng 50."),
        ],
        "ADD_COMPONENTS_RECOGNIZE": [
            mc("Trong phép cộng 34 + 22 = 56, số 22 mang tên gì?", "số hạng", ["tổng", "hiệu", "số trừ"], "34 và 22 là hai số được cộng nên đều là số hạng."),
            mc("Phép cộng 120 + 70 = 190 có tổng là số nào?", "190", ["120", "70", "50"], "Kết quả của phép cộng được gọi là tổng, nên tổng là 190."),
            mc("Mai gọi 45 là tổng trong 45 + 30 = 75. Cách gọi nào sửa đúng?", "45 là số hạng, 75 là tổng", ["45 là tổng, 30 là hiệu", "30 là tổng, 75 là số hạng", "75 là số trừ, 45 là tổng"], "45 là một số được cộng nên là số hạng; 75 là kết quả nên là tổng."),
        ],
        "SUB_COMPONENTS_RECOGNIZE": [
            mc("Trong 83 - 21 = 62, số 83 được gọi là gì?", "số bị trừ", ["số trừ", "hiệu", "tổng"], "Số đứng trước dấu trừ là số bị trừ."),
            mc("Trong phép tính 96 - 40 = 56, số 56 mang tên gì?", "hiệu", ["số bị trừ", "số trừ", "số hạng"], "Kết quả của phép trừ được gọi là hiệu."),
            mc("An nói trong 150 - 20 = 130 thì 150 là số trừ. Cặp tên nào đúng cho 150 và 20?", "số bị trừ và số trừ", ["số trừ và số bị trừ", "hiệu và số trừ", "số hạng và tổng"], "150 là lượng ban đầu đứng trước dấu trừ; 20 là lượng được bớt đi."),
        ],
        "ADD_WITHIN_1000_NO_CARRY": [
            nq("Tính 321 + 246.", 567, "Cộng từng hàng: 1 + 6 = 7, 2 + 4 = 6, 3 + 2 = 5; không hàng nào cần nhớ."),
            nq("Tính 530 + 240.", 770, "0 + 0 = 0, 3 chục + 4 chục = 7 chục, 5 trăm + 2 trăm = 7 trăm; được 770."),
            nq("Tủ A có 413 quyển, tủ B có 352 quyển. Không có cột nào vượt 9 khi cộng. Hai tủ có tất cả bao nhiêu quyển?", 765, "413 + 352 = 765; từng hàng cộng trực tiếp, không cần nhớ."),
        ],
        "ADD_WITHIN_1000_ONE_CARRY_MAX": [
            nq("Tính 235 + 148.", 383, "5 + 8 = 13, viết 3 nhớ 1; 3 + 4 + 1 = 8; 2 + 1 = 3."),
            nq("Tính 467 + 215.", 682, "7 + 5 = 12 tạo một lượt nhớ; hàng chục và hàng trăm sau đó cộng trực tiếp, được 682."),
            nq("Lớp A góp 356 tờ giấy, lớp B góp 127 tờ. Bạn Minh nói tổng là 473 vì quên cộng 1 nhớ ở hàng chục. Tổng đúng là bao nhiêu?", 483, "6 + 7 = 13, viết 3 nhớ 1; 5 + 2 + 1 = 8; 3 + 1 = 4, nên tổng 483."),
        ],
        "SUB_WITHIN_1000_NO_BORROW": [
            nq("Tính 865 - 342.", 523, "5 - 2 = 3, 6 - 4 = 2, 8 - 3 = 5; không cần mượn."),
            nq("Tính 770 - 250.", 520, "0 - 0 = 0, 7 chục - 5 chục = 2 chục, 7 trăm - 2 trăm = 5 trăm."),
            nq("Kho có 987 hộp, chuyển ra 654 hộp. Mỗi chữ số trên đều không nhỏ hơn chữ số dưới cùng hàng. Còn bao nhiêu hộp?", 333, "987 - 654 = 333 và không hàng nào cần mượn."),
        ],
        "SUB_WITHIN_1000_ONE_BORROW_MAX": [
            nq("Tính 462 - 147.", 315, "Mượn 1 chục: 12 - 7 = 5; còn 5 chục, 5 - 4 = 1; 4 - 1 = 3."),
            nq("Tính 753 - 236.", 517, "Mượn một chục ở hàng đơn vị: 13 - 6 = 7; còn 4 chục, 4 - 3 = 1; 7 - 2 = 5."),
            nq("Cửa hàng có 842 chai, bán 325 chai. Bạn Lan ghi 527 vì không giảm hàng chục sau khi mượn. Số chai còn lại đúng là bao nhiêu?", 517, "Mượn 1 chục để tính 12 - 5 = 7; hàng chục còn 3 nên 3 - 2 = 1; 8 - 3 = 5."),
        ],
        "ADD_SUB_TWO_OPERATORS_LEFT_TO_RIGHT": [
            nq("Tính theo thứ tự từ trái sang phải: 80 - 20 + 15.", 75, "80 - 20 = 60, rồi 60 + 15 = 75."),
            eq("Tính 90 + 25 - 10. Có thể nhập kết quả hoặc biểu thức cộng, trừ tương đương.", "90 + 25 - 10", 105, "90 + 25 = 115, rồi 115 - 10 = 105."),
            nq("Một hộp có 160 thẻ, lấy ra 40 thẻ rồi thêm 70 thẻ. Bạn Nam cộng 160 + 40 + 70. Còn đúng bao nhiêu thẻ?", 190, "Tình huống phải làm từ trái sang phải: 160 - 40 = 120, rồi 120 + 70 = 190."),
        ],
        "MENTAL_ADD_SUB_WITHIN_20": [
            nq("Tính nhẩm 7 + 8.", 15, "Bù 3 cho 7 thành 10, còn 5; 10 + 5 = 15.", numeric_max=20),
            nq("Tính nhẩm 16 - 7.", 9, "Có thể trừ 6 để về 10 rồi trừ tiếp 1; kết quả 9.", numeric_max=20),
            nq("Túi có 18 viên bi, lấy ra 5 viên. Bạn An nói còn 14. Hãy tìm số còn lại đúng.", 13, "18 - 5 = 13; có thể nhẩm 18 - 3 = 15 rồi trừ thêm 2.", numeric_max=20),
        ],
        "MENTAL_ADD_SUB_ROUND_TENS_HUNDREDS_1000": [
            nq("Tính nhẩm 60 + 20.", 80, "6 chục + 2 chục = 8 chục, tức 80."),
            nq("Tính nhẩm 800 - 300.", 500, "8 trăm - 3 trăm = 5 trăm, tức 500."),
            nq("Sân trường có 500 ghế, chuyển thêm 200 ghế tới. Minh nói tổng là 520 vì cộng chữ số 2 vào hàng chục. Tổng đúng là bao nhiêu?", 700, "5 trăm + 2 trăm = 7 trăm, nên 500 + 200 = 700."),
        ],
        "MULTIPLICATION_MEANING": [
            mc("Có 4 nhóm, mỗi nhóm 2 hình tròn. Cách cộng lặp lại nào biểu diễn đủ các nhóm?", "2 + 2 + 2 + 2", ["2 + 2 + 2", "2 + 2 + 2 + 2 + 2", "2 + 2"], "Bốn nhóm, mỗi nhóm 2 nghĩa là cộng số 2 đúng bốn lần."),
            nq("3 giỏ, mỗi giỏ 5 quả có tất cả bao nhiêu quả?", 15, "Ba nhóm bằng nhau, mỗi nhóm 5: 3 × 5 = 15.", numeric_max=50),
            nq("Có 5 hộp, mỗi hộp 2 bút. Bình tính 5 + 2 = 7. Muốn đếm đủ năm nhóm, kết quả phải là bao nhiêu?", 10, "Năm nhóm 2 bút là 5 × 2 = 10; phép cộng 5 + 2 không đếm đủ số nhóm.", numeric_max=50),
        ],
    }
