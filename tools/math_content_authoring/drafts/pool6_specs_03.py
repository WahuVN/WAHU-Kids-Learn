# -*- coding: utf-8 -*-
"""Draft pool-6 specs: skills 41-60."""


def build(g):
    nq, mc, tf, uq, iq = g.nq, g.mc, g.tf, g.uq, g.iq
    return {
        "THREE_COLLINEAR_POINTS": [
            mc("Các điểm D, E, F đều nằm trên cùng đường thẳng m. Ba điểm này được gọi là gì?", "thẳng hàng", ["không thẳng hàng", "tạo thành tam giác", "chỉ D và E thẳng hàng"], "Ba điểm cùng thuộc một đường thẳng là ba điểm thẳng hàng."),
            mc("P và Q nằm trên đường a, còn R nằm ngoài đường a. Kết luận nào đúng?", "P, Q, R không thẳng hàng", ["P, Q, R thẳng hàng", "R chắc chắn nằm giữa P và Q", "Không thể dùng vị trí của R để kết luận"], "R không thuộc đường thẳng đi qua P và Q nên ba điểm không thẳng hàng."),
            mc("Bạn Lan muốn kiểm tra A, B, C có thẳng hàng hay không. Cách nào trực tiếp nhất?", "Xem cả ba điểm có cùng nằm trên một đường thẳng", ["Đo xem AB và BC có bằng nhau", "Xem tên ba điểm có liên tiếp", "Đếm số chữ cái trong tên điểm"], "Điều kiện cần kiểm tra là cả ba điểm cùng thuộc một đường thẳng."),
        ],
        "QUADRILATERAL_RECOGNIZE": [
            nq("Một hình kín có đúng bốn cạnh là tứ giác. Hình đó có bao nhiêu cạnh?", 4, "Theo định nghĩa, tứ giác có đúng bốn cạnh."),
            mc("Trong các hình sau, hình nào chắc chắn thuộc nhóm tứ giác?", "hình vuông", ["hình tam giác", "hình có 5 cạnh", "đường cong kín"], "Hình vuông có bốn cạnh nên là một tứ giác."),
            mc("Minh vẽ một hình kín gồm bốn đoạn thẳng nhưng gọi đó là tam giác. Tên nào sửa đúng?", "hình tứ giác", ["hình tam giác", "đường thẳng", "đường gấp khúc mở"], "Hình kín có bốn cạnh là tứ giác, không phải tam giác."),
        ],
        "CYLINDER_RECOGNIZE": [
            mc("Đồ vật nào gần với khối trụ hơn các vật còn lại?", "ống đựng bút tròn", ["quả bóng", "quyển sách", "tấm giấy phẳng"], "Ống tròn có hai đáy gần hình tròn và mặt cong xung quanh, phù hợp dạng khối trụ."),
            mc("Đặc điểm nào giúp nhận ra khối trụ?", "Có hai đáy tròn và mặt cong xung quanh", ["Có một đỉnh nhọn", "Có sáu mặt vuông", "Không có đáy"], "Khối trụ có hai đáy tròn song song và một mặt cong bao quanh."),
            mc("Một lon hình trụ đặt nằm ngang. Hai mặt phẳng ở hai đầu của lon gần hình gì?", "hình tròn", ["hình tam giác", "hình vuông", "một điểm"], "Hai đáy của khối trụ vẫn là hình tròn dù vật được xoay theo hướng nào."),
        ],
        "SPHERE_RECOGNIZE": [
            mc("Vật nào gần dạng khối cầu?", "viên bi tròn", ["lon nước", "hộp sữa chữ nhật", "thước kẻ"], "Viên bi tròn đều theo mọi hướng nên gần dạng khối cầu."),
            mc("Mô tả nào đúng về khối cầu?", "Không có cạnh và không có đỉnh", ["Có hai đáy tròn", "Có bốn cạnh", "Có một đỉnh nhọn"], "Khối cầu tròn đều và không có cạnh hay đỉnh."),
            mc("An nói quả bóng và lon đều là khối cầu vì cùng có mặt cong. Điểm nào cho thấy lon khác khối cầu?", "Lon có hai đáy phẳng còn khối cầu thì không", ["Khối cầu có hai đáy tròn như lon", "Khối cầu có cạnh thẳng", "Lon không có mặt cong"], "Khối trụ có hai đáy phẳng; khối cầu không có đáy phẳng."),
        ],
        "DRAW_SEGMENT_GIVEN_LENGTH": [
            nq("Trên thước, đầu M ở vạch 4 cm. Đoạn MN dài 3 cm và N ở bên phải M. N nằm ở vạch nào?", 7, "Đi từ vạch 4 thêm 3 cm nên N ở vạch 7 cm.", unit="cm", numeric_max=20),
            iq("Trên thước cm, chọn hai đầu mút ở vạch 2 và vạch 10. Đoạn thẳng tạo được dài bao nhiêu cm?", 8, "Độ dài bằng khoảng cách giữa hai vạch: 10 - 2 = 8 cm.", numeric_max=20),
            nq("Bạn muốn vẽ đoạn PQ dài 7 cm, đặt P ở vạch 1 cm và Q ở bên phải. Minh chọn Q ở vạch 7. Q phải chuyển tới vạch nào?", 8, "Từ vạch 1 cần đi thêm 7 cm nên Q ở vạch 8; chọn vạch 7 chỉ tạo đoạn dài 6 cm.", unit="cm", numeric_max=20),
        ],
        "FOLD_CUT_COMPOSE_SHAPES": [
            mc("Hai mảnh tam giác bằng nhau được ghép sát theo một cạnh, không chồng lên nhau. Kết quả có thể là loại hình nào?", "một hình kín mới", ["một điểm duy nhất", "một đường thẳng vô hạn", "một khối cầu"], "Ghép các cạnh phù hợp của hai mảnh có thể tạo thành một hình kín mới."),
            mc("Một tờ giấy hình chữ nhật được cắt bằng một đường thẳng đi xuyên từ cạnh này sang cạnh đối diện. Ít nhất tờ giấy tách thành bao nhiêu mảnh?", "2", ["1", "3", "4"], "Một đường cắt xuyên hết tờ giấy chia nó thành hai phần."),
            mc("Có hai mảnh tam giác. Muốn ghép thành một hình kín lớn hơn, thao tác nào hợp lý?", "Đặt các cạnh phù hợp sát nhau mà không chồng hai mảnh", ["Để hai mảnh cách xa nhau", "Chỉ chạm hai đỉnh và để hở", "Chồng cả hai mảnh lên đúng một vị trí"], "Ghép hình cần các cạnh tiếp xúc phù hợp để tạo đường bao kín mà không chồng mảnh."),
        ],
        "HEAVIER_LIGHTER": [
            mc("Trên cân hai đĩa, đĩa chứa vật A thấp hơn đĩa chứa vật B. A thường thế nào so với B?", "nặng hơn", ["nhẹ hơn", "bằng nhau", "không thể so sánh"], "Khi cân hoạt động đúng, phía thấp hơn là phía nặng hơn."),
            mc("Nếu hộp X nhẹ hơn hộp Y thì hộp Y thế nào so với X?", "nặng hơn", ["nhẹ hơn", "bằng nhau", "không có quan hệ"], "Quan hệ nhẹ hơn và nặng hơn đảo chiều khi đổi thứ tự hai vật."),
            mc("Hai đĩa cân nằm ngang sau khi đặt hai túi lên. Kết luận nào phù hợp với phép cân đó?", "Hai túi có khối lượng bằng nhau", ["Túi trái nặng hơn", "Túi phải nặng hơn", "Một túi chắc chắn rỗng"], "Cân thăng bằng cho thấy hai phía có khối lượng bằng nhau trong phép cân."),
        ],
        "MASS_KG_READ_WRITE": [
            nq("Một thùng hàng ghi 7 kg. Phần số của khối lượng là bao nhiêu?", 7, "Nhãn 7 kg cho biết phần số là 7 và đơn vị là kg.", unit="kg"),
            mc("Trong các kí hiệu kg, cm, l, km, kí hiệu nào dùng cho kilôgam?", "kg", ["cm", "l", "km"], "Kilôgam được viết tắt là kg."),
            uq("Một túi nặng 4 kg, thêm vào túi khác nặng 3 kg. Hãy nhập tổng khối lượng kèm đơn vị.", 7, "kg", "Hai khối lượng cùng đơn vị kg nên 4 + 3 = 7 kg.", aliases=["kilôgam"]),
        ],
        "CAPACITY_LITER_READ_WRITE": [
            nq("Can nước ghi 6 l. Phần số của dung tích là bao nhiêu?", 6, "Nhãn 6 l cho biết dung tích có phần số 6.", unit="l"),
            mc("Kí hiệu nào dùng để ghi đơn vị lít trong các lựa chọn sau?", "l", ["kg", "cm", "km"], "Lít được kí hiệu bằng l."),
            nq("Bình có 5 l nước, rót thêm 3 l. Bạn Hoa lấy 5 - 3. Bình thực sự có bao nhiêu lít?", 8, "Rót thêm làm dung tích nước tăng: 5 l + 3 l = 8 l.", unit="l"),
        ],
        "LENGTH_DM_M_KM_RECOGNIZE_RELATION": [
            nq("3 m bằng bao nhiêu dm?", 30, "Mỗi mét bằng 10 dm nên 3 m = 30 dm.", unit="dm"),
            nq("Một quãng đường dài đúng 1 km. Nếu ghi bằng mét thì phần số là bao nhiêu?", 1000, "Theo quan hệ đơn vị đã học, 1 km = 1000 m.", unit="m"),
            mc("Để nói chiều dài một chiếc bàn học, đơn vị nào hợp lý hơn km?", "m", ["km", "kg", "l"], "Chiều dài bàn phù hợp với đơn vị mét; km dùng cho quãng đường dài."),
        ],
        "TIME_DAY_24_HOURS": [
            nq("Từ một thời điểm đến đúng cùng thời điểm vào ngày hôm sau là bao nhiêu giờ?", 24, "Một ngày đầy đủ có 24 giờ.", unit="giờ"),
            mc("Khoảng thời gian đủ một ngày đầy đủ được gọi tương đương với lựa chọn nào?", "24 giờ", ["12 giờ", "60 phút", "2 giờ"], "Theo quan hệ thời gian, một ngày đầy đủ bằng 24 giờ."),
            tf("Từ 9 giờ sáng hôm nay đến 9 giờ sáng hôm sau là một ngày đầy đủ.", True, "Hai thời điểm cùng giờ ở hai ngày liên tiếp cách nhau 24 giờ, tức một ngày đầy đủ."),
        ],
        "TIME_HOUR_60_MINUTES": [
            nq("Một giờ đầy đủ có bao nhiêu phút?", 60, "Một giờ đầy đủ gồm 60 phút, nên số phút cần điền là 60.", unit="phút"),
            mc("30 phút so với 1 giờ là khoảng thời gian thế nào?", "ngắn hơn", ["dài hơn", "bằng nhau", "không thể so sánh"], "Một giờ có 60 phút; 30 nhỏ hơn 60 nên 30 phút ngắn hơn 1 giờ."),
            mc("Một hoạt động kéo dài từ 10 giờ đến 11 giờ cùng buổi. Cách gọi nào tương đương khoảng thời gian đó?", "60 phút", ["24 giờ", "30 phút", "2 ngày"], "Từ 10 giờ đến 11 giờ là đúng một giờ, tương đương 60 phút."),
        ],
        "CALENDAR_DAYS_IN_MONTH_DATE": [
            nq("Tháng 6 có bao nhiêu ngày?", 30, "Tháng 6 có 30 ngày.", unit="ngày"),
            nq("Tháng 7 có bao nhiêu ngày?", 31, "Tháng 7 có 31 ngày.", unit="ngày"),
            mc("Ngày đang xem là 20 tháng 11. Ngày liền sau là ngày nào?", "21 tháng 11", ["19 tháng 11", "20 tháng 12", "22 tháng 11"], "Ngày liền sau tăng số ngày thêm 1 và vẫn trong cùng tháng, nên là 21 tháng 11."),
        ],
        "MONEY_VND_NOTE_RECOGNITION": [
            mc("Khi quan sát hình một tờ tiền, chi tiết nào đáng tin cậy nhất để đọc giá trị?", "Con số mệnh giá đi cùng đơn vị đồng", ["Màu nền của tờ tiền", "Kích thước hình trang trí", "Vị trí của hình vẽ"], "Giá trị cần được nhận biết từ con số mệnh giá và đơn vị đồng thể hiện trên tờ tiền."),
            tf("Hai tờ tiền có màu gần giống nhau thì chắc chắn có cùng giá trị.", False, "Màu sắc không đủ để kết luận giá trị; cần đọc con số mệnh giá và đơn vị đồng."),
            mc("Hai hình tờ tiền có con số mệnh giá khác nhau. Muốn xếp chúng từ giá trị nhỏ đến lớn, em nên làm gì trước?", "Đọc con số mệnh giá trên từng tờ rồi so sánh", ["Chỉ nhìn màu để đoán", "Chỉ so kích thước hình vẽ", "Chọn tờ có nhiều chữ hơn"], "Phải đọc giá trị được in trên từng tờ rồi mới so sánh, không dựa vào màu hay hình trang trí."),
        ],
        "MEASURE_WITH_RULER_CM": [
            nq("Một đoạn bắt đầu ở vạch 0 cm và kết thúc ở vạch 6 cm. Độ dài là bao nhiêu cm?", 6, "Độ dài bằng 6 - 0 = 6 cm.", unit="cm"),
            nq("Một que đặt từ vạch 4 cm đến vạch 12 cm. Que dài bao nhiêu cm?", 8, "Vì đầu que không ở vạch 0 nên lấy 12 - 4 = 8 cm.", unit="cm"),
            nq("Lan đo từ vạch 5 cm đến vạch 13 cm nhưng đọc 13 cm vì nhìn vạch cuối. Số đo đúng là bao nhiêu cm?", 8, "Phải tính khoảng cách giữa hai đầu: 13 - 5 = 8 cm.", unit="cm"),
        ],
        "MEASURE_WITH_COMMON_SCALE": [
            nq("Thang chia đều ghi 0, 3, 6, 9. Mỗi khoảng tăng bao nhiêu đơn vị?", 3, "Hiệu giữa hai vạch liên tiếp luôn là 3."),
            nq("Các vạch 20, 30, __, 50, 60 cách đều. Vạch trống mang giá trị nào?", 40, "Mỗi bước tăng 10 nên giữa 30 và 50 là 40."),
            nq("Thang chia đều bắt đầu 5, vạch kế là 10. Vạch thứ năm mang giá trị nào?", 25, "Các vạch lần lượt 5, 10, 15, 20, 25."),
        ],
        "CLOCK_MINUTE_HAND_AT_3_OR_6": [
            mc("Kim phút chỉ số 3, kim giờ vừa qua số 2. Đồng hồ đang chỉ mấy giờ?", "2 giờ 15 phút", ["2 giờ 30 phút", "3 giờ 15 phút", "2 giờ 3 phút"], "Kim phút ở số 3 tương ứng 15 phút; kim giờ vừa qua 2 nên là 2 giờ 15 phút."),
            mc("Kim phút ở số 6, kim giờ nằm giữa 9 và 10. Thời gian đúng là gì?", "9 giờ 30 phút", ["9 giờ 15 phút", "10 giờ 30 phút", "6 giờ 9 phút"], "Kim phút ở số 6 tương ứng 30 phút; kim giờ giữa 9 và 10 nên là 9 giờ 30 phút."),
            mc("Đồng hồ đang ở 3 giờ 15 phút. Kim phút đi tiếp tới số 6, kim giờ vẫn giữa 3 và 4. Thời gian mới là gì?", "3 giờ 30 phút", ["3 giờ 45 phút", "4 giờ 30 phút", "6 giờ 30 phút"], "Từ vị trí số 3 đến số 6 của kim phút chuyển từ 15 phút sang 30 phút; giờ vẫn là 3."),
        ],
        "MEASUREMENT_CONVERT_CALCULATE_LEARNED_UNITS": [
            nq("Một dải ruy-băng dài 4 m. Viết số đo đó bằng dm thì phần số là bao nhiêu?", 40, "Mỗi mét bằng 10 dm nên 4 m = 40 dm.", unit="dm"),
            nq("Một bao nặng 4 kg và bao khác nặng 5 kg. Tổng khối lượng là bao nhiêu kg?", 9, "Hai số đo cùng đơn vị kg nên cộng 4 + 5 = 9 kg.", unit="kg"),
            nq("Đoạn dây dài 3 m nối thêm 4 dm. Tổng chiều dài là bao nhiêu dm?", 34, "3 m = 30 dm; 30 dm + 4 dm = 34 dm.", unit="dm"),
        ],
        "MEASUREMENT_ESTIMATE_BASIC": [
            mc("Chiều dài một chiếc tẩy học sinh hợp lý nhất gần giá trị nào?", "5 cm", ["5 m", "50 m", "500 km"], "Một chiếc tẩy nhỏ thường dài vài xăng-ti-mét; 5 cm là mốc hợp lý."),
            mc("Chiều cao một chiếc bàn học hợp lý nhất gần mức nào?", "1 m", ["1 km", "10 m", "1 cm"], "Bàn học có kích thước cỡ mét; 1 m hợp lý hơn các mốc quá lớn hoặc quá nhỏ."),
            mc("Thanh chuẩn dài 10 cm. Một vật nhìn dài khoảng ba lần thanh chuẩn. Ước lượng hợp lý là bao nhiêu?", "30 cm", ["3 cm", "300 cm", "10 cm"], "Ba lần mốc 10 cm cho khoảng 30 cm."),
        ],
        "POLYLINE_LENGTH_SUM_SEGMENTS": [
            nq("Đường gấp khúc gồm hai đoạn 5 cm và 7 cm. Tổng độ dài là bao nhiêu cm?", 12, "Cộng độ dài từng đoạn: 5 + 7 = 12 cm.", unit="cm"),
            nq("Ba đoạn liên tiếp dài 2 cm, 6 cm và 4 cm. Cả đường gấp khúc dài bao nhiêu cm?", 12, "2 + 6 + 4 = 12 cm.", unit="cm"),
            nq("Đường gấp khúc MNPQ có MN = 8 cm, NP = 3 cm, PQ = 5 cm. Minh chỉ cộng 8 + 5. Tổng đúng là bao nhiêu cm?", 16, "Phải cộng đủ cả ba đoạn: 8 + 3 + 5 = 16 cm; Minh đã bỏ sót NP.", unit="cm"),
        ],
    }
