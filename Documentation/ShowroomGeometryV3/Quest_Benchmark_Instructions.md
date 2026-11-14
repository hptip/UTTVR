# Kiểm thử trên Quest 3S

1. Dùng Android Build Profile hiện có, giữ nguyên XR, URP, resolution và graphics API. Bảo đảm Showroom có trong Scene List. Không thay đổi các scene khác. Build **Development Build** để có nhiều profiler counters hơn; dùng cùng APK cho cả bốn chế độ.
2. Mở Showroom độc lập. `ShowroomGeometryController` mặc định dùng Quality10 trên Android và Original trên Desktop. Có thể chuyển về Original trong component để đối chiếu trong Play Mode; Edit Mode luôn giữ mesh gốc.
3. Trong Editor/desktop nhấn **F8**, hoặc gọi context menu **Run four-mode geometry benchmark** trên `ShowroomGeometryBenchmark`. Công cụ lần lượt đo Original, Quality50, Quality25, Quality10; mỗi mức mặc định warmup 90 frame rồi lấy 600 mẫu. Đặt cùng camera; tàu sa bàn được tạm dừng, các lớp được bật đồng nhất và phục hồi sau đo.
4. Trên kính, đặt file `ShowroomGeometryBenchmark.json` trong `Application.persistentDataPath` với nội dung:

```json
{"autoStart":true,"warmupFrames":180,"sampleFrames":600}
```

Tìm **applicationIdentifier** thực tế của Android trong Player Settings, không đoán package ID. Dùng ADB:

```powershell
$appId = 'THAY_BANG_APPLICATION_IDENTIFIER_THUC_TE'
adb shell mkdir -p /sdcard/Android/data/$appId/files
adb push ShowroomGeometryBenchmark.json /sdcard/Android/data/$appId/files/ShowroomGeometryBenchmark.json
```

5. Mở lại ứng dụng, vào Showroom và đứng yên nhìn đúng góc muốn đo. Công cụ giữ HMD tracking, không khóa đầu người dùng. Đọc `Camera position/angle drift` trong kết quả: thay đổi góc nhìn làm mất tính đối chiếu nghiêm ngặt. Đo lại nếu tư thế khác đáng kể. Nên đo riêng góc toàn cảnh và góc cận.
6. Lấy kết quả:

```powershell
adb pull /sdcard/Android/data/$appId/files/ShowroomGeometryV3 ./QuestResults
```

Các file gồm `Environment.txt`, `Summary.csv`, mẫu raw mỗi chế độ, ảnh mỗi chế độ và `Completed.txt`. Bỏ file cấu hình tự chạy khi không muốn benchmark lại. `Summary.csv` có triangles, vertices, draw calls, SetPass, CPU, GPU nếu có, FPS median, frame time p95/p99 và memory. Ô trống = chưa đo được; không coi GPU time trống là 0 ms. CPU profiler counters có đơn vị ns; chia 1.000.000 để ra ms.

7. Kiểm tra lại chọn tàu/cấu kiện BIM, highlight, mở/đóng popup, ẩn/hiện cả tám lớp, thay đổi tỉ lệ sa bàn, chạy/dừng tàu, ray VR và chuyển scene. Kiểm tra cả sau khi quay lại Showroom. Đo GPU trực tiếp bằng Meta Quest Developer Hub/OVR Metrics nếu Unity FrameTimingManager không cung cấp GPU timing. Không suy FPS standalone từ FPS Editor. Mục tiêu 72 Hz có ngân sách khoảng 13,89 ms/frame; cần kiểm tra p95/p99 và thermal sau nhiều phút, không chỉ median ngắn.

Không có kính trong `adb devices` khi triển khai, nên chưa có số liệu Quest thực tế và chưa xác nhận 72 FPS. Công cụ đo không tự build hoặc cài APK.
