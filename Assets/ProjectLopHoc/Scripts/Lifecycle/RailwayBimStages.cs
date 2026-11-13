using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

public sealed class BimStage
{
    public string AssetID{get;set;}public string StageKey{get;set;}public int SchemaVersion{get;set;}public string Status{get;set;}public string UpdatedAt{get;set;}
}
public sealed class BimStageField
{public string AssetID{get;set;}public string StageKey{get;set;}public string FieldKey{get;set;}public string Value{get;set;}public string DataType{get;set;}public string Unit{get;set;}}
public static class RailwayBimStages
{
    public const int Version=1;
    public sealed class Field
    {
        public string key,label,type,unit,seedSql;public bool readOnly;
        public Field(string key,string label,string type="Text",string unit="",string sql="''",bool readOnly=false){this.key=key;this.label=label;this.type=type;this.unit=unit;seedSql=sql;this.readOnly=readOnly;}
    }
    public sealed class Stage
    {public string key,name,sheet;public Field[] fields;public Stage(string key,string name,string sheet,params Field[] fields){this.key=key;this.name=name;this.sheet=sheet;this.fields=fields;}}
    public static readonly Stage[] All={
        new Stage("Planning","Quy hoạch","QuyHoach",
            new Field("PlanCode","Mã kế hoạch",sql:"'YTHL-QH-2020-'||a.AssetID"),new Field("Purpose","Mục tiêu sử dụng",sql:"'Duy trì an toàn khai thác và khả năng truy xuất '||CASE a.ComponentType WHEN 'Rail' THEN 'ray thép' WHEN 'Slab' THEN 'bản bê tông đường ray' WHEN 'Sleeper' THEN 'tà vẹt bê tông' WHEN 'Bolt' THEN 'bu lông liên kết' WHEN 'Fastener' THEN 'kẹp đàn hồi' WHEN 'Pad' THEN 'đệm ray' WHEN 'GuidePlate' THEN 'bản dẫn hướng' WHEN 'Anchor' THEN 'neo liên kết' WHEN 'BasePlate' THEN 'bản đế ray' WHEN 'Washer' THEN 'vòng đệm' WHEN 'Support' THEN 'gối đỡ' WHEN 'SteelDetail' THEN 'chi tiết thép' ELSE 'kết cấu đường ray' END"),new Field("Area","Khu vực / lý trình",sql:"a.ZoneID||' / '||a.ChainageStart||'–'||a.ChainageEnd||' m'"),new Field("PlanningUnit","Đơn vị lập quy hoạch",sql:"'Ban Quy hoạch và Phát triển tuyến Yên Tử – Hạ Long'"),new Field("PlanDate","Ngày lập","Date",sql:"date(l.InstallDate,'-730 days')"),new Field("Budget","Ngân sách dự kiến","Number","VND","round((CASE a.AssetType WHEN 'Component' THEN CASE a.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END WHEN 'Block' THEN (SELECT coalesce(sum(CASE c.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END),0) FROM Assets c WHERE c.ParentAssetID=a.AssetID) ELSE (SELECT coalesce(sum(CASE c.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END),0) FROM Assets b JOIN Assets c ON c.ParentAssetID=b.AssetID WHERE b.ParentAssetID=a.AssetID) END)*1.15,0)"),new Field("ApprovalStatus","Trạng thái phê duyệt",sql:"'Đã phê duyệt'"),new Field("Notes","Ghi chú",sql:"'Thuộc gói kết cấu đường trên '||a.ZoneID||'; phạm vi lý trình '||printf('%.3f',a.ChainageStart)||'–'||printf('%.3f',a.ChainageEnd)||' m; dự phòng ngân sách 15%.'")),
        new Stage("Design","Thiết kế","ThietKe",
            new Field("DesignDocument","Hồ sơ thiết kế",sql:"'YTHL-TKCS-2021-'||a.AssetID||'-RB'"),new Field("Designer","Đơn vị thiết kế",sql:"'Trung tâm Thiết kế kết cấu đường sắt Yên Hạ'"),new Field("DesignDate","Ngày thiết kế","Date",sql:"date(l.InstallDate,'-365 days')"),new Field("Standards","Tiêu chuẩn áp dụng",sql:"'Chỉ dẫn thiết kế nội bộ YTHL-CDKT-01 Rev.B; bản vẽ kết cấu đường trên TK-021'"),new Field("Material","Vật liệu thiết kế",sql:"CASE a.ComponentType WHEN 'Rail' THEN 'Thép ray R260, tiết diện thiết kế 60E1' WHEN 'Slab' THEN 'Bê tông C50, cốt thép CB400-V' WHEN 'Sleeper' THEN 'Bê tông dự ứng lực C60' WHEN 'Pad' THEN 'Cao su EPDM đàn hồi' WHEN 'GuidePlate' THEN 'Polyamide PA66 gia cường sợi thủy tinh' WHEN 'Bolt' THEN 'Thép cấp bền 8.8, mạ kẽm' WHEN 'Fastener' THEN 'Thép lò xo 60Si2Mn' WHEN 'Anchor' THEN 'Thép cấp bền 8.8, chống ăn mòn' WHEN 'Washer' THEN 'Thép tôi, mạ kẽm' WHEN 'BasePlate' THEN 'Thép kết cấu Q355, sơn bảo vệ' WHEN 'Support' THEN 'Thép kết cấu Q355' WHEN 'SteelDetail' THEN 'Thép kết cấu Q355' ELSE 'Tổ hợp ray thép, bê tông, liên kết đàn hồi' END"),new Field("DesignLifeYears","Tuổi thọ thiết kế","Number","năm","l.DesignLifeYears"),new Field("ApprovalDate","Ngày phê duyệt","Date",sql:"date(l.InstallDate,'-300 days')"),new Field("TechnicalNotes","Ghi chú kỹ thuật",sql:"'Kiểm soát dung sai lắp đặt '||CASE a.ComponentType WHEN 'Rail' THEN 'ray thép' WHEN 'Slab' THEN 'bản bê tông đường ray' WHEN 'Sleeper' THEN 'tà vẹt bê tông' WHEN 'Bolt' THEN 'bu lông liên kết' WHEN 'Fastener' THEN 'kẹp đàn hồi' WHEN 'Pad' THEN 'đệm ray' WHEN 'GuidePlate' THEN 'bản dẫn hướng' WHEN 'Anchor' THEN 'neo liên kết' WHEN 'BasePlate' THEN 'bản đế ray' WHEN 'Washer' THEN 'vòng đệm' WHEN 'Support' THEN 'gối đỡ' WHEN 'SteelDetail' THEN 'chi tiết thép' ELSE 'kết cấu đường ray' END||'; đối chiếu bản vẽ hoàn công và danh mục liên kết trước thay thế.'")),
        new Stage("Construction","Thi công","ThiCong",
            new Field("Contractor","Đơn vị thi công",sql:"'Đội Thi công đường trên Yên Hạ số 2'"),new Field("StartDate","Ngày bắt đầu","Date",sql:"date(l.InstallDate,'-90 days')"),new Field("CompletionDate","Ngày hoàn thành","Date",sql:"l.InstallDate"),new Field("InstallDate","Ngày lắp đặt","Date",sql:"l.InstallDate"),new Field("AcceptanceBatch","Đợt nghiệm thu",sql:"'YTHL-NT-2022-'||coalesce(a.BlockID,a.SegmentID,a.AssetID)||'-' ||a.AssetID"),new Field("AcceptanceResult","Kết quả nghiệm thu",sql:"'Đạt; kích thước lắp đặt và liên kết phù hợp hồ sơ nghiệm thu'"),new Field("ConstructionCost","Chi phí thi công","Number","VND","CASE a.AssetType WHEN 'Component' THEN CASE a.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END WHEN 'Block' THEN (SELECT coalesce(sum(CASE c.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END),0) FROM Assets c WHERE c.ParentAssetID=a.AssetID) ELSE (SELECT coalesce(sum(CASE c.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END),0) FROM Assets b JOIN Assets c ON c.ParentAssetID=b.AssetID WHERE b.ParentAssetID=a.AssetID) END"),new Field("Notes","Ghi chú",sql:"'Biên bản nghiệm thu NT-'||a.AssetID||'; vật tư có phiếu xuất kho và hồ sơ lắp đặt theo lô.'")),
        new Stage("Operation","Vận hành","VanHanh",
            new Field("CommissioningDate","Ngày đưa vào sử dụng","Date",sql:"l.CommissioningDate"),new Field("OperationalStatus","Trạng thái vận hành",sql:"replace(l.OperationalStatus,' — DEMO','')"),new Field("ConditionGrade","Tình trạng tài sản",sql:"l.ConditionGrade"),new Field("LastInspectionDate","Lần kiểm tra gần nhất","Date",sql:"l.LastInspectionDate"),new Field("NextInspectionDate","Ngày kiểm tra tiếp theo","Date",sql:"l.NextInspectionDate"),new Field("Operator","Đơn vị quản lý",sql:"'Đội Quản lý đường Yên Hạ – Tổ tuyến 021'"),new Field("Notes","Ghi chú",sql:"'Hồ sơ '||a.AssetID||'; theo dõi hiện trạng tại '||a.ZoneID||', đối chiếu ảnh kiểm tra và biên bản gần nhất.'")),
        new Stage("Maintenance","Bảo trì","BaoTri",
            new Field("MaintenancePlan","Kế hoạch bảo trì",sql:"CASE WHEN a.ComponentType='Rail' THEN 'Kiểm tra bề mặt lăn, mối nối và khuyết tật ray' WHEN a.ComponentType IN ('Slab','Sleeper') THEN 'Kiểm tra nứt, bong bật và thoát nước mặt bê tông' WHEN a.ComponentType='Pad' THEN 'Kiểm tra biến dạng, lão hóa và độ kín khít đệm ray' ELSE 'Kiểm tra lực siết, độ rơ và tình trạng chống ăn mòn liên kết' END||'; chu kỳ 6 tháng, kiểm tra bổ sung khi phát hiện bất thường.'"),new Field("LastMaintenanceDate","Ngày bảo trì gần nhất","Date",readOnly:true),new Field("NextMaintenanceDate","Ngày bảo trì tiếp theo","Date",sql:"CASE WHEN date(l.CommissioningDate,'+5 years')>date('now','localtime') THEN date(l.CommissioningDate,'+5 years') ELSE date('now','localtime','+180 days') END"),new Field("WorkType","Loại công việc",sql:"CASE WHEN a.ComponentType='Rail' THEN 'Kiểm tra bề mặt lăn, mối nối và khuyết tật ray' WHEN a.ComponentType IN ('Slab','Sleeper') THEN 'Kiểm tra nứt, bong bật và thoát nước mặt bê tông' WHEN a.ComponentType='Pad' THEN 'Kiểm tra biến dạng, lão hóa và độ kín khít đệm ray' ELSE 'Kiểm tra lực siết, độ rơ và tình trạng chống ăn mòn liên kết' END"),new Field("MaintenanceStatus","Trạng thái bảo trì",sql:"replace(l.MaintenanceStatus,' — DEMO','')"),new Field("TotalRecordedCost","Tổng chi phí đã ghi nhận","Number","VND",readOnly:true),new Field("Notes","Ghi chú",sql:"'Phối hợp phong tỏa khu đoạn khi bảo trì '||CASE a.ComponentType WHEN 'Rail' THEN 'ray thép' WHEN 'Slab' THEN 'bản bê tông đường ray' WHEN 'Sleeper' THEN 'tà vẹt bê tông' WHEN 'Bolt' THEN 'bu lông liên kết' WHEN 'Fastener' THEN 'kẹp đàn hồi' WHEN 'Pad' THEN 'đệm ray' WHEN 'GuidePlate' THEN 'bản dẫn hướng' WHEN 'Anchor' THEN 'neo liên kết' WHEN 'BasePlate' THEN 'bản đế ray' WHEN 'Washer' THEN 'vòng đệm' WHEN 'Support' THEN 'gối đỡ' WHEN 'SteelDetail' THEN 'chi tiết thép' ELSE 'kết cấu đường ray' END||'; cập nhật ảnh trước/sau và phiếu nghiệm thu công việc.'")),
        new Stage("Upgrade","Nâng cấp","NangCap",
            new Field("UpgradePlan","Kế hoạch nâng cấp",sql:"CASE WHEN coalesce(l.ReplacementPlanDate,'')='' THEN 'Chưa bố trí trong kế hoạch trung hạn' ELSE 'Thay thế '||CASE a.ComponentType WHEN 'Rail' THEN 'ray thép' WHEN 'Slab' THEN 'bản bê tông đường ray' WHEN 'Sleeper' THEN 'tà vẹt bê tông' WHEN 'Bolt' THEN 'bu lông liên kết' WHEN 'Fastener' THEN 'kẹp đàn hồi' WHEN 'Pad' THEN 'đệm ray' WHEN 'GuidePlate' THEN 'bản dẫn hướng' WHEN 'Anchor' THEN 'neo liên kết' WHEN 'BasePlate' THEN 'bản đế ray' WHEN 'Washer' THEN 'vòng đệm' WHEN 'Support' THEN 'gối đỡ' WHEN 'SteelDetail' THEN 'chi tiết thép' ELSE 'kết cấu đường ray' END||' theo đánh giá tình trạng' END"),new Field("Reason","Lý do nâng cấp",sql:"CASE WHEN coalesce(l.ReplacementPlanDate,'')='' THEN 'Tiếp tục khai thác; đánh giá lại sau kỳ kiểm tra tiếp theo' ELSE 'Tình trạng suy giảm; ưu tiên thay thế theo kế hoạch quản lý tài sản' END"),new Field("Changes","Hạng mục thay đổi",sql:"CASE WHEN coalesce(l.ReplacementPlanDate,'')='' THEN '' ELSE 'Thay thế '||CASE a.ComponentType WHEN 'Rail' THEN 'ray thép' WHEN 'Slab' THEN 'bản bê tông đường ray' WHEN 'Sleeper' THEN 'tà vẹt bê tông' WHEN 'Bolt' THEN 'bu lông liên kết' WHEN 'Fastener' THEN 'kẹp đàn hồi' WHEN 'Pad' THEN 'đệm ray' WHEN 'GuidePlate' THEN 'bản dẫn hướng' WHEN 'Anchor' THEN 'neo liên kết' WHEN 'BasePlate' THEN 'bản đế ray' WHEN 'Washer' THEN 'vòng đệm' WHEN 'Support' THEN 'gối đỡ' WHEN 'SteelDetail' THEN 'chi tiết thép' ELSE 'kết cấu đường ray' END||'; nghiệm thu kích thước và liên kết sau lắp đặt' END"),new Field("PlannedDate","Ngày dự kiến","Date",sql:"l.ReplacementPlanDate"),new Field("ActualDate","Ngày thực hiện","Date"),new Field("Executor","Đơn vị thực hiện",sql:"CASE WHEN coalesce(l.ReplacementPlanDate,'')='' THEN '' ELSE 'Đội Đại tu kết cấu đường trên Yên Hạ' END"),new Field("UpgradeCost","Chi phí","Number","VND","CASE WHEN coalesce(l.ReplacementPlanDate,'')='' THEN 0 ELSE round((CASE a.AssetType WHEN 'Component' THEN CASE a.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END WHEN 'Block' THEN (SELECT coalesce(sum(CASE c.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END),0) FROM Assets c WHERE c.ParentAssetID=a.AssetID) ELSE (SELECT coalesce(sum(CASE c.ComponentType WHEN 'Rail' THEN 3600000 WHEN 'Slab' THEN 12500000 WHEN 'Sleeper' THEN 950000 WHEN 'Fastener' THEN 185000 WHEN 'Bolt' THEN 45000 WHEN 'Anchor' THEN 85000 WHEN 'BasePlate' THEN 320000 WHEN 'GuidePlate' THEN 65000 WHEN 'Pad' THEN 95000 WHEN 'Washer' THEN 12000 WHEN 'Support' THEN 210000 ELSE 75000 END),0) FROM Assets b JOIN Assets c ON c.ParentAssetID=b.AssetID WHERE b.ParentAssetID=a.AssetID) END)*1.2,0) END"),new Field("ReplacementAssetID","Mã tài sản thay thế"),new Field("Notes","Ghi chú",sql:"'Theo dõi đề xuất nâng cấp '||a.AssetID||'; chỉ ghi ngày thực hiện sau nghiệm thu và bàn giao khai thác.'"))
    };
    public static Stage Get(string key)=>All.SingleOrDefault(s=>s.key==key)??throw new ArgumentException("Giai đoạn không hợp lệ: "+key);
    public static Stage FromSheet(string sheet)=>All.FirstOrDefault(s=>s.sheet==sheet);
    public static bool IsSheet(string sheet)=>FromSheet(sheet)!=null;
}

