# TheCafePOS WPF

Ứng dụng bán hàng tại một máy Windows, dùng .NET 10 và WPF.

## Chạy ứng dụng

```powershell
dotnet build TheCafePOS_WPF/TheCafePOS_WPF.slnx -c Release
dotnet run --project TheCafePOS_WPF/TheCafePOS_WPF/TheCafePOS_WPF.csproj -c Release
```

Nếu Visual Studio hoặc bản ứng dụng cũ đang giữ file Debug, đóng bản đó trước khi build Debug; có thể build Release riêng.

1. Lần chạy đầu: tự đặt tên đăng nhập và mật khẩu chủ quán. Không có mật khẩu/PIN mặc định. Mật khẩu tối thiểu 8 ký tự.
2. Chọn **Mở ca**, nhập tiền mặt đầu ca.
3. **Quản trị** cho phép thêm/sửa món, giá cơ bản, danh mục, trạng thái bán; tạo nhân viên và cấu hình tài khoản nhận chuyển khoản.
4. Chọn món và thanh toán. Tiền mặt phải đủ tổng tiền. VietQR cần cấu hình ngân hàng và quản lý kiểm tra giao dịch thực tế, sau đó nhập tài khoản quản lý để duyệt.
5. **Báo cáo** lọc theo khoảng ngày/ca, xem đơn và chi tiết món, doanh thu theo phương thức, món bán chạy, ca đã đóng và xuất CSV.
6. **Chốt ca mù** ghi số tiền thực tế và lưu chênh lệch. Chỉ nhân viên sở hữu ca được thanh toán/chốt ca; phải mở ca mới để bán tiếp.
7. **Đăng xuất / Đổi nhân viên** xóa phiên đã lưu và mở màn hình đăng nhập. Đóng màn hình đăng nhập lúc này sẽ thoát ứng dụng. Giỏ hàng phải được giữ hoặc hoàn tất trước khi đổi nhân viên. Khi đóng cửa sổ bình thường, giỏ chưa thanh toán tự chuyển thành đơn giữ.

Chọn **Giữ đăng nhập trên máy này (30 ngày)** để mở lại ứng dụng vào thẳng màn hình bán hàng. Phiên được mã hóa bằng Windows DPAPI theo tài khoản Windows, không lưu mật khẩu. Đăng xuất hoặc bỏ chọn sẽ xóa phiên; đổi/reset mật khẩu hay khóa tài khoản sẽ vô hiệu hóa phiên cũ. Khôi phục đăng nhập không tự mở hoặc đóng ca.

## Phân quyền

### Giao diện cảm ứng

- Tùy chỉnh món dùng bố cục tĩnh: kích cỡ/đường/đá bên trái, topping bên phải có nút chuyển trang. Ghi chú trải hết chiều ngang phía dưới, tự xuống dòng, tối đa 300 ký tự; không có vùng cuộn trong hộp tùy chỉnh. Nút số lượng và xác nhận luôn ở chân hộp thoại.

- Các chức năng mở/chốt ca, báo cáo, quản trị, hoàn tiền/thu chi và tài khoản nằm trong **Tiện ích** ở góc trên bên phải màn hình bán hàng. **Đơn giữ** và **Kho bao bì** vẫn truy cập trực tiếp.
- Nút/ô nhập dùng vùng chạm tối thiểu 48; dòng bảng và danh sách cao 52. Có thể vuốt danh sách món, giỏ hàng và các bảng dữ liệu. Danh mục món vuốt ngang.
- Chạm ô khách đưa, tiền mở/chốt ca, tiền hoàn, thu/chi, giá món/phụ thu size hoặc số lượng kho để mở bàn phím số. Hủy bàn phím giữ nguyên dữ liệu cũ; xác nhận mới cập nhật. Điều chỉnh tồn kho hỗ trợ số âm. Bàn phím vật lý vẫn sử dụng được; nhập chữ/mật khẩu sử dụng bàn phím hệ thống hoặc bàn phím vật lý.
- Nhật ký tem được thu gọn ở đáy màn hình, chạm **Nhật ký tem / In lại** để mở. Nút lưu phiếu hoàn và nút duyệt luôn nằm ngoài vùng cuộn của biểu mẫu.
- Hộp thoại được giới hạn trong vùng làm việc màn hình; biểu mẫu quản trị dài có vùng vuốt dự phòng. Chưa kiểm tra trực tiếp trên thiết bị cảm ứng hoặc các mức DPI thực tế.

