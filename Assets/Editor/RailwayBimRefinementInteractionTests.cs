using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEditor;
using Object=UnityEngine.Object;

public static partial class RailwayBimRefinementTests
{
    static int flow;static double wait;static RailwayBimDashboard ui;static RailwayBimVrPresentation vr;static RailwayBimStore liveStore,uiStore;static RailwayBimController switched;
    static string segment,block,bolt,second,selectedBefore;static Vector3 positionBefore,localBefore;static Quaternion rotationBefore;static float scrollBefore;static int countBefore;
    static XRRayInteractor testRay;static BimVrTestButton buttonReader;static XRSimulatedController testLeft,testRight;static bool oldMaximized;
    static Button.ButtonClickedEvent originalSaveEvent;static int forbiddenSubmit;
    static void ResetInteraction(){flow=0;wait=0;ui=null;vr=null;liveStore=null;uiStore=null;switched=null;testLeft=testRight=null;}
    static void CleanupInteraction()
    {
        if(switched!=null&&liveStore!=null)typeof(RailwayBimController).GetField("<Store>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(switched,liveStore);
        uiStore?.Dispose();uiStore=null;if(testLeft!=null&&testLeft.added)InputSystem.RemoveDevice(testLeft);if(testRight!=null&&testRight.added)InputSystem.RemoveDevice(testRight);
        if(command=="ui"||command=="vr"||command=="flight"||command=="topbar")EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).maximized=oldMaximized;
    }
    static bool WaitFor(double seconds,int next){flow=next;wait=EditorApplication.timeSinceStartup+seconds;return false;}
    static void SetupInteraction(RailwayBimController c)
    {
        ui=Object.FindFirstObjectByType<RailwayBimDashboard>();vr=c.GetComponent<RailwayBimVrPresentation>();var window=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));oldMaximized=window.maximized;window.maximized=true;window.Focus();
        var menu=Object.FindFirstObjectByType<SceneQuickMenu>();typeof(SceneQuickMenu).GetMethod("SetMenuOpen",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{false});
        segment=c.catalog.segments[0].id;block=c.catalog.blocks.First(b=>b.segmentID==segment).id;bolt=block+"-"+c.catalog.parts.First(p=>p.type=="Bolt").key;
    }
    static bool UiTick(RailwayBimController c)
    {
        if(EditorApplication.timeSinceStartup<wait)return false;
        switch(flow)
        {
            case 0:
                SetupInteraction(c);Check(ui.tabs.Length==3&&ui.stageButtons.Length==6,"Three primary tabs and six reused stage buttons");Check(ui.focusButton==null&&ui.measureButton==null&&ui.statsButton==null,"Removed focus/measure/statistics objects and obsolete top tabs");
                Check(ui.itemTemplate!=null&&ui.inputTemplate!=null&&ui.rowTemplate!=null,"Dynamic templates retained after inactive UI cleanup");ui.NavigationButtons[segment].onClick.Invoke();Check(c.SelectedID==segment&&c.Level==0,"LeftNav selects and focuses segment without descending");return WaitFor(.8,1);
            case 1:
                ScreenCapture.CaptureScreenshot("Temp/BimRefinement/PC_Overview.png");return WaitFor(.3,11);
            case 11:ui.detailButton.onClick.Invoke();ui.NavigationButtons[block].onClick.Invoke();Check(c.Level==1&&c.SelectedID==block,"Block UI selection and auto focus");ui.detailButton.onClick.Invoke();ui.NavigationButtons[bolt].onClick.Invoke();Check(c.Level==2&&c.SelectedID==bolt&&c.cameraController.current.distance<1.1f,"Bolt UI selection auto focuses close");return WaitFor(.8,2);
            case 2:
                ui.hideButton.onClick.Invoke();Check(c.IsIsolated&&ui.hideButton.GetComponentInChildren<TMPro.TMP_Text>().text.Contains("Hiện lại"),"Isolation button state clearly changes");ui.hideButton.onClick.Invoke();Check(!c.IsIsolated,"Isolation button restores visibility");ui.backButton.onClick.Invoke();Check(c.Level==1,"Quay lại button restores block context");ui.backButton.onClick.Invoke();Check(c.Level==0&&!ui.backButton.interactable,"Quay lại disabled at top level");
                // Use the dedicated stage fixture for edit/save tests; the production DB remains untouched.
                liveStore=c.Store;uiStore=new RailwayBimStore(StageDb);switched=c;typeof(RailwayBimController).GetField("<Store>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,uiStore);
                var expected=File.ReadAllLines("Temp/BimRefinement/PersistExpected.txt");selectedBefore=expected[0];second=expected[1];c.Locate(selectedBefore);ui.tabs[2].onClick.Invoke();
                foreach(var id in new[]{selectedBefore,second}){c.Locate(id);for(int i=0;i<6;i++){ui.stageButtons[i].onClick.Invoke();Check(c.SelectedID==id&&ui.ActiveStage==RailwayBimStages.All[i].key&&ui.Inputs.ContainsKey("Status"),"Stage UI opens "+RailwayBimStages.All[i].name+" without changing asset "+id);}}
                c.Locate(selectedBefore);ui.stageButtons[0].onClick.Invoke();string otherValue=uiStore.StageFields(second,"Planning")["Purpose"],design=uiStore.StageFields(selectedBefore,"Design")["Designer"];ui.Inputs["Purpose"].text="MINH HỌA — lưu từ nút hồ sơ trong Asset Panel";ui.saveButton.onClick.Invoke();
                Check(uiStore.StageFields(selectedBefore,"Planning")["Purpose"].Contains("Asset Panel")&&uiStore.StageFields(second,"Planning")["Purpose"]==otherValue&&uiStore.StageFields(selectedBefore,"Design")["Designer"]==design,"Actual Save button isolates asset and stage");return WaitFor(.6,3);
            case 3:
                ScreenCapture.CaptureScreenshot("Temp/BimRefinement/PC_SixStages.png");return WaitFor(.3,31);
            case 31:ui.ShowStage("Operation");ui.stageRecordsButton.onClick.Invoke();countBefore=uiStore.Inspections(selectedBefore).Count;ui.Inputs["Finding"].text="MINH HỌA — kiểm tra bằng Asset Panel";ui.Inputs["Inspector"].text="Bộ kiểm thử";ui.Inputs["Notes"].text="Dữ liệu thử nghiệm độc lập";ui.addInspectionButton.onClick.Invoke();Check(uiStore.Inspections(selectedBefore).Count==countBefore+1,"Operation panel preserves old inspections and adds a new inspection");
                ui.ShowStage("Maintenance");ui.stageRecordsButton.onClick.Invoke();countBefore=uiStore.Maintenance(selectedBefore).Count;ui.Inputs["MaintenanceType"].text="MINH HỌA";ui.Inputs["Description"].text="Thử luồng bảo trì trong Asset Panel";ui.Inputs["Cost"].text="1000";ui.Inputs["Result"].text="Hoàn thành thử nghiệm";ui.addMaintenanceButton.onClick.Invoke();Check(uiStore.Maintenance(selectedBefore).Count==countBefore+1,"Maintenance panel adds event and retains old history");
                uiStore.Db.RunInTransaction(()=>{for(int i=0;i<65;i++)uiStore.Event(selectedBefore,"Stage","MINH HỌA — kiểm thử phân trang",i.ToString(),(i+1).ToString(),"Maintenance");});ui.stageHistoryButton.onClick.Invoke();Check(ui.StageView=="History"&&uiStore.StageHistory(selectedBefore,"Maintenance").Count>0,"Stage history remains accessible inside Asset Panel");Check(ui.historyNextButton.interactable,"Older history page available beyond first 50 events");ui.historyNextButton.onClick.Invoke();Check(ui.historyPreviousButton.interactable,"History paging reaches older entries");ui.historyPreviousButton.onClick.Invoke();Check(!ui.historyPreviousButton.interactable,"History paging returns to newest entries");
                var fields=ui.Inputs;vr.SetPanelVisible(false);Check(c.cameraController.FreeMode,"Hiding PC UI enables bounded-speed free camera controls");positionBefore=c.cameraController.current.center;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(UnityEngine.InputSystem.Key.W,UnityEngine.InputSystem.Key.E));return WaitFor(.6,4);
            case 4:
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Check((c.cameraController.current.center-positionBefore).magnitude>.05f,"Actual W/E input moves free camera with UI hidden");positionBefore=c.cameraController.current.center;vr.SetPanelVisible(true);Check((c.cameraController.current.center-positionBefore).sqrMagnitude<.000001f&&c.SelectedID==selectedBefore,"Showing PC UI preserves camera and selection");c.FocusSelected();Check(c.cameraController.current.center!=positionBefore,"Explicit F-equivalent return to selected asset available");
                ui.search.text=second;Check(ui.NavigationButtons.ContainsKey(second),"Search still resolves exact asset ID");ui.NavigationButtons[second].onClick.Invoke();Check(c.SelectedID==second,"Search navigation uses unified selection");ui.search.text="";
                File.WriteAllLines("Temp/BimRefinement/UiPersistExpected.txt",new[]{selectedBefore,second,uiStore.StageFields(selectedBefore,"Planning")["Purpose"],uiStore.StageFields(second,"Planning")["Purpose"],uiStore.StageFields(selectedBefore,"Design")["Designer"],uiStore.Inspections(selectedBefore).Count.ToString(),uiStore.Maintenance(selectedBefore).Count.ToString()});return true;
        }
        return false;
    }
    static void ConfigureRay(XRRayInteractor ray)
    {
        testRay=ray;buttonReader=ray.GetComponent<BimVrTestButton>()??ray.gameObject.AddComponent<BimVrTestButton>();buttonReader.pressed=buttonReader.edge=false;
        var reader=new XRInputButtonReader("Test trigger");reader.inputSourceMode=XRInputButtonReader.InputSourceMode.ObjectReference;reader.SetObjectReference(buttonReader);ray.selectInput=reader;ray.uiPressInput=reader;
    }
    static void Press(bool down){buttonReader.pressed=down;buttonReader.edge=down;}
    static void Aim(RectTransform rect)
    {Canvas.ForceUpdateCanvases();var point=rect.TransformPoint(rect.rect.center);var origin=testRay.rayOriginTransform??testRay.transform;origin.position=vr.Origin.Camera.transform.position+vr.Origin.Camera.transform.right*.12f-Vector3.up*.12f;origin.rotation=Quaternion.LookRotation(point-origin.position);}
    static void Send(XRSimulatedController device,ControllerButton? button=null,Vector2 axis=default,bool allRight=false)
    {
        var state=new XRSimulatedControllerState{isTracked=true,trackingState=3,deviceRotation=Quaternion.identity,primary2DAxis=axis};if(button.HasValue)state.WithButton(button.Value,true);if(allRight){state.WithButton(ControllerButton.PrimaryButton,true);state.WithButton(ControllerButton.SecondaryButton,true);state.WithButton(ControllerButton.GripButton,true);}InputSystem.QueueStateEvent(device,state);
    }
    static bool VrTick(RailwayBimController c)
    {
        if(EditorApplication.timeSinceStartup<wait)return false;
        switch(flow)
        {
            case 0:SetupInteraction(c);c.Select(segment);vr.SetVrMode(true);return WaitFor(1,1);
            case 1:
                Check(vr.IsVr&&vr.WorldPanels.Count==3,"VR has distinct left navigation, right asset panel and lower commands canvases");Check(Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsSortMode.None).Length==1&&Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"One XR Origin and EventSystem");Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a=>a.enabled)==1,"One enabled AudioListener");
                foreach(var behaviour in vr.Origin.GetComponentsInChildren<Behaviour>(true))if(behaviour.GetType().Name.Contains("PoseDriver")||behaviour.GetType().Name=="XRController"||behaviour is QuestRayControllerInput)behaviour.enabled=false;
                foreach(var behaviour in Resources.FindObjectsOfTypeAll<Behaviour>())if(behaviour!=null&&behaviour.gameObject.scene.IsValid()&&(behaviour.GetType().Name=="XRDeviceSimulator"||behaviour.GetType().Name=="XRInteractionSimulator"))behaviour.enabled=false;
                ConfigureRay(vr.Origin.GetComponentsInChildren<XRRayInteractor>().First(r=>r.name.Contains("Left")));Aim((RectTransform)ui.detailButton.transform);return WaitFor(.7,2);
            case 2:Check(testRay.TryGetCurrentUIRaycastResult(out var hit)&&hit.gameObject.name==ui.detailButton.name,"Actual XR ray reaches relocated command button");Press(true);return WaitFor(.15,3);
            case 3:Press(false);return WaitFor(.5,4);
            case 4:
                Check(c.Level==1,"Trigger click opens detail through XR UI module");c.Locate(bolt);return WaitFor(.5,5);
            case 5:
                ConfigureRay(vr.Origin.GetComponentsInChildren<XRRayInteractor>().First(r=>r.name.Contains("Right")));c.Select(block+"-"+c.catalog.parts[0].key);return WaitFor(.5,6);
            case 6:var center=c.Visible(bolt).GetComponent<Renderer>().bounds.center;var origin=testRay.rayOriginTransform??testRay.transform;origin.position=center+Vector3.up*.3f;origin.rotation=Quaternion.LookRotation(Vector3.down);return WaitFor(.4,61);
            case 61:Press(true);return WaitFor(.15,7);
            case 7:Press(false);return WaitFor(.5,8);
            case 8:
                Check(c.SelectedID==bolt&&c.outline.HighlightedRenderers==1,"Right trigger physics selection and outline still work with auto focus");
                testLeft=InputSystem.AddDevice<XRSimulatedController>();InputSystem.SetDeviceUsage(testLeft,UnityEngine.InputSystem.CommonUsages.LeftHand);testRight=InputSystem.AddDevice<XRSimulatedController>();InputSystem.SetDeviceUsage(testRight,UnityEngine.InputSystem.CommonUsages.RightHand);
                selectedBefore=c.SelectedID;positionBefore=vr.Origin.transform.position;originalSaveEvent=ui.saveButton.onClick;ui.saveButton.onClick=new Button.ButtonClickedEvent();forbiddenSubmit=0;ui.saveButton.onClick.AddListener(()=>forbiddenSubmit++);EventSystem.current.SetSelectedGameObject(ui.saveButton.gameObject);Send(testRight,allRight:true);return WaitFor(.6,9);
            case 9:
                Check(c.Level==2&&c.SelectedID==selectedBefore&&(vr.Origin.transform.position-positionBefore).magnitude<.001f&&forbiddenSubmit==0,"A/B/right grip do not focus, go back, enter detail or submit a selected UI button");ui.saveButton.onClick=originalSaveEvent;Send(testRight);Send(testLeft,ControllerButton.PrimaryButton);return WaitFor(.5,10);
            case 10:
                Check(!vr.PanelVisible&&c.SelectedID==selectedBefore,"Left X hides all management panels without losing selection");localBefore=vr.Origin.Camera.transform.localPosition;positionBefore=vr.Origin.transform.position;Send(testLeft,axis:Vector2.down);return WaitFor(.7,11);
            case 11:
                Send(testLeft);Check((vr.Origin.transform.position-positionBefore).magnitude>.02f&&Mathf.Abs(vr.Origin.transform.position.y-positionBefore.y)<.001f,"Left joystick moves origin horizontally at limited speed while UI hidden");Check((vr.Origin.Camera.transform.localPosition-localBefore).magnitude<.001f,"Locomotion does not write tracked local HMD pose");rotationBefore=vr.Origin.transform.rotation;Send(testRight,axis:Vector2.right);return WaitFor(.5,12);
            case 12:Check(Mathf.Abs(Quaternion.Angle(rotationBefore,vr.Origin.transform.rotation)-vr.snapTurnDegrees)<.2f,"Right joystick performs one snap turn");rotationBefore=vr.Origin.transform.rotation;return WaitFor(.4,13);
            case 13:Check(Quaternion.Angle(rotationBefore,vr.Origin.transform.rotation)<.1f,"Held stick does not repeatedly turn without rearming");Send(testRight);positionBefore=vr.Origin.Camera.transform.position;Send(testLeft,ControllerButton.PrimaryButton);return WaitFor(.5,14);
            case 14:
                Check(vr.PanelVisible&&(vr.Origin.Camera.transform.position-positionBefore).magnitude<.001f&&c.SelectedID==selectedBefore,"Showing UI recenters panels without teleporting HMD or changing selection");Send(testLeft);vr.ToggleSidebar(false);Check(vr.LeftCollapsed&&!ui.leftBody.activeSelf,"LeftNav collapses independently");vr.ToggleSidebar(false);vr.ToggleSidebar(true);Check(vr.AssetCollapsed&&!ui.assetBody.activeSelf,"Asset panel collapses independently");vr.ToggleSidebar(true);
                Send(testLeft,ControllerButton.SecondaryButton);return WaitFor(.5,15);
            case 15:Check(Object.FindFirstObjectByType<SceneQuickMenu>().IsMenuOpen,"Left Y opens existing Scene Menu");Send(testLeft);return WaitFor(.3,16);
            case 16:Send(testLeft,ControllerButton.SecondaryButton);return WaitFor(.4,17);
            case 17:
                Check(!Object.FindFirstObjectByType<SceneQuickMenu>().IsMenuOpen,"Left Y closes menu");Send(testLeft);c.SetOverview();ConfigureRay(vr.Origin.GetComponentsInChildren<XRRayInteractor>().First(r=>r.name.Contains("Left")));return WaitFor(.8,18);
            case 18:
                var scroll=ui.listContent.GetComponentInParent<ScrollRect>();scroll.verticalNormalizedPosition=1;Aim(scroll.viewport);scrollBefore=scroll.verticalNormalizedPosition;positionBefore=vr.Origin.transform.position;Send(testLeft,axis:Vector2.down);return WaitFor(.8,19);
            case 19:
                results.Add("Scroll diagnostics: before="+scrollBefore+" after="+ui.listContent.GetComponentInParent<ScrollRect>().verticalNormalizedPosition+" reader="+testRay.uiScrollInput.ReadValue()+" device="+testLeft.primary2DAxis.ReadValue()+" hit="+(testRay.TryGetCurrentUIRaycastResult(out var scrollHit)?scrollHit.gameObject.name:"none"));
                Send(testLeft);Check(Mathf.Abs(ui.listContent.GetComponentInParent<ScrollRect>().verticalNormalizedPosition-scrollBefore)>.0001f,"Joystick scrolls LeftNav through actual XR UI input");Check((vr.Origin.transform.position-positionBefore).magnitude<.001f,"UI scroll does not also locomote");Aim((RectTransform)ui.search.transform);return WaitFor(.5,20);
            case 20:Press(true);return WaitFor(.15,21);
            case 21:Press(false);return WaitFor(.6,22);
            case 22:
                Check(vr.GetComponent<RailwayBimVrKeyboard>().IsOpen,"Relocated VR search opens independent world-space Vietnamese keyboard");var key=vr.GetComponentsInChildren<Button>().Single(b=>b.name=="Phím ă");Aim((RectTransform)key.transform);return WaitFor(.5,23);
            case 23:Press(true);return WaitFor(.15,24);
            case 24:Press(false);return WaitFor(.5,25);
            case 25:
                Check(ui.search.text=="ă","Actual ray types Vietnamese key despite input losing focus");ui.search.text="";vr.GetComponent<RailwayBimVrKeyboard>().Close();ScreenCapture.CaptureScreenshot("Temp/BimRefinement/VR_SpatialPanels.png");return WaitFor(.5,26);
            case 26:return true;
        }
        return false;
    }
}
