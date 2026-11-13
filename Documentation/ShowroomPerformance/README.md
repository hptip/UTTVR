# Showroom performance — giữ nguyên mô hình

Đọc Showroom_PreserveModel_Performance_Report.txt và Evidence/Regression.txt.

Tối ưu hoạt động trong Play/player; Edit Mode giữ Renderer nguồn để chỉnh sửa. Bật/tắt component ShowroomInstancedVisualGroup ở SaBan_YenTuHaLongMain/Model/Content/Layer_rail để opt-in/opt-out. Không chạy lại tool tạo sa bàn. Dữ liệu batch chỉ dùng mesh hiện có.

Fast visibility giữ root rail active trong khi phần vẽ ẩn; dùng SaBanLayer.IsVisible / SetVisible cho trạng thái UI. Adapter chỉ đăng ký cho lớp thuần visual đã qua guard. Các lớp khác giữ cơ chế SetActive.

Không sửa mesh/material nguồn dùng chung. Nếu thay mesh/material trong mô hình, tắt component tối ưu đến khi dữ liệu batch được cập nhật. Vị trí nguồn được lấy lại khi vào Play. Đánh giá lại khi bổ sung light/reflection probes.

Kết quả FPS/GPU là Unity Editor Windows, không phải Quest. Chưa build/cài lại APK trong nhiệm vụ này.
