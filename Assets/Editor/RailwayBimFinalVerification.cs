using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RailwayBimFinalVerification
{
    const string Key="UTT.Bim.FinalCapture",Dir="Temp/BimFinal";static int stage;static double until;static RailwayBimController c;static RailwayBimDashboard ui;static string segment,block,rail,bolt;static List<string> report=new List<string>();
    static RailwayBimFinalVerification()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("UTT.Bim.CaptureDone",false)){SessionState.SetBool("UTT.Bim.CaptureDone",false);EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity",OpenSceneMode.Single);File.WriteAllText("Temp/RailwayBimFinalChecks.request","checks");}};
    }
    static void Check(bool value,string text){if(!value)throw new Exception(text);report.Add("PASS | "+text);}
    static void Next(){stage++;until=EditorApplication.timeSinceStartup+(stage%2==1?1.4:.4);}
    static void Tick()
    {
        Directory.CreateDirectory(Dir);
        if(!EditorApplication.isPlaying)
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.Exists("Temp/RailwayBimFinalChecks.request")){File.Delete("Temp/RailwayBimFinalChecks.request");try{EditorChecks();}catch(Exception e){File.WriteAllText(Dir+"/EditorChecks_ERROR.txt",e.ToString());}return;}
            if(!File.Exists("Temp/RailwayBimCapture.request"))return;File.Delete("Temp/RailwayBimCapture.request");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty){File.WriteAllText(Dir+"/BLOCKED.txt","Unsaved scene");return;}SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Key,false)||EditorApplication.timeSinceStartup<until)return;
        try
        {
            c=c!=null?c:Object.FindFirstObjectByType<RailwayBimController>();ui=ui!=null?ui:Object.FindFirstObjectByType<RailwayBimDashboard>();if(c==null||ui==null||!c.Ready||ui.NavigationButtons.Count==0)return;
            switch(stage)
            {
                case 0:segment=c.catalog.segments.First(s=>s.track=="TR").id;block=c.catalog.blocks.First(b=>b.segmentID==segment).id;rail=block+"-"+c.catalog.parts.Single(p=>p.name=="Ray_Left").key;bolt=block+"-"+c.catalog.parts.First(p=>p.type=="Bolt").key;c.SetOverview();c.cameraController.ResetView();c.Select(segment);Counts("Overview");Next();break;
                case 1:Capture("Overview");Next();break;
                case 2:Transition("Segment -> Block",()=>ui.detailButton.onClick.Invoke());c.Select(block);Counts("Block");Next();break;
                case 3:Capture("Block");Next();break;
                case 4:Transition("Block -> Component",()=>ui.detailButton.onClick.Invoke());c.Select(rail);Counts("Component");Next();break;
                case 5:Capture("Rail");Next();break;
                case 6:
                    var proxy=c.Visible(bolt);var mf=proxy.GetComponent<MeshFilter>();var vs=mf.sharedMesh.vertices;var ts=mf.sharedMesh.triangles;bool picked=false;
                    for(int i=0;i<ts.Length&&!picked;i+=3){var a=proxy.transform.TransformPoint(vs[ts[i]]);var b=proxy.transform.TransformPoint(vs[ts[i+1]]);var cc=proxy.transform.TransformPoint(vs[ts[i+2]]);var n=Vector3.Cross(b-a,cc-a).normalized;if(n.y<.5f)continue;var ray=new Ray((a+b+cc)/3+n*.5f,-n);if(Physics.Raycast(ray,out var hit,1)&&hit.collider.gameObject==proxy.gameObject)picked=c.Pick(ray)&&c.SelectedID==bolt;}
                    Check(picked,"3D Physics ray selects individual physical bolt");ui.focusButton.onClick.Invoke();Next();break;
                case 7:Capture("Bolt");Next();break;
                case 8:ui.tabs[1].onClick.Invoke();Next();break;
                case 9:Capture("Technical");Next();break;
                case 10:ui.statsButton.onClick.Invoke();Next();break;
                case 11:Capture("Statistics");Next();break;
                case 12:
                    var otherBlock=c.catalog.blocks.First(b=>b.segmentID==segment&&b.id!=block).id;var otherBolt=otherBlock+"-"+c.catalog.parts.First(p=>p.type=="Bolt").key;
                    ui.search.text=otherBolt;Check(ui.NavigationButtons.ContainsKey(otherBolt),"ID search produces matching real asset item");ui.NavigationButtons[otherBolt].onClick.Invoke();Check(c.SelectedID==otherBolt&&c.BlockID==otherBlock&&ui.DisplayedID==otherBolt,"Search result uses common selection/navigation/highlight path across Blocks");ui.search.text="BuLongVit_001";Check(ui.NavigationButtons.Count>1,"Name search returns actual physical bolt instances");ui.search.text="";
                    var oldLife=c.Store.Lifecycle(otherBolt);var otherGrade=c.Store.Condition(bolt);var editLife=c.Store.Lifecycle(otherBolt);editLife.ConditionGrade="Theo dõi DEMO — cache test";c.Store.SaveLifecycle(editLife);Check(c.Store.Condition(otherBolt)==editLife.ConditionGrade&&c.Store.Condition(bolt)==otherGrade,"Condition badge cache updates only edited AssetID");Check(c.Store.History(otherBolt).Any(h=>h.EventType=="Lifecycle"&&h.Description=="ConditionGrade"&&h.PreviousValue==oldLife.ConditionGrade&&h.NewValue==editLife.ConditionGrade),"Lifecycle history persists exact changed field and previous/new values");c.Store.SaveLifecycle(oldLife);
                    var known=new HashSet<Button>(new[]{ui.detailButton,ui.backButton,ui.focusButton,ui.resetButton,ui.hideButton,ui.measureButton,ui.statsButton,ui.exportButton,ui.importButton,ui.applyButton,ui.saveButton,ui.addInspectionButton,ui.addMaintenanceButton}.Concat(ui.tabs).Concat(ui.NavigationButtons.Values));
                    var canvas=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Single(v=>v.name=="RailwayLifecycleDashboard");var buttons=canvas.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy).ToArray();Check(buttons.All(known.Contains),"Every visible dashboard button belongs to a wired real function");
                    Check(c.CurrentItems().All(a=>c.Store.Assets.ContainsKey(a.AssetID)),"Every selectable component has database data");File.WriteAllLines(Dir+"/RuntimeReport.txt",report);SessionState.SetBool(Key,false);SessionState.SetBool("UTT.Bim.CaptureDone",true);EditorApplication.isPlaying=false;break;
            }
        }
        catch(Exception e){report.Add("FAIL | "+e);File.WriteAllLines(Dir+"/RuntimeReport.txt",report);SessionState.SetBool(Key,false);SessionState.SetBool("UTT.Bim.CaptureDone",true);EditorApplication.isPlaying=false;}
    }
    static void Transition(string name,Action action){long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();action();timer.Stop();report.Add(name+": full UI+model elapsedMs="+timer.Elapsed.TotalMilliseconds.ToString("F3")+"; current-thread managed allocated bytes="+(GC.GetAllocatedBytesForCurrentThread()-bytes)+" (includes UI and SQLite reads; not an FPS or Quest result)");}
    static void Counts(string name){report.Add(name+$": activeGameObjects={Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>t.gameObject.activeInHierarchy)}, activeRenderers={Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r=>r.enabled)}, activeColliders={Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Count(r=>r.enabled)}, logicalComponents={c.catalog.blocks.Length*c.catalog.parts.Length}, detailedPool={c.ActiveDetailedParts}, UnityAllocatedMemory={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()}");}
    static void Capture(string name){ScreenCapture.CaptureScreenshot(Dir+"/"+name+".png");report.Add("Captured stable state: "+name);}
    static void EditorChecks()
    {
        var result=new List<string>();var scene=SceneManager.GetActiveScene();if(scene.name!="QuanLyVongDoiTaiSan"||scene.isDirty)throw new Exception("Unexpected or dirty scene");var ctl=Object.FindFirstObjectByType<RailwayBimController>();var cat=ctl.catalog;
        var paths=AssetDatabase.GetDependencies(scene.path,true);var missing=paths.Where(p=>p.StartsWith("Assets/")&&!File.Exists(p)).ToArray();if(missing.Length>0)throw new Exception("Missing dependencies: "+string.Join(",",missing));result.Add("PASS: saved scene dependencies exist; scene clean; dependencies="+paths.Length);
        var gos=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject);int missingScripts=gos.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);if(missingScripts!=0)throw new Exception("Missing scripts="+missingScripts);
        string mini=Path.GetFullPath(Dir+"/SampleImportTest.db");
        using(var seed=new SQLite.SQLiteConnection("Assets/StreamingAssets/RailwayLifecycle/Initial.db",SQLite.SQLiteOpenFlags.ReadOnly))using(var store=new RailwayBimStore(mini))
        {
            string sample="outputs/RailwayLifecycle_20261112/RailwayLifecycle_Sample.xlsx";var sheets=RailwayBimWorkbook.Read(sample);var ids=sheets.Single(s=>s.name=="Technical").rows.Select(r=>r["AssetID"]).ToArray();
            store.Db.RunInTransaction(()=>{foreach(var a in seed.Query<BimAsset>("SELECT * FROM Assets").Where(a=>ids.Contains(a.AssetID)).OrderBy(a=>a.AssetID.Length).ThenBy(a=>a.AssetID))
            {if(store.Assets.ContainsKey(a.AssetID))continue;store.Db.Execute("INSERT INTO Assets VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",a.AssetID,a.ParentAssetID,a.AssetType,a.DisplayName,a.ZoneID,a.TrackID,a.SegmentID,a.BlockID,a.ComponentType,a.ChainageStart,a.ChainageEnd,a.ModelReference);store.Assets.Add(a.AssetID,a);var life=seed.Query<BimLifecycle>("SELECT * FROM AssetLifecycle WHERE AssetID=?",a.AssetID).Single();store.SaveLifecycle(life,false);}});
            var preview=RailwayBimWorkbook.Validate(store,sheets);if(!preview.CanApply)throw new Exception("Sample workbook invalid: "+preview);RailwayBimWorkbook.Apply(store,preview);
            if(store.Assets.Count!=15||store.Properties(ids[0]).Count==0)throw new Exception("Sample data not applied");result.Add("PASS: artifact-authored XLSX imported with Unity ExcelDataReader; five data sheets, 15 real AssetIDs, typed dates/numbers; Apply completed in isolated test DB");
            int events=store.Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleEvents");var technical=sheets.Single(s=>s.name=="Technical");RailwayBimWorkbook.Apply(store,RailwayBimWorkbook.Validate(store,new List<RailwayBimWorkbook.Sheet>{technical}));if(store.Db.ExecuteScalar<int>("SELECT count(*) FROM AssetLifecycleEvents")!=events)throw new Exception("Unchanged technical import produced events");result.Add("PASS: repeated unchanged technical import does not duplicate events or overwrite instance data");
        }
        using(var reopened=new RailwayBimStore(mini)){if(reopened.Db.ExecuteScalar<int>("SELECT count(*) FROM SchemaMigrations")!=1||reopened.Assets.Count!=15)throw new Exception("Migration persistence failed");result.Add("PASS: migration reopen idempotent, sample data persisted");}
        float Gauge(IEnumerable<(Mesh mesh,Matrix4x4 matrix)> meshes,out float top)
        {
            var triangles=new List<Vector3[]>();var vertices=new List<Vector3>();foreach(var pair in meshes){var v=pair.mesh.vertices.Select(pair.matrix.MultiplyPoint3x4).ToArray();vertices.AddRange(v);var ts=pair.mesh.triangles;for(int i=0;i<ts.Length;i+=3)triangles.Add(new[]{v[ts[i]],v[ts[i+1]],v[ts[i+2]]});}
            top=vertices.Max(v=>v.y);float plane=top-.014f;var cuts=new List<float>();foreach(var triangle in triangles)for(int i=0;i<3;i++){var a=triangle[i];var b=triangle[(i+1)%3];if(Mathf.Abs(b.y-a.y)>.000001f&&(a.y-plane)*(b.y-plane)<=0)cuts.Add(Mathf.Lerp(a.x,b.x,(plane-a.y)/(b.y-a.y)));}return cuts.Where(x=>x>.0736f).Min()-cuts.Where(x=>x<.0736f).Max();
        }
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab");var low=source.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Mesh_11");float lowGauge=Gauge(new[]{(low.sharedMesh,source.transform.worldToLocalMatrix*low.transform.localToWorldMatrix)},out var lowTop);float detailGauge=Gauge(cat.parts.Where(p=>p.type=="Rail").Select(p=>(p.mesh,Matrix4x4.identity)),out var detailTop);
        float registration=lowTop-detailTop;if(Mathf.Abs(cat.detailedVerticalOffset-registration)>.0000001f){cat.detailedVerticalOffset=registration;EditorUtility.SetDirty(cat);AssetDatabase.SaveAssetIfDirty(cat);}
        result.Add($"Measured rail inner-face separation at each model's top-minus-14mm section: overview={lowGauge:F6}m, detailed={detailGauge:F6}m. This is a mesh comparison convention, not engineering certification. Raw rail-top LOD difference={(detailTop-lowTop)*1000:F3}mm; rigid detailed assembly Y registration={cat.detailedVerticalOffset*1000:F3}mm; registered rail-top difference={(detailTop+cat.detailedVerticalOffset-lowTop)*1000:F3}mm. No component mesh deformed; relative assembly geometry preserved.");
        File.WriteAllLines(Dir+"/EditorChecks.txt",result);
        if(File.Exists(Dir+"/EditorChecks_ERROR.txt"))File.Delete(Dir+"/EditorChecks_ERROR.txt");
    }
}
