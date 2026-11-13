using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static partial class RailwayBimRefinementTests
{
    const string StageDb="Temp/BimRefinement/StageTest.db";
    static void Stages(RailwayBimController c)
    {
        var source=c.Store;Check(source.Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleStages")==33434*6,"Six independent stage records for every existing asset");Check(source.Db.Query<RailwayBimBuild.ForeignKeyError>("PRAGMA foreign_key_check").Count==0,"Migration preserves foreign keys");
        var b=c.catalog.blocks[0];var ids=new[]{b.segmentID,b.id,b.id+"-"+c.catalog.parts[0].key,b.id+"-"+c.catalog.parts[1].key};string a=ids[2],other=ids[3];
        // A dedicated database exercises production Store/UI code without altering user records.
        if(File.Exists(StageDb))File.Delete(StageDb);using(var temp=new RailwayBimStore(StageDb))
        {
            temp.Db.Execute("ATTACH DATABASE ? AS source",source.Path);
            temp.Db.Execute("INSERT INTO Assets SELECT * FROM source.Assets WHERE AssetID IN (?,?,?,?) ORDER BY CASE AssetType WHEN 'Segment' THEN 0 WHEN 'Block' THEN 1 ELSE 2 END",ids[0],ids[1],ids[2],ids[3]);
            foreach(var table in new[]{"AssetProperties","AssetLifecycle","AssetInspections","AssetMaintenance"})temp.Db.Execute("INSERT INTO "+table+" SELECT * FROM source."+table+" WHERE AssetID IN (?,?,?,?)",ids[0],ids[1],ids[2],ids[3]);
            temp.Db.Execute("INSERT INTO AssetLifecycleEvents SELECT * FROM source.AssetLifecycleEvents WHERE AssetID IN (?,?,?,?)",ids[0],ids[1],ids[2],ids[3]);temp.Db.Execute("DETACH DATABASE source");
        }
        using(var store=new RailwayBimStore(StageDb))
        {
            Check(store.Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleStages")==24,"Idempotent seed also initializes subsequently added test registry rows");
            foreach(var id in new[]{a,other})foreach(var stage in RailwayBimStages.All)Check(store.StageFields(id,stage.key).Count==stage.fields.Length,"Fields available: "+id+" / "+stage.name);
            string oldOther=store.StageFields(other,"Planning")["Purpose"],oldDesign=store.StageFields(a,"Design")["Designer"];
            store.SaveStage(a,"Planning",new Dictionary<string,string>{{"Purpose","MINH HỌA — kiểm thử lưu bền sáu giai đoạn"}});
            Check(store.StageFields(other,"Planning")["Purpose"]==oldOther&&store.StageFields(a,"Design")["Designer"]==oldDesign,"Asset A planning change leaves asset B and design stage unchanged");
            int events=store.History(a).Count;store.SaveStage(a,"Planning",new Dictionary<string,string>{{"Purpose","MINH HỌA — kiểm thử lưu bền sáu giai đoạn"}});Check(store.History(a).Count==events,"Unchanged stage save adds no duplicate history");
            bool invalid=false;try{store.SaveStage(a,"Upgrade",new Dictionary<string,string>{{"ActualDate",DateTime.Today.AddDays(1).ToString("yyyy-MM-dd")}});}catch(ArgumentException){invalid=true;}Check(invalid,"Reject future actual upgrade date");
            invalid=false;try{store.SaveStage(a,"Planning",new Dictionary<string,string>{{"Budget","sai kiểu"}});}catch(ArgumentException){invalid=true;}Check(invalid,"Reject nonnumeric budget");
            invalid=false;try{store.SaveStage(a,"Planning",new Dictionary<string,string>{{"KhôngCóTrườngNày","x"}});}catch(ArgumentException){invalid=true;}Check(invalid,"Stage schema rejects unknown field");
            Check(store.Inspections(a).Count>0&&store.Maintenance(a).Count==source.Maintenance(a).Count,"Legacy inspection and maintenance preserved");
            File.WriteAllLines("Temp/BimRefinement/PersistExpected.txt",new[]{a,other,oldOther,oldDesign,store.Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleStages").ToString(),store.History(a).Count.ToString()});
        }
    }
    static void StagePersist(RailwayBimController c)
    {
        var expected=File.ReadAllLines("Temp/BimRefinement/PersistExpected.txt");using(var store=new RailwayBimStore(StageDb))
        {
            Check(store.StageFields(expected[0],"Planning")["Purpose"]=="MINH HỌA — kiểm thử lưu bền sáu giai đoạn","Changed stage persists through another Play session / DB reopen");
            Check(store.StageFields(expected[1],"Planning")["Purpose"]==expected[2]&&store.StageFields(expected[0],"Design")["Designer"]==expected[3],"Other asset/stage remain unchanged after restart");
            Check(store.Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleStages").ToString()==expected[4]&&store.History(expected[0]).Count.ToString()==expected[5],"No duplicate stage seed or events on reopen");
        }
        Check(c.Store.Assets.Count==33434,"Production registry unchanged after migration/restart");
    }
    static void StageExcel(RailwayBimController c)
    {
        using(var store=new RailwayBimStore(StageDb))
        {
            var expected=File.ReadAllLines("Temp/BimRefinement/PersistExpected.txt");string a=expected[0],b=expected[1];var export=RailwayBimWorkbook.ExportData(store,new[]{a,b});
            Check(export.Count(s=>RailwayBimStages.IsSheet(s.name))==6,"Export contains all six stage sheets");
            var file="Temp/BimRefinement/SixStages_RuntimeExport.xlsx";RailwayBimWorkbook.Write(file,export);var read=RailwayBimWorkbook.Read(file);var preview=RailwayBimWorkbook.Validate(store,read);Check(preview.CanApply,"Runtime XLSX roundtrip validates: "+preview);
            int oldEvents=store.History(a).Count;RailwayBimWorkbook.Apply(store,preview);Check(store.History(a).Count==oldEvents,"Unchanged workbook reimport creates no history duplicates");
            var planning=read.Single(s=>s.name=="QuyHoach");var row=planning.rows.Single(r=>r["AssetID"]==a);string header=planning.headers.Single(h=>h.StartsWith("Purpose|"));string beforeOther=store.StageFields(b,"Planning")["Purpose"],beforeDesign=store.StageFields(a,"Design")["Designer"];
            row[header]="MINH HỌA — cập nhật qua Excel sáu giai đoạn";row[planning.headers.Single(h=>h.StartsWith("Notes|"))]="";
            string beforeNote=store.StageFields(a,"Planning")["Notes"];preview=RailwayBimWorkbook.Validate(store,read);Check(preview.CanApply&&store.StageFields(a,"Planning")["Purpose"]!=row[header],"Preview does not write SQLite");RailwayBimWorkbook.Apply(store,preview);
            Check(store.StageFields(a,"Planning")["Purpose"]==row[header]&&store.StageFields(b,"Planning")["Purpose"]==beforeOther&&store.StageFields(a,"Design")["Designer"]==beforeDesign,"Confirm updates only specified asset and stage");Check(store.StageFields(a,"Planning")["Notes"]==beforeNote,"Blank stage cells retain existing values");
            planning.rows.Add(new Dictionary<string,string>(row));Check(!RailwayBimWorkbook.Validate(store,read).CanApply,"Reject duplicate AssetID+StageKey");planning.rows.RemoveAt(planning.rows.Count-1);
            row["StageKey"]="Design";Check(!RailwayBimWorkbook.Validate(store,read).CanApply,"Reject wrong StageKey for sheet");row["StageKey"]="Planning";
            var budget=planning.headers.Single(h=>h.StartsWith("Budget|"));string old=row[budget];row[budget]="sai";Check(!RailwayBimWorkbook.Validate(store,read).CanApply,"Reject invalid stage field type");row[budget]=old;
            // Export JSON is input for the formatted user-facing sample authored with artifact-tool.
            var sample=export.Where(s=>RailwayBimStages.IsSheet(s.name)).Select(s=>new SampleSheet{name=s.name,headers=s.headers.ToArray(),rows=s.rows.Select(r=>new SampleRow{values=s.headers.Select(h=>r.TryGetValue(h,out var v)?v:"").ToArray()}).ToArray()}).ToArray();
            File.WriteAllText("Temp/BimRefinement/SampleData.json",JsonUtility.ToJson(new SampleBook{sheets=sample},true));
        }
    }
    [Serializable] public class SampleBook{public SampleSheet[] sheets;}
    [Serializable] public class SampleSheet{public string name;public string[] headers;public SampleRow[] rows;}
    [Serializable] public class SampleRow{public string[] values;}
    static void UiPersist()
    {
        var expected=File.ReadAllLines("Temp/BimRefinement/UiPersistExpected.txt");using(var store=new RailwayBimStore(StageDb))
        {
            Check(store.StageFields(expected[0],"Planning")["Purpose"]==expected[2],"Asset Panel save persists after restarting Play Mode");Check(store.StageFields(expected[1],"Planning")["Purpose"]==expected[3]&&store.StageFields(expected[0],"Design")["Designer"]==expected[4],"Other asset/stage unchanged after UI save/restart");Check(store.Inspections(expected[0]).Count.ToString()==expected[5]&&store.Maintenance(expected[0]).Count.ToString()==expected[6],"Inspection/maintenance events persist without seed duplication on another Play session");
        }
    }
    static void SampleImport(RailwayBimController controller)
    {
        const string target="Temp/BimRefinement/SampleImportTest.db";File.Copy(StageDb,target,true);
        using(var store=new RailwayBimStore(target))
        {
            string path="outputs/RailwayLifecycle_Refinement_20261113/VongDoiTaiSan_6GiaiDoan_Mau.xlsx";var read=RailwayBimWorkbook.Read(path);var plan=RailwayBimWorkbook.Validate(store,read);Check(read.Count==6&&plan.CanApply,"Artifact-authored six-stage XLSX parses with production ExcelDataReader: "+plan);RailwayBimWorkbook.Apply(store,plan);Check(read.All(s=>s.rows.All(r=>store.Stage(r["AssetID"],r["StageKey"]).SchemaVersion==1)),"Formatted sample applies with correct AssetID+StageKey/version and real numeric/date cell types");
        }
        string legacy="outputs/RailwayLifecycle_20261112/RailwayLifecycle_Sample.xlsx";
        if(File.Exists(legacy)){var plan=RailwayBimWorkbook.PreviewFile(controller.Store,legacy);Check(plan.CanApply,"Existing five-sheet workbook still parses/previews against migrated production registry without writing user data: "+plan);}
    }
}
