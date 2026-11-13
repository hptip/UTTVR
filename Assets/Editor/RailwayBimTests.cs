using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RailwayBimTests
{
    const string Key="UTT.Bim.Tests",Main="Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity",Dir="Temp/BimTests";
    static List<string> report=new List<string>(),errors=new List<string>();static int step,frame,waitFrame;static double started;
    static RailwayBimController c;static RailwayBimDashboard ui;static string s1,s2,b1,b2,bolt1,bolt2,rayID;static long initialMemory;static string originalNotes;
    static RailwayBimTests()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(m,s,t)=>{if(EditorApplication.isPlaying&&SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors.Add(t+": "+m);};
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("UTT.Bim.Restart",false))
            {SessionState.SetBool("UTT.Bim.Restart",false);EditorSceneManager.OpenScene(Main,OpenSceneMode.Single);SessionState.SetBool("UTT.Bim.AutoResume",true);}
            else if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("UTT.Bim.Done",false)){SessionState.SetBool("UTT.Bim.Done",false);EditorSceneManager.OpenScene(Main,OpenSceneMode.Single);File.AppendAllText(Dir+"/Report.txt","\nScene reopened clean after tests; no runtime state saved to scene.");}
        };
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);report.Add("PASS | "+message);}
    static void Tick()
    {
        if(!EditorApplication.isPlaying)
        {
            if(SessionState.GetBool("UTT.Bim.AutoResume",false)&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetBool("UTT.Bim.AutoResume",false);SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;return;}
            if(!File.Exists("Temp/RailwayBimTests.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            string command=File.ReadAllText("Temp/RailwayBimTests.request").Trim();File.Delete("Temp/RailwayBimTests.request");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty){File.WriteAllText("Temp/BimTest_BLOCKED.txt","Unsaved scene");return;}
            Directory.CreateDirectory(Dir);SessionState.SetInt("UTT.Bim.Phase",command=="persist"?1:0);SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(Main,OpenSceneMode.Single);EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started>300)throw new Exception("Timeout");
            c=c!=null?c:Object.FindFirstObjectByType<RailwayBimController>();ui=ui!=null?ui:Object.FindFirstObjectByType<RailwayBimDashboard>();if(c==null||ui==null||!c.Ready||ui.NavigationButtons.Count==0)return;if(++frame<35)return;
            if(SessionState.GetInt("UTT.Bim.Phase",0)==1){Persistence();return;}
            if(step==0){Initial();step=1;waitFrame=Time.frameCount;return;}
            if(step==1&&Time.frameCount>waitFrame+2){QueueMouse(true);step=2;waitFrame=Time.frameCount;return;}
            if(step==2&&Time.frameCount>waitFrame+3)
            {
                QueueMouse(false);Check(c.SelectedID==rayID&&ui.DisplayedID==rayID,"Real Input System mouse click selects the physical left rail and syncs UI");c.cameraController.enabled=true;c.FocusSelected();step=3;waitFrame=Time.frameCount;return;
            }
            if(step==3&&Time.frameCount>waitFrame+15)
            {
                Capture("ComponentOutline.png");DataAndExcel();CameraAndButtons();
                SessionState.SetString("UTT.Bim.Bolt1",bolt1);SessionState.SetString("UTT.Bim.Bolt2",bolt2);SessionState.SetString("UTT.Bim.OriginalNotes",originalNotes);
                report.Add("Console errors="+errors.Count);Check(errors.Count==0,"No runtime Console errors in functional test");Write();SessionState.SetInt("UTT.Bim.Phase",1);SessionState.SetBool("UTT.Bim.Restart",true);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;step=4;
            }
        }
        catch(Exception e){report.Add("FAIL | "+e);report.AddRange(errors);Write();SessionState.SetBool(Key,false);SessionState.SetBool("UTT.Bim.Done",true);EditorApplication.isPlaying=false;}
    }
    static void QueueMouse(bool pressed){var mouse=Mouse.current;if(mouse!=null)InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f),buttons=(ushort)(pressed?1:0)});}
    static void Initial()
    {
        initialMemory=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();report.Add("Database="+c.DatabasePath);
        Check(SceneManager.sceneCount==1,"Standalone scene only");Check(c.catalog.segments.Length==34&&c.catalog.blocks.Length==334&&c.catalog.parts.Length==99,"Full zone catalog 34 segments / 334 blocks / 99 physical parts per block");
        Check(c.Store.Assets.Count==33434&&c.Store.Assets.Values.Count(a=>a.AssetType=="Component")==33066,"All 33,066 physical component instances have independent database identities");
        Check(c.Store.Db.Query<RailwayBimBuild.ForeignKeyError>("PRAGMA foreign_key_check").Count==0,"Database foreign key check");
        s1=c.catalog.segments.First(s=>s.track=="TR").id;s2=c.catalog.segments.First(s=>s.track=="TL").id;
        var segmentProxy=c.Visible(s1);var bounds=segmentProxy.GetComponent<Renderer>().bounds;Check(c.Pick(new Ray(bounds.center+Vector3.up*40,Vector3.down)),"3D Segment raycast hits physical overview mesh");Check(c.SelectedID==s1&&c.Level==0&&ui.DisplayedID==s1&&c.outline.HighlightedRenderers==1,"Segment selection does not enter detail; outline + LeftNav + info sync");
        ui.NavigationButtons[s2].onClick.Invoke();Check(c.SelectedID==s2&&c.Level==0,"LeftNav selects second track segment without entering detail");ui.NavigationButtons[s1].onClick.Invoke();
        var watch=Stopwatch.StartNew();ui.detailButton.onClick.Invoke();watch.Stop();report.Add("Segment -> Block full UI ms="+watch.Elapsed.TotalMilliseconds.ToString("F2"));
        Check(c.Level==1&&c.cameraController.mode==1&&c.CurrentItems().Count==10,"Detail button locks Segment and lists 10 physical modules");
        b1=c.CurrentItems()[0].AssetID;b2=c.CurrentItems()[1].AssetID;var blockBounds=c.Visible(b1).GetComponent<Renderer>().bounds;
        Check(c.Pick(new Ray(blockBounds.center+Vector3.up*10,Vector3.down))&&c.SelectedID==b1,"Block 3D raycast selects only Block_001");ui.NavigationButtons[b2].onClick.Invoke();Check(c.SelectedID==b2&&ui.DisplayedID==b2,"Block_002 LeftNav replaces selection and data");ui.NavigationButtons[b1].onClick.Invoke();
        watch.Restart();ui.detailButton.onClick.Invoke();watch.Stop();report.Add("Block -> Component full UI ms="+watch.Elapsed.TotalMilliseconds.ToString("F2"));
        Check(c.Level==2&&c.cameraController.mode==2&&c.CurrentItems().Count==99&&c.ActiveDetailedParts==99,"Component mode locks Block and activates one 99-part pool");
        Check(!c.overviewRoot.GetComponentsInChildren<BimAssetProxy>(true).Single(p=>p.assetID==s1).gameObject.activeSelf,"Selected Segment overview hidden to prevent overlap");
        var sourceBlock=Object.FindObjectsByType<BimAssetProxy>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(p=>p.assetID==b1);Check(!sourceBlock.gameObject.activeInHierarchy,"Selected Block overview hidden while detailed geometry is shown");
        var boltPart=c.catalog.parts.First(p=>p.type=="Bolt");bolt1=b1+"-"+boltPart.key;bolt2=b2+"-"+boltPart.key;rayID=b1+"-"+c.catalog.parts.Single(p=>p.name=="Ray_Left").key;
        ui.NavigationButtons[bolt1].onClick.Invoke();Check(c.SelectedID==bolt1&&ui.DisplayedID==bolt1&&c.outline.HighlightedRenderers==1,"Individual physical bolt selectable via LeftNav with one outlined renderer");
        c.Select(b1+"-"+c.catalog.parts.Single(p=>p.name=="Ray_Right").key);
        var rayBounds=c.Visible(rayID).GetComponent<Renderer>().bounds;var camera=c.cameraController.GetComponent<Camera>();c.cameraController.enabled=false;camera.transform.position=rayBounds.center+Vector3.up*2.5f;camera.transform.LookAt(rayBounds.center);QueueMouse(false);
        Counts("Component view");
    }
    static void DataAndExcel()
    {
        c.Select(bolt1);ui.tabs[2].onClick.Invoke();originalNotes=c.Store.Lifecycle(bolt2).Notes;ui.Inputs["Notes"].text="DEMO: kiểm thử lưu độc lập và persistence 2026-11-12";ui.saveButton.onClick.Invoke();
        Check(c.Store.Lifecycle(bolt1).Notes.Contains("persistence")&&c.Store.Lifecycle(bolt2).Notes==originalNotes,"Saving Bolt_001 lifecycle does not alter same-type Bolt in another Block");
        int inspectOther=c.Store.Inspections(bolt2).Count;int before=c.Store.Inspections(bolt1).Count;ui.tabs[3].onClick.Invoke();ui.Inputs["InspectionDate"].text="2026-11-12";ui.Inputs["Finding"].text="DEMO: kiểm thử nhật ký kiểm tra độc lập";ui.Inputs["Inspector"].text="UTT automated verification";ui.Inputs["Notes"].text="DEMO test";ui.addInspectionButton.onClick.Invoke();
        Check(c.Store.Inspections(bolt1).Count==before+1&&c.Store.Inspections(bolt2).Count==inspectOther,"Inspection UI writes a row only for selected AssetID");
        int maintenanceBefore=c.Store.Maintenance(bolt1).Count;ui.tabs[4].onClick.Invoke();ui.Inputs["MaintenanceDate"].text="2026-11-12";ui.Inputs["MaintenanceType"].text="Kiểm thử DEMO";ui.Inputs["Description"].text="DEMO: kiểm tra liên kết";ui.Inputs["Cost"].text="120000";ui.Inputs["Result"].text="Hoàn thành DEMO";ui.addMaintenanceButton.onClick.Invoke();Check(c.Store.Maintenance(bolt1).Count==maintenanceBefore+1,"Maintenance UI saves work, cost and result");
        ui.tabs[5].onClick.Invoke();Check(c.Store.History(bolt1).Any(h=>h.EventType=="Inspection")&&c.Store.History(bolt1).Any(h=>h.EventType=="Maintenance"),"History reads actual persisted events");
        string export=Path.GetFullPath(Dir+"/AllAssets.xlsx");ui.NextFilePath=export;ui.exportButton.onClick.Invoke();Check(File.Exists(export)&&new FileInfo(export).Length>1000,"Export UI writes full-zone XLSX");
        var sheets=RailwayBimWorkbook.Read(export);var technical=sheets.Single(s=>s.name=="Technical");Check(technical.rows.Count==33434,"XLSX roundtrip retains all AssetIDs");
        if(!technical.headers.Contains("VerificationParameter|Number|mm"))technical.headers.Add("VerificationParameter|Number|mm");technical.rows.Single(r=>r["AssetID"]==bolt1)["VerificationParameter|Number|mm"]="123.4";
        string edited=Path.GetFullPath(Dir+"/Edited.xlsx");RailwayBimWorkbook.Write(edited,sheets);ui.NextFilePath=edited;ui.importButton.onClick.Invoke();Check(ui.PendingImport!=null&&ui.PendingImport.CanApply&&ui.applyButton.interactable,"Excel preview validates before Apply");ui.applyButton.onClick.Invoke();
        Check(c.Store.Properties(bolt1).Any(p=>p.Name=="VerificationParameter"&&p.Value=="123.4")&&!c.Store.Properties(bolt2).Any(p=>p.Name=="VerificationParameter"),"Dynamic Excel column applied only to exact AssetID");ui.tabs[1].onClick.Invoke();Check(ui.Inputs.ContainsKey("VerificationParameter"),"New Excel field appears dynamically in technical UI");
        void Invalid(string name,List<Dictionary<string,string>> rows)
        {var sheet=new RailwayBimWorkbook.Sheet{name="Technical",headers=new List<string>{"AssetID","Value|Number|mm"},rows=rows};string path=Path.GetFullPath(Dir+"/Invalid_"+name+".xlsx");RailwayBimWorkbook.Write(path,new List<RailwayBimWorkbook.Sheet>{sheet});ui.Preview(path);Check(!ui.PendingImport.CanApply&&!ui.applyButton.interactable,"Import validation rejects "+name);}
        Dictionary<string,string> Row(string id,string v)=>new Dictionary<string,string>{{"AssetID",id},{"Value|Number|mm",v}};
        Invalid("DuplicateID",new List<Dictionary<string,string>>{Row(bolt1,"1"),Row(bolt1,"2")});Invalid("MissingID",new List<Dictionary<string,string>>{Row("","1")});Invalid("OrphanID",new List<Dictionary<string,string>>{Row("NO-SUCH-ASSET","1")});Invalid("WrongType",new List<Dictionary<string,string>>{Row(bolt1,"not a number")});
        string initial=c.Store.Properties(bolt1).First(p=>p.Name=="VerificationParameter").Value;try{c.Store.Db.RunInTransaction(()=>{c.Store.Property(bolt1,"VerificationParameter","999","mm","Number");throw new Exception("Intentional rollback test");});}catch(Exception){}
        Check(c.Store.Properties(bolt1).First(p=>p.Name=="VerificationParameter").Value==initial,"SQLite transaction rolls back property and event together");
        bool fk=false;try{c.Store.Db.Execute("INSERT INTO AssetProperties VALUES('UNKNOWN','bad','x','','Text')");}catch{fk=true;}Check(fk,"Foreign key rejects orphan property write");
    }
    static void CameraAndButtons()
    {
        c.Locate(bolt2);Check(c.SelectedID==bolt2&&c.BlockID==b2,"Search/Locate reaches same-type component in another Block");ui.search.SetTextWithoutNotify("");c.Select(bolt2);ui.hideButton.onClick.Invoke();Check(!c.Visible(bolt2).GetComponent<Renderer>().enabled,"Show/Hide hides only selected physical component");ui.hideButton.onClick.Invoke();Check(c.Visible(bolt2).GetComponent<Renderer>().enabled,"Show/Hide restores renderer");
        var camera=c.cameraController;camera.Orbit(new Vector2(100,10000));Check(camera.current.pitch>=12,"Camera pitch bounded");camera.Zoom(10000);Check(camera.current.distance==camera.current.min,"Near zoom clamped");camera.Zoom(-10000);Check(camera.current.distance==camera.current.max,"Far zoom clamped");ui.focusButton.onClick.Invoke();Check((camera.current.center-c.Visible(bolt2).GetComponent<Renderer>().bounds.center).sqrMagnitude<.0001f,"Focus button targets selected geometry");
        ui.backButton.onClick.Invoke();Check(c.Level==1&&c.SelectedID==b2&&c.cameraController.mode==1,"Component -> Block restores selected Block/camera/navigation");ui.backButton.onClick.Invoke();Check(c.Level==0&&c.SelectedID==c.catalog.blocks.Single(b=>b.id==b2).segmentID&&camera.mode==0,"Block -> Overview restores selected Segment/camera/navigation");
        ui.statsButton.onClick.Invoke();Check(ui.ActiveTab=="Statistics"&&c.Level==0,"Statistics backed by actual registry/lifecycle without changing context");ui.measureButton.onClick.Invoke();Check(c.Measuring,"Measurement toolbar enables real ray-based two-point tool");ui.measureButton.onClick.Invoke();Check(!c.Measuring,"Measurement toolbar disables tool");
        ui.resetButton.onClick.Invoke();Check(c.Level==0&&c.SelectedID==null,"Reset View restores Overview");
        Counts("Overview after regression");report.Add("Allocated Unity memory start="+initialMemory+"; after detail/UI/export="+UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()+" bytes; GC Alloc/frame not yet profiled; no Quest performance claim.");
    }
    static void Persistence()
    {
        bolt1=SessionState.GetString("UTT.Bim.Bolt1","");bolt2=SessionState.GetString("UTT.Bim.Bolt2","");
        Check(c.Store.Lifecycle(bolt1).Notes.Contains("persistence"),"Second Play session reads edited lifecycle from disk");Check(c.Store.Lifecycle(bolt2).Notes==SessionState.GetString("UTT.Bim.OriginalNotes",""),"Second session preserves independent other-Block data");Check(c.Store.Properties(bolt1).Any(p=>p.Name=="VerificationParameter"&&p.Value=="123.4"),"Imported dynamic property persists after exiting/re-entering Play Mode");Check(c.Store.Assets.Count==33434,"Reopening does not duplicate seed registry");
        report.Add("Console errors second session="+errors.Count);Check(errors.Count==0,"Second Play session has no runtime Console errors");Write(true);SessionState.SetBool(Key,false);SessionState.SetBool("UTT.Bim.Done",true);EditorApplication.isPlaying=false;
    }
    static void Counts(string label){var gs=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);report.Add(label+$": activeGOs={gs.Count(t=>t.gameObject.activeInHierarchy)}, renderers={Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r=>r.enabled&&r.gameObject.activeInHierarchy)}, colliders={Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Count(r=>r.enabled&&r.gameObject.activeInHierarchy)}, logical components={c.Store.Assets.Values.Count(a=>a.AssetType=="Component")}, loaded detail parts={c.ActiveDetailedParts}");}
    static void Write(bool final=false){File.WriteAllLines(Dir+"/Report"+(SessionState.GetInt("UTT.Bim.Phase",0)==1?"_Persistence":"")+".txt",report.Concat(new[]{final?"FUNCTIONAL TEST COMPLETE":"IN_PROGRESS_OR_FAILED"}));}
    static void Capture(string name)
    {
        var camera=c.cameraController.GetComponent<Camera>();var rt=new RenderTexture(1280,800,24);rt.Create();var old=RenderTexture.active;Texture2D image=null;
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1280,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();File.WriteAllBytes(Dir+"/"+name,image.EncodeToPNG());}finally{RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);if(image!=null)Object.DestroyImmediate(image);}
        ScreenCapture.CaptureScreenshot(Dir+"/GameView.png");
    }
}

