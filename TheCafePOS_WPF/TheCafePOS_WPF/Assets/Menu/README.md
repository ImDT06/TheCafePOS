# Menu The Coffee House

## Kiểm tra trực tiếp mã topping — 12/10/2026

Đã tra API `POST /api/v5/menu/product-detail` với `variants_code` của cả 22 topping. Tìm thêm và tải đúng bốn ảnh: Shot Espresso, Hạt Nổ Củ Năng, Foam Phô Mai, Hạt Nổ Yến Mạch. Kết luận trước đó chỉ dựa vào mục topping bán lẻ nên chưa đủ. Hiện 17/22 topping có ảnh chính thức; năm món Xốt Caramel, Syrup Hạnh Nhân, Shot Espresso SOE, Syrup Caramel và Syrup Tiramisu trả `thumbnail: null`, `images: []` trong chi tiết chính thức. Không gán ảnh món khác cho chúng.

Nguồn, mã sản phẩm và kết quả từng món: [topping-image-sources.csv](topping-image-sources.csv); nguồn thô lưu tại `artifacts/menu-source/topping-detail-<code>.json`. Chạy `scripts/complete-topping-images.ps1` để kiểm tra và tải bổ sung; bước này cũng được gọi khi refresh. Khi mở ứng dụng, ảnh topping còn trống được bổ sung theo snapshot kể cả dữ liệu đã ở schema 7, giữ ảnh riêng của quản lý. Rà lại 104 ảnh có nguồn đều khớp; 32 kiểm tra tùy chọn/migration/ảnh đạt. Các mục dưới đây là lịch sử kiểm tra trước lần bổ sung này.

## Rà soát ảnh combo và toàn bộ menu — 12/10/2026

Đã sửa hai ảnh gắn sai trong Ưu đãi riêng bạn: Combo Pizza & Nước và Combo Pasta & Nước trước đây dùng ảnh món lẻ. Nay dùng ảnh combo chính thức từ nhóm API `21`, lưu thành `Images/combo-pizza-nuoc.png` và `Images/combo-pasta-nuoc.png`. Cơ sở dữ liệu cũ tự sửa theo đúng ID combo và đường dẫn ảnh sai đã biết; không sửa ảnh món lẻ hay ảnh riêng do quản lý chọn.

Đã tải nguồn ảnh để so sánh nội dung hiển thị của cả 98 ảnh trong snapshot và hai ảnh combo. Sau sửa, cả 100 ảnh khớp ảnh nguồn; không phát hiện thêm trường hợp gắn nhầm ảnh trong các món đã nhập. Chín topping chưa tìm được ảnh chính thức vẫn để trống. Báo cáo từng món và URL nguồn: [image-audit.csv](image-audit.csv). Báo cáo ghi rõ ảnh nào đối chiếu menu Hanoi hiện tại, ảnh nào dùng menu mặc định đã tải trước đó; không khẳng định đã bao phủ toàn bộ sản phẩm website ngoài menu POS.

Script `scripts/audit-coffeehouse-images.ps1` dùng nguồn `artifacts/menu-source/image-audit-menu.json`, lưu bản ảnh trước sửa trong `artifacts/menu-source/image-audit`. Có thể dùng `-UseCachedDownloads` để kiểm tra lại từ các file nguồn đã tải. So sánh ảnh sau chuẩn hóa pixel, tránh nhầm khác metadata/định dạng PNG với khác hình. Build Release đạt; 29 kiểm tra tùy chọn/migration/ảnh combo đạt.

## Bổ sung theo homepage và ảnh đối chiếu của người dùng

Nguồn đúng: https://thecoffeehouse.com/homepage. Menu phụ thuộc vị trí: truy vấn với `lat=21.0285`, `long=105.8542`, `type=DELI` có đủ năm món trong ảnh; truy vấn không có tọa độ bỏ sót chúng. Đã đọc được API chi tiết với các header công khai của website (`TCH-APP-VERSION: 5.10.29`, `X-Client-Info: TCH-WEB`, device ID ngẫu nhiên). Không cần tài khoản hay đặt đơn.

Đã bổ sung Shan Nhài Cam Đào (79.000đ), Shan Xoài Chanh Leo (79.000đ), Frappe Xôi Xoài Dừa Non (89.000đ), Shan Xanh Quýt Cam và Shan Xanh Vải Hibiscus (size M mặc định 65.000đ; S 55.000đ, L 75.000đ). Size, đường/đá và topping của năm món lấy từ chi tiết API, không suy ra từ ảnh. Các món cùng xuất hiện trong Món Mới Phải Thử; nhóm chính là Trà Shan hoặc Frappe. Trạng thái hết món từng chi nhánh không được dùng làm tồn kho POS.

Đã tải 16 ảnh PNG chính thức vào `Images`: năm đồ uống và 11 topping được đối chiếu tên với mục topping bán lẻ. Tổng menu nhúng: 85 món/hàng hóa, 22 topping, 16 nhóm; 98 món/topping có ảnh, còn 9 topping trong `missing-images.csv`. Đây là bổ sung năm món người dùng chỉ ra, chưa phải toàn bộ menu của mọi địa điểm.

`scripts/import-homepage-additions.ps1` tải và nhập phần bổ sung; script refresh gọi bước này sau khi nhập menu cơ sở. Schema 7 bổ sung món còn thiếu và điền ảnh topping đang trống cho cơ sở dữ liệu cũ, giữ giá/ảnh do quản lý chỉnh và lịch sử đơn. Những ghi chú bên dưới ghi lại lần đối chiếu ban đầu, đã được đính chính ở mục này.

## Đối chiếu ngày 12/10/2026

Đã tải lại danh sách công khai bằng `POST /api/v5/menu`, body `{"type":"DELI"}`, header `src: TCH-WEB`. Menu nhúng hiện có 80 món/hàng hóa, 22 topping và 15 nhóm (bao gồm Topping). Bổ sung Combo 20/10 và Combo Sắc Xinh - Matcha Latte Tây Bắc (M), giá 119.000đ. Giá theo size của 79 món cũ không thay đổi. Nhóm quảng bá trùng món vẫn do ứng dụng quản lý riêng.

API danh sách trả phụ thu size: giá đầy đủ = `product.price + size.price`. API chi tiết món báo lỗi HTTP 500 trong lần kiểm tra này, nên các tùy chọn đường/đá và topping giữ từ bản chi tiết ngày 08/10; không khẳng định chúng đã được xác minh lại ngày 12/10. `CheckedDate` áp dụng cho danh mục và giá. Script import từ dữ liệu chi tiết từ chối giá size bằng 0 để tránh nhập nhầm phụ thu thành giá bán.

Đã kiểm tra 82 đường dẫn ảnh của 80 món và 2 topping: tất cả có file sẵn, gồm ảnh combo đã có trong workspace; không cần tải trùng. Còn 20 topping không có ảnh riêng trong dữ liệu nguồn, được liệt kê trong [missing-images.csv](missing-images.csv); chưa gán ảnh suy đoán. Hai ảnh Matcha Layers vẫn chưa có món tương ứng trong menu công khai.

Chạy `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/refresh-coffeehouse-menu.ps1 -CheckedDate 2026-10-12` từ gốc dự án để tái lập. Script lưu nguồn thô và nguồn đã chuẩn hóa trong `artifacts/menu-source`, cập nhật JSON/CSV và chỉ tải ảnh còn thiếu. Cần giữ `artifacts/menu-source/menu.json` làm nguồn tùy chọn chi tiết cũ. Các ghi chú bên dưới mô tả những lần nhập trước.

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
