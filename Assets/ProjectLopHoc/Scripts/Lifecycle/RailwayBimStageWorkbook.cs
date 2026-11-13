using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

public static partial class RailwayBimWorkbook
{
    static Dictionary<string,string> StagePatch(RailwayBimStore store,Sheet sheet,Dictionary<string,string> row)
    {
        var stage=RailwayBimStages.FromSheet(sheet.name);var values=new Dictionary<string,string>();
        foreach(var header in sheet.headers.Where(h=>h!="AssetID"&&h!="StageKey"&&h!="SchemaVersion"&&h!="Status"))
        {
            Header(header,out var key,out var type,out var unit);var field=stage.fields.SingleOrDefault(f=>f.key==key)??throw new ArgumentException("Trường không thuộc giai đoạn: "+key);
            if(type!=field.type||unit!=field.unit)throw new ArgumentException("Sai kiểu hoặc đơn vị của "+field.label);
            string value=Get(row,header);if(string.IsNullOrEmpty(value))continue;
            if(field.readOnly)
            {
                // Exported aggregates may be older than the current event history. They are reference only.
                continue;
            }
            values.Add(key,value);
        }
        return values;
    }
    static void ValidateStageRow(RailwayBimStore store,Sheet sheet,Dictionary<string,string> row,HashSet<string> pairs)
    {
        var stage=RailwayBimStages.FromSheet(sheet.name);string id=Get(row,"AssetID");
        if(Get(row,"StageKey")!=stage.key)throw new ArgumentException("StageKey không khớp sheet "+sheet.name+": cần "+stage.key);
        if(Get(row,"SchemaVersion")!=RailwayBimStages.Version.ToString(CI))throw new ArgumentException("SchemaVersion không được hỗ trợ");
        if(!pairs.Add(id+"|"+stage.key))throw new ArgumentException("Trùng AssetID + StageKey");
        store.ValidateStage(id,stage.key,StagePatch(store,sheet,row),string.IsNullOrEmpty(Get(row,"Status"))?null:Get(row,"Status"));
    }
    static void ApplyStageRow(RailwayBimStore store,Sheet sheet,Dictionary<string,string> row)
    {var stage=RailwayBimStages.FromSheet(sheet.name);store.SaveStage(Get(row,"AssetID"),stage.key,StagePatch(store,sheet,row),string.IsNullOrEmpty(Get(row,"Status"))?null:Get(row,"Status"));}
    static IEnumerable<Sheet> ExportStages(RailwayBimStore store,BimAsset[] assets)
    {
        var life=store.Db.Query<BimLifecycle>("SELECT * FROM AssetLifecycle").ToDictionary(l=>l.AssetID);
        var maintenance=store.Db.Query<BimMaintenance>("SELECT * FROM AssetMaintenance").ToLookup(m=>m.AssetID);
        foreach(var stage in RailwayBimStages.All)
        {
            var records=store.Db.Query<BimStage>("SELECT * FROM AssetLifecycleStages WHERE StageKey=?",stage.key).ToDictionary(s=>s.AssetID);
            var fields=store.Db.Query<BimStageField>("SELECT * FROM AssetStageFields WHERE StageKey=?",stage.key).ToLookup(f=>f.AssetID);
            var sheet=new Sheet{name=stage.sheet,headers=new List<string>{"AssetID","StageKey","SchemaVersion","Status"}};sheet.headers.AddRange(stage.fields.Select(f=>f.key+"|"+f.type+"|"+f.unit));
            foreach(var asset in assets)
            {
                var record=records[asset.AssetID];var row=new Dictionary<string,string>{{"AssetID",asset.AssetID},{"StageKey",stage.key},{"SchemaVersion",record.SchemaVersion.ToString(CI)},{"Status",record.Status}};
                var values=fields[asset.AssetID].ToDictionary(f=>f.FieldKey,f=>f.Value??"");
                foreach(var pair in RailwayBimStore.Synced(stage.key))values[pair.Key]=Convert.ToString(typeof(BimLifecycle).GetProperty(pair.Value).GetValue(life[asset.AssetID]),CI)??"";
                if(stage.key=="Maintenance"){values["LastMaintenanceDate"]=maintenance[asset.AssetID].Select(m=>m.MaintenanceDate).OrderByDescending(d=>d).FirstOrDefault()??"";values["TotalRecordedCost"]=maintenance[asset.AssetID].Sum(m=>m.Cost).ToString(CI);}
                foreach(var f in stage.fields)row[f.key+"|"+f.type+"|"+f.unit]=values.TryGetValue(f.key,out var value)?value:"";sheet.rows.Add(row);
            }
            yield return sheet;
        }
    }
}
