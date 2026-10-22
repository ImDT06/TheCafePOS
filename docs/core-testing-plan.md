# Phạm vi kiểm thử cốt lõi — TheCafePOS

Ngày lập: 21/10/2026. Mục đích: phục vụ môn Kiểm chứng phần mềm.

## 1. Phạm vi thống nhất

| Mã yêu cầu | Nhóm chức năng | Nội dung cần kiểm chứng |
|---|---|---|
| REQ-BH | Bán hàng và tính tiền | Thêm/sửa/xóa dòng món; size, topping, số lượng; tính đơn giá và tổng giỏ. |
| REQ-TT | Thanh toán | Tiền mặt thiếu/đủ/dư; điều kiện thanh toán; lưu đơn; chống thanh toán lặp; phê duyệt QR ở tầng nghiệp vụ. |
| REQ-KHO | Quản lý kho bao bì | Nhập kho, kiểm kê, điều chỉnh, trừ theo đơn; chặn tồn âm; lưu tồn và lịch sử nhất quán. |
| REQ-DN | Đăng nhập và phân quyền | Đăng nhập hợp lệ/không hợp lệ, khóa tài khoản, vai trò thu ngân/quản lý/chủ quán. |
| REQ-TD | Quản lý thực đơn | Thêm/sửa/xóa món và danh mục; kiểm tra dữ liệu; ngừng bán; bảo toàn lịch sử đơn. |

Mở ca là tiền điều kiện của kiểm thử thanh toán, chưa tách thành một nhóm chức năng cần làm đầy đủ trong bài. Kiểm thử kho chính sử dụng **bao bì**, không suy rộng kết quả sang kho nguyên liệu/công thức.

Chưa đưa vào phạm vi bài: khuyến mãi, thành viên/tích điểm, hoàn tiền, kênh giao hàng, hàng chờ pha chế, báo cáo nâng cao, máy in, sao lưu/khôi phục, đồng bộ, nâng cấp giao diện. Các chức năng này vẫn có trong phần mềm; không cần xóa để thu hẹp phạm vi kiểm thử.

Không dùng ngân hàng, SePay hoặc máy in thật trong bộ test cốt lõi. Kiểm tra QR trong bài chỉ chứng minh quy tắc phê duyệt nội bộ, không chứng minh tiền đã vào tài khoản ngân hàng.

## 2. Dữ liệu và kết quả chuẩn

- Mỗi lượt chạy dùng cơ sở dữ liệu mới trong `artifacts`, thông qua `THECAFEPOS_DATA_DIR`; không dùng dữ liệu cửa hàng.
- Tạo tài khoản kiểm thử riêng cho Owner, Manager, Cashier. Mật khẩu và tài khoản chỉ phục vụ dữ liệu thử.
- Tạo danh mục và món thử độc lập với thực đơn TCH để giá mẫu không thay đổi khi cập nhật menu.
- Món thử: giá cơ bản 25.000đ; size M phụ thu 0đ, L phụ thu 7.000đ; topping 5.000đ/phần.
- Chọn size L, 2 phần topping mỗi ly, 2 ly: đơn giá = 25.000 + 7.000 + 2 × 5.000 = **42.000đ**; thành tiền = **84.000đ**. Thêm một dòng M không topping, 1 ly: tổng giỏ = **109.000đ**.
- Với giỏ 2 ly M không topping: tổng **50.000đ**. Khách đưa 49.999đ bị từ chối; 50.000đ trả lại 0đ; 60.000đ trả lại 10.000đ.
- Bao bì thử trước bán: 10 ly M, 10 nắp, 10 ống hút. Bán 2 ly M: mỗi loại còn **8**; loại ly khác giữ nguyên.
- Tắt khuyến mãi, giảm giá, đổi điểm; dùng kênh tại quầy. Đăng nhập đúng nhân viên sở hữu ca mở trước ca thanh toán hợp lệ.

Các số trên là dữ liệu chuẩn đề xuất cho test mới; các test cũ có thể sử dụng dữ liệu tương đương. Không sửa kết quả mong đợi chỉ để khớp hành vi đang có của chương trình.

## 3. Ma trận yêu cầu và ca kiểm thử

**Có test**: đã tìm thấy assertion kiểm tra nội dung tương ứng trong mã hiện tại. **Một phần**: có test liên quan nhưng chưa đủ các biến thể/kết quả nêu trong dòng. **Cần bổ sung**: chưa tìm thấy assertion trực tiếp trong các file đã rà. Các nhãn này là trạng thái đối chiếu mã test, **không phải kết quả Pass/Fail của một lần chạy mới**.

