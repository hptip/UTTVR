# Quản lý vòng đời tài sản đường sắt

Mở `Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity`, rồi Play. Scene cũng có trong menu chuyển không gian: **Quản lý vòng đời tài sản**. Trong Editor có thể phóng lớn Game View bằng Shift+Space.

## Chọn và quan sát

- Ba cấp: **Phân đoạn → Mô-đun → Cấu kiện**. Có 34 phân đoạn, 334 mô-đun, 33.066 cấu kiện, trên hai đường ray.
- Chọn trên mô hình, LeftNav hoặc kết quả tìm kiếm sẽ cập nhật outline, hồ sơ và tự focus. Một lần chọn không tự xuống cấp. Tìm kiếm toàn khu vực, tối đa 150 kết quả; chọn kết quả ở cấp sâu sẽ mở đúng ngữ cảnh cha.
- **Xem chi tiết** mở cấp tiếp theo; **Quay lại** khôi phục cấp cha và góc nhìn trước đó. Quay lại bị khóa ở cấp đầu.
- **Toàn cảnh** trở về cấp phân đoạn và góc nhìn toàn Zone.
- **Cô lập tài sản** giữ đối tượng đang chọn và ẩn các visual tài sản khác, kể cả overview gộp. Terrain, cảnh nền và UI được giữ. **Hiện lại tài sản khác** khôi phục đúng snapshot; tài sản vốn ẩn không bị bật lại. Đổi lựa chọn/cấp sẽ kết thúc cô lập.
- Không còn nút Đến tài sản, Đo khoảng cách hoặc Thống kê. Muốn trở lại tài sản đang chọn, nhấn F trên PC hoặc chọn lại chính thẻ tài sản trong LeftNav trên VR.

## Asset Panel và sáu giai đoạn

Asset Panel có ba mục **Tổng quan, Thông số, Vòng đời**. Tổng quan cho sửa tên hiển thị; Thông số tạo trường động từ thuộc tính SQLite. Dùng **Lưu hồ sơ** để lưu thay đổi cho đúng mã tài sản.

Trong Vòng đời có sáu nút kế thừa kỹ thuật Stg1–Stg6 của ChiTietSaBan: **Quy hoạch, Thiết kế, Thi công, Vận hành, Bảo trì, Nâng cấp**. Chọn giai đoạn chỉ đổi nội dung trong một Asset Panel, không đổi mã tài sản.

- **Hồ sơ:** xem/sửa trạng thái và các trường của giai đoạn hiện tại.
- **Vận hành → Kiểm tra:** xem lịch sử và thêm kết quả kiểm tra, người kiểm tra, nhận xét, ngày, tình trạng, ghi chú.
- **Bảo trì → Bảo trì:** xem các công việc và thêm ngày, loại công việc, nội dung, chi phí, kết quả. Ngày bảo trì gần nhất và tổng chi phí được tính từ các bản ghi.
- **Nhật ký:** gồm sự kiện của giai đoạn đang chọn và nhật ký chung cũ chưa phân loại. Dùng **Nhật ký cũ hơn / mới hơn** để xem các trang 50 bản ghi; dữ liệu cũ không bị xóa.

Các nút Kiểm tra, Bảo trì và Lịch sử cấp cao đã bỏ; dữ liệu và chức năng được đặt bên trong Vòng đời.

## Điều khiển PC

- Chuột trái: chọn/bấm. Chuột phải + kéo: xoay; khi ẩn UI sẽ nhìn quanh tại chỗ, không quay vòng quanh tài sản. Con lăn: zoom. Chuột giữa + kéo: pan.
- **F:** trở lại tài sản đang chọn.
- **F2:** ẩn/hiện UI. Khi UI ẩn, **WASD** di chuyển camera, **Q/E** xuống/lên, **Shift** tăng tốc. Tốc độ thường 12 m/s, giữ Shift là 36 m/s. Camera bay tự do, xuyên vật thể, không có va chạm hoặc giới hạn khu vực; con lăn khi bay tiến/lùi theo hướng nhìn. Không kích hoạt di chuyển lúc đang nhập trường văn bản. Hiện UI không reset camera.
- **Alt + click:** chọn luân phiên các đối tượng trên cùng tia.
- **F1:** menu chuyển Scene; **5** chọn nhanh Scene này.
- **F8:** bật/tắt trình bày VR trong Editor để dùng với Simulator.

