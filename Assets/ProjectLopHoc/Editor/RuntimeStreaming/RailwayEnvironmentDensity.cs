using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Splines;

[InitializeOnLoad]
internal static partial class RailwayEnvironmentDensity
{
    const string Request="Temp/RailwayEnvironmentDensity.request";
    const string Folder="Assets/ProjectLopHoc/Generated/QuestEnvironmentDensity";
    const string Owned="EnvironmentDensity_Quest";
    static readonly string[] Types={"Station/Countryside","Countryside","Transition","Forest","Forest","Forest","Transition","Bridge Approach/Crossing","Bridge Crossing","Bridge Crossing/Exit","Transition","Residential","Residential","Residential/Transition","Countryside","Countryside","Station"};
    static readonly int[] Trees={3,5,8,30,30,25,12,3,0,0,8,5,6,6,5,5,0};
    static readonly int[] Houses={0,2,1,0,0,0,1,0,0,0,3,10,12,7,2,2,0};
    [Serializable] sealed class ZoneInfo { public string id,type;public float start,end;public int trees,houses; }
    [Serializable] sealed class Placement {public string zone,kind;public int variant;public float chainage,lateral,height;public Vector3 position;}
    [Serializable] sealed class Manifest
    {
        public float bridgeBeforeStart,bridgeBeforeEnd,bridgeStart,bridgeEnd,referenceSpeed=200,crossingSeconds;
        public int treesBefore,housesBefore,treesAfter,housesAfter,renderersBefore,renderersAfter,objectsBefore,objectsAfter,collidersBefore,collidersAfter,materialsBefore,materialsAfter;
        public float minimumDecorClearance=99999;
        public List<ZoneInfo> zones=new List<ZoneInfo>();public List<Placement> placements=new List<Placement>();
    }
    sealed class Route
    {
        readonly Vector3[] points=new Vector3[1601];readonly float length;
        public Route(SplineContainer s){length=s.CalculateLength();for(int i=0;i<points.Length;i++)points[i]=s.EvaluatePosition(i/(float)(points.Length-1));}
        public Vector3 At(float d,out Vector3 f){float t=Mathf.Clamp01(d/length)*(points.Length-1);int i=Mathf.Min((int)t,points.Length-2);f=(points[i+1]-points[i]).normalized;return Vector3.Lerp(points[i],points[i+1],t-i);}
        public float Nearest(Vector3 p,out float lateral){float best=float.PositiveInfinity,d=0;lateral=0;for(int i=0;i<points.Length;i++){var delta=p-points[i];delta.y=0;if(delta.sqrMagnitude<best){best=delta.sqrMagnitude;d=i*length/(points.Length-1);lateral=Mathf.Sqrt(best);}}return d;}
    }
    sealed class Geometry
    {
        public readonly List<Vector3> v=new List<Vector3>();public readonly List<int> t=new List<int>();public readonly List<Color32> c=new List<Color32>();
        public void Box(Vector3 p,Quaternion rotation,Vector3 size,Color color)
        {
            var corners=new Vector3[8];for(int i=0;i<8;i++)corners[i]=p+rotation*Vector3.Scale(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f),size);
            int[] faces={0,4,6,2,1,3,7,5,0,1,5,4,2,6,7,3,0,2,3,1,4,5,7,6};
            for(int face=0;face<6;face++){int first=v.Count;for(int k=0;k<4;k++){v.Add(corners[faces[face*4+k]]);c.Add(color);}t.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});}
        }
        public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetColors(c);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;}
    }
    static RailwayEnvironmentDensity()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{string mode=File.ReadAllText(Request).Trim();if(mode.Length==0)return;File.Delete(Request);if(mode=="run")Run();else if(mode=="verify")Verify();else if(mode=="finish")Finish();else if(mode=="test"){File.WriteAllText("Temp/QuestEnvironmentCapture.flag","Window-height visual checks");if(File.Exists("Temp/QuestRailwayPureProfile.flag"))File.Delete("Temp/QuestRailwayPureProfile.flag");File.WriteAllText("Temp/RailwayProfile.request","questFinal");}}
        catch(Exception e){File.WriteAllText("Temp/RailwayEnvironmentDensity_ERROR.txt",e.ToString());Debug.LogException(e);}
        finally{EditorUtility.ClearProgressBar();}
    }
    static Transform Find(Scene s,string name){foreach(var root in s.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;return null;}
    static void Finish()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var m=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/Manifest.json"));var route=new Route(manager.trainMover.splineRoute);
        var record=manager.zones.First(z=>z.zoneId=="Zone_018");var scene=EditorSceneManager.OpenScene(record.scenePath,OpenSceneMode.Additive);
        try
        {
            var root=Find(scene,"RuntimeZoneContent");var old=root.Find("FixedInfrastructure_Bridge/QuestFacilityVisual");var mf=old.GetComponent<MeshFilter>();var renderer=old.GetComponent<MeshRenderer>();
            // The previous facility bake also contained a water submesh. Retain the
            // concrete structure, replace only its obsolete water with the new river.
            var source=mf.sharedMesh;var materials=renderer.sharedMaterials;var keep=new List<int[]>();var mats=new List<Material>();
            for(int sub=0;sub<source.subMeshCount;sub++)if(!materials[sub].name.ToLowerInvariant().Contains("water")){keep.Add(source.GetTriangles(sub));mats.Add(materials[sub]);}
            if(keep.Count!=source.subMeshCount){var mesh=UnityEngine.Object.Instantiate(source);mesh.subMeshCount=keep.Count;for(int i=0;i<keep.Count;i++)mesh.SetTriangles(keep[i],i);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/ExistingBridgeConcrete.asset");mf.sharedMesh=mesh;renderer.sharedMaterials=mats.ToArray();}
            BuildBridge(scene,root,route,m,manager.GetComponent<RailwayMicroChunkStreamer>().data);EditorSceneManager.SaveScene(scene);
        }
        finally{EditorSceneManager.CloseScene(scene,true);}
        long before=0,after=0;
        foreach(string guid in AssetDatabase.FindAssets("t:Mesh",new[]{Folder}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);if(!path.EndsWith("_Cluster.asset",StringComparison.Ordinal))continue;var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var source=mesh.vertices;var colors=mesh.colors32;var map=new Dictionary<int,int>();var vertices=new List<Vector3>();var outputColors=new List<Color32>();var indices=mesh.GetTriangles(0);before+=source.Length;
            for(int i=0;i<indices.Length;i++){int old=indices[i];if(!map.TryGetValue(old,out int n)){n=vertices.Count;map.Add(old,n);vertices.Add(source[old]);outputColors.Add(colors[old]);}indices[i]=n;}
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(outputColors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();after+=vertices.Count;EditorUtility.SetDirty(mesh);
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Temp/RailwayEnvironmentDensity_Finish.txt",$"Decoration vertices {before}->{after}; triangle counts/placements preserved. Bridge under-deck and deep supports continuous across old180m section. Obsolete water submesh removed from a generated working copy only. Original facility asset retained.");
    }
    static void Verify()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();
        var report=new StringBuilder();int missing=0;long vertices=0,triangles=0;
        var route=new Route(manager.trainMover.splineRoute);
        foreach(var record in manager.zones)
        {
            var scene=EditorSceneManager.OpenScene(record.scenePath,OpenSceneMode.Additive);
            try
            {
                foreach(var root in scene.GetRootGameObjects())foreach(var tr in root.GetComponentsInChildren<Transform>(true))missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);
                var content=Find(scene,"RuntimeZoneContent");int stations=0;
                foreach(var mf in content.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(mf.sharedMesh==null)throw new InvalidOperationException("Missing mesh: "+scene.name+"/"+mf.name);
                    if(mf.GetComponentsInParent<Transform>(true).Any(t=>t.name.StartsWith("Station_")&&t.name.EndsWith("TrainRide")))stations++;
                    if(record.zoneId=="Zone_018"&&mf.transform.IsChildOf(content.Find("FixedInfrastructure_Bridge"))){var r=mf.GetComponent<Renderer>();var b=r.bounds;float d=route.Nearest(b.center,out float lateral);report.AppendLine($"Bridge mesh={mf.name}; enabled={r.enabled}; active={r.gameObject.activeInHierarchy}; center={b.center}; size={b.size}; nearestChainage={d:F2}; lateral={lateral:F2}; materials={string.Join(",",r.sharedMaterials.Select(mat=>mat.name))}");}
                    if(mf.name.StartsWith(Owned)){vertices+=mf.sharedMesh.vertexCount;for(int sub=0;sub<mf.sharedMesh.subMeshCount;sub++)triangles+=(long)mf.sharedMesh.GetIndexCount(sub)/3;}
                }
                report.AppendLine(record.zoneId+": stationMeshCount="+stations);
                if(record.zoneId=="Zone_018")
                {
                    Vector3 center=route.At(4170,out var f),right=Vector3.Cross(Vector3.up,f).normalized;
                    var go=new GameObject("Editor Bridge Inspection");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.cullingMask=~(1<<5);camera.farClipPlane=1800;
                    camera.transform.position=center+right*90-f*100+Vector3.up*25;camera.transform.LookAt(center+Vector3.down*5);
                    var rt=new RenderTexture(1280,720,24);rt.Create();var previous=RenderTexture.active;Texture2D image=null;
                    try{RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("Temp/QuestBridge_Exterior.png",image.EncodeToPNG());}
                    finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
                }
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        report.AppendLine($"MissingScripts={missing}; clusterVertices={vertices}; clusterTriangles={triangles}; zones={manager.zones.Count}; no scene changes saved by verification.");
        File.WriteAllText("Temp/RailwayEnvironmentDensity_Verification.txt",report.ToString());
        if(missing>0)throw new InvalidOperationException("Missing scripts found: "+missing);
    }
    static void Run()
    {
        if(File.Exists(Folder+"/Manifest.json"))throw new InvalidOperationException("Density already applied; refusing duplicate generation.");
        var main=SceneManager.GetActiveScene();if(main.name!="YenTuHaLongXanh"){if(main.isDirty)throw new InvalidOperationException("Unrelated scene has unsaved changes; not saving or discarding it.");main=EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity",OpenSceneMode.Single);}
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();if(manager.zones.Count!=17)throw new InvalidOperationException("Expected existing 17 zones");
        var route=new Route(manager.trainMover.splineRoute);var bridge=manager.fixedInfrastructure.First(f=>f.kind=="Bridge");
        var manifest=new Manifest{bridgeBeforeStart=bridge.startChainage,bridgeBeforeEnd=bridge.endChainage};
        float center=(bridge.startChainage+bridge.endChainage)*.5f,length=Mathf.Ceil((200f/3.6f*20f)/40)*40;
        manifest.bridgeStart=center-length*.5f;manifest.bridgeEnd=center+length*.5f;manifest.crossingSeconds=length/(200f/3.6f);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var shader=Shader.Find("UTT/Environment Cluster Fade");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Cluster shader unavailable");
        var material=new Material(shader);material.enableInstancing=true;AssetDatabase.CreateAsset(material,Folder+"/ClusterColour.mat");
        string[] housePaths={"Assets/ProjectLopHoc/YenTuFix/house_small.fbx","Assets/ProjectLopHoc/YenTuFix/house_porch.fbx","Assets/ProjectLopHoc/YenTuFix/house_two_storey.fbx"};
        var houseModels=housePaths.Select((p,i)=>ReadHouse(p,i)).ToArray();
        var beforeMaterials=new HashSet<Material>();var afterMaterials=new HashSet<Material>();
        for(int index=0;index<17;index++)
        {
            var record=manager.zones[index];EditorUtility.DisplayProgressBar("Railway environment",record.zoneId,index/17f);
            var scene=EditorSceneManager.OpenScene(record.scenePath,OpenSceneMode.Additive);var root=Find(scene,"RuntimeZoneContent");
            var info=new ZoneInfo{id=record.zoneId,type=Types[index],start=record.startChainage,end=record.endChainage};
            Audit(root,manifest,true,beforeMaterials);
            var vegetation=root.Find("Vegetation");if(vegetation!=null)foreach(Transform child in vegetation.Cast<Transform>().ToArray())if(child.name.StartsWith("VegetationChunk_")){var mesh=child.GetComponent<MeshFilter>()?.sharedMesh;if(mesh!=null)manifest.treesBefore+=mesh.GetTriangles(0).Length/42;UnityEngine.Object.DestroyImmediate(child.gameObject);}
            var houses=root.Find("Houses");if(houses!=null)foreach(Transform child in houses.Cast<Transform>().ToArray())if(child.name.StartsWith("RuralHouse_")){manifest.housesBefore++;if(PrefabUtility.IsPartOfPrefabInstance(child.gameObject))PrefabUtility.UnpackPrefabInstance(child.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);UnityEngine.Object.DestroyImmediate(child.gameObject);}
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))if(mf.name.StartsWith("TerrainZone_"))CarveValley(mf,route,manifest);
            var transitions=root.Find("TerrainTransitions");if(transitions!=null)foreach(var mf in transitions.GetComponentsInChildren<MeshFilter>(true))CutTransition(mf,route,manifest,scene.name);
            Physics.SyncTransforms();
            float a=record.startChainage,b=record.endChainage;Vector3 origin=route.At((a+b)*.5f,out _);var geometry=new Geometry();var random=new System.Random(13037+index*7919);
            int attempts=0;
            while(info.trees<Trees[index]&&attempts++<Trees[index]*60)
            {
                int cluster=info.trees/6,clusters=Mathf.Max(1,Mathf.CeilToInt(Trees[index]/6f));
                float d=Mathf.Lerp(a+35,b-35,(cluster+.5f)/clusters)+(float)(random.NextDouble()-.5)*55;
                d=Mathf.Clamp(d,a+12,b-12);if(!Allowed(d,manifest)||random.NextDouble()>Density(d,manifest))continue;
                float lateral=(cluster%2==0?1:-1)*(48+(cluster%3)*24+(float)(random.NextDouble()-.5)*23);
                Vector3 p=route.At(d,out var f)+Vector3.Cross(Vector3.up,f).normalized*lateral;p.y=GroundY(scene,p);
                route.Nearest(p,out float clearance);if(clearance<32)continue;
                float height=7+(float)random.NextDouble()*5;int variant=info.trees%2;
                AddTree(geometry,p-origin,height,variant,random);
                info.trees++;manifest.minimumDecorClearance=Mathf.Min(manifest.minimumDecorClearance,clearance-height*.3f);
                manifest.placements.Add(new Placement{zone=record.zoneId,kind="Tree",variant=variant,chainage=d,lateral=lateral,height=height,position=p});
            }
            attempts=0;
            while(info.houses<Houses[index]&&attempts++<Houses[index]*60)
            {
                int cluster=info.houses/4,clusters=Mathf.Max(1,Mathf.CeilToInt(Houses[index]/4f));float d=Mathf.Lerp(a+45,b-45,(cluster+.5f)/clusters)+(float)(random.NextDouble()-.5)*65;d=Mathf.Clamp(d,a+20,b-20);
                if(!Allowed(d,manifest)||random.NextDouble()>Density(d,manifest))continue;
                float lateral=(cluster%2==0?1:-1)*(65+(info.houses%4)*20+(float)random.NextDouble()*12);
                Vector3 p=route.At(d,out var f)+Vector3.Cross(Vector3.up,f).normalized*lateral;p.y=GroundY(scene,p);
                route.Nearest(p,out float clearance);if(clearance<40)continue;
                int variant=info.houses%3;var model=houseModels[variant];float scale=Mathf.Min((variant==2?9f:7f)/Mathf.Max(model.bounds.size.y,.01f),18/Mathf.Max(model.bounds.size.x,model.bounds.size.z));
                float radius=new Vector2(model.bounds.size.x,model.bounds.size.z).magnitude*scale*.5f;if(clearance-radius<25)continue;
                Quaternion rotation=Quaternion.LookRotation(f,Vector3.up)*Quaternion.Euler(0,(float)(random.NextDouble()-.5)*24,0);
                AppendHouse(geometry,model,p-origin,rotation,scale);
                info.houses++;manifest.minimumDecorClearance=Mathf.Min(manifest.minimumDecorClearance,clearance-radius);
                manifest.placements.Add(new Placement{zone=record.zoneId,kind="House",variant=variant,chainage=d,lateral=lateral,height=model.bounds.size.y*scale,position=p});
            }
            if(geometry.v.Count>0)CreateMeshObject(scene,root,Owned,origin,geometry,material,Folder+"/"+record.zoneId+"_Cluster.asset");
            if(record.zoneId==bridge.zoneId)BuildBridge(scene,root,route,manifest,manager.GetComponent<RailwayMicroChunkStreamer>().data);
            var activation=root.GetComponent<RailwayZoneActivation>();
            var stages=activation.chunks.Where(g=>g!=null).ToList();var density=root.Find(Owned);if(density!=null){density.gameObject.SetActive(false);stages.Add(density.gameObject);}activation.chunks=stages.Distinct().ToArray();
            Audit(root,manifest,false,afterMaterials);manifest.treesAfter+=info.trees;manifest.housesAfter+=info.houses;manifest.zones.Add(info);
            EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(scene,true);
        }
        manifest.materialsBefore=beforeMaterials.Count;manifest.materialsAfter=afterMaterials.Count;
        bridge.startChainage=manifest.bridgeStart;bridge.endChainage=manifest.bridgeEnd;bridge.zoneIds=manager.zones.Where(z=>z.endChainage>=bridge.startChainage&&z.startChainage<=bridge.endChainage).Select(z=>z.zoneId).ToArray();
        EditorUtility.SetDirty(manager);EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);AssetDatabase.SaveAssets();
        File.WriteAllText(Folder+"/Manifest.json",JsonUtility.ToJson(manifest,true));
        var report=new StringBuilder();report.AppendLine($"Trees {manifest.treesBefore}->{manifest.treesAfter}; houses {manifest.housesBefore}->{manifest.housesAfter}; zone renderers {manifest.renderersBefore}->{manifest.renderersAfter}; zone objects {manifest.objectsBefore}->{manifest.objectsAfter}; zone colliders {manifest.collidersBefore}->{manifest.collidersAfter}; unique zone materials {manifest.materialsBefore}->{manifest.materialsAfter}; minimum decor clearance after radius allowance={manifest.minimumDecorClearance:F1}m.");
        report.AppendLine($"Bridge {manifest.bridgeBeforeEnd-manifest.bridgeBeforeStart:F1}->{length:F1}m; {manifest.bridgeStart:F1}..{manifest.bridgeEnd:F1}m; target reference200km/h: {manifest.crossingSeconds:F2}s; synchronized zones={string.Join(",",bridge.zoneIds)}. Rail/spline/seat/XR/movement/dashboard unchanged.");
        foreach(var z in manifest.zones)report.AppendLine($"{z.id}: {z.start:F0}..{z.end:F0}m | {z.type} | trees={z.trees}, houses={z.houses}");
        File.WriteAllText("Temp/RailwayEnvironmentDensity_Implementation.txt",report.ToString());AssetDatabase.Refresh();
    }
    static bool Allowed(float d,Manifest m)=>d>180&&d<7820&&!(d>m.bridgeStart-90&&d<m.bridgeEnd+90);
    static float Density(float d,Manifest m){if(d<m.bridgeStart&&d>m.bridgeStart-350)return Mathf.Clamp01((m.bridgeStart-d)/350);if(d>m.bridgeEnd&&d<m.bridgeEnd+350)return Mathf.Clamp01((d-m.bridgeEnd)/350);return 1;}
    static float GroundY(Scene s,Vector3 p)
    {
        foreach(var root in s.GetRootGameObjects())foreach(var mc in root.GetComponentsInChildren<MeshCollider>(true))if(mc.name.StartsWith("TerrainZone_")&&mc.Raycast(new Ray(p+Vector3.up*250,Vector3.down),out var hit,600))return hit.point.y;
        throw new InvalidOperationException("No terrain below placement "+p);
    }
    static void Audit(Transform root,Manifest m,bool before,HashSet<Material> materials)
    {int objects=root.GetComponentsInChildren<Transform>(true).Length,renderers=root.GetComponentsInChildren<Renderer>(true).Length,colliders=root.GetComponentsInChildren<Collider>(true).Length;if(before){m.objectsBefore+=objects;m.renderersBefore+=renderers;m.collidersBefore+=colliders;}else{m.objectsAfter+=objects;m.renderersAfter+=renderers;m.collidersAfter+=colliders;}foreach(var r in root.GetComponentsInChildren<Renderer>(true))foreach(var mat in r.sharedMaterials)if(mat!=null)materials.Add(mat);}
    static void AddTree(Geometry g,Vector3 p,float height,int variant,System.Random random)
    {
        int start=g.v.Count;var uv=new List<Vector2>();var trunk=new List<int>();var canopy=new List<int>();
        typeof(StaticRailwayTerrainEnvironmentMvp).GetMethod("AddTree",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{g.v,uv,trunk,canopy,p,height,height*.24f,variant==0 ? 0.12f : 0.75f});
        g.t.AddRange(trunk);g.t.AddRange(canopy);Color leaf=variant==0?new Color(.16f,.32f,.10f):new Color(.11f,.25f,.12f);leaf*=.85f+(float)random.NextDouble()*.3f;leaf.a=1;
        for(int i=start;i<g.v.Count;i++)g.c.Add(i-start<14?new Color(.22f,.13f,.07f):leaf);
    }
    sealed class House {public Vector3[] v;public Color32[] c;public int[] t;public Bounds bounds;}
    static House ReadHouse(string path,int variant)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)throw new InvalidOperationException("House asset missing "+path);
        var v=new List<Vector3>();var colors=new List<Color32>();var tris=new List<int>();
        foreach(var mf in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh=mf.sharedMesh;if(mesh==null)continue;var renderer=mf.GetComponent<MeshRenderer>();var mats=renderer.sharedMaterials;
            var matrix=Matrix4x4.Translate(-source.transform.position)*mf.transform.localToWorldMatrix;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                int offset=v.Count;Color color=new Color(.68f,.62f,.49f);string name=sub<mats.Length&&mats[sub]!=null?mats[sub].name.ToLowerInvariant():"";
                if(name.Contains("roof")||name.Contains("tile"))color=new Color(.38f,.16f,.11f);else if(name.Contains("glass")||name.Contains("window"))color=new Color(.14f,.24f,.27f);
                foreach(var vertex in mesh.vertices){v.Add(matrix.MultiplyPoint3x4(vertex));colors.Add(color);}
                var indices=mesh.GetTriangles(sub);for(int i=0;i<indices.Length;i+=3){tris.Add(offset+indices[i]);tris.Add(offset+indices[matrix.determinant<0?i+2:i+1]);tris.Add(offset+indices[matrix.determinant<0?i+1:i+2]);}
            }
        }
        if(tris.Count/3>12000)throw new InvalidOperationException("House exceeds low-poly budget "+path+": "+tris.Count/3);
        Bounds bounds=new Bounds(v[0],Vector3.zero);foreach(var p in v)bounds.Encapsulate(p);
        return new House{v=v.ToArray(),c=colors.ToArray(),t=tris.ToArray(),bounds=bounds};
    }
    static void AppendHouse(Geometry g,House house,Vector3 position,Quaternion rotation,float scale)
    {int first=g.v.Count;Vector3 pivot=new Vector3(house.bounds.center.x,house.bounds.min.y,house.bounds.center.z);for(int i=0;i<house.v.Length;i++){g.v.Add(position+rotation*((house.v[i]-pivot)*scale));g.c.Add(house.c[i]);}foreach(int index in house.t)g.t.Add(first+index);}
    static void CreateMeshObject(Scene scene,Transform parent,string name,Vector3 position,Geometry geometry,Material material,string path)
    {var mesh=geometry.Mesh(name);var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}var existing=parent.Find(name);var go=existing!=null?existing.gameObject:new GameObject(name);if(existing==null){SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetParent(parent);}go.transform.position=position;var filter=go.GetComponent<MeshFilter>();if(filter==null)filter=go.AddComponent<MeshFilter>();filter.sharedMesh=saved;var r=go.GetComponent<MeshRenderer>();if(r==null)r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
    static void CarveValley(MeshFilter filter,Route route,Manifest m)
    {
        var mesh=filter.sharedMesh;var vertices=mesh.vertices;bool changed=false;
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=filter.transform.TransformPoint(vertices[i]);float d=route.Nearest(p,out float lateral);if(d<m.bridgeStart||d>m.bridgeEnd||lateral>400)continue;
            float along=Mathf.SmoothStep(0,1,Mathf.InverseLerp(m.bridgeStart,m.bridgeStart+80,d))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(m.bridgeEnd-80,m.bridgeEnd,d)));
            float across=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(180,400,lateral));p.y=Mathf.Lerp(p.y,Mathf.Min(p.y,-18),along*across);vertices[i]=filter.transform.InverseTransformPoint(p);changed=true;
        }
        if(changed){mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);var collider=filter.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;}
    }
    static void CutTransition(MeshFilter filter,Route route,Manifest m,string zone)
    {
        var source=filter.sharedMesh;if(source==null)return;var vertices=source.vertices;var lists=new List<int>[source.subMeshCount];bool changed=false;
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            var old=source.GetTriangles(sub);var kept=new List<int>();
            for(int i=0;i<old.Length;i+=3){Vector3 p=filter.transform.TransformPoint((vertices[old[i]]+vertices[old[i+1]]+vertices[old[i+2]])/3);float d=route.Nearest(p,out _);if(d>m.bridgeStart&&d<m.bridgeEnd){changed=true;continue;}kept.Add(old[i]);kept.Add(old[i+1]);kept.Add(old[i+2]);}lists[sub]=kept;
        }
        if(!changed)return;
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name=filter.name+"_BridgeCut";for(int sub=0;sub<lists.Length;sub++)mesh.SetTriangles(lists[sub],sub);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/"+zone+"_"+filter.name+"_Cut.asset");filter.sharedMesh=mesh;
        if(lists.All(list=>list.Count==0))filter.GetComponent<Renderer>().enabled=false;
    }
    static void BuildBridge(Scene scene,Transform root,Route route,Manifest m,RailwayMicroChunkData rails)
    {
        var parent=root.Find("FixedInfrastructure_Bridge");if(parent==null)throw new InvalidOperationException("Existing fixed bridge group missing");
        float oldStart=m.bridgeBeforeStart,oldEnd=m.bridgeBeforeEnd;var geometry=new Geometry();Vector3 origin=route.At((m.bridgeStart+m.bridgeEnd)*.5f,out _);
        var track=rails.chunks.First(c=>c.start>m.bridgeStart&&c.end<m.bridgeBeforeStart).right;
        var railMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectLopHoc/Generated/QuestRailway/Resources/"+track.meshResource+".asset");
        float bottomOffset=track.position.y+railMesh.bounds.min.y*track.scale.y-route.At(m.bridgeStart,out _).y;
        for(float d=m.bridgeStart;d<m.bridgeEnd;d+=20)
        {
            float span=Mathf.Min(20,m.bridgeEnd-d);Vector3 p=route.At(d+span*.5f,out var f);var right=Vector3.Cross(Vector3.up,f).normalized;var q=Quaternion.LookRotation(f,Vector3.up);
            geometry.Box(p+right*(-3.200011f)+Vector3.up*(bottomOffset-.55f)-origin,q,new Vector3(14,1.1f,span+.1f),new Color(.36f,.37f,.35f));
            foreach(float side in new[]{3.5f,-9.900022f})foreach(float y in new[]{.55f,1.05f})geometry.Box(p+right*side+Vector3.up*y-origin,q,new Vector3(.09f,.09f,span+.1f),new Color(.56f,.58f,.57f));
        }
        for(float d=m.bridgeStart;d<=m.bridgeEnd;d+=12)
        {Vector3 p=route.At(d,out var f);var right=Vector3.Cross(Vector3.up,f).normalized;var q=Quaternion.LookRotation(f,Vector3.up);foreach(float side in new[]{3.5f,-9.900022f})geometry.Box(p+right*side+Vector3.up*.45f-origin,q,new Vector3(.12f,1.2f,.12f),new Color(.46f,.48f,.47f));}
        for(float d=m.bridgeStart+20;d<m.bridgeEnd;d+=40)
        {Vector3 p=route.At(d,out var f);var right=Vector3.Cross(Vector3.up,f).normalized;float top=p.y+bottomOffset-1.1f;foreach(float side in new[]{0f,-6.400022f})geometry.Box(p+right*side+Vector3.up*((top-18)*.5f-p.y)-origin,Quaternion.LookRotation(f,Vector3.up),new Vector3(1.2f,top+18,1.2f),new Color(.31f,.32f,.30f));}
        var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectLopHoc/Generated/QuestRailway/RailwayFlatColour.mat");CreateMeshObject(scene,parent,"ExtendedBridgeQuest",origin,geometry,mat,Folder+"/ExtendedBridge.asset");
        var water=new Geometry();int rows=Mathf.CeilToInt((m.bridgeEnd-m.bridgeStart)/20);for(int row=0;row<=rows;row++){Vector3 p=route.At(Mathf.Lerp(m.bridgeStart,m.bridgeEnd,row/(float)rows),out var f);p.y=-8;var right=Vector3.Cross(Vector3.up,f).normalized;water.v.Add(p-right*310-origin);water.v.Add(p+right*310-origin);water.c.Add(Color.white);water.c.Add(Color.white);if(row<rows){int k=row*2;water.t.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}}
        var wm=AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectLopHoc/Generated/EnvironmentVisualFix/Water_Opaque.mat");
        var oldWater=Find(scene,"River_Crossing_Below_Existing_Bridge");if(oldWater!=null)UnityEngine.Object.DestroyImmediate(oldWater.gameObject);
        CreateMeshObject(scene,parent,"BridgeValleyWaterQuest",origin,water,wm,Folder+"/BridgeWater.asset");
    }
}
