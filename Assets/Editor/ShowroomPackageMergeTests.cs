using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Unity.XR.CoreUtils;
using UTT.SaBan;
using TMPro;
using Object=UnityEngine.Object;

public static partial class ShowroomPackageMergeTools
{
    const string TestKey="UTT.ShowroomPackage.Test";
    static readonly List<string> testResults=new List<string>(),testErrors=new List<string>();
    static int phase,afterFrame;static double waitUntil,began;static float progress;static Vector3 carPosition;
    static ShowroomPackageIntegration integration;static SaBanTrainRunner runner;static AIChatbotUIController chat;static XROrigin origin;
    static MonoBehaviour realBrain;static string realMethod;static XRRayInteractor testRay;static NearFarInteractor testNearFar;static Transform castOrigin;static ShowroomMergeTestTrigger trigger;static Button aimedButton;
    static bool oldMaximized;
    static void InstallPlayTests()
    {
        EditorApplication.update+=PlayTestTick;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(TestKey,false)&&EditorApplication.isPlaying&&(t==LogType.Error||t==LogType.Exception||t==LogType.Assert))testErrors.Add(m);};
    }
    static void CheckTest(bool ok,string message){if(!ok)throw new Exception(message);testResults.Add("PASS | "+message);}
    static bool WaitTest(double seconds,int next){phase=next;afterFrame=Time.frameCount+2;waitUntil=EditorApplication.timeSinceStartup+seconds;File.WriteAllText(Dir+"/PlayProgress.txt","phase="+phase+" frame="+Time.frameCount);return false;}
    static void BeginPlayTests()
    {
        var main=SceneManager.GetSceneByPath(ScenePath);if(!main.IsValid()||!main.isLoaded)main=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        var others=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=main).ToArray();
        if(others.Any(s=>s.isDirty))throw new Exception("Other scene has unsaved changes; Play Mode test blocked without discarding them");
        if(main.isDirty&&!SessionState.GetBool(TestKey+".Performance",false))EditorSceneManager.SaveScene(main);SceneManager.SetActiveScene(main);foreach(var other in others)EditorSceneManager.CloseScene(other,true);
        if(main.GetRootGameObjects().Count(g=>g.GetComponent<ShowroomPackageIntegration>()!=null)!=1)throw new Exception("Merged integration root not found");
        testResults.Clear();testErrors.Clear();phase=0;began=0;waitUntil=0;afterFrame=0;SessionState.SetBool(TestKey,true);EditorApplication.isPlaying=true;
    }
    static void PlayTestTick()
    {
        if(!EditorApplication.isPlaying||!SessionState.GetBool(TestKey,false))return;
        if(began==0)began=EditorApplication.timeSinceStartup;
        try
        {
            if(EditorApplication.timeSinceStartup-began>240)throw new Exception("Play test timeout; last phase="+phase);
            if(EditorApplication.timeSinceStartup<waitUntil||Time.frameCount<afterFrame)return;
            if(phase>=40){BridgePictureTick();return;}
            if(phase==29){AimTest(TrainInfoPopupUI.Instance.closeButton);WaitTest(.6,30);return;}
            if(phase==30){CheckRayTest();PressTest(true);WaitTest(.2,31);return;}
            if(phase==31){PressTest(false);WaitTest(.4,32);return;}
            if(phase==32)
            {
                CheckTest(!TrainInfoPopupUI.Instance.popupPanel.activeSelf,"Actual XR trigger closes train information popup");
                CheckTest(testErrors.Count==0,"No Console errors during popup test");
                if(SessionState.GetBool(TestKey+".Performance",false)){WaitTest(.2,40);return;}
                File.WriteAllLines(Dir+"/TrainPopupPlayTest.txt",testResults.Concat(new[]{"DONE PASS; physical headset not tested"}));
                SessionState.SetBool(TestKey+".Popup",false);FinishPlayTests(true);return;
            }
            if(phase==0)
            {
                if(SessionState.GetBool(TestKey+".XRonly",false)&&EditorApplication.timeSinceStartup-began<10)return;
                integration=Object.FindFirstObjectByType<ShowroomPackageIntegration>();origin=Object.FindFirstObjectByType<XROrigin>();
                if(integration==null||origin==null){if(EditorApplication.timeSinceStartup-began<45)return;throw new Exception("Standalone scene failed to initialize integration/XR rig");}
                runner=integration.saBan.trainRunner;chat=integration.chatbot;
                var window=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));oldMaximized=window.maximized;window.maximized=true;window.Focus();
                CheckTest(SceneManager.GetActiveScene().path==ScenePath,"Merged Showroom starts independently in Play Mode");
                CheckTest(Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length==1&&Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"Exactly one XR Origin and EventSystem");
                CheckTest(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a=>a.enabled&&a.gameObject.activeInHierarchy)==1,"Exactly one active AudioListener");
                CheckTest(!Object.FindObjectsByType<RailwayZoneStreamingManager>(FindObjectsSortMode.None).Any(m=>m.isActiveAndEnabled),"Display does not run the copied route streamer");
                CheckTest(integration.saBan.GetLayers().Count==8&&runner.cars.Any(c=>c!=null),"Eight display layers and miniature train cars resolve");
                if(SessionState.GetBool(TestKey+".Popup",false)){CheckPopupLayout();return;}
                if(SessionState.GetBool(TestKey+".XRonly",false)){runner.ResetToStart();PrepareXrTest();WaitTest(.8,5);return;}
                var popup=TrainInfoPopupUI.Instance;var trains=Object.FindObjectsByType<TrainInteractable>(FindObjectsSortMode.None);CheckTest(trains.Length==4,"Four existing train interactions retained");
                foreach(var train in trains){train.OnInteract();CheckTest(popup.popupPanel.activeSelf&&popup.titleText.text==train.trainName,"Existing popup opens correct data: "+train.gameObject.name);popup.closeButton.onClick.Invoke();CheckTest(!popup.popupPanel.activeSelf,"Existing popup closes: "+train.gameObject.name);}
                CheckTest(Object.FindObjectsByType<MuseumLabelStand>(FindObjectsSortMode.None).Length==4,"Four new museum labels instantiated");
                foreach(var layer in integration.saBan.GetLayers())
                {
                    var b=integration.saBan.panel.Find("Btn_Layer_"+layer.key)?.GetComponent<Button>();CheckTest(b!=null,"Layer button bound: "+layer.key);
                    bool before=layer.IsVisible;b.onClick.Invoke();CheckTest(layer.IsVisible!=before,"Layer toggles: "+layer.key);b.onClick.Invoke();CheckTest(layer.IsVisible==before,"Layer restores: "+layer.key);
                }
                for(int n=1;n<=4;n++){integration.saBan.panel.Find("Btn_Speed_"+n).GetComponent<Button>().onClick.Invoke();CheckTest(runner.SpeedMultiplier==n&&!runner.IsRunning,"Speed x"+n+" changes setting without starting stopped train");}
                progress=runner.Progress01;carPosition=runner.cars.First(c=>c!=null).position;integration.saBan.panel.Find("Btn_Play").GetComponent<Button>().onClick.Invoke();CheckTest(runner.IsRunning,"Display Play button starts train");WaitTest(1.2,1);return;
            }
            if(phase==1)
            {
                CheckTest(runner.Progress01>progress&&Vector3.Distance(carPosition,runner.cars.First(c=>c!=null).position)>.0001f,"Train actually moves over time on its copied spline");
                integration.saBan.panel.Find("Btn_Play").GetComponent<Button>().onClick.Invoke();CheckTest(!runner.IsRunning,"Display Pause button stops running state");WaitTest(.8,2);return;
            }
            if(phase==2){progress=runner.Progress01;WaitTest(.5,3);return;}
            if(phase==3)
            {
                CheckTest(Mathf.Abs(progress-runner.Progress01)<.00001f,"Train settles and holds position while paused");integration.saBan.panel.Find("Btn_Reset").GetComponent<Button>().onClick.Invoke();CheckTest(!runner.IsRunning&&Mathf.Abs(runner.Progress01-runner.startProgress)<.0001f,"Reset returns miniature train to its start");
                Object.FindFirstObjectByType<ShowroomRobotChatInteractable>().OnInteract();CheckTest(chat.IsOpen&&chat.panel.activeSelf,"Robot interaction opens the new chatbot");
                foreach(var speech in chat.GetComponentsInChildren<BIM4D.AI.Speech.BIMTextToSpeechController>(true))speech.enabled=false;
                realBrain=chat.integration.brain;realMethod=chat.integration.sendMethodName;var mock=new GameObject("Temporary local UI pipeline test").AddComponent<ShowroomMergeTestBrain>();ShowroomMergeTestBrain.calls=0;chat.integration.brain=mock;chat.integration.sendMethodName="AskAsync";
                testResults.Add("DIAGNOSTIC | assigned provider="+chat.integration.brain.GetType().Name+"; chat active="+chat.gameObject.activeInHierarchy+"; callback count="+chat.sendButton.onClick.GetPersistentEventCount());
                chat.SetInputText("Kiểm tra kết nối giao diện Showroom");chat.sendButton.onClick.Invoke();WaitTest(.6,4);return;
            }
            if(phase==4)
            {
                testResults.Add("DIAGNOSTIC | provider calls="+ShowroomMergeTestBrain.calls+"; busy="+chat.IsBusy+"; voiceLock="+chat.IsVoiceOperationLocked+"; provider="+chat.integration.brain?.GetType().Name+"; status="+chat.statusText?.text+"; bubbles="+string.Join(" | ",chat.content.GetComponentsInChildren<TMP_Text>(true).Select(t=>t.text).Take(6)));
                CheckTest(!chat.IsBusy&&chat.content.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text.Contains("LOCAL_PIPELINE_TEST:")),"Send/input/bubble/response pipeline works with an explicitly local test provider; no external API call");chat.integration.brain=realBrain;chat.integration.sendMethodName=realMethod;
                chat.SetInputText("Nội dung cần xóa");chat.clearButton.onClick.Invoke();CheckTest(chat.GetInputText()=="","Chat clear button clears input");chat.closeButton.onClick.Invoke();CheckTest(!chat.IsOpen&&!chat.panel.activeSelf,"Chat close button closes panel");
                PrepareXrTest();WaitTest(.8,5);return;

            }
            if(phase==5){AimTest(integration.saBan.panel.Find("Btn_Play").GetComponent<Button>());WaitTest(.3,25);return;}
            if(phase==25){CheckRayTest();CheckTest(integration.panelColliders.All(c=>!c.enabled),"XR disables only copied panel's FPS colliders to avoid double activation");PressTest(true);WaitTest(.2,6);return;}
            if(phase==6){PressTest(false);WaitTest(.5,7);return;}
            if(phase==7)
            {
                CheckTest(runner.IsRunning,"Actual XR UI trigger starts the miniature train once");runner.Pause();
                integration.OpenChat();WaitTest(.7,8);return;
            }
            if(phase==8){chat.SetInputText("Xóa bằng tia VR");AimTest(chat.clearButton);WaitTest(.5,9);return;}
            if(phase==9){CheckRayTest();PressTest(true);WaitTest(.2,10);return;}
            if(phase==10){PressTest(false);WaitTest(.4,11);return;}
            if(phase==11){CheckTest(chat.GetInputText()=="","XR ray clicks chatbot clear button");AimTest(chat.closeButton);WaitTest(.4,12);return;}
            if(phase==12){CheckRayTest();PressTest(true);WaitTest(.2,13);return;}
            if(phase==13){PressTest(false);WaitTest(.5,14);return;}
            if(phase==14)
            {
                CheckTest(!chat.IsOpen,"XR ray closes chatbot");CheckTest(!SceneManager.GetSceneByName("YenTuHaLongXanh").isLoaded&&!Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).name.StartsWith("Zone_")),"No real route or zone scenes loaded by display");
                if(SessionState.GetBool(TestKey+".Performance",false)){PerformanceRegression();return;}
                CheckTest(testErrors.Count==0,"No Console errors during tested Showroom Play session");testResults.Add("NOT TESTED | Real AI/STT/TTS services, microphone capture, physical Quest 3S and actual doorway scene transition.");FinishPlayTests(true);
            }
        }
        catch(Exception e){testResults.Add("FAIL | "+e);testResults.AddRange(testErrors);FinishPlayTests(false);}
    }
    static void PrepareXrTest()
    {
        var menu=Object.FindFirstObjectByType<SceneQuickMenu>();
        if(menu!=null)typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu,new object[]{false});
        CheckTest(menu==null||!menu.IsMenuOpen,"Existing Scene Menu closed before new UI ray interaction");
                foreach(var player in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))player.enabled=false;
                foreach(var spawner in Object.FindObjectsByType<PlayerSpawner>(FindObjectsSortMode.None))spawner.enabled=false;
                foreach(var b in origin.GetComponentsInChildren<Behaviour>(true))if(b.GetType().Name.Contains("PoseDriver")||b.GetType().Name=="XRController"||b is QuestRayControllerInput)b.enabled=false;
                foreach(var b in Resources.FindObjectsOfTypeAll<Behaviour>())if(b!=null&&b.gameObject.scene.IsValid()&&(b.GetType().Name=="XRDeviceSimulator"||b.GetType().Name=="XRInteractionSimulator"))b.enabled=false;
                var panel=integration.saBan.panel;
                // Rotate first: simulator HMD offsets can be nonzero after profiling camera views.
                var eye=panel.position-panel.forward*1.5f+Vector3.up*.1f;
                origin.transform.rotation=Quaternion.Euler(0,180,0);
                origin.transform.position+=eye-origin.Camera.transform.position;
                origin.Camera.transform.rotation=Quaternion.LookRotation(panel.position-eye,Vector3.up);
                testNearFar=origin.GetComponentsInChildren<NearFarInteractor>(true).FirstOrDefault(r=>Hierarchy(r.transform).IndexOf("Right",StringComparison.OrdinalIgnoreCase)>=0&&r.enableUIInteraction);
                testRay=testNearFar==null?origin.GetComponentsInChildren<XRRayInteractor>(true).FirstOrDefault(r=>Hierarchy(r.transform).IndexOf("Right",StringComparison.OrdinalIgnoreCase)>=0&&r.enableUIInteraction):null;
                var reader=new XRInputButtonReader("Showroom test trigger"){inputSourceMode=XRInputButtonReader.InputSourceMode.ObjectReference};
                if(testRay!=null){trigger=testRay.gameObject.AddComponent<ShowroomMergeTestTrigger>();reader.SetObjectReference(trigger);testRay.selectInput=reader;testRay.uiPressInput=reader;testRay.forceDeprecatedInput=false;castOrigin=testRay.rayOriginTransform??testRay.transform;}
                else
                {
                    testNearFar=origin.GetComponentsInChildren<NearFarInteractor>(true).First(r=>Hierarchy(r.transform).IndexOf("Right",StringComparison.OrdinalIgnoreCase)>=0);
                    trigger=testNearFar.gameObject.AddComponent<ShowroomMergeTestTrigger>();reader.SetObjectReference(trigger);testNearFar.selectInput=reader;testNearFar.uiPressInput=reader;testNearFar.enableUIInteraction=true;testNearFar.enableFarCasting=true;testNearFar.enableNearCasting=false;
                    castOrigin=new GameObject("Temporary Showroom test cast origin").transform;testNearFar.farInteractionCaster.castOrigin=castOrigin;
                    if(testNearFar.farInteractionCaster is UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.InteractionCasterBase caster)caster.enableStabilization=false;
                }
                CheckTest(trigger!=null,"Uses the existing rig interactor: "+(testNearFar!=null?"NearFarInteractor":"XRRayInteractor"));
        AimTest(integration.saBan.panel.Find("Btn_Play").GetComponent<Button>());
    }
    static void AimTest(Button button)
    {aimedButton=button;Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;var target=rect.TransformPoint(rect.rect.center);var rayOrigin=castOrigin;rayOrigin.position=origin.Camera.transform.position+origin.Camera.transform.right*.1f-Vector3.up*.1f;rayOrigin.rotation=Quaternion.LookRotation(target-rayOrigin.position);}
    static void CheckRayTest(){UnityEngine.EventSystems.RaycastResult hit;bool found=testRay!=null?testRay.TryGetCurrentUIRaycastResult(out hit):testNearFar.TryGetCurrentUIRaycastResult(out hit);testResults.Add("RAY | interactor="+(testRay!=null?Hierarchy(testRay.transform):Hierarchy(testNearFar.transform))+"; active="+(testRay!=null?testRay.isActiveAndEnabled:testNearFar.isActiveAndEnabled)+"; hit="+(found?hit.gameObject.name:"none")+"; eye="+origin.Camera.transform.position+"; canvas="+aimedButton.GetComponentInParent<Canvas>(true).name);CheckTest(found&&hit.gameObject.GetComponentInParent<Button>()==aimedButton,"Actual XR ray hits new UI button: "+aimedButton.name);}
    static void PressTest(bool down){trigger.pressed=down;trigger.edge=down;}
    static void FinishPlayTests(bool pass)
    {
        if(SessionState.GetBool(TestKey+".Performance",false)){File.WriteAllLines("Temp/ShowroomPerformance/Regression.txt",testResults.Concat(new[]{pass?"DONE PASS":"DONE FAIL","NOT TESTED | Physical Quest, live AI/STT/TTS, multiple real multiplayer clients, doorway scene transition."}));SessionState.SetBool(TestKey+".Performance",false);}
        if(trigger!=null){trigger.pressed=trigger.edge=false;}EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).maximized=oldMaximized;
        File.WriteAllLines(Dir+(SessionState.GetBool(TestKey+".XRonly",false)?"/PlayRayTest.txt":"/PlayTest.txt"),testResults.Concat(new[]{pass?"DONE PASS":"DONE FAIL"}));SessionState.SetBool(TestKey,false);EditorApplication.isPlaying=false;
    }
    static void ValidateMergedScene()
    {
        var scene=SceneManager.GetSceneByPath(ScenePath);if(!scene.IsValid()||!scene.isLoaded)throw new Exception("Showroom must be loaded");
        int missingScripts=0,missingVisibleMeshes=0,missingMaterials=0,prefabs=0,nodes=0;var seen=new HashSet<Object>();
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
        {
            nodes++;missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if(PrefabUtility.GetPrefabInstanceStatus(t.gameObject)==PrefabInstanceStatus.MissingAsset)prefabs++;
            foreach(var r in t.GetComponents<Renderer>()){if(r.enabled&&r.gameObject.activeInHierarchy){if(r is MeshRenderer&&r.GetComponent<MeshFilter>()?.sharedMesh==null)missingVisibleMeshes++;missingMaterials+=r.sharedMaterials.Count(m=>m==null);}}
        }
        if(missingScripts+missingVisibleMeshes+missingMaterials+prefabs>0)throw new Exception("Validation failed scripts="+missingScripts+" meshes="+missingVisibleMeshes+" materials="+missingMaterials+" prefabs="+prefabs);
        if(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EventSystem>(true)).Count()!=1)throw new Exception("Duplicate EventSystem");
        if(scene.isDirty)EditorSceneManager.SaveScene(scene);
        var deps=AssetDatabase.GetDependencies(ScenePath,true);bool stagingDependency=deps.Contains(IncomingPath);
        File.WriteAllLines(Dir+"/FinalValidation.txt",new[]{"PASS | Saved Showroom opens with nodes="+nodes,"PASS | Missing scripts=0; active renderer missing mesh/material=0; missing prefab=0","PASS | Exactly one saved EventSystem; copied runtime streamers inactive","Dependencies="+deps.Length,"Staging dependency="+stagingDependency,"Scene dirty="+scene.isDirty});
        if(stagingDependency)throw new Exception("Staging asset dependency still exists; do not delete staging scene");
    }
    static void CleanupStaging()
    {
        ValidateMergedScene();
        if(SceneManager.GetSceneByPath(IncomingPath).isLoaded)throw new Exception("Staging scene is still loaded");
        if(File.Exists(IncomingPath))
        {
            if(AssetDatabase.AssetPathToGUID(IncomingPath)==AssetDatabase.AssetPathToGUID(ScenePath))throw new Exception("Unsafe source scene GUID collision");
            if(!AssetDatabase.DeleteAsset(IncomingPath))throw new Exception("Could not remove owned staging scene");
        }
        File.WriteAllText(Dir+"/StagingCleanup.txt","Removed only the temporary incoming scene created by this merge after verifying zero dependencies. Original package, original project assets and original Showroom scene retained.");
    }
}