## Điều khiển VR

- **Cò trái/phải:** chọn đối tượng và bấm UI. Ray trên UI không chọn xuyên vào mô hình.
- **X trái:** ẩn/hiện nhóm UI quản lý, không xóa lựa chọn hoặc dữ liệu. Khi mở lại, chỉ đặt lại các bảng theo vị trí người dùng, không dịch chuyển đầu.
- **Y trái:** menu đổi không gian.
- **A, B và Grip phải:** không còn gọi focus, quay lại hoặc mở chi tiết trong Scene này; được để trống.
- Khi UI hiện: joystick cuộn danh sách đang được ray trỏ vào.
- Khi UI ẩn: joystick trái di chuyển ngang; joystick phải lên/xuống để bay lên/hạ xuống, trái/phải quay nấc 30°. Tốc độ mặc định 1,8 m/s, deadzone 0,22; có thể xuyên vật thể và rời phạm vi Zone khi quan sát. Có thể chỉnh thông số trên RailwayBimVrPresentation.

UI VR dùng ba Canvas riêng: LeftNav lệch trái, hồ sơ lệch phải và thanh thao tác phía trên vùng quan sát, không dùng lại một tấm desktop lớn. Bảng trái khoảng 0,57 × 1,01 m, bảng phải khoảng 0,84 × 1,19 m; độ sâu cơ sở 1,25 m và có góc hướng về người xem. Có thể **Thu gọn** từng bảng độc lập. Các bảng đứng yên sau khi đặt, không bám cứng vào đầu mỗi frame. Thao tác X ẩn rồi hiện lại UI cho phép recenter chủ động.

Chọn tài sản dịch chuyển XR Origin đến điểm quan sát thích hợp phía trên bounds, không ghi local pose hoặc ép xoay HMD. Locomotion chỉ hoạt động khi UI và Scene Menu đều đóng. Chế độ quan sát không kiểm tra va chạm; bay lên/xuống thay đổi XR Origin, không ghi local pose của kính. Không có trọng lực hoặc hệ thống đi bộ theo địa hình trong chế độ này.

Chọn ô nhập sẽ mở bàn phím ảo World Space riêng. Có chữ Việt **ă â đ ê ô ơ ư**, dấu thanh, hoa/thường, xóa lùi, xóa hết và khoảng trắng. Dấu được áp dụng lên nguyên âm trước con trỏ trong từ, không phải bộ gõ Telex tự động. Bàn phím PC vẫn dùng được trong Editor.

## SQLite và dữ liệu

Database làm việc trong Editor: `UserData/QuanLyVongDoiTaiSan.db`. Dữ liệu ban đầu chỉ được sao chép nếu chưa có file làm việc, từ `Assets/StreamingAssets/RailwayLifecycle/Initial.db`. Trên Android, dữ liệu StreamingAssets dạng URL được staging bằng UnityWebRequest. Không ghi database RailwayModels.db của Scene tham khảo.

Migration schema 2 giữ các bảng cũ, thêm:

- `AssetLifecycleStages`: khóa chính `(AssetID, StageKey)`, schema hồ sơ phiên bản 1, Status và UpdatedAt.
- `AssetStageFields`: khóa `(AssetID, StageKey, FieldKey)`, kiểu và đơn vị, khóa ngoại tới hồ sơ.
- `AssetLifecycleEvents.StageKey`: phân loại sự kiện mới; sự kiện cũ giữ nguyên và vẫn đọc được.

Có 200.604 hồ sơ giai đoạn và 1.504.530 trường lưu độc lập. Seed dùng INSERT OR IGNORE và marker phiên bản, không ghi đè dữ liệu đã sửa. Các trường trùng nghiệp vụ cũ như ngày lắp đặt, vận hành, tình trạng và tuổi thọ được đồng bộ trong cùng transaction; lịch sử kiểm tra/bảo trì dùng lại bảng cũ. Trường tổng hợp được tính khi đọc. Trạng thái kế hoạch nâng cấp minh họa được hiệu chỉnh khi đã có ngày dự kiến, nhưng không đụng các hồ sơ nâng cấp đã được chỉnh sửa qua ứng dụng.