Mỗi dòng dưới là một ca hoặc nhóm ca tham số hóa; khi triển khai, ghi kết quả riêng cho từng bộ dữ liệu. Không tính số dòng này hay số dòng `PASS` trong log thành tỷ lệ bao phủ mã nguồn.

### REQ-BH — Bán hàng và tính tiền

Nguồn: [Program.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/Program.cs), [MenuOptionsRegression.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/MenuOptionsRegression.cs), [CartChecks.cs](../scripts/ui-smoke/CartChecks.cs).

| Mã | Đầu vào / thao tác | Kết quả mong đợi | Kỹ thuật | Đối chiếu hiện tại |
|---|---|---|---|---|
| BH-01 | Chọn món, xác nhận 2 ly có topping | Giỏ có dòng món với đúng sản phẩm, số lượng và topping | Luồng hợp lệ | Có test — `Selecting a product adds a cart row`, `Cart retains quantity and selected topping`. |
| BH-02 | Size L, 2 topping/ly, 2 ly; thêm dòng M không topping | Dòng L = 84.000đ; tổng hai dòng = 109.000đ | Giá trị chuẩn tính độc lập | Một phần — `Size and topping quantities charged per cup` kiểm tra 84.000đ; cần assertion tổng giỏ nhiều dòng. |
| BH-03 | Số lượng -1, 0, 1, 999, 1000 | Chỉ 1 và 999 hợp lệ ở bước cấu hình món; giá tính theo số lượng | Giá trị biên | Một phần — `Zero configured quantity rejected`; chưa đủ hai đầu biên. |
| BH-04 | Không topping; 1, 10, 11 phần; topping không áp dụng; size/đường/đá không hỗ trợ | Không topping hợp lệ; 1–10 phần hợp lệ; lựa chọn sai bị từ chối | Phân lớp tương đương, biên | Một phần — có giới hạn 11 phần, topping không có sẵn, size/đường/đá sai; cần biên 1 và 10 rõ ràng. Số 0 ở UI phải được chuyển thành bỏ topping khỏi danh sách gửi vào service. |
| BH-05 | Sửa số lượng/tùy chọn rồi lưu; sửa rồi hủy | Lưu cập nhật đúng dòng và tổng; hủy giữ nguyên giỏ | Chuyển trạng thái | Một phần — có hủy sửa và tách một sản phẩm; cần test lưu sửa trực tiếp và tăng/giảm số lượng trên giỏ. |
| BH-06 | Xóa một dòng rồi Hoàn tác; xóa hết giỏ | Xóa cập nhật giỏ/tổng; Hoàn tác khôi phục dòng đúng vị trí; giỏ trống không thanh toán được | Chuyển trạng thái | Một phần — có xóa/Hoàn tác vị trí và chặn Hoàn tác sang đơn khác; cần assertion tổng tiền và luồng xóa toàn giỏ. |

### REQ-TT — Thanh toán

Nguồn: [Program.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/Program.cs), [CartChecks.cs](../scripts/ui-smoke/CartChecks.cs).

| Mã | Đầu vào / thao tác | Kết quả mong đợi | Kỹ thuật | Đối chiếu hiện tại |
|---|---|---|---|---|
| TT-01 | Giỏ 50.000đ; khách đưa -1, 0, 49.999, 50.000, 60.000đ | Ba giá trị đầu bị từ chối; đủ tiền thối 0đ; dư tiền thối 10.000đ | Giá trị biên | Một phần — có âm, 0, thiếu 49.000đ, tiền dư và một giao dịch trả đủ; cần assertion ngay dưới biên và tiền thối bằng 0. |
| TT-02 | Thanh toán giỏ rỗng, chưa mở ca, đã đóng ca, ca thuộc nhân viên khác | Từ chối; không tạo đơn và không đổi tồn | Bảng quyết định | Một phần — có chưa mở/đã đóng ca; cần giỏ rỗng và sai chủ ca cùng assertion không thay đổi dữ liệu. |
| TT-03 | Thanh toán tiền mặt hợp lệ rồi mở lại dữ liệu trong tiến trình khác | Lưu mã đơn, dòng món, tổng tiền, phương thức, ca; dữ liệu tồn tại sau khởi động lại | Tích hợp, lưu trữ | Một phần — có tham chiếu đơn/dòng/ca và số đơn sau restart; cần đối chiếu toàn bộ giá trị snapshot sau restart. |
| TT-04 | Gửi cùng mã thanh toán hai lần với cùng nội dung | Một đơn duy nhất; kho và lịch sử kho chỉ trừ/ghi một lần | Lặp yêu cầu | Có test — `Duplicate checkout is idempotent`, `Duplicate checkout does not duplicate stock history`. |
| TT-05 | Thanh toán QR chưa có duyệt; sau khi Owner duyệt đúng mã và số tiền | Chưa duyệt bị từ chối; đã duyệt lưu đơn QR, không tăng tiền mặt trong ngăn kéo | Bảng quyết định | Có test — `Boolean alone cannot authorize QR payment`, `QR revenue excluded from drawer cash`. Không gọi ngân hàng thật. |
| TT-06 | Mở hộp thoại tiền mặt, nhập thiếu rồi xác nhận; nhập tiền rồi hủy | Thiếu tiền không thanh toán; hủy giữ giỏ và số đơn đã lưu | Giao diện | Có test — `Insufficient cash cannot submit payment`, `Cancel cash payment preserves cart and orders`. |

