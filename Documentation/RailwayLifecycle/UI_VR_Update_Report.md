# Cập nhật giao diện tiếng Việt và tương tác VR — 13/11/2026

Scene: Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity

Đã lưu và mở lại Scene, trạng thái sạch. Bố cục tối màu, màu nhấn xanh ngọc, chữ và vùng bấm lớn hơn. Nhãn, tab, tên loại cấu kiện, nút và thông báo chính đã Việt hóa. Mã tài sản, khóa dữ liệu, tên file/CAD và nội dung người dùng vẫn giữ nguyên để không phá tương thích dữ liệu.

Đã thêm mục Quản lý vòng đời tài sản vào menu chuyển cảnh và đăng ký Scene trong danh sách dùng để tải Scene. Không tìm thấy asset BuildProfile tùy chỉnh; danh sách chung được dùng. Không Android Build.

VR dùng bảng trong không gian, TrackedDeviceGraphicRaycaster và XRUIInputModule. Tái sử dụng rig có sẵn; khi mở độc lập có prefab rig sao từ cấu hình Login, bỏ riêng Canvas đăng nhập và persistence. Camera kính được theo dõi, không bị camera orbit ghi đè. Điều chỉnh input ray chỉ trong Scene này và khôi phục khi rời Scene. Có cuộn bằng joystick và shader outline hỗ trợ stereo/instancing.

Điều khiển:
- Y trái / F1: menu chuyển không gian; phím 5 chọn nhanh Scene quản lý tài sản.
- X trái: ẩn/hiện, đặt lại bảng tài sản trước mặt.
- Cò trái/phải: bấm UI hoặc chọn cấu kiện.
- Nắm tay phải: mở cấp chi tiết; A: đến góc quan sát; B: cấp trước.
- F8 trong Editor: bật/tắt chế độ trình bày VR để dùng Simulator.
- Chọn ô nhập để mở bàn phím ảo có chữ Việt và dấu thanh. Bàn phím máy tính vẫn dùng được.

Kiểm thử đã đạt trong Play Mode:
1. Chuột thực qua XRUIInputModule chọn danh sách và mở chi tiết.
2. Ray XR thực với input/pose thử nghiệm bấm nút, chọn bu lông qua Physics, làm nổi bật đúng cấu kiện; UI không chọn xuyên mô hình.
3. Ray chọn ô nhập và bấm phím ă sau khi ô mất focus; nhập dấu thanh, xóa lùi và bật/tắt bảng.
4. Nút menu thật tải Scene bằng LoadSceneAsync từ Scene trong bộ nhớ; không tạo asset Scene thử nghiệm.
5. UI và rig cục bộ được hủy khi rời Scene; rig persistent có sẵn được tái sử dụng, không tạo trùng XR Origin/EventSystem.
6. Chuyển máy tính → VR nhiều lần vẫn có một camera và raycaster hoạt động. Rời Scene khôi phục vị trí, camera và input mode của rig có sẵn.
7. Không lỗi C# mới trong lượt kiểm thử UI/VR; kiểm tra log sau integration không thấy exception C# mới. Hai shader outline không có lỗi biên dịch. Scene không thiếu script hoặc dependency.
8. Đối chiếu SHA256: 83/83 file nguồn được bảo vệ giữ nguyên, gồm các Scene nguồn và 58 Zone.

Giới hạn: pose/cò được cấp bằng harness kiểm thử trong Editor; chưa xác minh kính hoặc các nút vật lý, FPS headset hay APK. Chưa kiểm thử Android staging và thao tác tệp trên thiết bị. Không tuyên bố đã chứng nhận Quest. Giữ nguyên seed, tài khoản, hệ thống tàu, XR nguồn và các Scene khác.

Chi tiết bằng chứng ở thư mục UI_VR_Evidence; hướng dẫn đầy đủ ở README.md.
