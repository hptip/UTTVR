# Showroom Geometry Optimization V3

Project: F:/GITHUB/UTTVR — Unity 6000.3.0f1. Branch: `codex/showroom-geometry-v3` (chưa commit).

## Kết quả và giới hạn

Đã audit toàn bộ MeshRenderer của Showroom, tạo mesh LOD offline riêng và tích hợp vào custom GPU instancing. Không sửa source FBX, prefab, material, collider, metadata BIM hoặc Scene QuanLyVongDoiTaiSan. Desktop/Edit Mode giữ mesh gốc; Android dùng profile Quality10 đã được nâng mức chi tiết tại những mesh không giảm an toàn.

**Chưa đạt mục tiêu 1,5–2 triệu triangles/frame hoặc xác nhận 72 FPS trên Quest.** Geometry estimate một lượt vẽ khác với frame counter có stereo, shadow, camera/phụ và nhiều pass. Xem số thực tế ở phần benchmark. Không suy hiệu năng standalone từ Editor.

## Audit

- [Bảng đầy đủ theo mesh và nhóm](Showroom_Geometry_Audit.csv), [báo cáo audit và top 20](Showroom_Geometry_Audit.md).
- Tổng geometry có trọng số theo instance/material slot đang active: 14.861.120 triangles cho một lượt vẽ. Không cộng lại các nguồn renderer đã được custom instancing quản lý.
- Bottleneck gồm ray lặp lại, hai mesh tàu Riyadh khoảng 1,85 triệu triangles/mesh và núi lặp lại. Không mặc định chỉ tối ưu ray.
- Không có LODGroup hoặc embedded Mesh LOD trước thay đổi. Custom instancing cũ dùng DrawMeshInstanced, không giảm geometry.

## Triển khai

- Mesh mới: `Assets/ProjectLopHoc/Generated/ShowroomGeometryV3/Meshes`.
- Profile: `Assets/ProjectLopHoc/Generated/ShowroomGeometryV3/ShowroomQuestGeometry.asset`.
- Dùng MeshLodUtility của Unity 6.3 trên bản sao; lấy index của từng mức, compact vertex buffer và giữ UV, normals, tangents, vertex colors cùng submesh/material mapping. Không chạy simplification lúc runtime; không dùng MeshUtility.Optimize để giảm triangles.
- Mỗi source mesh dùng chung một bộ phiên bản, không tạo mesh riêng cho từng instance. Các mức 50/25/10 là mục tiêu; tỷ lệ thực tế ghi riêng trong [Showroom_Optimized_Meshes.csv](Showroom_Optimized_Meshes.csv).
- Kiểm tra bounds, finite vertices, số submesh và không làm mất toàn bộ một material slot. Bounds chưa đủ: kiểm tra silhouette ba góc cho nhóm đóng góp lớn, yêu cầu IoU tối thiểu 95%; tăng lại chi tiết nếu không đạt. [Kết quả kiểm tra](SilhouetteValidation.csv).
- Mesh rất nhỏ, geometry biến dạng hoặc không giảm an toàn giữ nguyên. Glyph TMP sinh động không đưa vào profile mesh tĩnh.
- ShowroomGeometryController chỉ thay sharedMesh của MeshFilter trong Showroom. Giữ object identity, Transform và collider gốc. Sources của custom instancing không được vẽ thêm bởi MeshRenderer.
- Instancing dùng mesh vật lý đã giảm và RenderMeshInstanced với forceMeshLod=0. Không phụ thuộc việc API tự chọn embedded LOD. Không render đồng thời bản gốc và bản giảm. Giữ nguyên 390 logical batches của nhóm ray.
- Android build processor chỉ tắt BatchingStatic trên các target cần đổi mesh trong bản Scene dùng để build; tránh static batching vô hiệu hóa runtime mesh switching. Không sửa static flags trên Scene nguồn hoặc Desktop. Build processor chưa được xác minh qua một Android Build thực tế.

## Ảnh và rủi ro

Ảnh bốn chế độ của mesh ưu tiên ở [Images](Images); ảnh cùng góc nhìn khi benchmark ở [EditorBenchmark](EditorBenchmark).

Mức 10% ban đầu làm một số bracket ray biến thành dạng nhọn; đã loại mức đó khỏi cấu hình sử dụng và nâng chi tiết. Silhouette gate không thay thế kiểm tra mọi góc nhìn và texture trên kính. Geometry nhỏ/có nhiều hard edges cần mức cao hơn nên không thể ép đồng loạt về 10% mà bảo toàn hình dáng.

Profile giữ cả mesh gốc và các mức benchmark: tăng dung lượng và bộ nhớ mesh so với chỉ một mức; đọc memory thực đo. Chưa tối ưu AssetBundle/addressable stripping. Không có cơ sở khẳng định FPS tăng 5–10 lần. Draw calls/CPU còn có thể là bottleneck dù GPU geometry giảm.

Nếu bắt buộc đạt 1,5–2 triệu, bước tiếp theo nên là proxy/HLOD theo cụm cho phụ kiện CAD quá nhỏ, giữ collider/ID chọn cấu kiện và hiển thị bản chi tiết khi chọn/zoom; bake chi tiết nhỏ vào texture khi thích hợp. Chưa triển khai proxy thay thế thiếu kiểm chứng.

## Kiểm thử

[Regression.txt](Regression.txt) ghi kiểm thử Play Mode: chạy Showroom độc lập, popup tàu/BIM, đóng popup bằng XR trigger giả lập, ray UI, tám nhóm ẩn/hiện, chạy/dừng/đổi tốc độ tàu và menu scene. Đây là điều khiển interactor hiện có bằng test input trong Editor, chưa phải controller vật lý.

