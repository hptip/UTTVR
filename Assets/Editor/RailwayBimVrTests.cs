using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Input reader is attached only to transient Play Mode test objects.
public sealed class BimVrTestButton:MonoBehaviour,IXRInputButtonReader
{
    public bool pressed,edge;
    public bool ReadIsPerformed()=>pressed;
    public bool ReadWasPerformedThisFrame()=>edge;
    public bool ReadWasCompletedThisFrame()=>!pressed;
    public float ReadValue()=>pressed?1:0;
    public bool TryReadValue(out float value){value=ReadValue();return true;}
}
[InitializeOnLoad]
public static class RailwayBimVrTests
{
    const string Key="UTT.Bim.VRTest",Dir="Temp/BimVR";static int step;static double until,start;static RailwayBimController c;static RailwayBimDashboard ui;static RailwayBimVrPresentation vr;static XRRayInteractor ray;static BimVrTestButton reader;
    static string segment,block,bolt,oldSearch;static List<string> report=new List<string>(),errors=new List<string>();static Vector2 pointer;
    static RailwayBimVrTests()
    {
        EditorApplication.update+=Tick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false)&&EditorApplication.isPlaying&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))errors.Add(m);};
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);report.Add("PASS | "+message);}
    static void Next(int n,double delay=.7){step=n;until=EditorApplication.timeSinceStartup+delay;}
    static void Aim(RectTransform target)
    {Canvas.ForceUpdateCanvases();var point=target.TransformPoint(target.rect.center);var origin=ray.rayOriginTransform??ray.transform;origin.position=vr.Origin.Camera.transform.position;origin.rotation=Quaternion.LookRotation(point-origin.position);}
    static void Press(bool down){reader.pressed=down;reader.edge=down;}
    static void Tick()
    {
        if(!EditorApplication.isPlaying)
        {
            if(!File.Exists("Temp/BimVRTest.request")||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            File.Delete("Temp/BimVRTest.request");Directory.CreateDirectory(Dir);report.Clear();errors.Clear();step=0;start=0;until=0;
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty){File.WriteAllText(Dir+"/BLOCKED.txt","Unsaved scene");return;}
            EditorSceneManager.OpenScene(RailwayBimVrUpgrade.ScenePath);var window=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));window.maximized=true;window.Focus();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;return;
        }
        if(!SessionState.GetBool(Key,false))return;
        try
        {
            if(start==0)start=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-start>180)throw new Exception("Timeout");if(EditorApplication.timeSinceStartup<until)return;
            c=c!=null?c:Object.FindFirstObjectByType<RailwayBimController>();ui=ui!=null?ui:Object.FindFirstObjectByType<RailwayBimDashboard>();vr=vr!=null?vr:Object.FindFirstObjectByType<RailwayBimVrPresentation>();if(c==null||ui==null||!c.Ready)return;if(step==0&&ui.NavigationButtons.Count==0)return;
            switch(step)
            {
                case 0:
                    Check(vr!=null,"Scene adapter present; registry ready="+c.Store.Assets.Count);
                    if(vr.IsVr)vr.SetVrMode(false);segment=ui.NavigationButtons.Keys.First();block=c.catalog.blocks.First(b=>b.segmentID==segment).id;bolt=block+"-"+c.catalog.parts.First(p=>p.type=="Bolt").key;
                    // Close global selector via its real F1 input, so the asset UI receives subsequent pointer input.
                    InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(UnityEngine.InputSystem.Key.F1));Next(1);break;
                case 1:InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Canvas.ForceUpdateCanvases();pointer=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)ui.NavigationButtons[segment].transform).TransformPoint(((RectTransform)ui.NavigationButtons[segment].transform).rect.center));InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=pointer});Next(2);break;
                case 2:InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=pointer,buttons=1});Next(3,.2);break;
                case 3:InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=pointer});Next(4);break;
                case 4:
                    var debugHits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=pointer},debugHits);report.Add("Mouse target="+pointer+"; hits="+string.Join(",",debugHits.Take(6).Select(h=>h.gameObject.name))+"; module="+EventSystem.current.currentInputModule?.GetType().Name+"; selected="+c.SelectedID);
                    Check(c.SelectedID==segment,"Desktop mouse through XRUIInputModule selects navigation row");ScreenCapture.CaptureScreenshot(Dir+"/Vietnamese_Desktop.png");
                    Check(ui.listTitle.text.Contains("PHÂN ĐOẠN"),"Vietnamese navigation/header");ui.detailButton.onClick.Invoke();Check(c.Level==1,"Desktop detail button works");ui.backButton.onClick.Invoke();
                    Next(41,.6);break;
                case 41:vr.SetVrMode(true);Next(5,1.5);break;
                case 5:
                    Check(vr.IsVr&&vr.Origin!=null,"Direct standalone VR starts with authored fallback rig");
                    Check(Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length==1,"Exactly one active XR Origin");Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"Exactly one EventSystem");
                    Check(vr.dashboardCanvas.renderMode==RenderMode.WorldSpace&&vr.dashboardCanvas.GetComponent<TrackedDeviceGraphicRaycaster>().enabled,"World-space dashboard with tracked-device raycaster");
                    Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(k=>k.enabled)==1,"Single enabled camera; desktop orbit cannot override HMD");
                    foreach(var b in vr.Origin.GetComponentsInChildren<Behaviour>(true))if(b.GetType().Name.Contains("PoseDriver")||b.GetType().Name=="XRController"||b is QuestRayControllerInput)b.enabled=false;
                    ray=vr.Origin.GetComponentsInChildren<XRRayInteractor>().Last();reader=ray.gameObject.AddComponent<BimVrTestButton>();var input=new XRInputButtonReader("Test trigger");input.inputSourceMode=XRInputButtonReader.InputSourceMode.ObjectReference;input.SetObjectReference(reader);ray.selectInput=input;ray.uiPressInput=input;
                    Aim((RectTransform)ui.detailButton.transform);Next(6,1);break;
                case 6:
                    Check(ray.TryGetCurrentUIRaycastResult(out var hit)&&hit.gameObject!=null,"Actual XR ray hits dashboard graphic: "+(ray.TryGetCurrentUIRaycastResult(out var h)?h.gameObject.name:"none"));Press(true);Next(7,.2);break;
                case 7:Press(false);Next(8);break;
                case 8:
                    Check(c.Level==1,"Controller UI press opens selected segment through tracked input module");c.Locate(bolt);Next(9,1);break;
                case 9:
                    vr.SetPanelVisible(false);c.Select(block+"-"+c.catalog.parts.First(p=>p.type=="Rail").key);var proxy=c.Visible(bolt);var bounds=proxy.GetComponent<Renderer>().bounds;var target=bounds.center;var origin=ray.rayOriginTransform??ray.transform;origin.position=target+Vector3.up*.3f;origin.rotation=Quaternion.LookRotation(Vector3.down);Next(10);break;
                case 10:Press(true);Next(11,.15);break;
                case 11:Press(false);Next(12);break;
                case 12:
                    Check(c.SelectedID==bolt,"Controller trigger selects physical bolt via actual physics ray + central selection");Check(c.outline.HighlightedRenderers==1,"Controller selection drives same outline");
                    vr.SetPanelVisible(true);ui.ShowTab("Technical");Next(13,.5);break;
                case 13:
                    Aim((RectTransform)ui.search.transform);Next(131);break;
                case 131:Press(true);Next(132,.15);break;
                case 132:Press(false);Next(133);break;
                case 133:
                    Check(vr.GetComponent<RailwayBimVrKeyboard>().IsOpen,"Controller ray click opens field keyboard");var key=vr.dashboardCanvas.GetComponentsInChildren<Button>().Single(b=>b.name=="Phím ă");Aim((RectTransform)key.transform);Next(134);break;
                case 134:Check(ray.TryGetCurrentUIRaycastResult(out var keyHit)&&keyHit.gameObject.name=="Phím ă","Vietnamese key raycast is not covered by another key");Press(true);Next(135,.15);break;
                case 135:Press(false);Next(136);break;
                case 136:
                    Check(ui.search.text=="ă","Controller ray clicks Vietnamese virtual key after input loses focus");ui.search.SetTextWithoutNotify("");
                    oldSearch=ui.search.text;var keyboard=vr.GetComponent<RailwayBimVrKeyboard>();keyboard.Open(ui.search);ui.search.SetTextWithoutNotify("");keyboard.Insert("duong");keyboard.Tone('\u0300');Check(ui.search.text=="duòng","Vietnamese keyboard inserts text and composed tone independent of field focus");keyboard.Backspace();Check(ui.search.text=="duòn","Keyboard backspace preserves target/caret");keyboard.Close();ui.search.text=oldSearch;
                    vr.SetPanelVisible(false);Check(!vr.PanelVisible,"Asset panel toggle hides dashboard");vr.SetPanelVisible(true);Check(vr.PanelVisible,"Asset panel can reopen/reposition");
                    ScreenCapture.CaptureScreenshot(Dir+"/Vietnamese_VR.png");Check(UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings>0&&Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings).Any(i=>UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i)==RailwayBimVrUpgrade.ScenePath),"Scene registered in build list for menu loading");Next(14,1);break;
                case 14:
                    var menu=Object.FindFirstObjectByType<SceneQuickMenu>();var field=typeof(SceneQuickMenu).GetField("Destinations",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var destinations=((ValueTuple<string,string>[])field.GetValue(null));Check(destinations.Any(d=>d.Item1=="QuanLyVongDoiTaiSan"&&d.Item2=="Quản lý vòng đời tài sản"),"Global scene menu contains named lifecycle destination");
                    Check(errors.Count==0,"No C# runtime errors in UI/VR test");report.Add("LIMIT: simulated trigger/pose with production XR ray and UI module; physical headset/buttons not tested. No APK built.");File.WriteAllLines(Dir+"/Report.txt",report);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;break;
            }
        }
        catch(Exception e){report.Add("FAIL | "+e);report.AddRange(errors);File.WriteAllLines(Dir+"/Report.txt",report);SessionState.SetBool(Key,false);EditorApplication.isPlaying=false;}
    }
}