### REQ-KHO — Quản lý kho bao bì

Nguồn: [Program.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/Program.cs), [ManagementRegression.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/ManagementRegression.cs).

| Mã | Đầu vào / thao tác | Kết quả mong đợi | Kỹ thuật | Đối chiếu hiện tại |
|---|---|---|---|---|
| KHO-01 | Nhập thêm 50; nhập 0, -1; lý do trống | Tăng đúng 50 và ghi trước/sau/người thực hiện; các đầu vào sai không đổi tồn/lịch sử | Phân lớp tương đương | Có test — `Receipt records before, after and operator` và các test từ chối nhập kho. |
| KHO-02 | Kiểm kê 0, 20, -1; điều chỉnh -3, +5, 0 | Kiểm kê đặt tồn bằng số đếm; điều chỉnh cộng/trừ; từ chối số kiểm kê âm và điều chỉnh 0 | Biên, chuyển trạng thái | Có test — `Stock count accepts zero`, `Positive and negative adjustments apply correctly`. |
| KHO-03 | Điều chỉnh làm tồn âm; nhập vượt Int32; mã bao bì/loại thao tác không tồn tại | Từ chối; tồn và lịch sử giữ nguyên | Giá trị biên, dữ liệu sai | Có test — `Stock cannot become negative`, `Stock cannot overflow integer range`, `Invalid operations leave stock and history unchanged`. |
| KHO-04 | Bán 2 ly M; bán ly XL; bán topping riêng | Trừ đúng ly theo size, nắp và ống hút; topping riêng không trừ bao bì ly | Bảng quyết định | Một phần — có trừ ly M, ánh xạ XL, topping riêng; cần assert riêng số lượng nắp/ống hút và mọi loại không liên quan. |
| KHO-05 | Đơn nhiều dòng có một loại bao bì thiếu | Từ chối toàn bộ đơn; không đổi bất kỳ tồn, đơn hay lịch sử nào trong bộ nhớ và SQLite | Tính toàn vẹn giao dịch | Một phần — có thiếu bao bì, số đơn, tồn một loại, SQLite và không có lịch sử bán thất bại; cần đơn nhiều dòng và so sánh toàn bộ tồn trước/sau. |
| KHO-06 | Nhập/kiểm kê/điều chỉnh rồi khởi động lại | Tồn và nhật ký vẫn đúng, lưu cùng nhau | Tích hợp, lưu trữ | Có test — `Stock and audit saved together`, `Stock history survives restart`, `Counted and adjusted stock survives restart`. |

### REQ-DN — Đăng nhập và phân quyền

Nguồn: [Program.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/Program.cs), [FinanceRegression.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/FinanceRegression.cs).