Không đo API chatbot thực, nhiều máy multiplayer hoặc chuyển scene thật qua cửa. Source mesh/collider không đổi; không có thử nghiệm vật lý trên Quest. Xem [Integrity.txt](Integrity.txt) cho đối chiếu file nguồn/Scene khác và Transform.

## Benchmark và build Quest

Số liệu cuối cùng: [EditorBenchmark/Summary.csv](EditorBenchmark/Summary.csv), raw từng frame và Environment.txt. Frame time p95/p99, FPS median, triangles, vertices, draws, SetPass, CPU, GPU và memory chỉ ghi nếu counter có dữ liệu; ô trống là không đo được. Counter Editor có thể bao gồm chi phí Editor và các pass, không chỉ một lượt vẽ mô hình.

ADB không thấy Quest khi kiểm tra. Chưa build/cài APK trong nhiệm vụ này. [Hướng dẫn benchmark và build trên Quest](Quest_Benchmark_Instructions.md) có cấu hình tự đo cả bốn mức trong cùng APK. Cần cùng góc nhìn, cùng cấu hình XR/URP và kiểm tra camera drift; không tuyên bố đạt 72 FPS trước khi có kết quả headset.

## Rollback

Vì chưa commit, chỉ khôi phục hai file tracked vốn sạch trước nhiệm vụ:

```powershell
git restore -- Assets/ProjectLopHoc/Scenes/Showroom.unity Assets/ShowroomIntegration/Runtime/ShowroomInstancedVisualGroup.cs
```

Sau đó có thể bỏ riêng các thư mục mới `Assets/Editor/ShowroomGeometryV3`, `Assets/ShowroomIntegration/Runtime/GeometryV3`, `Assets/ProjectLopHoc/Generated/ShowroomGeometryV3` và meta tương ứng bằng Unity sau khi reference đã khôi phục. Không dùng reset --hard hoặc clean toàn project: trước nhiệm vụ đã có thay đổi chưa commit ở các Scene/file khác. Không tự động commit hoặc xóa source asset.

## API tham khảo

[Unity MeshLodUtility.GenerateMeshLods](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/meshlodutility/generatemeshlods), [RenderParams.forceMeshLod](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/renderparams/forcemeshlod).

## Số liệu cuối cùng — chỉ Windows Editor RTX 3060, D3D12, PC URP

Cùng camera, HMD input được giữ cố định chỉ trong test Editor. 180 mẫu/mức, warmup 90 frame. Không phải benchmark standalone Quest. Render Thread counter không khả dụng. Mọi kết quả raw và memory có trong EditorBenchmark.

|Mode|Triangles median (frame counter)|Vertices median|Draws median|SetPass|CPU main ms|GPU ms|FPS median|Frame p95 ms|Frame p99 ms|Used memory GiB|
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
|Original|25051713.0000|46060945.0000|9479.0000|222.0000|11.286|10.9087|88.5120|12.2787|14.8691|4.939|
|Quality50|17901885.0000|32248169.0000|9463.0000|222.0000|11.120|8.8453|89.8262|11.9554|12.2090|4.941|
|Quality25|15384442.0000|26937924.0000|9453.0000|222.0000|11.152|7.4711|89.3080|12.0154|12.3735|4.955|
|Quality10|14609165.0000|25538662.0000|9446.0000|222.0000|11.172|6.9612|89.3399|11.7477|12.0774|4.971|

GPU giảm trong phép đo Editor nhưng FPS gần như giữ nguyên, draw calls chưa giảm đáng kể. Không có cơ sở suy ra 72 FPS Quest. Đây chưa phải hoàn tất mục tiêu geometry 1,5–2 triệu/frame.

### Geometry estimate cuối cùng (một lượt vẽ, không phải frame counter)

```text
Original | active weighted triangles=14861120 | active weighted vertices=22167721
Quality50 | active weighted triangles=9780224 | active weighted vertices=15166179
Quality25 | active weighted triangles=7852615 | active weighted vertices=12595979
Quality10 | active weighted triangles=6772420 | active weighted vertices=11595035
```

### Top 20 ban đầu theo triangle contribution có trọng số

|Nhóm|Mesh|Instances active|Triangles có trọng số|
|---|---|---:|---:|
|train|obj9|1|1852424|
|train|obj18|1|1852424|
|rail|Mesh_60|2667|1128141|
|nature|Mountain_19_Depth_00|47|924020|
|rail|Mesh_56|2667|917448|
|rail|Mesh_54|2667|917448|
|nature|Mountain_6_Depth_00|29|570140|
|nature|Mountain_11_Depth_00|29|570111|
|rail|Mesh_51|2667|544068|
|rail|Mesh_57|2667|488061|
|rail|Object_4_mesh0|2667|437388|
|rail|Mesh_59|2667|330708|
|rail|Mesh_61|2667|320040|
|khac|Mesh_56|9|318816|
|khac|Mesh_59|9|278532|
|rail|Mesh_62|2667|256032|
|rail|Mesh_53|2667|202692|
|rail|Mesh_55|2667|189357|
|khac|Mesh_55|9|179271|
|rail|Cylinder|178|135992|

### Đối chiếu cuối cùng

Protected source/other-scene files changed=0; original Transform identity/serialization changed=0

Regression cuối cùng: DONE PASS. Scene Showroom được lưu, không chạy Play Mode khi bàn giao. Không có Android Build hoặc benchmark Quest thực tế.