public sealed partial class RailwayBimStore
{
    void MigrateStages()
    {
        Db.RunInTransaction(()=>
        {
            if(Db.ExecuteScalar<int>("SELECT count(*) FROM SchemaMigrations WHERE Version=2")==0)
            {
                Db.Execute("CREATE TABLE AssetLifecycleStages(AssetID TEXT NOT NULL REFERENCES Assets(AssetID),StageKey TEXT NOT NULL CHECK(StageKey IN ('Planning','Design','Construction','Operation','Maintenance','Upgrade')),SchemaVersion INTEGER NOT NULL CHECK(SchemaVersion=1),Status TEXT NOT NULL,UpdatedAt TEXT NOT NULL,PRIMARY KEY(AssetID,StageKey))");
                Db.Execute("CREATE TABLE AssetStageFields(AssetID TEXT NOT NULL,StageKey TEXT NOT NULL,FieldKey TEXT NOT NULL,Value TEXT,DataType TEXT NOT NULL CHECK(DataType IN ('Text','Number','Date')),Unit TEXT,PRIMARY KEY(AssetID,StageKey,FieldKey),FOREIGN KEY(AssetID,StageKey) REFERENCES AssetLifecycleStages(AssetID,StageKey))");
                Db.Execute("ALTER TABLE AssetLifecycleEvents ADD COLUMN StageKey TEXT");
                Db.Execute("CREATE INDEX IX_Events_AssetStage ON AssetLifecycleEvents(AssetID,StageKey)");
                Db.Execute("INSERT INTO SchemaMigrations VALUES(2,?)",DateTime.UtcNow.ToString("O"));
            }
            if(Db.ExecuteScalar<int>("SELECT count(*) FROM SeedVersions WHERE Version='six-stage-demo-1'")>0&&Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleStages")==Assets.Count*6)return;
            foreach(var stage in RailwayBimStages.All)
            {
                string status=stage.key=="Upgrade"?"Chưa có kế hoạch":"Hồ sơ minh họa";
                Db.Execute("INSERT OR IGNORE INTO AssetLifecycleStages SELECT AssetID,?,1,?,? FROM Assets",stage.key,status,DateTime.UtcNow.ToString("O"));
                foreach(var field in stage.fields.Where(f=>!f.readOnly))
                    Db.Execute("INSERT OR IGNORE INTO AssetStageFields SELECT a.AssetID,?,?,coalesce("+field.seedSql+",''),?,? FROM Assets a JOIN AssetLifecycle l ON l.AssetID=a.AssetID",stage.key,field.key,field.type,field.unit);
            }
            Db.Execute("INSERT OR IGNORE INTO SeedVersions VALUES('six-stage-demo-1',?)",DateTime.UtcNow.ToString("O"));
        });
        Db.RunInTransaction(()=>
        {
            if(Db.ExecuteScalar<int>("SELECT count(*) FROM SeedVersions WHERE Version='six-stage-demo-status-2'")>0)return;
            Db.Execute("UPDATE AssetLifecycleStages SET Status='Dự kiến — MINH HỌA' WHERE StageKey='Upgrade' AND Status='Chưa có kế hoạch' AND AssetID IN (SELECT AssetID FROM AssetLifecycle WHERE coalesce(ReplacementPlanDate,'')<>'') AND NOT EXISTS(SELECT 1 FROM AssetLifecycleEvents e WHERE e.AssetID=AssetLifecycleStages.AssetID AND e.StageKey='Upgrade')");
            Db.Execute("INSERT INTO SeedVersions VALUES('six-stage-demo-status-2',?)",DateTime.UtcNow.ToString("O"));
        });
        Db.RunInTransaction(()=>
        {
            if(Db.ExecuteScalar<int>("SELECT count(*) FROM SeedVersions WHERE Version='six-stage-demo-material-2'")>0)return;
            Db.Execute("UPDATE AssetStageFields SET Value=CASE WHEN AssetID IN (SELECT AssetID FROM Assets WHERE AssetType<>'Component') THEN 'Nhiều vật liệu — xem hồ sơ cấu kiện' ELSE 'Chưa xác nhận — minh họa cấu kiện đệm' END WHERE StageKey='Design' AND FieldKey='Material' AND Value='Thép — giả định minh họa' AND AssetID IN (SELECT AssetID FROM Assets WHERE AssetType<>'Component' OR ComponentType='Pad') AND NOT EXISTS(SELECT 1 FROM AssetLifecycleEvents e WHERE e.AssetID=AssetStageFields.AssetID AND e.StageKey='Design')");
            Db.Execute("INSERT INTO SeedVersions VALUES('six-stage-demo-material-2',?)",DateTime.UtcNow.ToString("O"));
        });
        MigrateBimScenario();
    }
    public BimStage Stage(string id,string key){Require(id);RailwayBimStages.Get(key);return Db.Query<BimStage>("SELECT * FROM AssetLifecycleStages WHERE AssetID=? AND StageKey=?",id,key).Single();}
    public Dictionary<string,string> StageFields(string id,string key)
    {
        Require(id);var definition=RailwayBimStages.Get(key);var result=Db.Query<BimStageField>("SELECT * FROM AssetStageFields WHERE AssetID=? AND StageKey=?",id,key).ToDictionary(f=>f.FieldKey,f=>f.Value??"");
        // Existing maintenance/inspection records stay authoritative; no duplicate event tables.
        var life=Lifecycle(id);var sync=Synced(key);foreach(var pair in sync)result[pair.Key]=Convert.ToString(typeof(BimLifecycle).GetProperty(pair.Value).GetValue(life),CultureInfo.InvariantCulture)??"";
        if(key=="Maintenance"){result["LastMaintenanceDate"]=Db.ExecuteScalar<string>("SELECT MAX(MaintenanceDate) FROM AssetMaintenance WHERE AssetID=?",id)??"";result["TotalRecordedCost"]=Db.ExecuteScalar<double>("SELECT coalesce(sum(Cost),0) FROM AssetMaintenance WHERE AssetID=?",id).ToString(CultureInfo.InvariantCulture);}
        foreach(var field in definition.fields)if(!result.ContainsKey(field.key))result[field.key]="";return result;
    }
    internal static Dictionary<string,string> Synced(string key)
    {
        var result=new Dictionary<string,string>();string[] names=key=="Design"?new[]{"DesignLifeYears"}:key=="Construction"?new[]{"InstallDate"}:key=="Operation"?new[]{"CommissioningDate","OperationalStatus","ConditionGrade","LastInspectionDate","NextInspectionDate","Notes"}:key=="Maintenance"?new[]{"MaintenanceStatus"}:Array.Empty<string>();foreach(var n in names)result[n]=n;if(key=="Upgrade")result["PlannedDate"]="ReplacementPlanDate";return result;
    }
    public void ValidateStage(string id,string key,IReadOnlyDictionary<string,string> fields,string status=null)
    {
        Require(id);var definition=RailwayBimStages.Get(key);
        foreach(var pair in fields)
        {
            var field=definition.fields.SingleOrDefault(f=>f.key==pair.Key)??throw new ArgumentException("Thông số không thuộc giai đoạn: "+pair.Key);
            if(field.readOnly)continue;if(!string.IsNullOrEmpty(pair.Value)){ValueValid(pair.Value,field.type);if(field.type=="Number"&&double.Parse(pair.Value,CultureInfo.InvariantCulture)<0)throw new ArgumentException(field.label+" không được âm");}
        }
        var merged=StageFields(id,key);foreach(var p in fields)if(!definition.fields.Single(f=>f.key==p.Key).readOnly)merged[p.Key]=p.Value??"";
        DateTime? Date(string f)=>merged.TryGetValue(f,out var value)&&!string.IsNullOrEmpty(value)?DateValid(value):(DateTime?)null;
        void Before(string a,string b){var left=Date(a);var right=Date(b);if(left.HasValue&&right.HasValue&&left>right)throw new ArgumentException("Thứ tự ngày không hợp lệ: "+a+" → "+b);}
        if(key=="Design"){Before("DesignDate","ApprovalDate");var design=Date("DesignDate");var start=StageFields(id,"Construction")["StartDate"];if(design.HasValue&&!string.IsNullOrEmpty(start)&&design> DateValid(start))throw new ArgumentException("Ngày thiết kế phải trước thi công");}
        if(key=="Construction"){Before("StartDate","CompletionDate");Before("StartDate","InstallDate");var end=Date("CompletionDate");var commissioning=Lifecycle(id).CommissioningDate;if(end.HasValue&&!string.IsNullOrEmpty(commissioning)&&end>DateValid(commissioning))throw new ArgumentException("Thi công phải hoàn thành trước vận hành");}
        if(key=="Operation"){var commissioning=Date("CommissioningDate");var completed=StageFields(id,"Construction")["CompletionDate"];if(commissioning.HasValue&&!string.IsNullOrEmpty(completed)&&commissioning<DateValid(completed))throw new ArgumentException("Ngày vận hành phải sau khi hoàn thành thi công");}
        if(key=="Planning"){var plan=Date("PlanDate");var design=StageFields(id,"Design")["DesignDate"];if(plan.HasValue&&!string.IsNullOrEmpty(design)&&plan>DateValid(design))throw new ArgumentException("Ngày quy hoạch phải trước thiết kế");}
        if(key=="Upgrade")
        {var actual=Date("ActualDate");if(actual>DateTime.Today)throw new ArgumentException("Ngày nâng cấp đã thực hiện không được ở tương lai");string replacement=merged["ReplacementAssetID"];if(!string.IsNullOrEmpty(replacement)){Require(replacement);if(replacement==id)throw new ArgumentException("Tài sản không thể tự thay thế chính nó");}}
        var life=Lifecycle(id);foreach(var pair in Synced(key))if(merged.TryGetValue(pair.Key,out var value)){var property=typeof(BimLifecycle).GetProperty(pair.Value);property.SetValue(life,property.PropertyType==typeof(double)?(object)double.Parse(value,CultureInfo.InvariantCulture):value);}Validate(life);
        if(status!=null&&string.IsNullOrWhiteSpace(status))throw new ArgumentException("Trạng thái hồ sơ không được rỗng");
    }
    public void SaveStage(string id,string key,IReadOnlyDictionary<string,string> fields,string status=null)
    {
        ValidateStage(id,key,fields,status);var definition=RailwayBimStages.Get(key);var before=StageFields(id,key);var original=Stage(id,key);
        Db.RunInTransaction(()=>
        {
            bool changed=false;foreach(var pair in fields)
            {
                var field=definition.fields.Single(f=>f.key==pair.Key);if(field.readOnly||before[field.key]==(pair.Value??""))continue;changed=true;
                Db.Execute("INSERT INTO AssetStageFields VALUES(?,?,?,?,?,?) ON CONFLICT(AssetID,StageKey,FieldKey) DO UPDATE SET Value=excluded.Value",id,key,field.key,pair.Value??"",field.type,field.unit);Event(id,"Stage",field.label,before[field.key],pair.Value,key);
            }
            if(status!=null&&status!=original.Status){changed=true;Event(id,"Stage","Trạng thái hồ sơ",original.Status,status,key);}
            if(!changed)return;
            var life=Lifecycle(id);foreach(var p in Synced(key))if(fields.TryGetValue(p.Key,out var value)){var property=typeof(BimLifecycle).GetProperty(p.Value);property.SetValue(life,property.PropertyType==typeof(double)?(object)double.Parse(value,CultureInfo.InvariantCulture):value);}SaveLifecycle(life,false);
            Db.Execute("UPDATE AssetLifecycleStages SET Status=?,UpdatedAt=? WHERE AssetID=? AND StageKey=?",status??original.Status,DateTime.UtcNow.ToString("O"),id,key);
        });
    }
    public List<BimHistory> StageHistory(string id,string stage,int offset=0,int limit=200)=>Db.Query<BimHistory>("SELECT EventID,AssetID,EventType,EventDate,Description,PreviousValue,NewValue FROM AssetLifecycleEvents WHERE AssetID=? AND (StageKey=? OR StageKey IS NULL) ORDER BY EventDate DESC,EventID DESC LIMIT ? OFFSET ?",id,stage,limit,offset);
    public int StageHistoryCount(string id,string stage)=>Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleEvents WHERE AssetID=? AND (StageKey=? OR StageKey IS NULL)",id,stage);
}
