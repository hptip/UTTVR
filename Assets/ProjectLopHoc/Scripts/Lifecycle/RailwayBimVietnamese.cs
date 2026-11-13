using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>Presentation only: stable database keys, CAD names and asset IDs remain unchanged.</summary>
public static class RailwayBimVietnamese
{
    static readonly Dictionary<string,string> Labels=new Dictionary<string,string>
    {
        {"General","Tổng quan"},{"Technical","Thông số"},{"Lifecycle","Vòng đời"},{"Inspection","Kiểm tra"},{"Maintenance","Bảo trì"},{"History","Lịch sử"},{"Statistics","Thống kê"},
        {"Segment","Phân đoạn"},{"Block","Mô-đun"},{"Component","Cấu kiện"},{"Rail","Thanh ray"},{"Slab","Tấm nền bê tông"},{"Sleeper","Tà vẹt"},{"Bolt","Bu lông"},{"Clip","Kẹp ray"},{"Pad","Đệm ray"},{"Fastener","Liên kết ray"},
        {"AssetID","Mã tài sản"},{"ParentAssetID","Mã tài sản cha"},{"AssetType","Cấp tài sản"},{"DisplayName","Tên hiển thị"},{"ZoneID","Khu vực"},{"TrackID","Đường ray"},{"SegmentID","Mã phân đoạn"},{"BlockID","Mã mô-đun"},{"ComponentType","Loại cấu kiện"},{"ChainageStart","Lý trình đầu"},{"ChainageEnd","Lý trình cuối"},{"ModelReference","Tham chiếu mô hình"},
        {"ModelWidth","Chiều rộng mô hình"},{"ModelHeight","Chiều cao mô hình"},{"ModelLength","Chiều dài mô hình"},{"DataProvenance","Nguồn dữ liệu"},{"ModelSource","Nguồn mô hình"},
        {"InstallDate","Ngày lắp đặt"},{"CommissioningDate","Ngày đưa vào sử dụng"},{"DesignLifeYears","Tuổi thọ thiết kế (năm)"},{"ConditionGrade","Đánh giá tình trạng"},{"OperationalStatus","Trạng thái vận hành"},{"LastInspectionDate","Lần kiểm tra gần nhất"},{"NextInspectionDate","Ngày kiểm tra tiếp theo"},{"MaintenanceStatus","Trạng thái bảo trì"},{"ReplacementPlanDate","Ngày dự kiến thay thế"},{"Notes","Ghi chú"},
        {"InspectionDate","Ngày kiểm tra"},{"Finding","Nhận xét"},{"Inspector","Người kiểm tra"},{"MaintenanceDate","Ngày bảo trì"},{"MaintenanceType","Loại bảo trì"},{"Description","Nội dung"},{"Cost","Chi phí"},{"Result","Kết quả"},
        {"DemoSeed","Khởi tạo dữ liệu minh họa"},{"PropertyChanged","Đổi thông số"},{"LifecycleChanged","Đổi hồ sơ vòng đời"},{"Renamed","Đổi tên"},{"Import","Nhập dữ liệu"},
        {"Track_Right","Đường bên phải"},{"Track_Left","Đường bên trái"},{"Right","Bên phải"},{"Left","Bên trái"},{"Number","Số"},{"Text","Văn bản"},{"Date","Ngày"}
        ,{"Anchor","Neo liên kết"},{"BasePlate","Bản đế"},{"GuidePlate","Bản dẫn hướng"},{"SteelDetail","Chi tiết thép"},{"Support","Gối đỡ"},{"Washer","Vòng đệm"}
        ,{"TR","Đường bên phải"},{"TL","Đường bên trái"}
    };
    public static string Text(string value)
    {
        if(string.IsNullOrEmpty(value))return value??"";
        if(Labels.TryGetValue(value,out var label))return label;
        return value.Replace("DEMO","MINH HỌA").Replace("Module ","Mô-đun ");
    }
    public static string Name(BimAsset asset)
    {
        if(asset.AssetType=="Segment")return "Phân đoạn "+asset.AssetID.Split('-')[asset.AssetID.Split('-').Length-1];
        if(asset.AssetType=="Block")return "Mô-đun "+asset.AssetID.Split('-')[asset.AssetID.Split('-').Length-1];
        // Explicit user names are preserved. CAD-generated component names use translated type plus immutable ID.
        if(asset.DisplayName.Contains("_")||asset.DisplayName=="Concrete_Slab")return Text(asset.ComponentType)+" · "+asset.AssetID.Substring(asset.AssetID.LastIndexOf('-')+1);
        return Text(asset.DisplayName);
    }
}