Mã ví dụ: `Z021-TR-S001` → `Z021-TR-S001-B001` → `Z021-TR-S001-B001-C005`. Mỗi đường có 17 phân đoạn, 167 mô-đun; mỗi mô-đun có 99 đối tượng hình học nguồn. ID không được tạo lại khi Play hoặc đổi tên hiển thị.

## Excel

Mẫu hiện hành: `outputs/RailwayLifecycle_Refinement_20261113/VongDoiTaiSan_6GiaiDoan_Mau.xlsx`, gồm hai mã cấu kiện có thật và sáu sheet:

| Sheet | StageKey |
|---|---|
| QuyHoach | Planning |
| ThietKe | Design |
| ThiCong | Construction |
| VanHanh | Operation |
| BaoTri | Maintenance |
| NangCap | Upgrade |

Giữ nguyên AssetID, StageKey, SchemaVersion và tiêu đề dòng 1. Các cột giai đoạn dùng `FieldKey|Text/Number/Date|Đơn vị`. Tên khóa cố định phục vụ validation; sheet HuongDan giải thích nhãn tiếng Việt. Ô trống giữ dữ liệu cũ; không dùng ô trống để xóa. Các cột tự tính trong BaoTri chỉ để tham khảo và không được ghi đè vào database.

**Xem trước tệp** kiểm tra ID, giai đoạn, phiên bản, trùng `(AssetID, StageKey)`, kiểu, đơn vị và trình tự ngày. **Xác nhận nhập** kiểm tra lại và lưu bằng transaction, rollback nếu lỗi. Một dòng chỉ cập nhật tài sản/giai đoạn của dòng đó. Nhập lặp dữ liệu không đổi không tạo sự kiện trùng.

Export toàn bộ còn chứa Technical, Assets, Inspections và Maintenance để giữ thuộc tính và các sự kiện. Import vẫn nhận workbook cũ có sheet Lifecycle; không trộn Lifecycle cũ với sáu sheet mới trong cùng lần nhập để tránh hai nguồn dữ liệu xung đột. Sự kiện kiểm tra/bảo trì được áp dụng sau các hồ sơ giai đoạn để giữ trạng thái mới nhất từ sự kiện.

Technical tiếp tục cho thêm cột tùy chỉnh `Tên|Text/Number/Date|đơn vị`. Đổi tên cột Technical được xem là thêm thông số mới; thông số cũ giữ nguyên. Sáu giai đoạn có schema trường xác định, không tự nhận cột lạ. Assets chỉ cập nhật DisplayName, không đổi ID hay quan hệ cha/con. ID sự kiện trống tạo sự kiện mới; giữ ID để cập nhật sự kiện đã có.

Trên Android, Excel nằm trong `Application.persistentDataPath/TaiLieu`; tệp nhập tên `NhapTaiSan.xlsx`. Thao tác tệp trên thiết bị chưa được xác minh thực tế.

## Phạm vi và giới hạn

Model nguồn có 97 MeshRenderer; renderer gốc chứa hai ray và một tấm nền rời. Tách riêng renderer này cho tổng 99 đối tượng nguồn, không phải chứng nhận BOM 98 cấu kiện. Model chi tiết dùng offset cứng -0,013226 m để trùng đỉnh ray overview; không biến dạng mesh. Profile hai LOD vẫn chênh khoảng 1,03 mm ở mặt cắt so sánh. Không dùng dữ liệu này để chứng nhận khổ đường hoặc dung sai.

Terrain, cây, nhà và dây điện được giữ làm bối cảnh; chưa nằm trong registry cấu kiện đường ray. Dữ liệu đơn vị, kế hoạch, ngày và chi phí là **MINH HỌA**, không phải hồ sơ kiểm định thực tế. Không tạo hoạt động nâng cấp hoàn thành giả trong tương lai.

