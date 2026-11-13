using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class RailwayBimRefinement
{
    static void ApplyBimScenario()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new Exception("Unexpected scene: "+scene.path);
        var c=Object.FindFirstObjectByType<RailwayBimController>();
        Undo.RecordObject(c.cameraController,"Increase BIM camera speed");
        c.cameraController.freeSpeed=12;
        var vr=c.GetComponent<RailwayBimVrPresentation>();Undo.RecordObject(vr,"Increase BIM VR observation speed");vr.locomotionSpeed=1.8f;
        using(var store=new RailwayBimStore("UserData/QuanLyVongDoiTaiSan.db"))
        {
            if(store.Assets.Count!=33434)throw new Exception("Unexpected registry count");
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Dir+"/BimScenarioApplied.txt","Saved existing BIM scene; PC 12 m/s (Shift 36); VR 1.8 m/s. Upgraded only matching generated defaults; kept IDs, record dates and user edits. Scenario data is fictional, not field certification.");
        File.WriteAllText("Temp/BimRefinementTest.request","bimdata");
    }
}

public static partial class RailwayBimRefinementTests
{
    static void BimScenario(RailwayBimController c)
    {
        var db=c.Store.Db;
        Check(c.Store.Assets.Count==33434,"All 33434 asset IDs retained");
        Check(db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleStages")==200604,"Six independent stage profiles retained");
        Check(db.Query<RailwayBimBuild.ForeignKeyError>("PRAGMA foreign_key_check").Count==0,"Scenario migration has no foreign key errors");
        var rail=c.Store.Assets.Values.First(a=>a.ComponentType=="Rail");
        var bolt=c.Store.Assets.Values.First(a=>a.ComponentType=="Bolt");
        var pad=c.Store.Assets.Values.First(a=>a.ComponentType=="Pad");
        Check(c.Store.StageFields(rail.AssetID,"Design")["Material"].Contains("R260"),"Rail has a specific design material rather than placeholder text");
        Check(c.Store.StageFields(pad.AssetID,"Design")["Material"].Contains("EPDM"),"Rail pad has its own elastomer material");
        Check(c.Store.StageFields(rail.AssetID,"Construction")["ConstructionCost"]!=c.Store.StageFields(bolt.AssetID,"Construction")["ConstructionCost"],"Costs vary by component type");
        var block=c.Store.Assets[rail.ParentAssetID];
        double total=c.Store.Children(block.AssetID).Sum(a=>double.Parse(c.Store.StageFields(a.AssetID,"Construction")["ConstructionCost"],System.Globalization.CultureInfo.InvariantCulture));
        Check(Math.Abs(total-double.Parse(c.Store.StageFields(block.AssetID,"Construction")["ConstructionCost"],System.Globalization.CultureInfo.InvariantCulture))<.01,"Block construction estimate equals its component estimates");
        Check(c.Store.StageFields(bolt.AssetID,"Planning")["PlanCode"].StartsWith("YTHL-QH"),"Specific planning code and project records are visible");
        Check(c.cameraController.freeSpeed==12&&c.GetComponent<RailwayBimVrPresentation>().locomotionSpeed==1.8f,"Saved camera speeds are PC 12 m/s and VR 1.8 m/s");
        // A small fixture exercises exact-default protection and replay safety without
        // editing real user profiles. Keep a full parent chain for foreign keys.
        const string path="Temp/BimRefinement/BimScenarioFixture.db";
        if(File.Exists(path))File.Delete(path);
        var ids=new[]{block.ParentAssetID,block.AssetID,rail.AssetID,bolt.AssetID};
        using(var fixture=new RailwayBimStore(path))
        {
            fixture.Db.Execute("ATTACH DATABASE ? AS source",c.Store.Path);
            fixture.Db.Execute("INSERT INTO Assets SELECT * FROM source.Assets WHERE AssetID IN (?,?,?,?) ORDER BY CASE AssetType WHEN 'Segment' THEN 0 WHEN 'Block' THEN 1 ELSE 2 END",ids[0],ids[1],ids[2],ids[3]);
            fixture.Db.Execute("INSERT INTO AssetLifecycle SELECT * FROM source.AssetLifecycle WHERE AssetID IN (?,?,?,?)",ids[0],ids[1],ids[2],ids[3]);
            fixture.Db.Execute("DETACH DATABASE source");
        }
        using(var fixture=new RailwayBimStore(path))
        {
            fixture.Db.Execute("UPDATE AssetStageFields SET Value='Quản lý mô hình đường sắt — MINH HỌA' WHERE StageKey='Planning' AND FieldKey='Purpose'");
            fixture.Db.Execute("UPDATE AssetStageFields SET Value='Ghi chú do người sử dụng nhập' WHERE AssetID=? AND StageKey='Planning' AND FieldKey='Notes'",bolt.AssetID);
            fixture.SaveStage(rail.AssetID,"Planning",new System.Collections.Generic.Dictionary<string,string>{{"Purpose","Nội dung người sử dụng tự nhập"}});
            fixture.Db.Execute("DELETE FROM SeedVersions WHERE Version='bim-yen-ha-scenario-2026-v2'");
        }
        int events;
        using(var fixture=new RailwayBimStore(path))
        {
            Check(fixture.StageFields(rail.AssetID,"Planning")["Purpose"]=="Nội dung người sử dụng tự nhập","Migration preserves user-edited stage data");
            Check(fixture.StageFields(bolt.AssetID,"Planning")["Notes"]=="Ghi chú do người sử dụng nhập","Exact-default comparison preserves custom fields even without history");
            Check(fixture.StageFields(bolt.AssetID,"Planning")["Purpose"].StartsWith("Duy trì an toàn"),"Old generated defaults upgrade in existing databases");
            events=fixture.Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleEvents");
        }
        using(var fixture=new RailwayBimStore(path))
            Check(fixture.Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleEvents")==events&&fixture.Db.ExecuteScalar<int>("SELECT count(*) FROM SeedVersions WHERE Version='bim-yen-ha-scenario-2026-v2'")==1,"Reopening is idempotent; no duplicate migration or history");
        Check(db.ExecuteScalar<int>("SELECT count(*) FROM AssetStageFields WHERE StageKey='Upgrade' AND FieldKey='ActualDate' AND Value>date('now','localtime')")==0,"No completed future upgrades introduced");
    }
}
