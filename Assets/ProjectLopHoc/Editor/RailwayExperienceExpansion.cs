using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
internal static class RailwayExperienceExpansion
{
    const string Request="Temp/RailwayExperience.request";
    static RailwayExperienceExpansion()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        try
        {
            string mode=File.ReadAllText(Request).Trim();if(mode.Length==0)return;File.Delete(Request);
            if(mode=="inspect")
            {
                var report=new StringBuilder();
                foreach(string path in new[]{"Assets/ProjectLopHoc/YenTuFix/catenary portal mast.glb","Assets/ProjectLopHoc/YenTuFix/Overhead contact wire run.glb"})
                {
                    var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null){report.AppendLine(path+": NOT IMPORTED");continue;}
                    Bounds bounds=default;bool first=true;int triangles=0;
                    foreach(var mf in asset.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var matrix=asset.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;
                        foreach(var v in mf.sharedMesh.vertices){var p=matrix.MultiplyPoint3x4(v);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                        triangles+=mf.sharedMesh.triangles.Length/3;
                    }
                    report.AppendLine(path+": "+bounds+" triangles="+triangles);
                }
                File.WriteAllText("Temp/RailwayExperience_Assets.txt",report.ToString());
            }
            else if(mode=="apply"||mode=="residential")
            {
                var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.isDirty)throw new InvalidOperationException("Unsaved scene; not discarding it");
                if(current.name!="YenTuHaLongXanh")EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity");
                if(mode=="apply")RailwayRevisionTools.ExpandTwoTunnels();
                RailwayEnvironmentDensity.ExpandSettlementAndEngineering(mode=="residential");
            }
            else if(mode=="test")
            {
                if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Unsaved scene");
                EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity");
                File.WriteAllText("Temp/RailwayExperienceValidation.request","test");
            }
            else if(mode=="finish")
            {
                RailwayRevisionTools.ExpandTwoTunnels();
                RailwayEnvironmentDensity.AuditExperienceSectors();
            }
            else if(mode=="wireFix")RailwayEnvironmentDensity.AlignMessengerToContactWire();
            else if(mode=="wireTest")File.WriteAllText("Temp/RailwayExperienceValidation.request","overheadOnly");
        }
        catch(Exception e){File.WriteAllText("Temp/RailwayExperience_ERROR.txt",e.ToString());Debug.LogException(e);}
    }
}