Đã kiểm thử trong Editor bằng UI thật, physics ray và thiết bị/input mô phỏng. Driver theo dõi HMD được giữ trong cấu hình sản phẩm; bài thử tự động có đóng băng pose để kiểm tra đầu vào xác định. Chưa kiểm thử headset Quest 3S, Android/IL2CPP, FPS, GPU memory hoặc mức độ thoải mái trên thiết bị. Có link.xml giới hạn ở các DTO SQLite để bảo toàn model reflection khi stripping, nhưng chưa xác minh bằng APK. Import/export toàn bộ dữ liệu lớn chưa được chứng nhận thời gian xử lý hoặc RAM trên Quest.

Báo cáo nhiệm vụ: `F:/UnityTemp/QuanLyVongDoiTaiSan_UI_Audit.txt` và `F:/UnityTemp/QuanLyVongDoiTaiSan_UI_Refinement_Report.txt`.


## Hồ sơ BIM giả định — cập nhật 13/11/2026

Thông tin nghiệp vụ được trình bày như một hồ sơ dự án đường sắt: mã quy hoạch/bản vẽ/nghiệm thu riêng, đơn vị phụ trách, vật liệu theo loại cấu kiện, kế hoạch kiểm tra/bảo trì và dự toán. Ví dụ: ray R260/tiết diện thiết kế 60E1, đệm EPDM, bu lông cấp bền 8.8; đây là các giả định thiết kế của kịch bản, không phải kết luận phân tích vật liệu từ mesh. Chỉ dẫn YTHL-CDKT-01 Rev.B là mã hồ sơ nội bộ giả định, không phải chứng nhận tuân thủ quy chuẩn.

Chi phí là dự toán kịch bản theo loại cấu kiện; tổng thi công của Block bằng tổng cấu kiện, Segment bằng tổng các Block. Ngân sách có dự phòng 15%; dự toán thay thế khi có kế hoạch cộng 20%. Không tự sửa kích thước đo từ model, ID hoặc ngày hồ sơ đã có.

Nhãn MINH HỌA/DEMO lặp lại được thay bằng nội dung nghiệp vụ cụ thể. Nguồn giả định vẫn ghi tại DataProvenance và tài liệu này. Các tên đơn vị, nhân sự, vật liệu và chi phí không phải hồ sơ hiện trường đã xác thực. Nhật ký khởi tạo cũ giữ nguyên để truy xuất nguồn gốc.

Migration nội dung `bim-yen-ha-scenario-2026-v2` chạy một lần trên DB đang dùng và DB được sao chép từ seed khi cài mới. Chỉ thay giá trị mặc định cũ khớp chính xác, bảo vệ giai đoạn có sự kiện sửa dữ liệu, không ghi đè trường tùy chỉnh. Hồ sơ kiểm tra/bảo trì tự nhập được giữ; các dòng khởi tạo có ID DEMO chỉ được cập nhật nếu nội dung vẫn đúng mẫu gốc. File Excel cũ đã xuất không tự đổi; xuất lại từ Scene để lấy nội dung hiện tại.


Highlight tài sản sử dụng viền hồng đậm tương phản (RGB 1 / 0,08 / 0,45), rộng 5 pixel, thay cho viền xanh cyan 3 pixel. Vẫn dùng silhouette outline xuyên che khuất; không thay material gốc của cấu kiện.

Kiểm thử cập nhật bay tự do: Play Mode đã xác nhận camera PC xuyên collider bằng phím E, VR Origin xuyên collider/bay lên ngoài giới hạn Zone cũ, giữ local pose HMD, chọn tài sản khi UI ẩn không lỗi. Chưa kiểm thử kính thật trong lượt này.


## Thanh thao tác phía trên

BimCommands nằm ở phía trên vùng mô hình trên desktop; trong VR thanh lệnh được nâng 0,52 m so với tầm mắt, cách trước khoảng 1,31 m. StatusFooter thay chỗ BimHeader ở đầu giao diện, hiển thị trạng thái, đường dẫn tài sản và nút ẩn UI. Header trang trí cũ đã xóa khỏi Scene đích sau khi kiểm tra tham chiếu; không sửa prefab nguồn.

Trong VR, StatusFooter nằm phía trên các nút trong cùng Canvas Thao tác, không để lại dưới Canvas desktop đang tắt. Vẫn chỉ có ba Canvas bảng quản lý; ẩn/hiện và chuyển về desktop khôi phục đúng hierarchy/vị trí.
