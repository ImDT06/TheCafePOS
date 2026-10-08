# Menu The Coffee House

Nguồn: https://order.thecoffeehouse.com/order và API menu công khai https://api.thecoffeehouse.com/api/v5/menu, đối chiếu ngày 08/10/2026. Lấy menu mặc định với type=DELI, chưa chọn cửa hàng/địa chỉ; không phải cam kết giá/tồn kho của mọi chi nhánh.

- `thecoffeehouse.json`: snapshot nhúng vào ứng dụng, 79 món/hàng hóa + 22 topping, 14 nhóm. Bỏ nhóm quảng bá Món Mới Phải Thử để tránh trùng hai món đã có trong Pizza & Pasta.
- `menu-reference.csv`: bảng đối chiếu tên, nhóm, giá từng size, ảnh và nguồn; mở bằng Excel.
- `scripts/import-thecoffeehouse-menu.ps1`: chuyển dữ liệu API đã tải thành snapshot. Không tự ghi vào cơ sở dữ liệu cửa hàng.
- Thông tin giá và size lấy trực tiếp từ nhóm Size của API. Nhỏ=S, Vừa=M, Lớn=L; thẻ món hiển thị giá của size mặc định do API chỉ định (kèm tên size khi có nhiều lựa chọn). Giá cơ bản lưu là giá thấp nhất, phụ thu từng size luôn không âm. Sau khi chạm món, chọn size trong hộp tùy chỉnh.
- Americano Classic: S=49.000đ, M=55.000đ, L=65.000đ; Americano Nóng: M=55.000đ. Không suy ra size từ ảnh. Không tự áp dụng nhãn giảm 20% trên ảnh.
- Hình ưu tiên file người dùng trong `Images`; ảnh các món còn thiếu tải từ thumbnail chính thức vào `Images`. Các file được đóng gói khi build/publish, không cần tải ảnh khi bán hàng.
- 12/14 ảnh người dùng đã khớp: 10 đồ uống và 2 topping. `matcha-layers-dau.png`, `matcha-layers-xoai.png` chưa có món tương ứng trong snapshot công khai này; giữ nguyên file, chưa tự đặt giá hay bật bán.
- Bánh, đồ ăn và hàng lưu niệm có loại hàng bán lẻ riêng: không size ly, không đường/đá, không trừ bao bì đồ uống và không tạo tem ly. Chưa có định mức/tồn kho thực phẩm riêng.
- Topping mua kèm lấy tên, giá và danh sách món được áp dụng từ nhóm type=2. Độ ngọt/lượng đá lấy nguyên nhãn, thứ tự và mặc định từ options của từng món trong API; không quy đổi “Ít ngọt”, “Thêm ngọt” hay “Đá riêng” thành phần trăm. Số phần topping vẫn theo POS; chưa mô phỏng tùy chọn thay sữa hoặc toàn bộ ràng buộc đặt hàng của website.
- Bao bì vẫn là cấu hình nội bộ: S/M dùng pack-1, L dùng pack-2. Đây không phải thông tin dung tích/bao bì lấy từ website. Cần cấu hình bao bì thực tế (đặc biệt ly nóng và chai 1 lít) trong Quản trị khi vận hành.

## Nâng cấp dữ liệu

MenuSchemaVersion 3 nhập snapshot một lần khi mở bản ứng dụng mới. Món trùng tên/ID được cập nhật theo yêu cầu nhập menu, giữ ID cũ và ánh xạ lại topping. Các món mẫu nguyên bản được ẩn, không xóa lịch sử; món riêng đã đổi tên được giữ. Đơn đã thanh toán và đơn giữ không bị ghi lại. Đơn giữ chứa món mẫu đã ẩn cần sửa/xóa trước thanh toán. Những lần mở tiếp theo không ghi đè chỉnh sửa menu của quản lý.

Kiểm tra: build Release thành công; 147 kiểm tra nghiệp vụ/catalog/báo cáo đạt. Dựng màn hình WPF 1240×780 và 960×680; giải mã được 81 ảnh món/topping; kiểm tra chuyển size Americano M → S → L cập nhật 55.000 → 49.000 → 65.000đ. NuGet có cảnh báo NU1900 do không truy cập được nguồn kiểm tra bảo mật.

## Tùy chọn từng món (schema 4)

Đã đối chiếu đủ size và giá của 79 món với bản nguồn đã lưu ngày 08/10/2026. Xem bảng đầy đủ ở [options-reference.csv](options-reference.csv). Script import-coffeehouse-options.ps1 kiểm tra size/giá và nhập đúng danh sách đường/đá; không suy luận từ tên món hoặc ảnh. Lần đối chiếu này API không trả dữ liệu khi tải lại, nên sử dụng snapshot nguồn artifacts/menu-source/menu.json đã tải trước đó, không khẳng định dữ liệu vừa được làm mới.

Schema 4 chỉ bổ sung tùy chọn cho món đã nhập, giữ nguyên giá, size, bao bì, ảnh và trạng thái do cửa hàng chỉnh; size/giá đã có từ schema 3. Đơn cũ giữ nguyên mô tả phần trăm. Đơn giữ có lựa chọn cũ không phù hợp phải mở sửa món trước thanh toán. Khi sửa, giao diện thông báo thay đổi và chọn mặc định hợp lệ. Nhãn lựa chọn mới được lưu cùng đơn và in lên tem. Món tự tạo vẫn dùng tùy chọn phần trăm cũ. Quản trị chọn mặc định trong đúng danh sách của món.

Kiểm tra: 20 kiểm tra mới cho tùy chọn/migration/lưu đơn/in tem, 157 kiểm tra hồi quy cũ đạt (tổng 177). Kiểm tra phiên Windows chạy ngoài sandbox để truy cập DPAPI; build Release thành công, có cảnh báo NU1900 do không tải được dữ liệu bảo mật NuGet.
