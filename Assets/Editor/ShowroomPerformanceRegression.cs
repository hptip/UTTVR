using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UTT.SaBan;
using Object=UnityEngine.Object;

public static partial class ShowroomPackageMergeTools
{
    static void BridgePictureTick()
    {
        var group=integration.saBan.GetComponentsInChildren<ShowroomInstancedVisualGroup>(true).Single();
        if(phase==40)
        {
            var bridge=integration.saBan.FindLayer("bridge").GetComponentsInChildren<MeshRenderer>(true).First(r=>r.enabled&&r.sharedMaterials.Length>1);
            var b=bridge.bounds;var target=b.center;var position=target+new Vector3(.7f,.65f,1).normalized*Mathf.Max(.4f,b.extents.magnitude*2.2f);
            origin.Camera.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));group.enabled=false;WaitTest(.5,41);return;
        }
        if(phase==41){ScreenCapture.CaptureScreenshot("Temp/ShowroomPerformance/Images_baseline/08_bridge_detail.png");WaitTest(.4,42);return;}
        if(phase==42){group.enabled=true;WaitTest(.5,43);return;}
        if(phase==43){ScreenCapture.CaptureScreenshot("Temp/ShowroomPerformance/Images_after/08_bridge_detail.png");WaitTest(.5,44);return;}
        CheckTest(group.sources.All(r=>!r.enabled),"Restoring GPU instancing after exact bridge A/B images does not duplicate sources");
        CheckTest(testErrors.Count==0,"No Console errors during optimized functional/ray/bridge image test");FinishPlayTests(true);
    }
    static void PerformanceRegression()
    {
        var root=integration.saBan;var groups=root.GetComponentsInChildren<ShowroomInstancedVisualGroup>(true);
        CheckTest(groups.Length==1&&groups[0].sources.Length==49074,"Exactly one optimized existing rail layer; 49,074 original sources retained");
        CheckTest(groups[0].sources.All(r=>r!=null&&!r.enabled),"Instanced sources do not render twice in Play Mode");
        foreach(var item in Object.FindObjectsByType<BIMInteractable>(FindObjectsSortMode.None))
        {item.OnInteract();CheckTest(TrainInfoPopupUI.Instance.popupPanel.activeSelf&&!string.IsNullOrEmpty(TrainInfoPopupUI.Instance.detailsText.text),"BIM component metadata popup: "+item.name);TrainInfoPopupUI.Instance.closeButton.onClick.Invoke();}
        foreach(var board in Object.FindObjectsByType<ObjectInfoBoard>(FindObjectsSortMode.None))CheckTest(GameObject.Find("InfoBoard_"+board.name)!=null,"Floating information board built: "+board.name);
        runner.ResetToStart();root.panel.Find("Btn_Play").GetComponent<Button>().onClick.Invoke();root.panel.Find("Btn_Speed_4").GetComponent<Button>().onClick.Invoke();
        foreach(string key in new[]{"nature","rail","nature","train","rail","train"})
        {var layer=root.FindLayer(key);bool before=layer.IsVisible;var clock=System.Diagnostics.Stopwatch.StartNew();root.panel.Find("Btn_Layer_"+key).GetComponent<Button>().onClick.Invoke();clock.Stop();CheckTest(layer.IsVisible!=before,"Combined running visibility sequence: "+key+"; callback ms="+clock.Elapsed.TotalMilliseconds.ToString("F3"));}
        root.panel.Find("Btn_Speed_1").GetComponent<Button>().onClick.Invoke();CheckTest(runner.IsRunning&&runner.SpeedMultiplier==1,"Reducing speed preserves running state after mixed layer sequence");
        CheckTest(root.GetLayers().All(l=>l.IsVisible),"All eight independent layers restored after sequence");
        CheckTest(groups[0].sources.All(r=>!r.enabled),"Repeated visibility toggles do not re-enable original duplicated renderers");
        runner.Pause();runner.Play();CheckTest(runner.IsRunning,"Train can resume after pause");runner.ResetToStart();
        var menu=Object.FindFirstObjectByType<SceneQuickMenu>();var setter=typeof(SceneQuickMenu).GetMethod("SetMenuOpen",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        setter.Invoke(menu,new object[]{true});CheckTest(menu.IsMenuOpen,"Scene Menu can open");setter.Invoke(menu,new object[]{false});CheckTest(!menu.IsMenuOpen,"Scene Menu can close");
        Object.FindObjectsByType<TrainInteractable>(FindObjectsSortMode.None).First().OnInteract();WaitTest(.8,29);
    }
}
