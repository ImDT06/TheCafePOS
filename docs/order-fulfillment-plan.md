# Kế hoạch đơn tại quầy và xác nhận QR — 09/10/2026

## Cơ sở và phạm vi

Khảo sát tài liệu sản phẩm/API công khai, đối chiếu code hiện tại; chưa phỏng vấn nhân viên hay quan sát trực tiếp tại quán. Không khẳng định đây là quy trình nội bộ của mọi cửa hàng The Coffee House.

- Square có số ticket/tên khách để nhận diện đơn và quản lý trạng thái chuẩn bị, sẵn sàng, hoàn thành: https://squareup.com/help/us/en/article/5194-print-order-tickets và https://squareup.com/help/us/en/article/8454-manage-orders-with-square . Chỉ tham khảo cách nhận diện/điều phối; không triển khai in.
- SePay có webhook được cấu hình xác thực và Test mode: https://developer.sepay.vn/vi/sepay-webhooks/tao-webhook và https://developer.sepay.vn/vi/tien-ich-khac/test-mode/tao-webhook .
- payOS cung cấp thông báo thanh toán: https://payos.vn/docs/du-lieu-tra-ve/webhook/ . Tài liệu thử nghiệm hiện hướng dẫn production với số tiền nhỏ, không mặc định coi đây là sandbox miễn phí: https://payos.vn/docs/moi-truong-test/ .

Đề xuất áp dụng: một quầy, một máy Windows, gọi món và trả tiền tại quầy, nhận món theo số gọi. Chọn Tại chỗ/Mang đi; không bắt buộc bàn, tên hoặc điện thoại. Máy in, chuông gọi, màn hình khách riêng và nhiều máy pha chế nằm ngoài đợt này.

## Đối chiếu hiện trạng

- Order đã có ID duy nhất, số thứ tự ngày, dòng món và trạng thái thanh toán; chưa có loại đơn, tiến độ chuẩn bị/giao.
- Checkout lưu đơn, kho và ca cùng giao dịch; chống thanh toán lặp bằng ID. Tận dụng cơ chế này, không trừ kho lại khi đổi trạng thái phục vụ.
- QR hiện chỉ tạo ảnh qua VietQR và quản lý duyệt thủ công. Chưa có backend, payment intent bền vững hoặc nhận webhook.
- Đơn giữ lưu được vào SQLite; giỏ đang sửa vẫn có thể mất khi crash. Recall xóa đơn giữ trước khi giỏ mới được lưu bền vững.

## Quy tắc nghiệp vụ đề xuất

