using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static partial class RailwayRevisionTools
{
    const string Request="Temp/RailwayRevision.request";
    static RailwayRevisionTools()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{string mode=File.ReadAllText(Request).Trim();File.Delete(Request);if(mode=="inspect")Inspect();else if(mode=="tunnel")InstallTunnel();else if(mode=="viewTunnel")ViewTunnel();else if(mode=="testYenTu"||mode=="testLogin"){if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Unsaved scene");UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/"+(mode=="testYenTu"?"YenTuHaLongXanh":"Login")+".unity");File.WriteAllText(mode=="testYenTu"?"Temp/TrainExteriorView.request":"Temp/XRSessionRevision.request","test");}}
        catch(Exception e){File.WriteAllText("Temp/RailwayRevision_ERROR.txt",e.ToString());Debug.LogException(e);}
    }
    static void Inspect()
    {
        var report=new StringBuilder();
        foreach(string name in new[]{"Tunnel","MountainRidge","Mountain_Peak"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/"+name+".prefab");
            foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var matrix=name=="Tunnel"?Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(0,90,0),new Vector3(1.45f,1.55f,1.45f))*prefab.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix:mf.transform.localToWorldMatrix;
                bool first=true;Bounds bounds=default;foreach(var v in mf.sharedMesh.vertices){var p=matrix.MultiplyPoint3x4(v);if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
                report.AppendLine($"{name}/{mf.transform.parent?.name}/{mf.name}: center={bounds.center} size={bounds.size} vertices={mf.sharedMesh.vertexCount} triangles={mf.sharedMesh.triangles.Length/3}");
            }
        }
        File.WriteAllText("Temp/RailwayRevision_Geometry.txt",report.ToString());
    }
    static partial void InstallTunnel();
}