| Mã | Đầu vào / thao tác | Kết quả mong đợi | Kỹ thuật | Đối chiếu hiện tại |
|---|---|---|---|---|
| DN-01 | Đúng tài khoản/mật khẩu của từng vai trò | Đăng nhập đúng người và đúng quyền | Phân lớp tương đương | Một phần — các vai trò được đăng nhập trong test; có assertion Owner, cần bảng assertion danh tính/quyền rõ cho cả ba vai trò. |
| DN-02 | Sai mật khẩu; tên không tồn tại; tên/mật khẩu trống | Từ chối đăng nhập, không cấp phiên cho người đăng nhập thất bại | Phân lớp tương đương | Một phần — có sai mật khẩu; cần trường hợp trống/không tồn tại và assertion trạng thái phiên. |
| DN-03 | Đăng nhập sai 5 lần rồi dùng mật khẩu đúng ngay | Khóa tạm thời theo quy tắc ứng dụng | Biên, chuyển trạng thái | Có test — `Repeated attempts are temporarily locked`; thời điểm hết khóa chưa được kiểm thử. |
| DN-04 | Tài khoản bị quản lý khóa, rồi được mở khóa | Khi khóa không đăng nhập được; mở khóa đăng nhập lại được | Chuyển trạng thái | Có test — `Disabled account cannot log in`, `Re-enabled account can log in` trong nhóm finance. |
| DN-05 | Cashier sửa menu, chỉnh kho, tạo nhân viên, duyệt thao tác quản lý | Bị từ chối ở tầng service, không chỉ ẩn nút trên UI | Bảng quyết định quyền | Có test — các assertion `Cashier cannot ...` trong Program. |
| DN-06 | Owner/Manager quản trị hợp lệ; Manager thao tác tài khoản Owner | Cho phép đúng quyền; không cho Manager thay mật khẩu Owner | Bảng quyết định quyền | Một phần — có Owner quản trị và `Manager cannot reset owner password`; cần ma trận cho phép/từ chối đầy đủ cho thao tác thực đơn/kho với Manager. |

### REQ-TD — Quản lý thực đơn

Nguồn: [Program.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/Program.cs), [ManagementRegression.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/ManagementRegression.cs), [MenuOptionsRegression.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/MenuOptionsRegression.cs), [AmericanoRegression.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/AmericanoRegression.cs).

| Mã | Đầu vào / thao tác | Kết quả mong đợi | Kỹ thuật | Đối chiếu hiện tại |
|---|---|---|---|---|
| TD-01 | Tạo món hợp lệ rồi sửa tên/giá/cấu hình và mở lại dữ liệu | Tạo một món; cập nhật đúng ID, không nhân đôi; dữ liệu được lưu | CRUD, lưu trữ | Một phần — có tạo và món tồn tại sau restart; cần assertion cập nhật đầy đủ và số bản ghi. |
| TD-02 | Tên rỗng/khoảng trắng; giá -1, 0, 0,5, 1; danh mục không tồn tại | Với các trường khác hợp lệ, chỉ giá nguyên dương và tên/danh mục hợp lệ được lưu | Biên, phân lớp tương đương | Một phần — `Reject invalid menu price` kiểm tra giá âm; cần tên trống, giá 0/thập phân/1 và danh mục sai. |
| TD-03 | Xóa topping đang được gán cho đồ uống | Xóa topping và tham chiếu trong đồ uống; đồ uống vẫn lưu được | Toàn vẹn tham chiếu | Có test — `Deleting a topping removes it from drinks`, `Drink stays savable after its topping is deleted`. |
| TD-04 | Xóa danh mục còn món theo cách thông thường; xóa danh mục rỗng | Danh mục còn món bị chặn; danh mục rỗng được xóa | Bảng quyết định | Có test — `Category with products cannot be deleted`, `Empty category can be deleted`. Tùy chọn xóa kèm món là luồng riêng. |
| TD-05 | Món hết hàng/ngừng bán được thêm hoặc vẫn nằm trong giỏ khi thanh toán | Từ chối bán theo trạng thái hiện tại | Chuyển trạng thái | Một phần — có `Sold-out product cannot be sold`; cần ngừng bán và món trong giỏ bị đổi trạng thái trước thanh toán. |
| TD-06 | Bán món, sau đó đổi giá/đổi tên/xóa món khỏi thực đơn | Đơn đã thanh toán giữ nguyên tên, giá, tùy chọn và tổng tiền | Snapshot lịch sử | Một phần — có độc lập với sửa giỏ và migration giữ lịch sử; cần test trực tiếp CRUD món sau khi bán. |

## 4. Thứ tự bổ sung test

1. **BH-02, BH-03, BH-04, TD-02:** tính nhiều dòng, biên số lượng/topping và dữ liệu thực đơn. Dùng test tham số hóa, kết quả tính tay độc lập.
2. **TT-01, TT-02, KHO-04, KHO-05:** biên tiền, giỏ rỗng/sai chủ ca, toàn bộ bao bì và đơn nhiều dòng thất bại. So sánh trạng thái trước/sau cả bộ nhớ lẫn SQLite.
3. **DN-01, DN-02, DN-06:** bảng vai trò và các trường hợp đăng nhập thất bại, kiểm tra cả trạng thái sau lỗi.
4. **TD-01, TD-05, TD-06, TT-03:** CRUD, trạng thái bán và lịch sử dữ liệu qua khởi động lại.
5. **BH-05, BH-06:** bổ sung kiểm thử tương tác lưu sửa, tăng/giảm và xóa toàn giỏ; đối chiếu tổng tiền hiển thị.

