using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

internal static partial class RailwayRevisionTools
{
    const string TunnelFolder="Assets/ProjectLopHoc/Generated/QuestTunnelMountain";
    const float TunnelStart=2400,TunnelEnd=2600,TrackCenter=-3.200011f;
    sealed class TunnelGeometry
    {
        public List<Vector3> vertices=new List<Vector3>();public List<int> triangles=new List<int>();public List<Color> colors=new List<Color>();
        public void Polygon(List<Vector3> polygon,Func<Vector3,Vector3> warp,Color color)
        {if(polygon.Count<3)return;int first=vertices.Count;foreach(var p in polygon){vertices.Add(warp(p));colors.Add(color);}for(int i=1;i<polygon.Count-1;i++){triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);}}
        public Mesh Mesh(string name){var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
    }
    static partial void InstallTunnel() => InstallTunnelAt(2400,2600,"Forest_01");
    public static void ExpandTwoTunnels()
    {
        InstallTunnelAt(2200,2800,"Forest_01");
        InstallTunnelAt(1100,1700,"Forest_02");
    }
    static void InstallTunnelAt(float start,float end,string id)
    {
        float center=(start+end)*.5f;
        var previous=SceneManager.GetActiveScene();if(previous.isDirty&&previous.name!="YenTuHaLongXanh")throw new InvalidOperationException("Unsaved unrelated scene; refusing to discard changes.");string previousPath=previous.path;
        var main=previous.name=="YenTuHaLongXanh"?previous:EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity",OpenSceneMode.Single);
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var route=manager.trainMover.splineRoute;float length=route.CalculateLength();
        Vector3 origin=route.EvaluatePosition(center/length);
        Vector3 Warp(Vector3 p)
        {float t=Mathf.Clamp01((center+p.z)/length);Vector3 position=route.EvaluatePosition(t),f=route.EvaluateTangent(t);return position+Vector3.Cross(Vector3.up,f.normalized)*(p.x+TrackCenter)+Vector3.up*p.y-origin;}
        Directory.CreateDirectory(TunnelFolder);AssetDatabase.Refresh();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/Tunnel.prefab");
        var sourceMeshes=prefab.GetComponentsInChildren<MeshFilter>(true).Where(m=>!m.transform.GetComponentsInParent<Transform>(true).Any(t=>t.name.StartsWith("Ballast"))).ToArray();
        var transformBase=Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(0,90,0),new Vector3(1.45f,1.55f,1.45f))*prefab.transform.worldToLocalMatrix;
        Bounds bounds=default;bool first=true;foreach(var mf in sourceMeshes)foreach(var p in mf.sharedMesh.vertices){var v=(transformBase*mf.transform.localToWorldMatrix).MultiplyPoint3x4(p);if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}
        if(bounds.size.z<1)throw new InvalidOperationException("Invalid tunnel repeat length");
        int modules=Mathf.CeilToInt((end-start)/bounds.size.z);float step=(end-start)/modules;
        var tunnel=new TunnelGeometry();
        for(int module=0;module<modules;module++)foreach(var mf in sourceMeshes)
        {
            var matrix=transformBase*mf.transform.localToWorldMatrix;var mesh=mf.sharedMesh;var v=mesh.vertices;var indices=mesh.triangles;
            bool grass=mf.GetComponentsInParent<Transform>(true).Any(t=>t.name.Contains("Grass"));
            float minZ=float.PositiveInfinity,maxZ=float.NegativeInfinity;foreach(var vertex in v){float z=matrix.MultiplyPoint3x4(vertex).z;minZ=Mathf.Min(minZ,z);maxZ=Mathf.Max(maxZ,z);}
            // Source roof spans less Z than its walls. Fit each structural part
            // to the same repeat interval; aggregate bounds left roof gaps.
            Vector3 Convert(Vector3 p){p=matrix.MultiplyPoint3x4(p);return new Vector3((p.x-bounds.center.x)/bounds.size.x*26,(p.y-bounds.min.y)/bounds.size.y*18-1.5f,(p.z-minZ)/Mathf.Max(.001f,maxZ-minZ)*step+start-center+module*step);}
            for(int i=0;i<indices.Length;i+=3)tunnel.Polygon(new List<Vector3>{Convert(v[indices[i]]),Convert(v[indices[i+1]]),Convert(v[indices[i+2]])},Warp,grass?new Color(.22f,.32f,.13f):new Color(.38f,.40f,.37f));
        }
        // Subtract an open, full-length rail corridor before warping the source
        // mountain onto the unchanged route. No mountain faces can seal the bore.
        var mountainPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/MountainRidge.prefab");
        var mountainFilter=mountainPrefab.GetComponentInChildren<MeshFilter>();var mountainMesh=mountainFilter.sharedMesh;var mountainMatrix=mountainFilter.transform.localToWorldMatrix;
        var source=mountainMesh.vertices;Bounds mountainBounds=default;first=true;foreach(var p in source){var v=mountainMatrix.MultiplyPoint3x4(p);if(first){mountainBounds=new Bounds(v,Vector3.zero);first=false;}else mountainBounds.Encapsulate(v);}
        Vector3 MountainPoint(Vector3 v){v=mountainMatrix.MultiplyPoint3x4(v);float x=(v.x-mountainBounds.center.x)/mountainBounds.size.x*220,y=(v.y-mountainBounds.min.y)/mountainBounds.size.y*65-4,z=(v.z-mountainBounds.center.z)/mountainBounds.size.z*(end-start+10);float roof=24*(1-Mathf.SmoothStep(28,65,Mathf.Abs(x)));return new Vector3(x,Mathf.Max(y,roof),z);}
        var mountain=new TunnelGeometry();var triangles=mountainMesh.triangles;
        for(int i=0;i<triangles.Length;i+=3)
        {
            var inside=new List<Vector3>{MountainPoint(source[triangles[i]]),MountainPoint(source[triangles[i+1]]),MountainPoint(source[triangles[i+2]])};
            foreach(var plane in new[]{new Vector4(1,0,0,14),new Vector4(-1,0,0,14),new Vector4(0,1,0,17)})
            {
                if(inside.Count<3)break;
                var outside=Clip(inside,plane,false);mountain.Polygon(outside,Warp,new Color(.22f,.32f,.13f));inside=Clip(inside,plane,true);
            }
        }
        var old=manager.fixedInfrastructure.FirstOrDefault(f=>f.id=="TunnelMountain_"+id);
        string groupName="TunnelMountainGroup_"+id;
        var owner=manager.zones.First(z=>z.zoneId==(old!=null?old.zoneId:manager.zones.First(q=>q.startChainage<=center&&q.endChainage>center).zoneId));
        var scene=EditorSceneManager.OpenScene(owner.scenePath,OpenSceneMode.Additive);
        try
        {
            var content=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="RuntimeZoneContent");
            var group=content.Find(groupName);if(group==null){group=new GameObject(groupName).transform;SceneManager.MoveGameObjectToScene(group.gameObject,scene);group.SetParent(content,false);}
            group.position=origin;
            SavePart(group,"TunnelInterior_SourcePrefab",tunnel,id);SavePart(group,"Mountain_SourceRidge_OpenBore",mountain,id);
            foreach(var item in new[]{("TunnelEntrance_Front",start),("TunnelEntrance_Rear",end)})
            {var marker=group.Find(item.Item1);if(marker==null){marker=new GameObject(item.Item1).transform;marker.SetParent(group,false);}marker.position=route.EvaluatePosition(item.Item2/length);marker.rotation=Quaternion.LookRotation(route.EvaluateTangent(item.Item2/length),Vector3.up);}
            var activation=content.GetComponent<RailwayZoneActivation>();if(activation!=null){var chunks=activation.chunks.Where(g=>g!=null).ToList();if(!chunks.Contains(group.gameObject))chunks.Add(group.gameObject);activation.chunks=chunks.ToArray();group.gameObject.SetActive(false);}
            EditorSceneManager.SaveScene(scene);
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
        var record=manager.fixedInfrastructure.FirstOrDefault(f=>f.id=="TunnelMountain_"+id);if(record==null){record=new RailwayZoneStreamingManager.FixedInfrastructureRecord();manager.fixedInfrastructure.Add(record);}
        record.id="TunnelMountain_"+id;record.kind="TunnelMountain";record.startChainage=start;record.endChainage=end;record.zoneId=owner.zoneId;
        record.zoneIds=manager.zones.Where(z=>z.startChainage<=end+20&&z.endChainage>=start-20).Select(z=>z.zoneId).ToArray();
        record.mountainAssetReference="Assets/ProjectLopHoc/Prefabs/MountainRidge.prefab";record.tunnelAssetReference="Assets/ProjectLopHoc/Prefabs/Tunnel.prefab";
        EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/RailwayRevision_Tunnel_"+id+".txt",$"Fixed source-based {id} {start}..{end}m, owner={owner.zoneId}; modules={modules}, repeat={step:F3}m (source measured span={bounds.size.z:F3}m); tunnelTriangles={tunnel.triangles.Count/3}, mountainTriangles={mountain.triangles.Count/3}. Source assets unchanged; both-track bore; synchronized zones={string.Join(",",record.zoneIds)}.");
        if(previousPath!=main.path)EditorSceneManager.OpenScene(previousPath,OpenSceneMode.Single);
    }
    static List<Vector3> Clip(List<Vector3> input,Vector4 plane,bool keepInside)
    {
        var result=new List<Vector3>();for(int i=0;i<input.Count;i++){Vector3 a=input[i],b=input[(i+1)%input.Count];float da=a.x*plane.x+a.y*plane.y+a.z*plane.z-plane.w,db=b.x*plane.x+b.y*plane.y+b.z*plane.z-plane.w;bool ain=keepInside?da<=0:da>=0,bin=keepInside?db<=0:db>=0;if(ain)result.Add(a);if(ain!=bin)result.Add(Vector3.Lerp(a,b,da/(da-db)));}return result;
    }
    static void SavePart(Transform parent,string name,TunnelGeometry geometry,string assetPrefix="")
    {
        string path=TunnelFolder+"/"+assetPrefix+name+".asset";var mesh=geometry.Mesh(name);var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
        var part=parent.Find(name);if(part==null){part=new GameObject(name).transform;part.SetParent(parent,false);}
        var mf=part.GetComponent<MeshFilter>();if(mf==null)mf=part.gameObject.AddComponent<MeshFilter>();mf.sharedMesh=saved;
        var r=part.GetComponent<MeshRenderer>();if(r==null)r=part.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectLopHoc/Generated/QuestRailway/RailwayFlatColour.mat");r.shadowCastingMode=ShadowCastingMode.Off;
    }
    static void ViewTunnel()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();if(manager==null)throw new InvalidOperationException("YenTu scene required");
        var route=manager.trainMover.splineRoute;float length=route.CalculateLength();
        var scenes=new List<Scene>();var temporary=new GameObject("Editor tunnel geometry inspection");int missing=0;
        try
        {
            foreach(var z in manager.zones.Where(z=>z.endChainage>=2250&&z.startChainage<=2800))
            {
                var scene=EditorSceneManager.OpenScene(z.scenePath,OpenSceneMode.Additive);scenes.Add(scene);
                foreach(var root in scene.GetRootGameObjects())foreach(var tr in root.GetComponentsInChildren<Transform>(true)){missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);var activation=tr.GetComponent<RailwayZoneActivation>();if(activation!=null)foreach(var chunk in activation.chunks)if(chunk!=null)chunk.SetActive(true);}
            }
            foreach(var chunk in manager.GetComponent<RailwayMicroChunkStreamer>().data.chunks.Where(c=>c.end>=2250&&c.start<=2800))foreach(var track in new[]{chunk.right,chunk.left})
            {
                var go=new GameObject("Temporary rail preview");go.transform.SetParent(temporary.transform);go.transform.SetPositionAndRotation(track.position,track.rotation);go.transform.localScale=track.scale;
                go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectLopHoc/Generated/QuestRailway/Resources/"+track.meshResource+".asset");go.AddComponent<MeshRenderer>().sharedMaterials=track.materials;
            }
            var cameraObject=new GameObject("Temporary tunnel inspection camera");cameraObject.transform.SetParent(temporary.transform);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.farClipPlane=800;camera.stereoTargetEye=StereoTargetEyeMask.None;
            for(int i=0;i<2;i++)
            {
                float d=i==0?2340:2500;Vector3 p=route.EvaluatePosition(d/length),f=route.EvaluateTangent(d/length),right=Vector3.Cross(Vector3.up,f.normalized);
                camera.transform.position=p+Vector3.up*(i==0?7:2)+(i==0?right*8:Vector3.zero);camera.transform.LookAt((Vector3)route.EvaluatePosition((i==0?2420:2550)/length)+Vector3.up*(i==0?7:3));
                var rt=new RenderTexture(1280,720,24);rt.Create();var previous=RenderTexture.active;Texture2D image=null;
                try{RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(i==0?"Temp/TunnelMountain_Entrance.png":"Temp/TunnelMountain_Interior.png",image.EncodeToPNG());}
                finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);}
            }
            File.WriteAllText("Temp/TunnelMountain_VisualCheck.txt",$"Saved portal/interior inspection renders from existing saved scenes. Missing scripts={missing}; temporary rail previews only; source scenes not saved/changed by inspection.");
        }
        finally{UnityEngine.Object.DestroyImmediate(temporary);foreach(var scene in scenes)EditorSceneManager.CloseScene(scene,true);}
    }
}
