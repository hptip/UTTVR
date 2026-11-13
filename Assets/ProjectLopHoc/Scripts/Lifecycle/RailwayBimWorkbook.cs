using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Xml;
using ExcelDataReader;

/// <summary>Existing ExcelDataReader for import; standards-based XLSX writer for runtime export.</summary>
public static partial class RailwayBimWorkbook
{
    public sealed class Sheet{public string name;public List<string> headers=new List<string>();public List<Dictionary<string,string>> rows=new List<Dictionary<string,string>>();}
    public sealed class Preview
    {
        public List<Sheet> sheets;public List<string> errors=new List<string>();public int updates,additions,newFields,orphans;
        public bool CanApply=>errors.Count==0;
        public override string ToString()=>$"Cập nhật: {updates}; mới: {additions}; trường mới: {newFields}; ID không có mô hình: {orphans}; lỗi: {errors.Count}"+(errors.Count>0?"\n"+string.Join("\n",errors.Take(8)):"\nÔ trống giữ nguyên dữ liệu; Apply dùng đúng nội dung đã preview.");
    }
    static readonly CultureInfo CI=CultureInfo.InvariantCulture;
    public static List<Sheet> Read(string path)
    {
        var result=new List<Sheet>();using(var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var reader=ExcelReaderFactory.CreateReader(stream))
        do
        {
            if(!new[]{"Assets","Technical","Lifecycle","Inspections","Maintenance"}.Contains(reader.Name)&&!RailwayBimStages.IsSheet(reader.Name))continue;
            var s=new Sheet{name=reader.Name};if(!reader.Read())continue;for(int i=0;i<reader.FieldCount;i++)s.headers.Add(Convert.ToString(reader.GetValue(i),CI)?.Trim()??"");
            if(s.headers.Any(string.IsNullOrWhiteSpace)||s.headers.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=s.headers.Count)throw new ArgumentException(s.name+": header rỗng hoặc trùng");
            if(!s.headers.Contains("AssetID"))throw new ArgumentException(s.name+": thiếu AssetID");
            while(reader.Read())
            {
                var row=new Dictionary<string,string>();for(int i=0;i<s.headers.Count;i++){var value=reader.GetValue(i);row[s.headers[i]]=value is DateTime date?date.ToString("yyyy-MM-dd"):value is DateTimeOffset offset?offset.ToString("yyyy-MM-dd",CI):Convert.ToString(value,CI)?.Trim()??"";}
                if(row.Values.Any(v=>!string.IsNullOrEmpty(v)))s.rows.Add(row);
            }
            result.Add(s);
        }while(reader.NextResult());
        if(result.Count==0)throw new ArgumentException("Không có sheet Assets/Technical/Lifecycle/Inspections/Maintenance");return result;
    }
    public static Preview Validate(RailwayBimStore store,List<Sheet> sheets)
    {
        var plan=new Preview{sheets=sheets};var fields=new HashSet<string>(store.Db.Query<BimProperty>("SELECT DISTINCT Name FROM AssetProperties").Select(p=>p.Name));var addedFields=new HashSet<string>();var stagePairs=new HashSet<string>();
        if(sheets.Any(s=>s.name=="Lifecycle")&&sheets.Any(s=>RailwayBimStages.IsSheet(s.name)))plan.errors.Add("Không nhập đồng thời sheet Lifecycle cũ và các sheet sáu giai đoạn: dữ liệu vòng đời sẽ có hai nguồn xung đột. Dùng một cấu trúc trong mỗi lần nhập.");
        foreach(var sheet in sheets)
        {
            var seen=new HashSet<string>();int index=1;
            foreach(var sourceRow in sheet.rows)
            {
                var row=EffectiveRow(store,sheet.name,sourceRow);
                index++;string id=Get(row,"AssetID");bool newRecord=false;
                try
                {
                    if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("Thiếu AssetID");
                    if(!store.Assets.ContainsKey(id)){plan.orphans++;throw new ArgumentException("Orphan AssetID: "+id);}
                    string eventKey=sheet.name=="Inspections"?Get(row,"InspectionID"):sheet.name=="Maintenance"?Get(row,"MaintenanceID"):id;
                    if(!string.IsNullOrEmpty(eventKey)&&!seen.Add(eventKey))throw new ArgumentException("ID trùng trong sheet: "+eventKey);
                    if(RailwayBimStages.IsSheet(sheet.name))ValidateStageRow(store,sheet,row,stagePairs);
                    else if(sheet.name=="Technical")
                    {
                        foreach(var field in row.Where(k=>k.Key!="AssetID"&&!string.IsNullOrEmpty(k.Value))){Header(field.Key,out var name,out var type,out _);RailwayBimStore.ValueValid(field.Value,type);if(!fields.Contains(name))addedFields.Add(name);}
                    }
                    else if(sheet.name=="Lifecycle"){var life=Clone(store.Lifecycle(id));Patch(life,row);RailwayBimStore.Validate(life);}
                    else if(sheet.name=="Inspections")
                    {
                        if(RailwayBimStore.DateValid(Get(row,"InspectionDate"))<RailwayBimStore.DateValid(store.Lifecycle(id).InstallDate))throw new ArgumentException("Ngày kiểm tra trước lắp đặt");if(string.IsNullOrWhiteSpace(Get(row,"Finding"))||string.IsNullOrWhiteSpace(Get(row,"ConditionGrade")))throw new ArgumentException("Thiếu Finding/ConditionGrade");
                        var owner=string.IsNullOrEmpty(eventKey)?null:store.Db.ExecuteScalar<string>("SELECT AssetID FROM AssetInspections WHERE InspectionID=?",eventKey);if(owner!=null&&owner!=id)throw new ArgumentException("InspectionID thuộc AssetID khác");if(owner==null){plan.additions++;newRecord=true;}
                    }
                    else if(sheet.name=="Maintenance")
                    {
                        if(RailwayBimStore.DateValid(Get(row,"MaintenanceDate"))<RailwayBimStore.DateValid(store.Lifecycle(id).InstallDate))throw new ArgumentException("Ngày bảo trì trước lắp đặt");RailwayBimStore.ValueValid(Get(row,"Cost"),"Number");if(double.Parse(Get(row,"Cost"),CI)<0)throw new ArgumentException("Cost âm");if(string.IsNullOrWhiteSpace(Get(row,"Description")))throw new ArgumentException("Thiếu Description");
                        var owner=string.IsNullOrEmpty(eventKey)?null:store.Db.ExecuteScalar<string>("SELECT AssetID FROM AssetMaintenance WHERE MaintenanceID=?",eventKey);if(owner!=null&&owner!=id)throw new ArgumentException("MaintenanceID thuộc AssetID khác");if(owner==null){plan.additions++;newRecord=true;}
                    }
                    if(!newRecord)plan.updates++;
                }
                catch(Exception e){plan.errors.Add(sheet.name+" dòng "+index+": "+e.Message);}
            }
        }
        plan.newFields=addedFields.Count;return plan;
    }
    public static Preview PreviewFile(RailwayBimStore store,string path)=>Validate(store,Read(path));
    public static void Apply(RailwayBimStore store,Preview preview)
    {
        var fresh=Validate(store,preview.sheets);if(!fresh.CanApply)throw new ArgumentException(fresh.ToString());
        try{store.Db.RunInTransaction(()=>
        {
            foreach(var sheet in preview.sheets.OrderBy(s=>s.name=="Inspections"||s.name=="Maintenance"?1:0))foreach(var sourceRow in sheet.rows)
            {
                var row=EffectiveRow(store,sheet.name,sourceRow);
                string id=Get(row,"AssetID");
                if(RailwayBimStages.IsSheet(sheet.name)){ApplyStageRow(store,sheet,row);continue;}
                switch(sheet.name)
                {
                    case "Assets":if(!string.IsNullOrEmpty(Get(row,"DisplayName"))&&store.Assets[id].DisplayName!=row["DisplayName"])store.Rename(id,row["DisplayName"]);break;
                    case "Technical":
                        var old=store.Properties(id).ToDictionary(p=>p.Name);foreach(var f in row.Where(k=>k.Key!="AssetID"&&!string.IsNullOrEmpty(k.Value))){Header(f.Key,out var name,out var type,out var unit);if(!old.TryGetValue(name,out var property)||property.Value!=f.Value||property.DataType!=type||property.Unit!=unit)store.Property(id,name,f.Value,unit,type);}break;
                    case "Lifecycle":var oldLife=store.Lifecycle(id);var life=Clone(oldLife);Patch(life,row);if(typeof(BimLifecycle).GetProperties().Any(p=>!Equals(p.GetValue(oldLife),p.GetValue(life))))store.SaveLifecycle(life);break;
                    case "Inspections":ApplyInspection(store,id,row);break;
                    case "Maintenance":ApplyMaintenance(store,id,row);break;
                }
            }
        });}catch{store.ReloadRegistry();throw;}store.ReloadRegistry();
    }
    static bool Same(object existing,Dictionary<string,string> row)=>existing!=null&&existing.GetType().GetProperties().All(p=>(Convert.ToString(p.GetValue(existing),CI)??"")==Get(row,p.Name));
    static void ApplyInspection(RailwayBimStore store,string id,Dictionary<string,string> row)
    {
        string iid=Get(row,"InspectionID");var old=string.IsNullOrEmpty(iid)?null:store.Db.Query<BimInspection>("SELECT * FROM AssetInspections WHERE InspectionID=?",iid).FirstOrDefault();if(Same(old,row))return;
        if(old==null){store.AddInspection(new BimInspection{AssetID=id,InspectionID=iid,InspectionDate=Get(row,"InspectionDate"),ConditionGrade=Get(row,"ConditionGrade"),Finding=Get(row,"Finding"),Inspector=Get(row,"Inspector"),Notes=Get(row,"Notes")});return;}
        store.Db.Execute("UPDATE AssetInspections SET InspectionDate=?,ConditionGrade=?,Finding=?,Inspector=?,Notes=? WHERE InspectionID=? AND AssetID=?",Get(row,"InspectionDate"),Get(row,"ConditionGrade"),Get(row,"Finding"),Get(row,"Inspector"),Get(row,"Notes"),iid,id);store.Event(id,"InspectionImport","Excel update",iid,iid);
        var latest=store.Db.Query<BimInspection>("SELECT * FROM AssetInspections WHERE AssetID=? ORDER BY InspectionDate DESC,InspectionID DESC LIMIT 1",id).Single();store.Db.Execute("UPDATE AssetLifecycle SET LastInspectionDate=?,ConditionGrade=?,NextInspectionDate=CASE WHEN NextInspectionDate<=? THEN NULL ELSE NextInspectionDate END WHERE AssetID=?",latest.InspectionDate,latest.ConditionGrade,latest.InspectionDate,id);
    }
    static void ApplyMaintenance(RailwayBimStore store,string id,Dictionary<string,string> row)
    {
        string mid=Get(row,"MaintenanceID");var old=string.IsNullOrEmpty(mid)?null:store.Db.Query<BimMaintenance>("SELECT * FROM AssetMaintenance WHERE MaintenanceID=?",mid).FirstOrDefault();if(Same(old,row))return;
        if(old==null){store.AddMaintenance(new BimMaintenance{AssetID=id,MaintenanceID=mid,MaintenanceDate=Get(row,"MaintenanceDate"),MaintenanceType=Get(row,"MaintenanceType"),Description=Get(row,"Description"),Cost=double.Parse(Get(row,"Cost"),CI),Result=Get(row,"Result")});return;}
        store.Db.Execute("UPDATE AssetMaintenance SET MaintenanceDate=?,MaintenanceType=?,Description=?,Cost=?,Result=? WHERE MaintenanceID=? AND AssetID=?",Get(row,"MaintenanceDate"),Get(row,"MaintenanceType"),Get(row,"Description"),double.Parse(Get(row,"Cost"),CI),Get(row,"Result"),mid,id);store.Event(id,"MaintenanceImport","Excel update",mid,mid);
        var latest=store.Db.Query<BimMaintenance>("SELECT * FROM AssetMaintenance WHERE AssetID=? ORDER BY MaintenanceDate DESC,MaintenanceID DESC LIMIT 1",id).Single();store.Db.Execute("UPDATE AssetLifecycle SET MaintenanceStatus=? WHERE AssetID=?",latest.Result,id);
    }
    static BimLifecycle Clone(BimLifecycle a){var b=new BimLifecycle();foreach(var p in typeof(BimLifecycle).GetProperties())p.SetValue(b,p.GetValue(a));return b;}
    static Dictionary<string,string> EffectiveRow(RailwayBimStore store,string sheet,Dictionary<string,string> original)
    {
        object existing=null;
        if(sheet=="Inspections"&&!string.IsNullOrEmpty(Get(original,"InspectionID")))existing=store.Db.Query<BimInspection>("SELECT * FROM AssetInspections WHERE InspectionID=?",Get(original,"InspectionID")).FirstOrDefault();
        if(sheet=="Maintenance"&&!string.IsNullOrEmpty(Get(original,"MaintenanceID")))existing=store.Db.Query<BimMaintenance>("SELECT * FROM AssetMaintenance WHERE MaintenanceID=?",Get(original,"MaintenanceID")).FirstOrDefault();
        if(existing==null)return original;var result=new Dictionary<string,string>(original);
        foreach(var p in existing.GetType().GetProperties())if(p.Name!="AssetID"&&string.IsNullOrEmpty(Get(result,p.Name)))result[p.Name]=Convert.ToString(p.GetValue(existing),CI)??"";
        return result;
    }
    static void Patch(BimLifecycle life,Dictionary<string,string> row)
    {foreach(var p in typeof(BimLifecycle).GetProperties())if(p.Name!="AssetID"&&row.TryGetValue(p.Name,out var value)&&!string.IsNullOrEmpty(value))p.SetValue(life,p.PropertyType==typeof(double)?(object)double.Parse(value,CI):value);}
    static string Get(Dictionary<string,string> row,string key)=>row.TryGetValue(key,out var value)?value:"";
    public static void Header(string text,out string name,out string type,out string unit)
    {var parts=text.Split('|');name=parts[0].Trim();type=parts.Length>1?parts[1].Trim():"Text";unit=parts.Length>2?parts[2].Trim():"";if(string.IsNullOrEmpty(name)||parts.Length>3)throw new ArgumentException("Header không hợp lệ: "+text);}
    public static List<Sheet> ExportData(RailwayBimStore store,IEnumerable<string> ids=null)
    {
        var selected=ids!=null?new HashSet<string>(ids):null;var assets=store.Assets.Values.Where(a=>selected==null||selected.Contains(a.AssetID)).OrderBy(a=>a.AssetID).ToArray();
        var allProps=store.Db.Query<BimProperty>("SELECT * FROM AssetProperties").Where(p=>selected==null||selected.Contains(p.AssetID)).ToLookup(p=>p.AssetID);
        var headers=allProps.SelectMany(g=>g).Select(p=>p.Name+"|"+p.DataType+"|"+p.Unit).Distinct().OrderBy(n=>n).ToArray();
        var technical=new Sheet{name="Technical",headers=new[]{"AssetID"}.Concat(headers).ToList()};foreach(var a in assets){var row=new Dictionary<string,string>{{"AssetID",a.AssetID}};foreach(var p in allProps[a.AssetID])row[p.Name+"|"+p.DataType+"|"+p.Unit]=p.Value;technical.rows.Add(row);}
        Sheet Typed<T>(string name,IEnumerable<T> records){var ps=typeof(T).GetProperties();return new Sheet{name=name,headers=ps.Select(p=>p.Name).ToList(),rows=records.Select(r=>ps.ToDictionary(p=>p.Name,p=>Convert.ToString(p.GetValue(r),CI)??"")).ToList()};}
        var output=new List<Sheet>{technical,Typed("Inspections",store.Db.Query<BimInspection>("SELECT * FROM AssetInspections").Where(a=>selected==null||selected.Contains(a.AssetID))),Typed("Maintenance",store.Db.Query<BimMaintenance>("SELECT * FROM AssetMaintenance").Where(a=>selected==null||selected.Contains(a.AssetID))),Typed("Assets",assets)};
        output.AddRange(ExportStages(store,assets));return output;
    }
    const string NS="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public static void Write(string path,List<Sheet> sheets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));using(var stream=File.Create(path))using(var zip=new ZipArchive(stream,ZipArchiveMode.Create))
        {
            void Entry(string name,Action<XmlWriter> body){using(var output=zip.CreateEntry(name,CompressionLevel.Fastest).Open())using(var xml=XmlWriter.Create(output,new XmlWriterSettings{Encoding=new System.Text.UTF8Encoding(false)})){xml.WriteStartDocument();body(xml);xml.WriteEndDocument();}}
            Entry("[Content_Types].xml",x=>{x.WriteStartElement("Types","http://schemas.openxmlformats.org/package/2006/content-types");foreach(var pair in new[]{new[]{"rels","application/vnd.openxmlformats-package.relationships+xml"},new[]{"xml","application/xml"}}){x.WriteStartElement("Default");x.WriteAttributeString("Extension",pair[0]);x.WriteAttributeString("ContentType",pair[1]);x.WriteEndElement();}void Override(string p,string t){x.WriteStartElement("Override");x.WriteAttributeString("PartName",p);x.WriteAttributeString("ContentType",t);x.WriteEndElement();}Override("/xl/workbook.xml","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");for(int i=0;i<sheets.Count;i++)Override($"/xl/worksheets/sheet{i+1}.xml","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");x.WriteEndElement();});
            void Rels(string path,IEnumerable<(string id,string type,string target)> rels){Entry(path,x=>{x.WriteStartElement("Relationships","http://schemas.openxmlformats.org/package/2006/relationships");foreach(var r in rels){x.WriteStartElement("Relationship");x.WriteAttributeString("Id",r.id);x.WriteAttributeString("Type","http://schemas.openxmlformats.org/officeDocument/2006/relationships/"+r.type);x.WriteAttributeString("Target",r.target);x.WriteEndElement();}x.WriteEndElement();});}
            Rels("_rels/.rels",new[]{("rId1","officeDocument","xl/workbook.xml")});Rels("xl/_rels/workbook.xml.rels",sheets.Select((s,i)=>("rId"+(i+1),"worksheet",$"worksheets/sheet{i+1}.xml")));
            Entry("xl/workbook.xml",x=>{x.WriteStartElement("workbook",NS);x.WriteAttributeString("xmlns","r",null,"http://schemas.openxmlformats.org/officeDocument/2006/relationships");x.WriteStartElement("sheets");for(int i=0;i<sheets.Count;i++){x.WriteStartElement("sheet");x.WriteAttributeString("name",sheets[i].name);x.WriteAttributeString("sheetId",(i+1).ToString());x.WriteAttributeString("r","id","http://schemas.openxmlformats.org/officeDocument/2006/relationships","rId"+(i+1));x.WriteEndElement();}x.WriteEndElement();x.WriteEndElement();});
            for(int i=0;i<sheets.Count;i++){var s=sheets[i];Entry($"xl/worksheets/sheet{i+1}.xml",x=>{x.WriteStartElement("worksheet",NS);x.WriteStartElement("sheetViews");x.WriteStartElement("sheetView");x.WriteAttributeString("workbookViewId","0");x.WriteStartElement("pane");x.WriteAttributeString("ySplit","1");x.WriteAttributeString("topLeftCell","A2");x.WriteAttributeString("state","frozen");x.WriteEndElement();x.WriteEndElement();x.WriteEndElement();x.WriteStartElement("cols");for(int col=0;col<s.headers.Count;col++){x.WriteStartElement("col");x.WriteAttributeString("min",(col+1).ToString());x.WriteAttributeString("max",(col+1).ToString());x.WriteAttributeString("width",col==0?"40":"28");x.WriteAttributeString("customWidth","1");x.WriteEndElement();}x.WriteEndElement();x.WriteStartElement("sheetData");int index=0;void Row(IEnumerable<string> values){x.WriteStartElement("row");x.WriteAttributeString("r",(++index).ToString());foreach(var value in values){x.WriteStartElement("c");x.WriteAttributeString("t","inlineStr");x.WriteStartElement("is");x.WriteElementString("t",value??"");x.WriteEndElement();x.WriteEndElement();}x.WriteEndElement();}Row(s.headers);foreach(var row in s.rows)Row(s.headers.Select(h=>Get(row,h)));x.WriteEndElement();x.WriteEndElement();});}
        }
    }
}
