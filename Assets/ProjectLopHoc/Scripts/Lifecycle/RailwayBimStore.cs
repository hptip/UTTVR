using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using SQLite;

public sealed class BimAsset
{
    public string AssetID{get;set;} public string ParentAssetID{get;set;} public string AssetType{get;set;}
    public string DisplayName{get;set;} public string ZoneID{get;set;} public string TrackID{get;set;}
    public string SegmentID{get;set;} public string BlockID{get;set;} public string ComponentType{get;set;}
    public double ChainageStart{get;set;} public double ChainageEnd{get;set;} public string ModelReference{get;set;}
}
public sealed class BimProperty {public string AssetID{get;set;}public string Name{get;set;}public string Value{get;set;}public string Unit{get;set;}public string DataType{get;set;}}
public sealed class BimLifecycle
{
    public string AssetID{get;set;}public string InstallDate{get;set;}public string CommissioningDate{get;set;}
    public double DesignLifeYears{get;set;}public string ConditionGrade{get;set;}public string OperationalStatus{get;set;}
    public string LastInspectionDate{get;set;}public string NextInspectionDate{get;set;}public string MaintenanceStatus{get;set;}
    public string ReplacementPlanDate{get;set;}public string Notes{get;set;}
}
public sealed class BimInspection {public string InspectionID{get;set;}public string AssetID{get;set;}public string InspectionDate{get;set;}public string ConditionGrade{get;set;}public string Finding{get;set;}public string Inspector{get;set;}public string Notes{get;set;}}
public sealed class BimMaintenance {public string MaintenanceID{get;set;}public string AssetID{get;set;}public string MaintenanceDate{get;set;}public string MaintenanceType{get;set;}public string Description{get;set;}public double Cost{get;set;}public string Result{get;set;}}
public sealed class BimHistory {public string EventID{get;set;}public string AssetID{get;set;}public string EventType{get;set;}public string EventDate{get;set;}public string Description{get;set;}public string PreviousValue{get;set;}public string NewValue{get;set;}}