### Hoàn tiền và thu/chi trong ca

Mở **Hoàn tiền / Thu chi** khi đang đăng nhập bằng nhân viên sở hữu ca mở.

- **Hoàn tiền:** chọn ngày bán, tìm số thứ tự hoặc mã đơn, chọn đơn và nhập số tiền hoàn toàn bộ/một phần cùng lý do. Hoàn theo phương thức thanh toán gốc. Với QR, thực hiện chuyển khoản ngoài ứng dụng và nhập mã giao dịch. Đánh dấu đã trả tiền, sau đó quản lý nhập tài khoản để duyệt và ghi nhận.
- Không được hoàn vượt số tiền còn lại. Cùng mã phiếu không bị ghi nhận hai lần. Phiếu lưu đơn gốc, thời gian, ca thực hiện, nhân viên, người duyệt và lý do. Đơn gốc không bị xóa; trạng thái hoàn được hiển thị trong màn hình tìm đơn.
- Kho không tự tăng khi hoàn tiền vì đồ uống có thể đã pha. Nếu có bao bì thực sự thu hồi, dùng điều chỉnh kho và ghi rõ lý do.
- **Thu/chi:** nhập số tiền nguyên dương, chọn thu vào/chi ra và nhập lý do hoặc chứng từ; quản lý duyệt trước khi ghi sổ. Phiếu không làm tăng doanh thu bán hàng. Không cho chi/hoàn tiền mặt vượt tiền dự kiến trong ngăn kéo.
- Tiền chốt ca = tiền đầu ca + bán tiền mặt + thu thêm − chi ra − hoàn tiền mặt trong ca. Hoàn một đơn bán ở ca trước chỉ ảnh hưởng ca thực hiện hoàn; số liệu ca đã đóng giữ nguyên.
- Báo cáo và CSV có riêng bán hàng, hoàn tiền, thu/chi. Khoản hoàn được lọc theo **ngày/ca hoàn**, không theo ngày bán gốc; doanh thu thuần của một khoảng có thể âm. Món bán chạy vẫn thống kê số lượng và doanh thu trước hoàn vì hoàn theo số tiền, chưa phân bổ theo dòng món.

### Đổi mật khẩu và khóa nhân viên

- Mọi người dùng có thể chọn **Đổi mật khẩu**, nhập mật khẩu hiện tại và xác nhận mật khẩu mới.
- **Quản trị → Nhân viên:** chọn tài khoản rồi đặt lại mật khẩu hoặc khóa/mở khóa. Quản lý được quản lý thu ngân; chủ quán được quản lý thu ngân và quản lý. Không được khóa/reset chính mình hoặc tài khoản chủ quán qua luồng này.
- Mật khẩu được reset là mật khẩu tạm; lần đăng nhập tiếp theo bắt buộc đổi trước khi thao tác nghiệp vụ. Không cho khóa nhân viên đang giữ ca mở; chốt ca trước khi khóa.
- Các thao tác tài khoản được ghi nhật ký. Chưa có khôi phục mật khẩu chủ quán qua email, đổi vai trò tài khoản hay màn hình tra cứu toàn bộ nhật ký xác thực.

### Chọn món, sửa món và topping