1. Giữ OrderId làm khóa nội bộ. Số gọi #025 là số dễ đọc, duy nhất trong ngày tại cửa hàng; ngày dùng Asia/Ho_Chi_Minh. Không dùng số gọi đơn độc để khớp chuyển khoản.
2. Cấp số gọi chính thức khi thanh toán được ghi nhận thành công, cùng giao dịch tạo đơn/hàng phục vụ. Số trên giỏ trước đó chỉ là dự kiến. Đơn nháp/đơn giữ chưa chiếm chỗ trong hàng phục vụ.
3. Số gọi không đổi khi sửa tiến độ, đổi ca hoặc mở lại app. Qua ngày không xóa đơn chưa giao; hiển thị thêm ngày cho đơn cũ. ID gồm ngày và số để tránh nhầm #001 của hai ngày.
4. Tách PaymentStatus, RefundStatus và FulfillmentStatus. Thanh toán thành công không có nghĩa đã giao; hoàn tiền không tự có nghĩa đã dừng pha.
5. Luồng thường: Chờ làm → Đang làm → Chờ giao → Đã giao. Xếp cũ trước theo PaidAt/sequence bền vững. FIFO là thứ tự mặc định; không khóa nhân viên phải làm xong đơn trước mới được làm đơn sau.
6. ReadyQuantity và DeliveredQuantity theo dòng món, giới hạn 0 ≤ delivered ≤ ready ≤ ordered. Dòng 3 ly có thể làm/giao 1 ly trước; không gắn "đã giao" cho cả đơn còn thiếu. Hỗ trợ nút Xong toàn bộ và Giao toàn bộ có thông tin số gọi rõ ràng. Bánh/hàng bán lẻ cũng cần được giao, không chỉ đồ uống.
7. Tất cả số lượng đã chuẩn bị thì Chờ giao; giao một phần vẫn còn trong danh sách đang phục vụ và có nhãn Giao 1/3. Giao đủ mới kết thúc. Bấm nhầm chỉ được hoàn tác bằng thao tác có ghi nhật ký; đơn đã giao cần quản lý mở lại và nhập lý do.
8. Dừng phục vụ là quyết định riêng có lý do/quản lý duyệt, không xóa đơn hoặc tự hoàn tiền. Đơn hoàn toàn bộ mà chưa giao cần quyết định dừng/tiếp tục; hoàn một phần theo tiền hiện tại không được tự suy ra món nào bị hủy. Luồng hủy một phần theo món để đợt sau.
9. Thu ngân được cập nhật tiến độ trên máy hiện tại. Lưu người thao tác, thời điểm và trước/sau. Không bắt buộc đang sở hữu ca bán mới được giao đơn đã thanh toán; đổi ca không làm mất hàng chờ.
10. Loại đơn được lưu trong giỏ nháp/đơn giữ/đơn thanh toán. Mặc định Mang đi là đề xuất ban đầu, cho đổi rõ ràng trước trả tiền; sau trả tiền thay đổi có nhật ký. Loại đơn chưa tự thay định mức bao bì: cần cấu hình vật tư thực tế trước khi áp dụng khác biệt.

## UX trên một máy

- Chọn Tại chỗ/Mang đi ngay đầu giỏ; nhãn luôn nhìn thấy trong màn hình thanh toán.
- Thanh toán xong hiện số gọi lớn, loại đơn, số món, tiền thối (nếu có); chuyển sang đơn mới mà vẫn có lối xem lại số vừa bán.
- Vì chưa in và chưa có màn hình khách, nhân viên phải đọc/cho khách xem số gọi. Phần mềm không tự biết danh tính khách; số gọi chỉ là cách đối chiếu.
- Nút Đơn đang phục vụ luôn truy cập được, có số đơn còn lại; không giấu trong Tiện ích.
- Bộ lọc Tất cả / Chờ làm / Đang làm / Chờ giao; tìm số gọi và lọc loại đơn. Thẻ hiển thị số, ngày khi cần, giờ thanh toán, thời gian chờ, tiến độ và ghi chú món.
- Chi tiết dòng món cho tăng/giảm số đã làm, giao từng phần hoặc giao đủ. Thời gian chờ tính từ PaidAt, không từ lúc bắt đầu nhập giỏ. Ngưỡng nhắc chờ là cấu hình quán, không gọi là chuẩn ngành.

## Các đợt triển khai

### 1. Phân loại và lưu trạng thái bền vững

Thêm loại đơn, PaidAt, trạng thái phục vụ, lượng đã làm/giao và lịch sử chuyển trạng thái. Lưu giỏ đang nhập theo từng thay đổi có debounce; lưu ngay trước chuyển luồng. Recall chuyển đơn giữ sang giỏ nháp theo giao dịch, checkout xóa nháp trong giao dịch chốt đơn.

Migration: đơn thanh toán lịch sử đánh dấu Legacy/không theo dõi phục vụ, loại đơn Không xác định; không tự đẩy toàn bộ đơn cũ vào hàng chờ hay gán Mang đi cho lịch sử. Đơn mới bắt đầu theo dõi. Đổi tiến độ không thay tiền, kho hoặc báo cáo bán hàng.

### 2. Hàng phục vụ và nhận món

Làm màn hình hàng phục vụ, số gọi sau thanh toán, cập nhật tiến độ theo lượng, giao một phần/giao đủ, hoàn tác có nhật ký và xử lý đơn chưa giao qua ca/ngày. Thu ngân bán và xem hàng phục vụ trên cùng máy; chưa giả định có đồng bộ máy pha chế khác.

### 3. QR tự động, triển khai tách biệt

