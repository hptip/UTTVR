using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UTT.SaBan;

public static partial class ShowroomPerformanceAudit
{
    [Serializable] public sealed class ViewPose{public string name;public Vector3 position;public Quaternion rotation;}
    [Serializable] public sealed class ViewPoses{public ViewPose[] views;}
    static ViewPoses views;static int pictureIndex;
    static void BeginPictures()
    {
        foreach(var pair in originalStates)if(pair.Key!=null)pair.Key.enabled=pair.Value;
        foreach(var g in playRoot.GetComponentsInChildren<ShowroomInstancedVisualGroup>(true))g.suppressRendering=false;
        playRoot.trainRunner.ResetToStart();playRoot.trainRunner.SetSpeed(1);
        string path=Dir+"/CameraPoses.json";
        if(SessionState.GetString(ProfileKey+".Tag","")=="baseline")
        {
            var list=new System.Collections.Generic.List<ViewPose>();
            SetCamera(false);list.Add(new ViewPose{name="01_overview",position=profileCamera.transform.position,rotation=profileCamera.transform.rotation});
            SetCamera(true);list.Add(new ViewPose{name="02_close",position=profileCamera.transform.position,rotation=profileCamera.transform.rotation});
            foreach(var entry in new[]{("rail","03_rail"),("nature","04_mountains"),("bridge","05_bridge"),("station","06_station"),("train","07_train")})
            {
                var layer=playRoot.FindLayer(entry.Item1);var rs=layer.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&r.GetComponent<MeshFilter>()?.sharedMesh!=null).ToArray();
                var r=rs.OrderBy(r=>Vector3.Distance(r.bounds.center,playRoot.transform.position)).First();var b=r.bounds;
                float distance=Mathf.Max(entry.Item1=="rail"?.6f:.3f,b.extents.magnitude*2.2f);
                var target=b.center;var position=target+new Vector3(.7f,.65f,1).normalized*distance;
                list.Add(new ViewPose{name=entry.Item2,position=position,rotation=Quaternion.LookRotation(target-position)});
            }
            views=new ViewPoses{views=list.ToArray()};File.WriteAllText(path,JsonUtility.ToJson(views,true));
        }
        else views=JsonUtility.FromJson<ViewPoses>(File.ReadAllText(path));
        pictureIndex=0;phase=100;frame=0;waitFrames=0;
    }
    static void PictureTick()
    {
        if(pictureIndex>=views.views.Length){FinishProfile();return;}
        var view=views.views[pictureIndex];
        if(phase==100)
        {
            profileCamera.transform.SetPositionAndRotation(view.position,view.rotation);
            frame=0;waitFrames=8;phase=101;return;
        }
        string directory=Dir+"/Images_"+SessionState.GetString(ProfileKey+".Tag","");Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(directory+"/"+view.name+".png");pictureIndex++;phase=100;frame=0;waitFrames=8;
    }
}