- Trong **Quản trị → Thực đơn**, lưu/chọn món rồi mở **Cấu hình size / topping / đường đá**. Chọn loại đồ uống hoặc topping, size M/L/XL được bán, phụ thu từng size, bao bì ly tương ứng, tùy chọn đường/đá và mức mặc định. Chọn danh sách topping được thêm vào từng đồ uống.
- Giá topping là giá cơ bản của sản phẩm topping. Mỗi loại có thể chọn 0–10 phần trên **mỗi ly**. Thành tiền = (giá cơ bản + phụ thu size + tổng giá topping mỗi ly) × số lượng.
- Topping mẫu được chuyển sang món thêm, mặc định không bán riêng. Quản lý có thể bật bán riêng; topping bán riêng không có size/đường/đá, không trừ bao bì đồ uống và không tạo tem ly.
- **Sửa** thay đổi cả dòng món; **Tách 1** mở một sản phẩm để sửa riêng và chỉ giảm dòng gốc sau khi lưu. Đóng hộp thoại không thay đổi giỏ. Có ghi chú pha chế, số lượng mỗi dòng từ 1–999.
- Giỏ chưa thanh toán là đơn nháp: thu ngân được sửa/xóa; xóa toàn giỏ cần xác nhận. Chưa có luồng gửi đơn pha chế trước thanh toán. Phê duyệt quản lý vẫn áp dụng cho xác nhận chuyển khoản.
- Đơn giữ bảo toàn giá và tùy chọn lúc thêm. Sửa món áp dụng thực đơn hiện tại và có thông báo. Món, size hoặc topping ngừng bán sẽ chặn thanh toán và yêu cầu sửa/xóa dòng.
- Bao bì ly lấy theo size đã chọn; mỗi đồ uống trừ thêm một nắp và ống hút. Tem đánh số liên tục trên toàn bộ số ly trong đơn. Chưa có cấu hình mang đi/tại chỗ, chọn không lấy ống hút hay số túi theo đơn.

Kiểm tra thủ công: chọn 2 ly cùng loại có 2 phần topping mỗi ly → đối chiếu tiền → tách 1 ly, đổi đường/topping → giữ đơn và gọi lại → thanh toán → kiểm tra tồn ly/nắp/ống hút và tem 1/2, 2/2. Kiểm tra hủy hộp thoại sửa không làm đổi giỏ. Đóng/mở lại để kiểm tra cấu hình được lưu.

### Nhập kho, kiểm kê và điều chỉnh bao bì

Mở **Kho Bao Bì**, chọn một dòng bao bì. Quản lý/chủ quán có thể chọn:

- **Nhập kho:** nhập số lượng bổ sung, phải lớn hơn 0.
- **Kiểm kê:** nhập tổng số lượng đếm thực tế, có thể bằng 0. Hệ thống tính chênh lệch với tồn hiện tại.
- **Điều chỉnh:** nhập số tăng hoặc giảm, ví dụ `5` hoặc `-3` cho bao bì hỏng.

Nhập lý do, nhà cung cấp hoặc số phiếu rồi bấm **Lưu thay đổi kho**. Tồn sau thay đổi hiển thị trước khi lưu; hệ thống chặn tồn âm và số vượt giới hạn. Tồn kho và lịch sử lưu cùng giao dịch SQLite. Thu ngân chỉ được xem. Dòng màu vàng là bao bì chạm ngưỡng cảnh báo.

Lịch sử lưu thời gian, người thực hiện, lý do, số lượng trước/sau và cả bao bì trừ khi bán hàng. Có thể lọc theo bao bì đang chọn. Dữ liệu cũ tiếp tục sử dụng được, lịch sử chỉ bắt đầu từ các thay đổi sau nâng cấp. Đây là thao tác từng loại bao bì, chưa có phiếu nhập nhiều dòng, giá nhập hoặc công nợ nhà cung cấp.

| Quyền | Thu ngân (Cashier) | Quản lý (Manager) | Chủ quán (Owner) |
|---|---|---|---|
| Bán hàng, giữ đơn, mở/chốt ca của mình | Có | Có | Có |
| Quản trị thực đơn, tài khoản ngân hàng, báo cáo | Không | Có | Có |
| Tạo thu ngân | Không | Có | Có |
| Tạo quản lý | Không | Không | Có |
| Duyệt xác nhận chuyển khoản | Không | Có | Có |
| Sửa/xóa món trong giỏ chưa thanh toán | Có | Có | Có |
| Lập phiếu hoàn tiền và thu/chi trong ca của mình | Cần quản lý duyệt | Cần xác thực duyệt | Cần xác thực duyệt |
| Đổi mật khẩu của mình | Có | Có | Có |
| Reset mật khẩu / khóa tài khoản khác | Không | Thu ngân | Thu ngân và quản lý |

Mật khẩu được băm PBKDF2-SHA256 với salt riêng. Năm lần đăng nhập sai liên tiếp sẽ khóa tên đăng nhập một phút trong phiên ứng dụng. Nhật ký phê duyệt lưu người thao tác, người duyệt, hành động và thời gian. Kiểm soát này áp dụng trong ứng dụng; người có quyền sửa trực tiếp file dữ liệu Windows vẫn có thể thay đổi dữ liệu.