/// <summary>Separate database namespace; instance writes always use AssetID. No calls to the legacy database.</summary>
public sealed partial class RailwayBimStore:IDisposable
{
    public SQLiteConnection Db{get;private set;}
    public string Path{get;private set;}
    public Dictionary<string,BimAsset> Assets{get;private set;}
    Dictionary<string,string> conditionCache=new Dictionary<string,string>();
    public RailwayBimStore(string path,string seedPath=null)
    {
        Path=path;Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        if(!File.Exists(path)&&!string.IsNullOrEmpty(seedPath)&&File.Exists(seedPath))File.Copy(seedPath,path);
        Db=new SQLiteConnection(path);Db.Execute("PRAGMA foreign_keys=ON");Db.ExecuteScalar<int>("PRAGMA busy_timeout=5000");Migrate();ReloadRegistry();MigrateStages();
    }
    void Migrate()
    {
        Db.RunInTransaction(()=>
        {
            Db.Execute("CREATE TABLE IF NOT EXISTS SchemaMigrations(Version INTEGER PRIMARY KEY,AppliedAt TEXT NOT NULL)");
            if(Db.ExecuteScalar<int>("SELECT count(*) FROM SchemaMigrations WHERE Version=1")>0)return;
            Db.Execute("CREATE TABLE IF NOT EXISTS Assets(AssetID TEXT PRIMARY KEY,ParentAssetID TEXT REFERENCES Assets(AssetID),AssetType TEXT NOT NULL CHECK(AssetType IN ('Segment','Block','Component')),DisplayName TEXT NOT NULL,ZoneID TEXT NOT NULL,TrackID TEXT NOT NULL,SegmentID TEXT,BlockID TEXT,ComponentType TEXT,ChainageStart REAL,ChainageEnd REAL,ModelReference TEXT)");
            Db.Execute("CREATE INDEX IF NOT EXISTS IX_Assets_Parent ON Assets(ParentAssetID)");
            Db.Execute("CREATE TABLE IF NOT EXISTS AssetProperties(AssetID TEXT NOT NULL REFERENCES Assets(AssetID),Name TEXT NOT NULL,Value TEXT,Unit TEXT,DataType TEXT NOT NULL CHECK(DataType IN ('Text','Number','Date')),PRIMARY KEY(AssetID,Name))");
            Db.Execute("CREATE TABLE IF NOT EXISTS AssetLifecycle(AssetID TEXT PRIMARY KEY REFERENCES Assets(AssetID),InstallDate TEXT,CommissioningDate TEXT,DesignLifeYears REAL CHECK(DesignLifeYears>=0),ConditionGrade TEXT,OperationalStatus TEXT,LastInspectionDate TEXT,NextInspectionDate TEXT,MaintenanceStatus TEXT,ReplacementPlanDate TEXT,Notes TEXT)");
            Db.Execute("CREATE TABLE IF NOT EXISTS AssetInspections(InspectionID TEXT PRIMARY KEY,AssetID TEXT NOT NULL REFERENCES Assets(AssetID),InspectionDate TEXT NOT NULL,ConditionGrade TEXT,Finding TEXT,Inspector TEXT,Notes TEXT)");
            Db.Execute("CREATE INDEX IF NOT EXISTS IX_Inspections_Asset ON AssetInspections(AssetID)");
            Db.Execute("CREATE TABLE IF NOT EXISTS AssetMaintenance(MaintenanceID TEXT PRIMARY KEY,AssetID TEXT NOT NULL REFERENCES Assets(AssetID),MaintenanceDate TEXT NOT NULL,MaintenanceType TEXT,Description TEXT,Cost REAL CHECK(Cost>=0),Result TEXT)");
            Db.Execute("CREATE INDEX IF NOT EXISTS IX_Maintenance_Asset ON AssetMaintenance(AssetID)");
            Db.Execute("CREATE TABLE IF NOT EXISTS AssetLifecycleEvents(EventID TEXT PRIMARY KEY,AssetID TEXT NOT NULL REFERENCES Assets(AssetID),EventType TEXT NOT NULL,EventDate TEXT NOT NULL,Description TEXT,PreviousValue TEXT,NewValue TEXT)");
            Db.Execute("CREATE INDEX IF NOT EXISTS IX_Events_Asset ON AssetLifecycleEvents(AssetID)");
            Db.Execute("CREATE TABLE IF NOT EXISTS SeedVersions(Version TEXT PRIMARY KEY,AppliedAt TEXT)");
            Db.Execute("INSERT INTO SchemaMigrations VALUES(1,?)",DateTime.UtcNow.ToString("O"));
        });
    }
    public void ReloadRegistry(){Assets=Db.Query<BimAsset>("SELECT * FROM Assets").ToDictionary(a=>a.AssetID,StringComparer.Ordinal);conditionCache=Db.Query<BimLifecycle>("SELECT AssetID,ConditionGrade FROM AssetLifecycle").ToDictionary(a=>a.AssetID,a=>a.ConditionGrade,StringComparer.Ordinal);}
    public string Condition(string id)=>conditionCache.TryGetValue(id,out var grade)?grade:"Chưa có hồ sơ";
    public void Require(string id){if(string.IsNullOrWhiteSpace(id)||!Assets.ContainsKey(id))throw new ArgumentException("AssetID không tồn tại trong mô hình: "+id);}
    public List<BimAsset> Children(string id)=>Assets.Values.Where(a=>a.ParentAssetID==id).OrderBy(a=>a.AssetID,StringComparer.Ordinal).ToList();
    public List<BimProperty> Properties(string id){Require(id);return Db.Query<BimProperty>("SELECT * FROM AssetProperties WHERE AssetID=? ORDER BY Name",id);}
    public BimLifecycle Lifecycle(string id){Require(id);return Db.Query<BimLifecycle>("SELECT * FROM AssetLifecycle WHERE AssetID=?",id).Single();}
    public List<BimInspection> Inspections(string id)=>Db.Query<BimInspection>("SELECT * FROM AssetInspections WHERE AssetID=? ORDER BY InspectionDate DESC,InspectionID",id);
    public List<BimMaintenance> Maintenance(string id)=>Db.Query<BimMaintenance>("SELECT * FROM AssetMaintenance WHERE AssetID=? ORDER BY MaintenanceDate DESC,MaintenanceID",id);
    public List<BimHistory> History(string id)=>Db.Query<BimHistory>("SELECT * FROM AssetLifecycleEvents WHERE AssetID=? ORDER BY EventDate DESC,EventID",id);
    public static void ValueValid(string value,string type)
    {
        if(type=="Number"&&(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)||double.IsNaN(n)||double.IsInfinity(n)))throw new ArgumentException("Giá trị số không hợp lệ: "+value);
        if(type=="Date"&&!string.IsNullOrEmpty(value))DateValid(value);
        if(type!="Text"&&type!="Number"&&type!="Date")throw new ArgumentException("Kiểu dữ liệu không hợp lệ: "+type);
    }
    public static DateTime DateValid(string value){if(!DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))throw new ArgumentException("Ngày phải theo yyyy-MM-dd: "+value);return date;}
    public static void Validate(BimLifecycle life)
    {
        DateTime install=DateValid(life.InstallDate);foreach(var field in new[]{life.CommissioningDate,life.LastInspectionDate,life.NextInspectionDate,life.ReplacementPlanDate})if(!string.IsNullOrEmpty(field)&&DateValid(field)<install)throw new ArgumentException("Ngày vòng đời phải sau ngày lắp đặt");
        if(!string.IsNullOrEmpty(life.LastInspectionDate)&&!string.IsNullOrEmpty(life.NextInspectionDate)&&DateValid(life.NextInspectionDate)<=DateValid(life.LastInspectionDate))throw new ArgumentException("Lịch kiểm tra tiếp theo phải sau lần gần nhất");
        if(life.DesignLifeYears<0||double.IsNaN(life.DesignLifeYears)||double.IsInfinity(life.DesignLifeYears))throw new ArgumentException("Tuổi thọ không hợp lệ");
        if(string.IsNullOrWhiteSpace(life.ConditionGrade)||string.IsNullOrWhiteSpace(life.OperationalStatus))throw new ArgumentException("Thiếu tình trạng hoặc trạng thái vận hành");
    }
    public void Property(string id,string name,string value,string unit="",string type="Text",bool log=true)
    {
        Require(id);if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Thiếu tên thông số");ValueValid(value,type);
        var old=Db.ExecuteScalar<string>("SELECT Value FROM AssetProperties WHERE AssetID=? AND Name=?",id,name);
        Db.Execute("INSERT INTO AssetProperties(AssetID,Name,Value,Unit,DataType) VALUES(?,?,?,?,?) ON CONFLICT(AssetID,Name) DO UPDATE SET Value=excluded.Value,Unit=excluded.Unit,DataType=excluded.DataType",id,name,value,unit,type);
        if(log)Event(id,"Technical",name,old,value);
    }
    public void SaveProperties(string id,IEnumerable<BimProperty> properties)
    {Require(id);var ps=properties.ToArray();foreach(var p in ps)ValueValid(p.Value,p.DataType);Db.RunInTransaction(()=>{foreach(var p in ps)Property(id,p.Name,p.Value,p.Unit,p.DataType);});}
    public void Rename(string id,string name){Require(id);if(string.IsNullOrWhiteSpace(name))throw new ArgumentException("Tên không được rỗng");Db.RunInTransaction(()=>{string old=Assets[id].DisplayName;Db.Execute("UPDATE Assets SET DisplayName=? WHERE AssetID=?",name,id);Event(id,"Name","DisplayName",old,name);});Assets[id].DisplayName=name;}
    public void SaveLifecycle(BimLifecycle life,bool log=true)
    {
        Require(life.AssetID);Validate(life);var old=log?Db.Query<BimLifecycle>("SELECT * FROM AssetLifecycle WHERE AssetID=?",life.AssetID).FirstOrDefault():null;
        void Mutate(){
        Db.Execute("INSERT INTO AssetLifecycle VALUES(?,?,?,?,?,?,?,?,?,?,?) ON CONFLICT(AssetID) DO UPDATE SET InstallDate=excluded.InstallDate,CommissioningDate=excluded.CommissioningDate,DesignLifeYears=excluded.DesignLifeYears,ConditionGrade=excluded.ConditionGrade,OperationalStatus=excluded.OperationalStatus,LastInspectionDate=excluded.LastInspectionDate,NextInspectionDate=excluded.NextInspectionDate,MaintenanceStatus=excluded.MaintenanceStatus,ReplacementPlanDate=excluded.ReplacementPlanDate,Notes=excluded.Notes",life.AssetID,life.InstallDate,life.CommissioningDate,life.DesignLifeYears,life.ConditionGrade,life.OperationalStatus,life.LastInspectionDate,life.NextInspectionDate,life.MaintenanceStatus,life.ReplacementPlanDate,life.Notes);
        if(log)foreach(var field in typeof(BimLifecycle).GetProperties().Where(p=>p.Name!="AssetID"))
        {string before=old!=null?Convert.ToString(field.GetValue(old),CultureInfo.InvariantCulture)??"":"";string after=Convert.ToString(field.GetValue(life),CultureInfo.InvariantCulture)??"";if(before!=after)Event(life.AssetID,"Lifecycle",field.Name,before,after);}
        }
        if(log)Db.RunInTransaction(Mutate);else Mutate();
        conditionCache[life.AssetID]=life.ConditionGrade;
    }
    public void AddInspection(BimInspection i)
    {
        Require(i.AssetID);if(DateValid(i.InspectionDate)<DateValid(Lifecycle(i.AssetID).InstallDate))throw new ArgumentException("Kiểm tra trước ngày lắp đặt");if(string.IsNullOrWhiteSpace(i.Finding)||string.IsNullOrWhiteSpace(i.ConditionGrade))throw new ArgumentException("Thiếu kết quả hoặc tình trạng kiểm tra");
        if(string.IsNullOrEmpty(i.InspectionID))i.InspectionID=Guid.NewGuid().ToString("N");
        Db.RunInTransaction(()=>{Db.Execute("INSERT INTO AssetInspections VALUES(?,?,?,?,?,?,?)",i.InspectionID,i.AssetID,i.InspectionDate,i.ConditionGrade,i.Finding,i.Inspector,i.Notes);Db.Execute("UPDATE AssetLifecycle SET LastInspectionDate=CASE WHEN LastInspectionDate IS NULL OR LastInspectionDate<? THEN ? ELSE LastInspectionDate END,NextInspectionDate=CASE WHEN NextInspectionDate<=? THEN NULL ELSE NextInspectionDate END,ConditionGrade=? WHERE AssetID=?",i.InspectionDate,i.InspectionDate,i.InspectionDate,i.ConditionGrade,i.AssetID);Event(i.AssetID,"Inspection",i.Finding,"",i.ConditionGrade,"Operation");});
        conditionCache[i.AssetID]=i.ConditionGrade;
    }
    public void AddMaintenance(BimMaintenance m)
    {
        Require(m.AssetID);if(DateValid(m.MaintenanceDate)<DateValid(Lifecycle(m.AssetID).InstallDate)||m.Cost<0||double.IsNaN(m.Cost)||double.IsInfinity(m.Cost))throw new ArgumentException("Ngày/chi phí bảo trì không hợp lệ");if(string.IsNullOrWhiteSpace(m.Description))throw new ArgumentException("Thiếu công việc bảo trì");if(string.IsNullOrEmpty(m.MaintenanceID))m.MaintenanceID=Guid.NewGuid().ToString("N");
        Db.RunInTransaction(()=>{Db.Execute("INSERT INTO AssetMaintenance VALUES(?,?,?,?,?,?,?)",m.MaintenanceID,m.AssetID,m.MaintenanceDate,m.MaintenanceType,m.Description,m.Cost,m.Result);Db.Execute("UPDATE AssetLifecycle SET MaintenanceStatus=? WHERE AssetID=?",m.Result,m.AssetID);Event(m.AssetID,"Maintenance",m.Description,"",m.Result,"Maintenance");});
    }
    public void Event(string id,string type,string description,string oldValue,string newValue,string stageKey=null)
    {string stamp=DateTime.UtcNow.ToString("O");Db.Execute("INSERT INTO AssetLifecycleEvents(EventID,AssetID,EventType,EventDate,Description,PreviousValue,NewValue,StageKey) VALUES(?,?,?,?,?,?,?,?)",Guid.NewGuid().ToString("N"),id,type,stamp,description,oldValue??"",newValue??"",stageKey);if(stageKey!=null)Db.Execute("UPDATE AssetLifecycleStages SET UpdatedAt=? WHERE AssetID=? AND StageKey=?",stamp,id,stageKey);}
    public void Dispose(){Db?.Close();Db?.Dispose();Db=null;}
}