Không cần thêm chức năng sản phẩm để có nhiều test. Chỉ sửa nghiệp vụ khi test phát hiện sai lệch so với yêu cầu đã thống nhất; ghi rõ yêu cầu, dữ liệu tái hiện và kết quả trước/sau sửa.

## 5. Cách chạy và ghi nhận

Từ thư mục gốc, chạy các bộ hiện có:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-regressions.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/test-ui.ps1
```

Hai lệnh này hiện chạy cả các kiểm tra ngoài phạm vi năm nhóm; chưa có chế độ lọc riêng cho bài. Phần `finance` có test phân quyền và cần dữ liệu tạo bởi `create`/`reload`, vì vậy không chạy tùy tiện một nhóm trên cơ sở dữ liệu rỗng. Kiểm tra phiên đăng nhập dùng Windows DPAPI có thể cần chạy bằng tài khoản Windows người dùng; UI smoke cần desktop Windows tương tác.

Bộ regression hiện là chương trình C# với `Check`/`Reject`, không phải bộ xUnit/NUnit/MSTest chạy bằng `dotnet test`. UI smoke thao tác các cửa sổ WPF thật trên dữ liệu riêng. Phần lớn test nghiệp vụ có SQLite nên thuộc kiểm thử tích hợp; không gọi tất cả là unit test.

Mẫu ghi nhận cho mỗi biến thể test:

| Mã ca / bộ dữ liệu | Yêu cầu | Tiền điều kiện | Bước thực hiện | Kết quả mong đợi | Kết quả thực tế | Pass/Fail/Blocked | Minh chứng / mã lỗi |
|---|---|---|---|---|---|---|---|
| TT-01/49999 | REQ-TT | Ca mở, giỏ 50.000đ, kho đủ | Khách đưa 49.999đ, xác nhận | Từ chối; không có đơn mới; tồn không đổi | Chưa thực thi | — | Điền đường dẫn log sau khi chạy |

Đợt lập tài liệu này chỉ rà mã và phạm vi, không chạy lại test. Các kết quả chạy trước đây không tự động được coi là kết quả của từng ca trong ma trận mới.

### Kết quả bổ sung sau đợt hoàn thiện luồng bán hàng/thanh toán

Ngày 21/10/2026: đã thêm [CheckoutFlowRegression.cs](../TheCafePOS_WPF/TheCafePOS.RegressionTests/CheckoutFlowRegression.cs) và mở rộng [CartChecks.cs](../scripts/ui-smoke/CartChecks.cs). Lượt chạy `scripts/test-regressions.ps1` đạt **249 assertion**, lượt chạy `scripts/test-ui.ps1` đạt **60 assertion**. Log tại `artifacts/regression-checkout.log` và `artifacts/ui-checkout.log` (dữ liệu sinh cục bộ, không đưa vào Git).

Phần bổ sung kiểm tra: giỏ rỗng/ca đóng ở bước kiểm tra trước thanh toán; tổng nhu cầu bao bì nhiều dòng; không ghi dữ liệu trong bước kiểm tra; tiền âm/thiếu/thập phân/vượt giới hạn; trả đủ thối 0; trừ đủ ly/nắp/ống hút; thanh toán lặp; tổng tiền khi tăng/giảm; lỗi kho giữ giỏ và cho thử lại; xác nhận thành công tự đóng. Đây là kết quả cho bộ test đã chạy, chưa có nghĩa toàn bộ các biến thể trong ma trận trên đã được triển khai.

## 6. Điều kiện hoàn thành phạm vi bài

- Năm nhóm có yêu cầu, dữ liệu, kết quả mong đợi và liên kết tới test cụ thể.
- Các trường hợp hợp lệ, không hợp lệ và biên ưu tiên được chạy và ghi kết quả thật.
- Luồng tích hợp được kiểm chứng: **đăng nhập → mở ca → chọn món → tính tiền → thanh toán → lưu đơn và trừ kho → mở lại dữ liệu**.
- Lỗi tiền/kho/phân quyền còn tồn tại được sửa hoặc ghi rõ trong báo cáo, không bỏ test hay đổi kỳ vọng để che lỗi.
- Chỉ báo cáo phạm vi đã kiểm thử; không tuyên bố bao phủ 100% hoặc kiểm thử ngân hàng/máy in thật khi chưa có minh chứng.