## Dữ liệu

- File SQLite: `%LOCALAPPDATA%\TheCafePOS\cafe.db` (theo tài khoản Windows).
- Có thể đặt biến `THECAFEPOS_DATA_DIR` để dùng thư mục dữ liệu riêng; bộ kiểm thử sử dụng thư mục riêng trong `artifacts`.
- SQLite lưu các tài liệu JSON trong bảng `documents`: trạng thái cửa hàng, tài khoản/nhật ký duyệt, đơn giữ và cấu hình ngân hàng. Đơn hàng, kho và ca được lưu cùng một giao dịch; nếu ghi thất bại, trạng thái bộ nhớ được khôi phục.
- Đơn đã thanh toán lưu bản sao các dòng món, không thay đổi theo sửa giá thực đơn về sau. Mã đơn duy nhất tách biệt số thứ tự trong ngày. Thanh toán lặp cùng mã không trừ kho lần nữa.
- Dữ liệu mẫu chỉ tạo khi chưa có trạng thái cửa hàng. Nếu không đọc được dữ liệu, ứng dụng báo lỗi thay vì ghi đè bằng dữ liệu mẫu.
- Sao lưu bằng cách đóng ứng dụng rồi sao chép `cafe.db`; khôi phục khi ứng dụng đã đóng. Không lưu cơ sở dữ liệu thật vào Git.

## Kiểm thử

```powershell
powershell -File scripts/test-regressions.ps1
```

Đây là chương trình kiểm thử hồi quy độc lập, trả mã lỗi khi có kiểm tra thất bại. Kiểm tra: phân quyền, băm mật khẩu, khóa đăng nhập, dữ liệu thực đơn, tiền mặt thiếu/âm, duyệt QR, thanh toán trùng, hoàn tác khi thiếu kho, đóng/mở ca, tổng hợp báo cáo và CSV. Lượt chạy thứ hai mở lại cơ sở dữ liệu bằng tiến trình mới để kiểm tra dữ liệu tồn tại qua khởi động lại.

Kiểm tra giao diện thủ công: tạo chủ quán → thêm nhân viên/món → mở ca → bán tiền mặt → giữ/gọi đơn → cấu hình QR và quản lý duyệt → chốt ca → lọc báo cáo/xuất CSV → đóng/mở lại ứng dụng và kiểm tra dữ liệu.

Trong môi trường triển khai hiện tại, build Release thành công. Việc chạy chương trình kiểm thử bị Windows Application Control chặn với lỗi `0x800711C7`, kể cả khi chạy ngoài sandbox; chưa có kết quả kiểm thử runtime hay kiểm tra giao diện trực tiếp.

## Giới hạn hiện tại

- QR tạo ảnh từ dịch vụ VietQR qua mạng; xác nhận tiền là thao tác thủ công có quản lý duyệt, chưa có webhook/đối soát tự động.
- Dịch vụ tem tạo dữ liệu và nhật ký, chưa gửi lệnh tới máy in vật lý.
- SupabaseService cũ chưa được dùng cho lưu trữ hoặc đồng bộ. Phiên bản này lưu tại máy, không hỗ trợ nhiều máy cùng bán hay đồng bộ nhiều chi nhánh.
- Cách lưu snapshot phù hợp quy mô một máy; cần tách bảng và truy vấn có chỉ mục khi lượng dữ liệu lớn. Ứng dụng giới hạn một tiến trình trong phiên Windows, không dùng chung file dữ liệu giữa các phiên/máy.
- Hoàn tiền hiện theo số tiền của đơn, chưa chọn số lượng từng món để hoàn hoặc tự chuyển tiền qua ngân hàng. Báo cáo chưa tính giá vốn/lợi nhuận. Chưa có sửa/xóa phiếu tài chính; nhập sai cần ghi phiếu đối ứng có lý do và quản lý duyệt khi phù hợp.
- Giỏ đang nhập được giữ khi đóng cửa sổ bình thường; chưa phục hồi tự động khi mất điện hoặc tiến trình bị dừng đột ngột.


Cấu hình Supabase dự phòng được đọc từ biến môi trường THECAFEPOS_SUPABASE_URL và THECAFEPOS_SUPABASE_KEY; không lưu khóa trong mã nguồn. Luồng bán hàng hiện tại dùng SQLite, không cần hai biến này.
