# Showroom — nội dung hợp nhất

Mở `Assets/ProjectLopHoc/Scenes/Showroom.unity`. Scene gốc được bổ sung trực tiếp, không thay bằng Scene package.

- Sa bàn: `SaBan_YenTuHaLongMain`, root tại **(10, 0, 19)**. Console khoảng **(10, 1.45, 28.7)**, ở cạnh bàn phía người chơi.
- Console: chạy/dừng tàu, x1–x4, trở về điểm đầu, bật/tắt tám lớp. Khi dùng VR, đóng Scene Menu bằng Y trước khi trỏ vào console.
- Bốn bảng thuyết minh nằm cạnh các mô hình tàu hiện tại. Các popup thông số cũ vẫn dùng như trước.
- Robot vẫn bám người chơi và mở giao diện chat qua cơ chế tương tác PC/VR. Phím C mở/đóng theo code UI nhập từ package; Esc đóng. Console sa bàn tạm ẩn trong lúc chat để không chồng bảng.
- UI chat có cấu trúc nhập, gửi, xóa, đóng, mic và đọc phản hồi. Adapter Showroom quản lý World Space UI/keyboard riêng; không đổi rig hay Input Actions dùng chung.

## Dịch vụ AI và giọng nói

Luồng UI đã kiểm thử bằng provider cục bộ chỉ tồn tại trong test Editor. Scene lưu vẫn nối Xkiro, không dùng provider giả làm backend.

Provider thật hiện báo thiếu API key. Cấu hình tại **Tools → BIM AI → Create or Open Local API Config**. Không đặt khóa trong chat hoặc tài liệu chia sẻ. STT, TTS và microphone cần kiểm thử riêng với dịch vụ/thiết bị thực.

## Phạm vi bảo toàn và hiệu năng

Cửa, PlayerSpawner, XR prefab, Scene Menu và 417 đối tượng cũ được giữ các giá trị hiện tại. Sa bàn dùng spline và geometry bản sao; streamer/HUD lái tàu lẫn trong bản sao đã tắt, không nạp 58 Zone thật.

Scene sau merge có khoảng 96.796 node; package nguồn có trên 54.000 MeshRenderer. Chưa xác nhận hiệu năng Quest 3S; cần đo/tối ưu riêng sa bàn trước khi build thiết bị. Không có Android Build trong nhiệm vụ này.

Một cache glyph của TextMeshPro fallback tự tăng khi test; không copy đè font package và không hoàn tác font đã sửa của người dùng. Chi tiết trong báo cáo.

Báo cáo đầy đủ: `F:/UnityTemp/Showroom_Package_Merge_Report.txt`. Evidence gốc ở `F:/UTT_vr/Temp/ShowroomMerge`.
