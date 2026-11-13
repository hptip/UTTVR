using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

internal static partial class RailwayEnvironmentDensity
{
    [Serializable] sealed class BilateralStamp {public bool complete;public string region="300..800m";}
    public static void ApplyBilateralAndMeshOptimization()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var route=new Route(manager.trainMover.splineRoute);bool hasStamp=File.Exists(SectorFolder+"/Bilateral.json");int slopeVertices=0,slopes=0,missing=0,renderers=0,collidersRemoved=0;long beforeVertices=0,afterVertices=0;
        var processed=new HashSet<Mesh>();
        foreach(var zone in manager.zones)
        {
            var scene=EditorSceneManager.OpenScene(zone.scenePath,OpenSceneMode.Additive);
            try
            {
                var root=Find(scene,"RuntimeZoneContent");
                var activation=root.GetComponent<RailwayZoneActivation>();if(activation!=null){activation.objectsPerFrame=1;activation.millisecondsPerFrame=.25f;EditorUtility.SetDirty(activation);}
                var slope=root.Find("Experience_SubgradeReinforcedSlope");
                if(slope!=null)
                {
                    slopes++;
                    if(!hasStamp)
                    {
                        var filter=slope.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var vertices=mesh.vertices;var colors=mesh.colors32;var triangles=mesh.triangles;var geometry=new Geometry();geometry.v.AddRange(vertices);geometry.c.AddRange(colors);geometry.t.AddRange(triangles);var mirrored=new Dictionary<int,int>();
                        for(int i=0;i<triangles.Length;i+=3)
                        {
                            Vector3 center=slope.TransformPoint((vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3);float d=route.Nearest(center,out _);Vector3 reference=route.At(d,out var forward);var right=Vector3.Cross(Vector3.up,forward).normalized;
                            if(Vector3.Dot(center-reference,right)<11.5f)continue; // exclude the shared roadbed foundation
                            int[] face=new int[3];
                            for(int k=0;k<3;k++)
                            {
                                int old=triangles[i+k];if(!mirrored.TryGetValue(old,out int n))
                                {
                                    Vector3 p=slope.TransformPoint(vertices[old]);float distance=route.Nearest(p,out _);Vector3 axis=route.At(distance,out var tangent);var normal=Vector3.Cross(Vector3.up,tangent).normalized;float x=Vector3.Dot(p-axis,normal);
                                    p+=normal*(-6.400022f-2*x);n=geometry.v.Count;mirrored.Add(old,n);geometry.v.Add(slope.InverseTransformPoint(p));geometry.c.Add(colors[old]);slopeVertices++;
                                }
                                face[k]=n;
                            }
                            geometry.t.Add(face[0]);geometry.t.Add(face[2]);geometry.t.Add(face[1]);
                        }
                        var replacement=geometry.Mesh(mesh.name);EditorUtility.CopySerialized(replacement,mesh);UnityEngine.Object.DestroyImmediate(replacement);EditorUtility.SetDirty(mesh);
                    }
                    float bottom=RailBottom(manager,route,550);
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("TerrainZone_")))
                    {
                        var mesh=filter.sharedMesh;var vertices=mesh.vertices;
                        for(int i=0;i<vertices.Length;i++)
                        {
                            Vector3 p=filter.transform.TransformPoint(vertices[i]);float d=route.Nearest(p,out _);if(d<300||d>800)continue;Vector3 center=route.At(d,out var f);float x=Vector3.Dot(p-center,Vector3.Cross(Vector3.up,f).normalized);float mirrored=-6.400022f-x;if(mirrored<12||mirrored>72)continue;
                            float ramp=Mathf.SmoothStep(0,1,Mathf.InverseLerp(300,350,d))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(750,800,d)));float height=Mathf.Max(0,Mathf.Min((mirrored-12)/30,(72-mirrored)/30))*14*ramp;
                            p.y=Mathf.Max(p.y,center.y+bottom+height-.15f);vertices[i]=filter.transform.InverseTransformPoint(p);
                        }
                        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                    }
                }
                foreach(var t in root.GetComponentsInChildren<Transform>(true))missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // Authored environment is static/noninteractive. Keep explicit
                    // physics needed by stations and roadbeds; disable decorative passes.
                    r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;r.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;renderers++;
                    var filter=r.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;var mesh=filter.sharedMesh;
                    if(!AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/ProjectLopHoc/Generated/",StringComparison.Ordinal)||!processed.Add(mesh))continue;
                    beforeVertices+=mesh.vertexCount;CompactStaticMesh(mesh);afterVertices+=mesh.vertexCount;
                }
                foreach(var collider in root.GetComponentsInChildren<MeshCollider>(true))
                {
                    // Preserve walking/spawn support while syncing modified terrain.
                    if(collider.name.StartsWith("TerrainZone_")){var mesh=collider.sharedMesh;collider.sharedMesh=null;collider.sharedMesh=mesh;}
                }
                EditorSceneManager.SaveScene(scene);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        long railBefore=0,railAfter=0;int railCount=0;
        foreach(var chunk in manager.GetComponent<RailwayMicroChunkStreamer>().data.chunks)
            foreach(var track in new[]{chunk.right,chunk.left})
            {
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectLopHoc/Generated/QuestRailway/Resources/"+track.meshResource+".asset");if(mesh==null)throw new InvalidOperationException("Missing rail resource");
                railBefore+=mesh.vertexCount;CompactStaticMesh(mesh);railAfter+=mesh.vertexCount;railCount++;
            }
        File.WriteAllText(SectorFolder+"/Bilateral.json",JsonUtility.ToJson(new BilateralStamp{complete=true},true));AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        File.WriteAllText("Temp/RailwaySmoothness_Changes.txt",$"Subgrade both sides in{slopes}existing zone batches; added mirrored vertices={slopeVertices}; generated meshes compacted={processed.Count}, vertices{beforeVertices}->{afterVertices}; rail mesh resources={railCount},vertices{railBefore}->{railAfter}; environment renderer passes reduced={renderers}; scenery TerrainZone mesh colliders removed={collidersRemoved}; missingScripts={missing}. Station/train/rail colliders kept. Activation1object/frame,.25ms. Source assets/spline/train/XR unchanged.");
        if(missing>0)throw new InvalidOperationException("Missing script found");
    }
    static void CompactStaticMesh(Mesh mesh)
    {
        var vertices=mesh.vertices;var normals=mesh.normals;var colors=mesh.colors32;var uv=mesh.uv;var map=new Dictionary<(Vector3,Vector3,Color32,Vector2),int>();var output=new List<Vector3>();var n=new List<Vector3>();var c=new List<Color32>();var u=new List<Vector2>();var triangles=new List<int[]>();
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            var indices=mesh.GetTriangles(sub);for(int i=0;i<indices.Length;i++)
            {int old=indices[i];var normal=normals.Length==vertices.Length?normals[old]:Vector3.up;var color=colors.Length==vertices.Length?colors[old]:new Color32(255,255,255,255);var tex=uv.Length==vertices.Length?uv[old]:Vector2.zero;var key=(vertices[old],normal,color,tex);if(!map.TryGetValue(key,out int index)){index=output.Count;map.Add(key,index);output.Add(vertices[old]);n.Add(normal);c.Add(color);u.Add(tex);}indices[i]=index;}
            triangles.Add(indices);
        }
        mesh.Clear();mesh.indexFormat=output.Count<=65535?IndexFormat.UInt16:IndexFormat.UInt32;mesh.SetVertices(output);mesh.SetNormals(n);if(colors.Length>0)mesh.SetColors(c);if(uv.Length>0)mesh.SetUVs(0,u);mesh.subMeshCount=triangles.Count;for(int i=0;i<triangles.Count;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateBounds();mesh.OptimizeIndexBuffers();mesh.OptimizeReorderVertexBuffer();EditorUtility.SetDirty(mesh);
    }
    public static void InspectBilateralSubgrade()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var route=new Route(manager.trainMover.splineRoute);var zone=manager.zones.First(z=>z.startChainage<=550&&z.endChainage>550);var scene=EditorSceneManager.OpenScene(zone.scenePath,OpenSceneMode.Additive);var temporary=new GameObject("Editor bilateral inspection");
        try
        {
            foreach(var root in scene.GetRootGameObjects())foreach(var activation in root.GetComponentsInChildren<RailwayZoneActivation>(true))foreach(var chunk in activation.chunks)if(chunk!=null)chunk.SetActive(true);
            foreach(var chunk in manager.GetComponent<RailwayMicroChunkStreamer>().data.chunks.Where(c=>c.end>=300&&c.start<=800))foreach(var track in new[]{chunk.right,chunk.left})
            {var go=new GameObject("Temporary rail preview");go.transform.SetParent(temporary.transform);go.transform.SetPositionAndRotation(track.position,track.rotation);go.transform.localScale=track.scale;go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectLopHoc/Generated/QuestRailway/Resources/"+track.meshResource+".asset");go.AddComponent<MeshRenderer>().sharedMaterials=track.materials;}
            var cameraObject=new GameObject("Bilateral inspection camera");cameraObject.transform.SetParent(temporary.transform);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.farClipPlane=1600;camera.cullingMask=~(1<<5);camera.fieldOfView=75;
            Vector3 p=route.At(540,out var f);camera.transform.position=p-f*55+Vector3.up*38;camera.transform.LookAt(p+f*90+Vector3.up*3);
            var rt=new RenderTexture(1280,720,24);rt.Create();var previous=RenderTexture.active;Texture2D texture=null;
            try{RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Temp/Subgrade_BothSides.png",texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);}
        }
        finally{UnityEngine.Object.DestroyImmediate(temporary);EditorSceneManager.CloseScene(scene,true);}
    }
}
