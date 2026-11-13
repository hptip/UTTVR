using System;
using System.Linq;

public sealed partial class RailwayBimStore
{
    // Fictional project scenario. Exact old defaults and user-stage edits are guarded;
    // the marker makes this a one-time data-content upgrade, not a recurring reset.
    const string BimScenarioVersion="bim-yen-ha-scenario-2026-v2";
    void MigrateBimScenario()
    {
        if(Assets.Count==0||Db.ExecuteScalar<int>("SELECT count(*) FROM SeedVersions WHERE Version=?",BimScenarioVersion)>0)return;
        Db.RunInTransaction(()=>
        {
            void UpgradeDefault(string stage,string field,string oldSql)
            {
                string next=RailwayBimStages.Get(stage).fields.Single(f=>f.key==field).seedSql;
                string from=" FROM Assets a JOIN AssetLifecycle l ON l.AssetID=a.AssetID WHERE a.AssetID=AssetStageFields.AssetID)";
                Db.Execute("UPDATE AssetStageFields SET Value=(SELECT coalesce("+next+",'')"+from+" WHERE StageKey=? AND FieldKey=? AND Value=(SELECT coalesce("+oldSql+",'')"+from+" AND NOT EXISTS(SELECT 1 FROM AssetLifecycleEvents e WHERE e.AssetID=AssetStageFields.AssetID AND e.StageKey=?)",stage,field,stage);
            }
            UpgradeDefault("Planning","PlanCode","'MH-QH-'||a.AssetID");
            UpgradeDefault("Planning","Purpose","'Quản lý mô hình đường sắt — MINH HỌA'");
            UpgradeDefault("Planning","PlanningUnit","'Nhóm quy hoạch mô phỏng UTT'");
            UpgradeDefault("Planning","Budget","1000000+(CAST(substr(a.AssetID,-3) AS INTEGER)%12)*75000");
            UpgradeDefault("Planning","ApprovalStatus","'Đã phê duyệt — MINH HỌA'");
            UpgradeDefault("Planning","Notes","'MINH HỌA phục vụ học tập; không phải hồ sơ kiểm định hoặc tiêu chuẩn thực tế.'");
            UpgradeDefault("Design","DesignDocument","'MH-TK-'||a.AssetID");
            UpgradeDefault("Design","Designer","'Nhóm thiết kế mô phỏng UTT'");
            UpgradeDefault("Design","Standards","'Chưa liên kết tiêu chuẩn được xác nhận'");
            UpgradeDefault("Design","Material","CASE WHEN a.AssetType<>'Component' THEN 'Nhiều vật liệu — xem hồ sơ cấu kiện' WHEN a.ComponentType='Pad' THEN 'Chưa xác nhận — minh họa cấu kiện đệm' WHEN a.ComponentType IN ('Slab','Sleeper') THEN 'Bê tông — giả định minh họa' ELSE 'Thép — giả định minh họa' END");
            UpgradeDefault("Design","TechnicalNotes","'MINH HỌA phục vụ học tập; không phải hồ sơ kiểm định hoặc tiêu chuẩn thực tế.'");
            UpgradeDefault("Construction","Contractor","'Nhóm thi công mô phỏng UTT'");
            UpgradeDefault("Construction","AcceptanceBatch","'MH-NT-'||a.AssetID");
            UpgradeDefault("Construction","AcceptanceResult","'Đạt theo kịch bản học tập; không phải nghiệm thu thực tế'");
            UpgradeDefault("Construction","ConstructionCost","500000+(CAST(substr(a.AssetID,-3) AS INTEGER)%9)*50000");
            UpgradeDefault("Construction","Notes","'MINH HỌA phục vụ học tập; không phải hồ sơ kiểm định hoặc tiêu chuẩn thực tế.'");
            UpgradeDefault("Operation","OperationalStatus","l.OperationalStatus");
            UpgradeDefault("Operation","Operator","'Nhóm vận hành mô phỏng UTT'");
            UpgradeDefault("Operation","Notes","'MINH HỌA phục vụ học tập; không phải hồ sơ kiểm định hoặc tiêu chuẩn thực tế.'");
            UpgradeDefault("Maintenance","MaintenancePlan","'Kiểm tra định kỳ liên kết và bề mặt — MINH HỌA'");
            UpgradeDefault("Maintenance","WorkType","'Vệ sinh và kiểm tra theo kịch bản học tập'");
            UpgradeDefault("Maintenance","MaintenanceStatus","l.MaintenanceStatus");
            UpgradeDefault("Maintenance","Notes","'MINH HỌA phục vụ học tập; không phải hồ sơ kiểm định hoặc tiêu chuẩn thực tế.'");
            UpgradeDefault("Upgrade","UpgradePlan","CASE WHEN coalesce(l.ReplacementPlanDate,'')='' THEN 'Chưa có kế hoạch' ELSE 'Dự kiến thay thế — MINH HỌA' END");
            UpgradeDefault("Upgrade","Reason","'Chưa có hoạt động nâng cấp thực tế'");
            UpgradeDefault("Upgrade","Changes","''");
            UpgradeDefault("Upgrade","Executor","''");
            UpgradeDefault("Upgrade","UpgradeCost","0");
            UpgradeDefault("Upgrade","Notes","'MINH HỌA phục vụ học tập; không phải hồ sơ kiểm định hoặc tiêu chuẩn thực tế.'");
            UpgradeDefault("Design","Material","'Nhiều vật liệu — xem hồ sơ cấu kiện'");
            UpgradeDefault("Design","Material","'Chưa xác nhận — minh họa cấu kiện đệm'");
            Db.Execute("UPDATE AssetLifecycleStages SET Status=CASE StageKey WHEN 'Planning' THEN 'Đã phê duyệt' WHEN 'Design' THEN 'Đã phát hành hồ sơ Rev.B' WHEN 'Construction' THEN 'Đã nghiệm thu' WHEN 'Operation' THEN 'Đang khai thác' WHEN 'Maintenance' THEN 'Theo kế hoạch bảo trì' ELSE CASE WHEN AssetID IN (SELECT AssetID FROM AssetLifecycle WHERE coalesce(ReplacementPlanDate,'')<>'') THEN 'Đã lập kế hoạch thay thế' ELSE 'Chưa có kế hoạch' END END WHERE Status IN ('Hồ sơ minh họa','Dự kiến — MINH HỌA','Chưa có kế hoạch') AND NOT EXISTS(SELECT 1 FROM AssetLifecycleEvents e WHERE e.AssetID=AssetLifecycleStages.AssetID AND e.StageKey=AssetLifecycleStages.StageKey)");
            Db.Execute("UPDATE AssetLifecycle SET OperationalStatus=replace(OperationalStatus,' — DEMO','') WHERE OperationalStatus='Đang vận hành — DEMO'");
            Db.Execute("UPDATE AssetLifecycle SET MaintenanceStatus=replace(MaintenanceStatus,' — DEMO','') WHERE MaintenanceStatus IN ('Đã bảo trì — DEMO','Có kế hoạch — DEMO','Theo dõi định kỳ — DEMO')");
            Db.Execute("UPDATE AssetLifecycle SET Notes='Hồ sơ '||AssetID||'; quản lý theo gói kết cấu đường trên Zone 021, kiểm tra định kỳ và lưu biên bản theo mã tài sản.' WHERE Notes='DEMO: tuổi thọ, ngày và chi phí là giả định, không phải tiêu chuẩn hay hồ sơ đo thực tế.'");
            Db.Execute("UPDATE AssetProperties SET Value='Hồ sơ BIM giả định của dự án Yên Hạ; kích thước hình học lấy từ mô hình, thông tin nghiệp vụ theo kịch bản dự án.' WHERE Name='DataProvenance' AND Value='DEMO — kích thước hình học từ model; hồ sơ vòng đời giả lập phục vụ học tập.'");
            // Only generated, unedited legacy records. Keep IDs, dates and user records.
            Db.Execute("UPDATE AssetInspections SET Finding=CASE ConditionGrade WHEN 'Tốt' THEN 'Bề mặt và liên kết ổn định; chưa ghi nhận hư hỏng cần can thiệp' WHEN 'Cần theo dõi' THEN 'Dấu hiệu lão hóa bề mặt nhẹ; lập ảnh theo dõi tại kỳ kiểm tra tiếp theo' WHEN 'Cần kiểm tra' THEN 'Cần kiểm tra chi tiết liên kết và đối chiếu biên bản lắp đặt' WHEN 'Cần bảo trì' THEN 'Lập phiếu vệ sinh, xử lý ăn mòn và kiểm tra lại liên kết' WHEN 'Đã bảo trì' THEN 'Đã hoàn thành kiểm tra, chờ nghiệm thu công việc bảo trì' ELSE 'Đưa vào danh mục đánh giá thay thế trong kế hoạch 2027' END, Inspector='Tổ Kiểm tra đường 021 – Nguyễn Minh Hòa', Notes='Biên bản KT-2026-06-'||AssetID||'; ảnh hiện trạng được quản lý theo mã cấu kiện.' WHERE InspectionID='DEMO-I-'||AssetID AND Inspector='Nhóm mô phỏng UTT' AND Notes='Dữ liệu DEMO, không phải kiểm định thật.' AND Finding LIKE 'DEMO:%'");
            Db.Execute("UPDATE AssetMaintenance SET MaintenanceType='Bảo dưỡng định kỳ kết cấu đường trên', Description=CASE WHEN AssetID IN (SELECT AssetID FROM Assets WHERE ComponentType='Rail') THEN 'Vệ sinh bề mặt lăn, kiểm tra mối nối và cập nhật ảnh hiện trạng ray' WHEN AssetID IN (SELECT AssetID FROM Assets WHERE ComponentType IN ('Slab','Sleeper')) THEN 'Vệ sinh bề mặt, kiểm tra vết nứt và thông thoáng đường thoát nước' WHEN AssetID IN (SELECT AssetID FROM Assets WHERE ComponentType IN ('Pad','GuidePlate')) THEN 'Vệ sinh, kiểm tra biến dạng và vị trí tiếp xúc của chi tiết đàn hồi/cách điện' ELSE 'Vệ sinh cụm liên kết, kiểm tra lực siết và phục hồi lớp bảo vệ bề mặt' END, Cost=(SELECT CASE a.ComponentType WHEN 'Rail' THEN 250000 WHEN 'Slab' THEN 500000 WHEN 'Sleeper' THEN 150000 WHEN 'Bolt' THEN 25000 WHEN 'Washer' THEN 15000 WHEN 'Pad' THEN 30000 WHEN 'GuidePlate' THEN 25000 WHEN 'Fastener' THEN 45000 WHEN 'BasePlate' THEN 80000 WHEN 'Anchor' THEN 35000 WHEN 'SteelDetail' THEN 30000 WHEN 'Support' THEN 60000 ELSE 750000 END FROM Assets a WHERE a.AssetID=AssetMaintenance.AssetID), Result='Đã nghiệm thu công việc; bàn giao khai thác và tiếp tục theo dõi định kỳ' WHERE MaintenanceID='DEMO-M-'||AssetID AND MaintenanceType='Bảo trì mô phỏng' AND Description IN ('DEMO: vệ sinh và kiểm tra bề mặt ray','DEMO: vệ sinh, kiểm tra liên kết và ghi nhận hiện trạng') AND Result='Hoàn thành theo kịch bản DEMO' AND Cost=CASE WHEN AssetID IN (SELECT AssetID FROM Assets WHERE ComponentType='Bolt') THEN 120000 ELSE 750000 END");
            Db.Execute("INSERT INTO SeedVersions VALUES(?,?)",BimScenarioVersion,DateTime.UtcNow.ToString("O"));
        });
    }
}