Chọn một nhà cung cấp sau khi kiểm tra ngân hàng hỗ trợ, điều kiện tài khoản và chi phí hiện hành. Ưu tiên đánh giá SePay Test mode cho bài thực hành vì có môi trường thử riêng; không mặc định đăng ký/mua dịch vụ hoặc chuyển tiền thật.

- Tạo PaymentIntent riêng: ID, mã tham chiếu duy nhất, số tiền, nội dung đơn cố định, hạn, tài khoản nhận và ca/người tạo. Không cho sửa giỏ dưới QR đang hiệu lực; thay giỏ tạo yêu cầu mới.
- Backend HTTPS nhận webhook, xác minh theo cơ chế nhà cung cấp, kiểm tra tiền vào đúng tài khoản/VND/tham chiếu/số tiền. Khóa API/chữ ký đặt trên backend; WPF xác thực với backend, không tin thông báo phía khách hoặc ảnh chuyển khoản.
- Lưu sự kiện ngân hàng bền vững, chống trùng bằng ID giao dịch nhà cung cấp trước khi phản hồi thành công. Trạng thái thanh toán không lùi vì webhook đến sai thứ tự. WPF truy vấn trạng thái khi mất kết nối/mở lại, không cần giữ cửa sổ QR mở.
- Backend đã nhận tiền và POS đã ghi sổ là hai bước khác nhau: lưu ID sự kiện đã xử lý ở POS; chốt đơn/trừ kho/cấp số gọi một lần trong giao dịch SQLite. Nếu ca đóng/kho thiếu, giữ trạng thái Đã nhận tiền – Cần xử lý, không bỏ giao dịch hoặc yêu cầu khách trả lại.
- Tiền đến muộn, sai tham chiếu, thiếu/thừa tiền, đã thanh toán tiền mặt hoặc yêu cầu đã hủy: vào Cần đối soát. Không tự giao, tự hoàn hoặc ghi thu lần hai. Bản đầu không tự cộng nhiều chuyển khoản nhỏ để tất toán.
- Mất mạng vẫn bán tiền mặt. QR không xác minh được giữ Chờ xác nhận; duyệt thủ công nếu giữ lại phải có quản lý và tham chiếu giao dịch, rồi khử trùng khi thông báo đến sau.
- Tách rõ Test/Live, dùng dữ liệu thử trước. Thử tiền thật chỉ sau khi người dùng chủ động cấu hình và cho phép.

## Điều kiện nghiệm thu

- Không yêu cầu số bàn/tên khách trong luồng tại quầy; loại đơn tồn tại qua giữ/gọi lại/mở lại app.
- Chờ QR không xuất hiện trong hàng pha; thanh toán lặp không tạo số gọi, đơn hoặc trừ kho lần hai.
- Hai đơn liên tiếp có số gọi đúng; khởi động lại, đổi ca, qua 0 giờ không gây trùng định danh hoặc mất đơn chưa giao.
- Đơn 3 ly + 1 bánh, hoàn tất/giao một phần không bị coi là giao đủ; không thể giao vượt số đã làm.
- Crash sau gọi đơn giữ hoặc sau chốt tiền không làm mất đơn hoặc bán trùng. Đổi trạng thái không đổi doanh thu/tồn kho.
- Nhật ký ghi đúng người/giờ; thao tác mở lại/dừng phục vụ kiểm tra quyền tại service.
- Dữ liệu cũ không tự biến thành hàng chờ mới. Thu ngân xem được đơn ca trước chưa giao.
- QR thử: webhook hợp lệ, giả mạo, trùng, sai tiền, sai tài khoản, sai mã, đến trễ, đến khi POS offline, ca đã đóng, thiếu kho, chuyển tiền mặt rồi webhook đến; tất cả phải có kết quả xác định và không thu/trừ kho hai lần.

Đề xuất làm đợt 1 và 2 trước; đợt 3 chỉ triển khai live sau khi có tài khoản tích hợp, backend và quyết định nhà cung cấp. Đây là kế hoạch, chưa thay đổi nghiệp vụ hoặc kết nối dịch vụ ngoài.
