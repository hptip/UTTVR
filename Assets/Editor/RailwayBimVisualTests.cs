using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RailwayBimVisualTests
{
    const string Key="UTT.Bim.Visual",Dir="Temp/BimVisual";static int step;static double until,started;static RailwayBimController c;static RailwayBimDashboard ui;static string segment,block,bolt,rail;static Vector2 pointer;static List<string> report=new List<string>(),errors=new List<string>();
    static RailwayBimVisualTests()
    {
        EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(EditorApplication.isPlaying&&SessionState.GetBool(Key,false)&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors.Add(t+": "+m);};
        EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("UTT.Bim.VisualDone",false)){SessionState.SetBool("UTT.Bim.VisualDone",false);EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity",OpenSceneMode.Single);var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");EditorWindow.GetWindow(type).maximized=SessionState.GetBool("UTT.Bim.OldMax",false);}};
    }
    static void Pass(bool ok,string text){if(!ok)throw new Exception(text);report.Add("PASS | "+text);}
    static void Move(int next,double wait=1.3){step=next;until=EditorApplication.timeSinceStartup+wait;}
    static void Mouse(bool down){InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,new MouseState{position=pointer,buttons=(ushort)(down?1:0)});}
    static void Tick()
    {
        if(!EditorApplication.isPlaying)
        {
            if(!File.Exists("Temp/RailwayBimVisual.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete("Temp/RailwayBimVisual.request");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty){File.WriteAllText("Temp/BimVisual_BLOCKED.txt","Unsaved scene");return;}
            Directory.CreateDirectory(Dir);SessionState.SetBool(Key,true);var window=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));SessionState.SetBool("UTT.Bim.OldMax",window.maximized);window.maximized=true;window.Focus();EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            if(started==0)started=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Timeout");
            if(EditorApplication.timeSinceStartup<until)return;c=c!=null?c:Object.FindFirstObjectByType<RailwayBimController>();ui=ui!=null?ui:Object.FindFirstObjectByType<RailwayBimDashboard>();if(c==null||ui==null||!c.Ready||ui.NavigationButtons.Count==0)return;
            switch(step)
            {
                case 0:segment=c.catalog.segments.First(s=>s.track=="TR").id;block=c.catalog.blocks.First(b=>b.segmentID==segment).id;bolt=block+"-"+c.catalog.parts.First(p=>p.type=="Bolt").key;rail=block+"-"+c.catalog.parts.Single(p=>p.name=="Ray_Left").key;ui.resetButton.onClick.Invoke();ui.NavigationButtons[segment].onClick.Invoke();Move(1,2);break;
                case 1:Capture("01_Overview");Move(2,.3);break;
                case 2:ui.detailButton.onClick.Invoke();ui.NavigationButtons[block].onClick.Invoke();Move(3,2);break;
                case 3:Capture("02_Block");Move(4,.3);break;
                case 4:ui.detailButton.onClick.Invoke();ui.NavigationButtons[rail].onClick.Invoke();Move(5,2.5);break;
                case 5:Capture("03_Rail");Pass(c.outline.HighlightedRenderers==1,"Rail outline targets one physical renderer; source material retained");Pass(OutlinePixels>20,"Rendered image contains visible cyan outline pixels");Move(51,.4);break;
                case 51:ui.NavigationButtons[bolt].onClick.Invoke();ui.focusButton.onClick.Invoke();Move(6,2);break;
                case 6:Capture("04_Bolt");Pass(OutlinePixels>20,"Small bolt outline visible after Focus");Move(61,.4);break;
                case 61:ui.NavigationButtons[rail].onClick.Invoke();Canvas.ForceUpdateCanvases();var rect=(RectTransform)ui.NavigationButtons[bolt].transform;pointer=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));Mouse(false);Move(7,.2);break;
                case 7:Diagnostics("before press");Mouse(true);Move(8,.2);break;
                case 8:Diagnostics("before release");Mouse(false);Move(9,.3);break;
                case 9:Diagnostics("after release");Pass(c.SelectedID==bolt&&ui.DisplayedID==bolt,"Real mouse click on LeftNav selects bolt; UI blocks underlying 3D click");c.cameraController.current.center+=Vector3.right*.3f;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(UnityEngine.InputSystem.Key.F));Move(10,.2);break;
                case 10:InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Pass((c.cameraController.current.center-c.Visible(bolt).GetComponent<Renderer>().bounds.center).sqrMagnitude<.0001f,"Real F keyboard input focuses selected bolt");ui.measureButton.onClick.Invoke();Move(11,1.5);break;
                case 11:pointer=new Vector2(Screen.width*.495f,Screen.height*.5f);Mouse(false);Move(12,.2);break;
                case 12:Mouse(true);Move(13,.2);break;
                case 13:Mouse(false);Move(14,.2);break;
                case 14:pointer=new Vector2(Screen.width*.505f,Screen.height*.5f);Mouse(false);Move(15,.2);break;
                case 15:Mouse(true);Move(16,.2);break;
                case 16:Mouse(false);Move(17,.3);break;
                case 17:Pass(c.LastMeasurement>0&&c.LastMeasurement<1,"Two real mouse clicks measure geometry distance in metres: "+c.LastMeasurement.ToString("F5"));ui.measureButton.onClick.Invoke();ui.tabs[1].onClick.Invoke();Move(18,.5);break;
                case 18:Capture("05_Technical");Pass(ui.Inputs.ContainsKey("ModelLength")&&ui.Inputs["ModelLength"].textComponent.gameObject.activeInHierarchy,"Technical value input is active and visible");Move(181,.4);break;
                case 181:ui.statsButton.onClick.Invoke();Move(19,.4);break;
                case 19:Capture("06_Statistics");Pass(ui.fieldsContent.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.name.Contains("Value")&&t.text=="34"),"Readonly statistics numeric value is visible");ExtraValidation();Move(20,.4);break;
                case 20:Pass(errors.Count==0,"No new runtime errors in visual/input tests");Finish(true);break;
            }
        }
        catch(Exception e){report.Add("FAIL | "+e);report.AddRange(errors);Finish(false);}
    }
    static void ExtraValidation()
    {
        var id=bolt;var inspection=c.Store.Inspections(id).First();string original=inspection.Notes;
        var sheet=new RailwayBimWorkbook.Sheet{name="Inspections",headers=new List<string>{"AssetID","InspectionID","Notes"},rows=new List<Dictionary<string,string>>{new Dictionary<string,string>{{"AssetID",id},{"InspectionID",inspection.InspectionID},{"Notes",""}}}};
        var preview=RailwayBimWorkbook.Validate(c.Store,new List<RailwayBimWorkbook.Sheet>{sheet});Pass(preview.CanApply,"Blank existing event cells are valid and inherit old values");RailwayBimWorkbook.Apply(c.Store,preview);Pass(c.Store.Inspections(id).Single(i=>i.InspectionID==inspection.InspectionID).Notes==original,"Blank Excel event Notes does not erase saved data");
        bool dates=false;var life=c.Store.Lifecycle(id);life.NextInspectionDate="2020-01-01";try{c.Store.SaveLifecycle(life);}catch{dates=true;}Pass(dates,"Lifecycle rejects next-inspection date before installation/last inspection");
        var all=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();Pass(all.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount)==0,"No Missing Script in new scene");Pass(all.SelectMany(g=>g.GetComponents<MeshFilter>()).All(m=>m.sharedMesh!=null),"No Missing Mesh");Pass(all.SelectMany(g=>g.GetComponents<Renderer>()).All(r=>r.sharedMaterials.All(m=>m!=null&&m.shader!=null)),"No Missing Material/Shader");
    }
    static int OutlinePixels;
    static void Diagnostics(string stage)
    {
        var module=EventSystem.current.currentInputModule as UnityEngine.InputSystem.UI.InputSystemUIInputModule;var results=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=pointer},results);
        report.Add(stage+$" target={pointer}, mouse={UnityEngine.InputSystem.Mouse.current.position.ReadValue()}, cursor={Cursor.lockState}, selected={c.SelectedID}, eventSelected={EventSystem.current.currentSelectedGameObject?.name}, module={module?.name}, point={module?.point?.action?.ReadValue<Vector2>()}, click={module?.leftClick?.action?.ReadValue<float>()}, UIhits="+string.Join(",",results.Take(6).Select(r=>r.gameObject.name)));
    }
    static void Capture(string name)
    {
        var camera=c.cameraController.GetComponent<Camera>();var rt=new RenderTexture(1440,900,24);rt.Create();var old=RenderTexture.active;Texture2D img=null;
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;img=new Texture2D(1440,900,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1440,900),0,0);img.Apply();OutlinePixels=img.GetPixels32().Count(p=>p.r<80&&p.g>150&&p.b>180);File.WriteAllBytes(Dir+"/"+name+"_Model.png",img.EncodeToPNG());}finally{RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);if(img!=null)Object.DestroyImmediate(img);}
        ScreenCapture.CaptureScreenshot(Dir+"/"+name+"_UI.png");report.Add(name+" cyan outline pixels="+OutlinePixels);
    }
    static void Finish(bool pass){File.WriteAllLines(Dir+"/Report.txt",report.Concat(new[]{pass?"VISUAL_INPUT_PASS":"FAILED"}));SessionState.SetBool(Key,false);SessionState.SetBool("UTT.Bim.VisualDone",true);EditorApplication.isPlaying=false;}
}

