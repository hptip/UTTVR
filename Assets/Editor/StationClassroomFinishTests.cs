using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using ProjectLopHoc.Features.Lecture;
using ProjectLopHoc.UI;
using ProjectLopHoc.Core.ScriptableObjects;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static partial class StationClassroomFinish
{
    const string TestKey="UTT.StationClassroomFinish.Test";
    static readonly List<string> TestRows=new List<string>(),TestErrors=new List<string>();
    static int phase;static double start,wait;static int waitFrame;
    static LectureBoardController testBoard;static StudentSlideReviewUI studentUI;static TeacherLecturePanelController teacherUI;
    static ClassroomConfigSO originalConfig;static Sprite firstSprite,secondSprite;static int imageCount;
    static StationClassroomFinish(){EditorApplication.update+=TestTick;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(TestKey,false)&&EditorApplication.isPlaying&&(t==LogType.Exception||t==LogType.Error))TestErrors.Add(m);};}
    public static void FixFonts()
    {
        Guard();var scene=SceneManager.GetSceneByPath(Classroom);var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/Roboto Vietnamese SDF.asset");
        if(font==null||!font.HasCharacters("Bài giảng trướcBài giảng sau",out uint[] missing,true,false))throw new Exception("Existing Vietnamese font does not cover labels");
        foreach(var panel in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.Trim()=="StudentSlidePanel"||t.name.Trim()=="SlideControlPanel"))
            foreach(string name in new[]{"PreviousLessonButton","NextLessonButton","SlideName","SlideCounter"})
            {
                var text=panel.Find(name).GetComponentInChildren<TMP_Text>(true);Undo.RecordObject(text,"Use existing Vietnamese font");text.font=font;if(name.EndsWith("Button"))text.fontStyle=FontStyles.Bold;EditorUtility.SetDirty(text);
            }
        Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    public static string BeginTests()
    {
        Guard();for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved open scene: "+SceneManager.GetSceneAt(i).path);
        var classroom=SceneManager.GetSceneByPath(Classroom);if(!classroom.isLoaded)classroom=EditorSceneManager.OpenScene(Classroom,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(classroom);
        foreach(var s in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s!=classroom).ToArray())EditorSceneManager.CloseScene(s,true);
        var window=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));SessionState.SetBool(TestKey+".Max",window.maximized);window.maximized=true;window.Focus();
        TestRows.Clear();TestErrors.Clear();phase=0;start=wait=0;SessionState.SetBool(TestKey,true);EditorApplication.isPlaying=true;return "Play tests started; two real lessons only";
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);TestRows.Add("PASS | "+message);}
    static void Delay(int next){phase=next;wait=EditorApplication.timeSinceStartup+.3;waitFrame=Time.frameCount+3;File.WriteAllText(Dir+"/TestProgress.txt","phase="+phase);}
    static void Click(Button button)
    {
        Check(button!=null&&button.IsActive()&&button.IsInteractable(),"Button enabled: "+button.transform.parent.name+"/"+button.name);
        ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
    }
    static Button ButtonAt(Transform panel,string name)=>panel.Find(name).GetComponent<Button>();
    static void MouseRaycastClick(Button button)
    {
        Canvas.ForceUpdateCanvases();var canvas=button.GetComponentInParent<Canvas>();var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"Mouse raycast reaches unobstructed lesson button: "+button.transform.parent.name+"/"+button.name);
        ExecuteEvents.Execute(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static void Synchronized(int lecture,int slide)
    {
        Check(testBoard.DisplayLectureIndex==lecture&&testBoard.DisplaySlideIndex==slide,"Common lesson/slide state="+lecture+"/"+slide);
        Check(studentUI.slideImageDisplay.sprite==testBoard.DisplaySprite&&teacherUI.transform.Find("SlideDisplay").GetComponent<Image>().sprite==testBoard.DisplaySprite,"Both panel previews use exact same current Sprite");
        Check(studentUI.lectureDropdown.value==lecture&&teacherUI.transform.Find("LectureDropdown").GetComponent<TMP_Dropdown>().value==lecture,"Both dropdowns reflect common lesson");
        Check(studentUI.lectureTitleText.text.Contains(testBoard.DisplayLectureName)&&teacherUI.transform.Find("SlideName").GetComponent<TMP_Text>().text.Contains(testBoard.DisplayLectureName),"Both lesson titles updated");
        var block=new MaterialPropertyBlock();testBoard.boardRenderer.GetPropertyBlock(block);Check(block.GetTexture(Shader.PropertyToID("_BaseMap"))==testBoard.DisplaySprite.texture,"Projection board texture matches new lesson; old content not retained");
    }
    static void TestTick()
    {
        if(!EditorApplication.isPlaying||!SessionState.GetBool(TestKey,false))return;
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(start==0)start=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-start>180)throw new Exception("Test timeout");
            if(EditorApplication.timeSinceStartup<wait||Time.frameCount<waitFrame)return;
            if(phase==0)
            {
                if(EditorApplication.timeSinceStartup-start<4||Time.frameCount<40)return;
                testBoard=Object.FindFirstObjectByType<LectureBoardController>(FindObjectsInactive.Include);studentUI=Object.FindFirstObjectByType<StudentSlideReviewUI>(FindObjectsInactive.Include);teacherUI=Object.FindFirstObjectByType<TeacherLecturePanelController>(FindObjectsInactive.Include);
                if(testBoard==null||studentUI==null||teacherUI==null)return;
                originalConfig=testBoard.classroomConfig;studentUI.gameObject.SetActive(true);teacherUI.gameObject.SetActive(true);testBoard.SelectLecture(0);imageCount=Object.FindObjectsByType<Image>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;Delay(1);return;
            }
            if(phase==1)
            {
                Check(testBoard.LectureCount==2,"Using two actual configured lessons, no synthetic third lesson");Check(testBoard.SlideCount==10,"Lesson 1 has ten actual slides");Synchronized(0,0);firstSprite=testBoard.DisplaySprite;
                Check(!studentUI.previousLessonButton.interactable&&!teacherUI.previousLessonButton.interactable,"Previous lesson disabled on first lesson in both panels");Click(studentUI.nextLessonButton);Delay(2);return;
            }
            if(phase==2)
            {
                Check(testBoard.SlideCount==10,"Lesson 2 texture assets load all ten slides without changing importers");Synchronized(1,0);secondSprite=testBoard.DisplaySprite;
                Check(firstSprite!=secondSprite,"Changing lesson changes the actual image");Check(!studentUI.nextLessonButton.interactable&&!teacherUI.nextLessonButton.interactable,"Next lesson disabled on final lesson in both panels");
                testBoard.NextLecture();Check(testBoard.DisplayLectureIndex==1,"No lesson wrap at end");Click(studentUI.previousLessonButton);Delay(3);return;
            }
            if(phase==3){Synchronized(0,0);Click(teacherUI.nextLessonButton);Delay(4);return;}
            if(phase==4){Synchronized(1,0);Click(teacherUI.previousLessonButton);Delay(5);return;}
            if(phase==5){Synchronized(0,0);Click(ButtonAt(studentUI.transform,"SlideNext"));Delay(6);return;}
            if(phase==6){Synchronized(0,1);Click(ButtonAt(teacherUI.transform,"SlidePrev"));Delay(7);return;}
            if(phase==7){Synchronized(0,0);studentUI.lectureDropdown.value=1;Delay(8);return;}
            if(phase==8){Synchronized(1,0);teacherUI.transform.Find("LectureDropdown").GetComponent<TMP_Dropdown>().value=0;Delay(9);return;}
            if(phase==9){Synchronized(0,0);Check(testBoard.DisplaySprite==firstSprite,"Returning to lesson reuses cached Sprite");testBoard.SelectLecture(1);Delay(10);return;}
            if(phase==10)
            {
                Synchronized(1,0);Check(testBoard.DisplaySprite==secondSprite,"Texture-backed lesson also reuses cached Sprite, no repeated allocation");
                Check(Object.FindObjectsByType<Image>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==imageCount,"No duplicate Image objects created by navigation");
                testBoard.SelectLecture(-1);testBoard.SelectLecture(2);Check(testBoard.DisplayLectureIndex==1,"Out of range lesson selection is safe");
                testBoard.classroomConfig=null;testBoard.RefreshPresentation();Delay(11);return;
            }
            if(phase==11)
            {
                Check(testBoard.LectureCount==0&&testBoard.DisplaySprite==null,"Null/empty configuration handled without stale slide");
                Check(studentUI.slideImageDisplay.sprite==null&&!studentUI.slideImageDisplay.enabled&&teacherUI.transform.Find("SlideDisplay").GetComponent<Image>().sprite==null,"Both previews clear previous image");
                Check(!studentUI.nextLessonButton.interactable&&!studentUI.previousLessonButton.interactable&&!teacherUI.nextLessonButton.interactable&&!teacherUI.previousLessonButton.interactable,"All lesson buttons disabled with no lessons");
                testBoard.classroomConfig=AssetDatabase.LoadAssetAtPath<ClassroomConfigSO>("Assets/Room101_Config.asset");testBoard.SelectLecture(0);Delay(12);return;
            }
            if(phase==12)
            {
                Check(testBoard.LectureCount==1&&!studentUI.nextLessonButton.interactable&&!studentUI.previousLessonButton.interactable&&!teacherUI.nextLessonButton.interactable&&!teacherUI.previousLessonButton.interactable,"Single existing lesson configuration disables both directions");
                testBoard.classroomConfig=originalConfig;testBoard.SelectLecture(0);Delay(13);return;
            }
            if(phase==13)
            {
                Synchronized(0,0);
                foreach(var panel in new[]{studentUI.transform,teacherUI.transform})foreach(string name in new[]{"PreviousLessonButton","NextLessonButton"})
                {var button=ButtonAt(panel,name);var text=button.GetComponentInChildren<TMP_Text>();Check(text.font.HasCharacters(text.text,out uint[] missing,true,false),"Vietnamese glyph coverage: "+panel.name+"/"+name);Check(button.GetComponent<Image>().raycastTarget,"Button has raycastable graphic: "+name);}
                var canvas=studentUI.GetComponentInParent<Canvas>();Check(canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>()!=null,"Existing tracked-device raycaster retained");
                Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"Exactly one EventSystem");
                var menu=Object.FindFirstObjectByType<SceneQuickMenu>();if(menu!=null)typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(menu,new object[]{false});
                teacherUI.gameObject.SetActive(false);studentUI.gameObject.SetActive(true);Delay(14);return;
            }
            if(phase==14){MouseRaycastClick(studentUI.nextLessonButton);Delay(15);return;}
            if(phase==15){Check(testBoard.DisplayLectureIndex==1,"Student mouse click changes actual lesson");studentUI.gameObject.SetActive(false);teacherUI.gameObject.SetActive(true);Delay(16);return;}
            if(phase==16){MouseRaycastClick(teacherUI.previousLessonButton);Delay(17);return;}
            if(phase==17)
            {
                studentUI.gameObject.SetActive(true);Synchronized(0,0);Check(TestErrors.Count==0,"No new Console errors during lesson tests");
                File.WriteAllLines(Dir+"/PlayTests.txt",TestRows.Concat(new[]{"DONE PASS","NOT TESTED: lesson 3 (only two exist), genuinely missing-slide folder, real multiplayer clients or physical Quest controller."}));FinishTests();
            }
        }
        catch(Exception e){File.WriteAllLines(Dir+"/PlayTests.txt",TestRows.Concat(new[]{"FAIL | "+e}).Concat(TestErrors));FinishTests();}
    }
    static void FinishTests()
    {
        if(testBoard!=null&&originalConfig!=null)testBoard.classroomConfig=originalConfig;
        SessionState.SetBool(TestKey,false);EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).maximized=SessionState.GetBool(TestKey+".Max",false);EditorApplication.isPlaying=false;
    }
}
