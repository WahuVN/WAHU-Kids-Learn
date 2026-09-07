# -*- coding: utf-8 -*-
"""Draft pool-6 specs: skills 21-40."""


def build(g):
    nq, mc = g.nq, g.mc
    return {
        "DIVISION_MEANING": [
            nq("Có 12 chiếc kẹo chia đều cho 2 bạn. Mỗi bạn nhận mấy chiếc?", 6, "Chia 12 thành 2 phần bằng nhau thì mỗi phần có 6 chiếc.", numeric_max=10),
            nq("20 que tính được xếp thành các bó 5 que. Có tất cả bao nhiêu bó?", 4, "Mỗi bó 5 que nên 20 : 5 = 4 bó.", numeric_max=10),
            nq("Có 10 quả chia đều vào 5 đĩa. Nam lấy 10 - 5 và nói mỗi đĩa 5 quả. Mỗi đĩa thực sự có bao nhiêu quả?", 2, "Chia đều 10 quả vào 5 đĩa phải dùng 10 : 5 = 2; phép trừ không mô tả việc chia đều.", numeric_max=10),
        ],
        "MULTIPLICATION_COMPONENTS": [
            mc("Trong 5 × 4 = 20, số 4 được gọi là gì?", "thừa số", ["tích", "thương", "số chia"], "Hai số 5 và 4 được nhân với nhau nên đều là thừa số."),
            mc("Trong 2 × 9 = 18, số nào là tích?", "18", ["2", "9", "11"], "Kết quả của phép nhân được gọi là tích, nên 18 là tích."),
            mc("Lan gọi 25 là thừa số trong 5 × 5 = 25. Cách nói nào đúng?", "5 là thừa số, 25 là tích", ["25 là thừa số, 5 là tổng", "5 là tích, 25 là thừa số", "25 là thương, 5 là số chia"], "Hai số 5 là các thừa số; kết quả 25 là tích."),
        ],
        "DIVISION_COMPONENTS": [
            mc("Trong 18 : 2 = 9, số 2 mang tên gì?", "số chia", ["số bị chia", "thương", "tích"], "2 là số dùng để chia 18 nên là số chia."),
            mc("Trong 35 : 5 = 7, số nào là thương?", "7", ["35", "5", "30"], "Kết quả của phép chia được gọi là thương, nên thương là 7."),
            mc("Mai nói trong 40 : 5 = 8 thì 40 là số chia. Cặp tên nào đúng cho 40 và 8?", "số bị chia và thương", ["số chia và thương", "thương và số bị chia", "số hạng và tổng"], "40 là lượng được đem chia nên là số bị chia; 8 là kết quả nên là thương."),
        ],
        "TIMES_TABLE_2": [
            nq("Tính 2 × 7.", 14, "Theo bảng nhân 2, 2 × 7 = 14.", numeric_max=20),
            nq("Tính 2 × 10.", 20, "Hai nhóm 10 hoặc mười nhóm 2 đều cho tích 20.", numeric_max=20),
            nq("Có 8 xe đạp, mỗi xe có 2 bánh. Bạn An tính 8 + 2. Có tất cả đúng bao nhiêu bánh?", 16, "Tám nhóm 2 bánh là 8 × 2 = 16; phép cộng 8 + 2 không đếm đủ tám xe.", numeric_max=20),
        ],
        "TIMES_TABLE_5": [
            nq("Tính 5 × 6.", 30, "Theo bảng nhân 5, 5 × 6 = 30.", numeric_max=50),
            nq("Tính 5 × 9.", 45, "Chín nhóm 5 cho 45.", numeric_max=50),
            nq("Có 7 bó hoa, mỗi bó 5 bông. Lan ghi 12 vì lấy 7 + 5. Tổng số bông đúng là bao nhiêu?", 35, "Bảy nhóm 5 bông là 7 × 5 = 35; 7 + 5 không biểu diễn đủ các nhóm.", numeric_max=50),
        ],
        "DIVIDE_TABLE_2": [
            nq("Tính 14 : 2.", 7, "Vì 2 × 7 = 14 nên 14 : 2 = 7.", numeric_max=10),
            nq("Tính 18 : 2.", 9, "Vì 2 × 9 = 18 nên thương là 9.", numeric_max=10),
            nq("Có 20 chiếc tất, ghép thành từng đôi 2 chiếc. Minh nói cần 8 đôi. Cần đúng bao nhiêu đôi?", 10, "20 : 2 = 10 đôi; kiểm tra 10 × 2 = 20.", numeric_max=10),
        ],
        "DIVIDE_TABLE_5": [
            nq("Tính 10 : 5.", 2, "Vì 5 × 2 = 10 nên 10 : 5 = 2.", numeric_max=10),
            nq("Tính 45 : 5.", 9, "Vì 5 × 9 = 45 nên thương là 9.", numeric_max=10),
            nq("Có 20 bút chia vào các hộp, mỗi hộp 5 bút. Bạn Bình nói cần 5 hộp. Cần đúng bao nhiêu hộp?", 4, "20 : 5 = 4 hộp; bốn nhóm 5 bút ghép lại đúng 20.", numeric_max=10),
        ],
        "OPERATION_MEANING_FROM_VISUAL": [
            mc("Ba nhóm 2 chấm được gộp thành một nhóm lớn. Phép tính nào biểu diễn đúng số chấm?", "2 + 2 + 2", ["3 + 2", "6 - 2", "6 : 2"], "Gộp các lượng là quan hệ cộng; ba nhóm 2 được biểu diễn bằng 2 + 2 + 2."),
            mc("Có 9 chấm, gạch bỏ 4 chấm. Phép tính nào mô tả số chấm còn nhìn thấy?", "9 - 4", ["9 + 4", "8 - 4", "4 - 3"], "Gạch bỏ một phần khỏi lượng ban đầu là quan hệ trừ, nên dùng 9 - 4."),
            mc("Bức hình có 4 hàng bằng nhau, mỗi hàng 5 chấm. Hoa chọn 4 + 5 để tìm tất cả. Phép tính nào sửa đúng?", "4 × 5", ["4 + 5", "5 - 4", "5 + 5"], "Bốn nhóm bằng nhau, mỗi nhóm 5 chấm, phải dùng phép nhân 4 × 5."),
        ],
        "WP_ONE_STEP_ADD_MORE": [
            nq("Nam có 32 thẻ, được tặng thêm 16 thẻ. Nam có tất cả bao nhiêu thẻ?", 48, "Từ 'thêm' cho biết số lượng tăng: 32 + 16 = 48."),
            nq("Kho có 214 hộp, nhập thêm 325 hộp. Sau khi nhập có bao nhiêu hộp?", 539, "214 + 325 = 539."),
            nq("Buổi sáng cửa hàng nhận 420 chai, buổi chiều nhận thêm 150 chai. Bình định lấy 420 - 150. Tổng số chai nhận được là bao nhiêu?", 570, "Cả hai lượt đều là lượng nhận vào nên phải cộng: 420 + 150 = 570."),
        ],
        "WP_ONE_STEP_SUB_LESS": [
            nq("Có 58 chiếc lá, bỏ đi 25 chiếc. Còn lại bao nhiêu chiếc?", 33, "Số lá bị bớt khỏi lượng ban đầu: 58 - 25 = 33."),
            nq("Kho có 965 túi, chuyển đi 432 túi. Còn bao nhiêu túi?", 533, "965 - 432 = 533."),
            nq("Lớp có 70 tờ bìa, đã dùng 26 tờ. An lại cộng 70 + 26. Số tờ chưa dùng đúng là bao nhiêu?", 44, "Đã dùng nghĩa là bớt khỏi lượng ban đầu: 70 - 26 = 44."),
        ],
        "WP_ONE_STEP_MORE_THAN": [
            nq("Mai có 18 huy hiệu. Linh có nhiều hơn Mai 9 huy hiệu. Linh có bao nhiêu huy hiệu?", 27, "Linh nhiều hơn lượng mốc 9 nên lấy 18 + 9 = 27."),
            nq("Dây A dài 140 cm. Dây B dài hơn dây A 25 cm. Dây B dài bao nhiêu cm?", 165, "140 + 25 = 165 cm.", unit="cm"),
            nq("Kệ dưới có 350 quyển. Kệ trên nhiều hơn kệ dưới 140 quyển. Nam lại trừ 350 - 140. Kệ trên thực sự có bao nhiêu quyển?", 490, "Đại lượng cần tìm nhiều hơn lượng mốc nên phải cộng: 350 + 140 = 490."),
        ],
        "WP_ONE_STEP_LESS_THAN": [
            nq("An có 42 viên bi. Bình có ít hơn An 13 viên. Bình có bao nhiêu viên?", 29, "Bình ít hơn lượng mốc 13 nên lấy 42 - 13 = 29."),
            nq("Sợi dây đỏ dài 85 cm. Dây xanh ngắn hơn 15 cm. Dây xanh dài bao nhiêu cm?", 70, "85 - 15 = 70 cm.", unit="cm"),
            nq("Thùng lớn có 720 quả. Thùng nhỏ ít hơn 210 quả. Mai cộng 720 + 210. Thùng nhỏ đúng có bao nhiêu quả?", 510, "Thùng nhỏ ít hơn nên phải trừ phần chênh lệch: 720 - 210 = 510."),
        ],
        "WP_ONE_STEP_MULTIPLICATION_CONTEXT": [
            nq("Có 4 bàn, mỗi bàn 2 bạn. Có tất cả bao nhiêu bạn?", 8, "Bốn nhóm 2 bạn: 4 × 2 = 8.", numeric_max=50),
            nq("Có 3 hộp, mỗi hộp 5 viên phấn. Có tất cả bao nhiêu viên?", 15, "Ba nhóm 5 viên: 3 × 5 = 15.", numeric_max=50),
            nq("Một đội có 5 hàng, mỗi hàng 2 bạn. Hoa lấy 5 + 2 = 7. Đội có đúng bao nhiêu bạn?", 10, "Năm nhóm 2 bạn là 5 × 2 = 10; phép cộng hai số không đếm đủ năm hàng.", numeric_max=50),
        ],
        "WP_ONE_STEP_DIVISION_CONTEXT": [
            nq("Có 16 quả chia đều cho 2 rổ. Mỗi rổ có bao nhiêu quả?", 8, "16 : 2 = 8 quả mỗi rổ.", numeric_max=10),
            nq("Có 40 chiếc bút, mỗi hộp xếp 5 chiếc. Cần bao nhiêu hộp?", 8, "40 : 5 = 8 hộp.", numeric_max=10),
            nq("Có 30 nhãn dán chia đều thành 5 phần. Minh lấy 30 - 5. Mỗi phần thực sự có bao nhiêu nhãn?", 6, "Chia đều 30 thành 5 phần phải dùng 30 : 5 = 6.", numeric_max=10),
        ],
        "WP_SELECT_OPERATION_ONE_STEP": [
            mc("Có 18 viên bi, ăn bớt 6 viên rồi hỏi còn lại. Phép tính nào đúng?", "18 - 6", ["18 + 6", "6 + 5", "18 + 5"], "Bớt 6 viên khỏi 18 viên và hỏi còn lại là tình huống phép trừ."),
            mc("Có 4 túi, mỗi túi 5 quả rồi hỏi tất cả. Phép tính nào phù hợp?", "4 × 5", ["4 + 5", "5 - 4", "20 : 5"], "Nhiều nhóm bằng nhau và hỏi tổng nên dùng phép nhân 4 × 5."),
            mc("Có 20 que chia đều cho 2 bạn. Lan chọn 20 - 2. Muốn tìm số que mỗi bạn cần dùng phép tính nào?", "20 : 2", ["20 + 2", "20 - 2", "2 + 2"], "Chia đều thành hai phần bằng nhau phải dùng phép chia 20 : 2."),
        ],
        "POINT_RECOGNIZE": [
            mc("Tên nào phù hợp để ghi cạnh một vị trí được đánh dấu là một điểm?", "M", ["MN", "3 cm", "đường M"], "Một điểm thường được đặt tên bằng một chữ cái in hoa như M."),
            mc("Điều nào đúng khi nói về điểm P?", "P chỉ một vị trí", ["P có độ dài 5 cm", "P có hai đầu mút", "P kéo dài mãi về hai phía"], "Điểm biểu diễn một vị trí và không có độ dài."),
            mc("Hình đánh dấu bốn vị trí P, Q, R, S. Có bao nhiêu điểm đã được đặt tên?", "4", ["1", "2", "3"], "Mỗi chữ P, Q, R, S đặt tên cho một điểm riêng, nên có 4 điểm."),
        ],
        "LINE_SEGMENT_RECOGNIZE": [
            mc("Đoạn thẳng PQ có những điểm nào là hai đầu mút?", "P và Q", ["chỉ P", "chỉ Q", "không có đầu mút"], "Tên đoạn thẳng PQ cho biết hai đầu mút là P và Q."),
            mc("Mô tả nào nhận ra một đoạn thẳng?", "Nét thẳng bị giới hạn bởi hai đầu mút", ["Nét thẳng kéo dài mãi hai phía", "Nét cong không có đầu mút", "Một chấm chỉ vị trí"], "Đoạn thẳng là phần thẳng nằm giữa hai đầu mút."),
            mc("Bạn nối thẳng C với D và dừng nét ở đúng hai điểm đó. Tên hình thích hợp là gì?", "đoạn thẳng CD", ["đường thẳng CD", "đường cong CD", "điểm CD"], "Nét thẳng bị giới hạn bởi C và D là đoạn thẳng CD."),
        ],
        "CURVE_RECOGNIZE": [
            mc("Nét nào là ví dụ của đường cong?", "Nét uốn thành một cung", ["Nét thẳng không đổi hướng", "Một điểm đơn", "Đoạn thẳng nối hai điểm"], "Nét uốn đổi hướng liên tục là một đường cong."),
            mc("Một nét vẽ lượn như sóng thuộc loại đường nào?", "đường cong", ["đường thẳng", "đoạn thẳng", "điểm"], "Nét lượn sóng thay đổi hướng nên là đường cong."),
            mc("Nét từ A đi thẳng một đoạn rồi uốn vòng về bên trái. Nhận xét nào chính xác?", "Nét có phần cong vì đã uốn đổi hướng", ["Toàn bộ vẫn là đường thẳng", "Phải khép kín mới được gọi là cong", "Nét chỉ là một điểm"], "Chỉ cần nét uốn và đổi hướng thì đã có phần đường cong; không cần khép kín."),
        ],
        "STRAIGHT_LINE_RECOGNIZE": [
            mc("Đặc điểm nào phù hợp với đường thẳng?", "Không uốn và có thể kéo dài về hai phía", ["Có đúng hai đầu mút", "Luôn tạo thành hình kín", "Luôn uốn cong"], "Đường thẳng giữ cùng hướng và có thể kéo dài về cả hai phía."),
            mc("Nét d không cong và không bị giới hạn ở hai đầu. d là gì?", "đường thẳng", ["đoạn thẳng", "đường cong", "điểm"], "Nét không cong và có thể kéo dài không giới hạn là đường thẳng."),
            mc("Ba điểm M, N, P đều được đánh dấu trên đường thẳng a. Phát biểu nào đúng?", "M, N, P cùng thuộc a", ["a chỉ là đoạn MN", "P không thuộc a", "a có hai đầu mút M và P"], "Dữ kiện cho cả ba điểm đều nằm trên cùng đường thẳng a."),
        ],
        "POLYLINE_RECOGNIZE": [
            mc("Hình nào tạo thành một đường gấp khúc?", "Ba đoạn thẳng nối tiếp tại các đầu", ["Ba đoạn thẳng rời nhau", "Một nét cong duy nhất", "Một điểm và một đoạn rời"], "Đường gấp khúc gồm nhiều đoạn thẳng nối tiếp nhau."),
            mc("Các đoạn MN, NP, PQ nối tiếp nhau tạo thành hình gì?", "đường gấp khúc MNPQ", ["đoạn thẳng MQ", "đường thẳng MQ", "đường cong MNPQ"], "Ba đoạn nối tiếp tại N và P tạo thành đường gấp khúc MNPQ."),
            mc("Một đường gấp khúc mở đi qua sáu điểm liên tiếp. Nó gồm bao nhiêu đoạn thẳng?", "5", ["3", "4", "6"], "Giữa sáu điểm liên tiếp có năm khoảng nối, nên tạo năm đoạn thẳng."),
        ],
    }
