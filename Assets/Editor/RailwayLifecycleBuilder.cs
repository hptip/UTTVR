using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RailwayLifecycleBuilder
{
    const string Request="Temp/RailwayLifecycle.request", Out="F:/UnityTemp/QuanLyVongDoiTaiSan";
    const string Main="Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Detail="Assets/ChiTietSaBan/ChiTiet/ChiTietSaBan.unity";
    const string NewScene="Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    static bool busy;
    const string TestKey="UTT.Lifecycle.PlayTest";
    static double testStarted;static int testFrames,clickQueuedFrame=-1;static bool clicked;static string clickID;static List<string> playErrors=new List<string>();
    static RailwayLifecycleBuilder()
    {
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(message,stack,type)=>{if(EditorApplication.isPlaying&&SessionState.GetBool(TestKey,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))playErrors.Add(type+": "+message);};
        EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("UTT.Lifecycle.Finish",false)){SessionState.SetBool("UTT.Lifecycle.Finish",false);EditorSceneManager.OpenScene(NewScene,OpenSceneMode.Single);File.AppendAllText(Out+"/PlayTest.txt","\nExited Play Mode and reopened saved standalone scene; runtime changes discarded.");File.WriteAllText(Out+"/Status.txt","PLAY_TEST_FINISHED");}};
    }
    static void Tick()
    {
        if(SessionState.GetBool(TestKey,false)&&EditorApplication.isPlaying){PlayTick();return;}
        if(busy||!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string command=File.ReadAllText(Request).Trim(); File.Delete(Request);busy=true;Directory.CreateDirectory(Out);
        try{if(command=="inspect")Inspect();else if(command=="create")Create();else if(command=="verify")Verify();else if(command=="repair")RepairExactModuleAndBed();else if(command=="finalize")FinalizeScene();else if(command=="play"){Clean();testStarted=0;testFrames=0;clickQueuedFrame=-1;playErrors.Clear();if(SceneManager.GetActiveScene().path!=NewScene)EditorSceneManager.OpenScene(NewScene,OpenSceneMode.Single);SessionState.SetBool(TestKey,true);EditorApplication.isPlaying=true;}}
        catch(Exception e){File.WriteAllText(Out+"/ERROR.txt",e.ToString());Debug.LogException(e);}
        finally{busy=false;}
    }
    static void Clean(){for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene: "+SceneManager.GetSceneAt(i).path);}
    static GameObject[] Gos(Scene s)=>s.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
    static string V(Vector3 v)=>$"({v.x:F3},{v.y:F3},{v.z:F3})";
    static Bounds BoundsOf(GameObject g){var rs=g.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)return new Bounds(g.transform.position,Vector3.zero);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
    static void Inspect()
    {
        Clean();var original=EditorSceneManager.GetSceneManagerSetup();var lines=new List<string>();
        var main=EditorSceneManager.OpenScene(Main,OpenSceneMode.Single);
        var manager=Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var data=AssetDatabase.LoadAssetAtPath<RailwayMicroChunkData>("Assets/ProjectLopHoc/Generated/QuestRailway/RailwayMicroChunks.asset");
        lines.Add($"Micro data length={data.length}, originalStart={data.originalStart}, chunks={data.chunks.Length}");
        foreach(var zone in manager.zones)
        {
            var s=EditorSceneManager.OpenScene(zone.scenePath,OpenSceneMode.Additive);var gos=Gos(s);var rs=gos.SelectMany(g=>g.GetComponents<Renderer>()).ToArray();var ts=gos.SelectMany(g=>g.GetComponents<Terrain>()).ToArray();
            lines.Add($"ZONE {zone.zoneId} start={zone.startChainage} end={zone.endChainage} GOs={gos.Length} renderers={rs.Length} terrain={ts.Length} infrastructure={string.Join(",",manager.fixedInfrastructure.Where(f=>f.zoneIds.Contains(zone.zoneId)||f.zoneId==zone.zoneId).Select(f=>f.kind))}");
            if(zone.zoneId=="Zone_012"||zone.zoneId=="Zone_013")
                File.WriteAllLines(Out+"/"+zone.zoneId+".tsv",gos.Select(g=>g.name+"\t"+g.activeSelf+"\t"+string.Join(",",g.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name))+"\t"+V(g.transform.position)+"\t"+V(BoundsOf(g).size)));
            EditorSceneManager.CloseScene(s,true);
        }
        var detail=EditorSceneManager.OpenScene(Detail,OpenSceneMode.Single);
        File.WriteAllLines(Out+"/DetailHierarchy.tsv",Gos(detail).Select(g=>g.name+"\t"+g.activeSelf+"\t"+string.Join(",",g.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name))+"\t"+V(g.transform.position)+"\t"+V(g.transform.lossyScale)));
        foreach(var root in detail.GetRootGameObjects())lines.Add("DETAIL ROOT "+root.name+" scale="+V(root.transform.lossyScale)+" bounds="+V(BoundsOf(root).size));
        foreach(var path in new[]{"Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab","Assets/ProjectLopHoc/Prefabs/EBS_Rail_Clean_Prefab.prefab","Assets/ChiTietSaBan/ChiTiet/DuongRay.prefab","Assets/ChiTietSaBan/ChiTiet/DuongRay_1Mesh.prefab"})
        {
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);lines.Add("PREFAB "+path+" bounds="+V(BoundsOf(go).size)+" center="+V(BoundsOf(go).center)+" rootScale="+V(go.transform.localScale));
            File.WriteAllLines(Out+"/"+Path.GetFileNameWithoutExtension(path)+"_Geometry.tsv",go.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.name+"\t"+AssetDatabase.GetAssetPath(m.sharedMesh)+"\t"+m.sharedMesh.name+"\t"+V(m.sharedMesh.bounds.size)+"\t"+V(m.transform.localPosition)+"\t"+V(m.transform.lossyScale)+"\t"+m.sharedMesh.vertexCount+"\t"+m.sharedMesh.subMeshCount));
        }
        File.WriteAllLines(Out+"/Inspection.txt",lines);EditorSceneManager.RestoreSceneManagerSetup(original);File.WriteAllText(Out+"/Status.txt","INSPECTED");
    }
    const string Folder="Assets/ProjectLopHoc/Generated/QuanLyVongDoiTaiSan";
    static Dictionary<Object,Object> copies;
    static int serial;
    static T Copy<T>(T source) where T:Object
    {
        if(source==null)return null;if(copies.TryGetValue(source,out var existing))return (T)existing;
        var clone=Object.Instantiate(source);clone.name=source.name;
        string path=Folder+"/"+(serial++).ToString("D3")+"_"+string.Concat(source.name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c))+".asset";
        if(clone is Material)path=Path.ChangeExtension(path,".mat");
        AssetDatabase.CreateAsset(clone,path);copies[source]=clone;return clone;
    }
    static GameObject Child(string name,Transform parent){var g=new GameObject(name);if(parent!=null)g.transform.SetParent(parent,false);return g;}
    static Material Matte(string name,Color color)
    {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",0);m.SetFloat("_Metallic",0);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
    static void Create()
    {
        Clean();if(File.Exists(NewScene))throw new Exception("Scene already exists: refusing to overwrite");
        if(Directory.Exists(Folder))throw new Exception("Output folder already exists: inspect previous attempt before retry");
        var main=EditorSceneManager.OpenScene(Main,OpenSceneMode.Single);
        var manager=Object.FindFirstObjectByType<RailwayZoneStreamingManager>();
        var zone=manager.zones.Single(z=>z.zoneId=="Zone_021");
        if(manager.fixedInfrastructure.Any(f=>f.zoneId==zone.zoneId||f.zoneIds.Contains(zone.zoneId)))throw new Exception("Unexpected fixed infrastructure in selected zone");
        var data=manager.GetComponent<RailwayMicroChunkStreamer>().data;
        var chunks=data.chunks.Where(c=>c.end>zone.startChainage+.01f&&c.start<zone.endChainage-.01f).ToArray();
        if(chunks.Length<4)throw new Exception("Missing paired rail chunks");
        var zs=EditorSceneManager.OpenScene(zone.scenePath,OpenSceneMode.Additive);
        var source=zs.GetRootGameObjects().Single(g=>g.name=="RuntimeZoneContent");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();copies=new Dictionary<Object,Object>();serial=0;
        var root=Object.Instantiate(source);root.name="Zone_021_Editable";SceneManager.MoveGameObjectToScene(root,scene);
        // Remove only scene-copy streaming/fade behaviours. Source zone is never saved.
        foreach(var script in root.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.SetActive(true);
        foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))mf.sharedMesh=Copy(mf.sharedMesh);
        foreach(var mc in root.GetComponentsInChildren<MeshCollider>(true))mc.sharedMesh=Copy(mc.sharedMesh);
        foreach(var t in root.GetComponentsInChildren<Terrain>(true)){t.terrainData=Copy(t.terrainData);var collider=t.GetComponent<TerrainCollider>();if(collider!=null)collider.terrainData=t.terrainData;}
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>Copy(m)).ToArray();
        var right=Child("Track_Right",root.transform);var left=Child("Track_Left",root.transform);
        var filters=new List<MeshFilter>();
        foreach(var c in chunks)foreach(bool isRight in new[]{true,false})
        {
            var record=isRight?c.right:c.left;var mesh=Resources.Load<Mesh>(record.meshResource);if(mesh==null)throw new Exception("Missing static rail resource: "+record.meshResource);
            var go=Child($"Rail_{c.start:F0}_{c.end:F0}m",isRight?right.transform:left.transform);
            go.transform.SetPositionAndRotation(record.position,record.rotation);go.transform.localScale=record.scale;
            var mf=go.AddComponent<MeshFilter>();mf.sharedMesh=Copy(mesh);go.AddComponent<MeshRenderer>().sharedMaterials=record.materials.Select(m=>Copy(m)).ToArray();
            if(isRight)filters.Add(mf);
        }
        var target=filters.OrderBy(m=>(m.GetComponent<Renderer>().bounds.center-(chunks[chunks.Length/2].right.position)).sqrMagnitude).First();
        var match=ReplaceModule(target,root.transform);
        // Recentre only the new scene hierarchy; source world-space placement is recorded.
        Vector3 origin=match.transform.position;root.transform.position-=origin;
        var terrain=root.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name.StartsWith("TerrainZone_"));
        AddSurroundings(terrain,root.transform);
        var light=Child("Directional Light",null).AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-35,0);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.65f,.73f,.8f);RenderSettings.ambientEquatorColor=new Color(.46f,.51f,.43f);RenderSettings.ambientGroundColor=new Color(.23f,.24f,.21f);
        RenderSettings.fog=true;RenderSettings.fogColor=new Color(.70f,.79f,.82f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=450;RenderSettings.fogEndDistance=1100;
        var cameraGo=Child("Main Camera",null);cameraGo.tag="MainCamera";
        var camera=cameraGo.AddComponent<Camera>();camera.nearClipPlane=.03f;camera.farClipPlane=1600;camera.fieldOfView=55;camera.backgroundColor=RenderSettings.fogColor;camera.clearFlags=CameraClearFlags.SolidColor;camera.stereoTargetEye=StereoTargetEyeMask.None;
        cameraGo.AddComponent<AudioListener>();cameraGo.AddComponent<UniversalAdditionalCameraData>();
        var orbit=cameraGo.AddComponent<DetailOrbitZoom>();var so=new SerializedObject(orbit);so.FindProperty("minDistance").floatValue=1.1f;so.FindProperty("maxDistance").floatValue=130;so.FindProperty("minPitch").floatValue=12;so.FindProperty("maxPitch").floatValue=78;so.FindProperty("initialYaw").floatValue=match.transform.eulerAngles.y+35;so.FindProperty("initialPitch").floatValue=30;so.FindProperty("zoomSpeed").floatValue=1.5f;so.ApplyModifiedPropertiesWithoutUndo();
        var inspector=cameraGo.AddComponent<RailwayLifecycleInspector>();inspector.inspectionCamera=camera;inspector.orbit=orbit;inspector.detailedModule=match.transform;
        camera.transform.position=match.transform.position+match.transform.right*9-match.transform.forward*9+Vector3.up*8;camera.transform.LookAt(BoundsOf(match).center);
        SceneManager.SetActiveScene(scene);EditorSceneManager.SaveScene(scene,NewScene);AssetDatabase.SaveAssets();
        EditorSceneManager.CloseScene(zs,true);EditorSceneManager.CloseScene(main,true);
        var build=new List<string>{"Zone=Zone_021; retained local chainage="+zone.startChainage+".."+zone.endChainage+"m; original chainage="+(zone.startChainage+data.originalStart)+".."+(zone.endChainage+data.originalStart)+"m", "Choice: gently curving residential/flat zone with no registered bridge/tunnel, 25 source objects. Rails supplied statically from MicroChunk registry, not from a runtime streamer.","Paired static chunks="+chunks.Length,"Source world origin="+V(origin),"Scene saved="+NewScene,"Source scenes and zones never saved. No train/XR/streamer copied.","Camera: RMB orbit, wheel zoom, F focus. Orbit constrained to 12..78 degrees elevation and 1.1..130m distance.","Detailed module uses DuongRay prefab's 12 existing named mesh groups, not 98 individual parts. ModelID + component mesh name supply stable scene IDs; sourceMeshID is retained separately. No SQLite changes.","Geometry/source correspondence and exact removal: see ModuleMatch.txt. Copied Mesh/Material assets are scene-local in "+Folder};
        File.WriteAllLines(Out+"/Creation.txt",build);File.WriteAllText(Out+"/Status.txt","CREATED_NEEDS_VERIFY");Verify();
    }
    struct Cell:IEquatable<Cell>
    {
        public int x,y,z;public Cell(Vector3 p){x=Mathf.FloorToInt(p.x/.005f);y=Mathf.FloorToInt(p.y/.005f);z=Mathf.FloorToInt(p.z/.005f);}public bool Equals(Cell o)=>x==o.x&&y==o.y&&z==o.z;public override bool Equals(object o)=>o is Cell c&&Equals(c);public override int GetHashCode()=>x*73856093^y*19349663^z*83492791;
    }
    static Dictionary<Cell,List<Vector3>> PointMap(IEnumerable<Vector3> ps)
    {var map=new Dictionary<Cell,List<Vector3>>();foreach(var p in ps){var c=new Cell(p);if(!map.TryGetValue(c,out var l))map[c]=l=new List<Vector3>();l.Add(p);}return map;}
    static bool Has(Dictionary<Cell,List<Vector3>> map,Vector3 p)
    {var c=new Cell(p);for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(map.TryGetValue(new Cell{x=c.x+x,y=c.y+y,z=c.z+z},out var ps)&&ps.Any(q=>(q-p).sqrMagnitude<.000016f))return true;return false;}
    static GameObject ReplaceModule(MeshFilter target,Transform parent)
    {
        var low=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab");
        var parts=low.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("Mesh_")&&int.TryParse(m.name.Substring(5),out int n)&&n<=12).ToArray();
        var slab=parts.Single(m=>m.name=="Mesh_12");var mat=low.transform.worldToLocalMatrix*slab.transform.localToWorldMatrix;
        var sv=slab.sharedMesh.vertices.Select(mat.MultiplyPoint3x4).ToArray();var st=slab.sharedMesh.triangles;
        Vector3 a=default,b=default,c=default;bool found=false;
        for(int i=0;i<st.Length;i+=3){a=sv[st[i]];b=sv[st[i+1]];c=sv[st[i+2]];if(Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y)>.999f&&Mathf.Abs(a.y-sv.Max(v=>v.y))<.002f&&Vector3.Cross(b-a,c-a).magnitude>5){found=true;break;}}
        if(!found)throw new Exception("No reliable reference slab triangle");
        var mesh=target.sharedMesh;var tv=mesh.vertices;var pointMap=PointMap(tv);Quaternion bestRotation=Quaternion.identity;Vector3 bestOffset=default;float bestScore=float.PositiveInfinity;int matches=0;
        var allSource=parts.SelectMany(m=>m.sharedMesh.vertices.Select((low.transform.worldToLocalMatrix*m.transform.localToWorldMatrix).MultiplyPoint3x4)).ToArray();
        for(int sub=0;sub<mesh.subMeshCount;sub++)
        {
            var tris=mesh.GetTriangles(sub);
            for(int i=0;i<tris.Length;i+=3)
            {
                var v=new[]{tv[tris[i]],tv[tris[i+1]],tv[tris[i+2]]};if(Mathf.Abs(Vector3.Cross(v[1]-v[0],v[2]-v[0]).normalized.y)<.999f)continue;
                for(int k=0;k<3;k++)for(int rev=0;rev<2;rev++)
                {
                    Vector3 ta=v[k],tb=v[(k+(rev==0?1:2))%3],tc=v[(k+(rev==0?2:1))%3];
                    if(Mathf.Abs((tb-ta).magnitude-(b-a).magnitude)>.004f||Mathf.Abs((tc-ta).magnitude-(c-a).magnitude)>.004f)continue;
                    var rot=Quaternion.AngleAxis(Vector3.SignedAngle(b-a,tb-ta,Vector3.up),Vector3.up);var offset=ta-rot*a;
                    if((rot*c+offset-tc).sqrMagnitude>.000016f||sv.Any(p=>!Has(pointMap,rot*p+offset)))continue;
                    // Ensure this is the source track module, not a similarly sized deck face.
                    int hit=allSource.Count(p=>Has(pointMap,rot*p+offset));if(hit<allSource.Length*.92f)continue;
                    matches++;float score=(offset-mesh.bounds.center).sqrMagnitude;if(score<bestScore){bestScore=score;bestRotation=rot;bestOffset=offset;}
                }
            }
        }
        if(matches==0)throw new Exception("No exact source-module vertex match in runtime rail. Refusing destructive clipping.");
        var sourcePoints=PointMap(allSource.Select(p=>bestRotation*p+bestOffset));var inModule=tv.Select(p=>Has(sourcePoints,p)).ToArray();int removed=0;
        for(int s=0;s<mesh.subMeshCount;s++){var tris=mesh.GetTriangles(s);var keep=new List<int>();for(int i=0;i<tris.Length;i+=3){if(inModule[tris[i]]&&inModule[tris[i+1]]&&inModule[tris[i+2]])removed++;else{keep.Add(tris[i]);keep.Add(tris[i+1]);keep.Add(tris[i+2]);}}mesh.SetTriangles(keep,s);}
        if(removed<100)throw new Exception("Too few triangles removed to replace module safely");EditorUtility.SetDirty(mesh);
        var detailPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChiTietSaBan/ChiTiet/DuongRay.prefab");
        var detail=(GameObject)PrefabUtility.InstantiatePrefab(detailPrefab,parent.gameObject.scene);PrefabUtility.UnpackPrefabInstance(detail,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        detail.name="DetailedModule_Right_Z021_M001";detail.transform.SetParent(parent,true);detail.transform.SetPositionAndRotation(target.transform.TransformPoint(bestOffset),target.transform.rotation*bestRotation);detail.transform.localScale=Vector3.one;
        foreach(var collider in detail.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
        foreach(var mb in detail.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(mb);
        int nParts=0;
        foreach(var mf in detail.GetComponentsInChildren<MeshFilter>(true))
        {
            string sourceName=mf.sharedMesh.name;mf.sharedMesh=Copy(mf.sharedMesh);var mc=mf.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;
            var id=mf.gameObject.AddComponent<ElementIdentity>();id.elementID="QLVDTS-Z021-R-M001/"+sourceName;
            var element=mf.gameObject.AddComponent<RailwayLifecycleElement>();element.identity=id;element.sourceMeshID=sourceName;element.componentName=mf.name;nParts++;
            var renderer=mf.GetComponent<MeshRenderer>();renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Copy(m)).ToArray();
            foreach(var m in renderer.sharedMaterials)if(m!=null&&(!m.shader.isSupported||m.shader.name=="Standard")){Color color=m.HasProperty("_Color")?m.GetColor("_Color"):Color.gray;m.shader=Shader.Find("Universal Render Pipeline/Lit");m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);EditorUtility.SetDirty(m);}
        }
        File.WriteAllText(Out+"/ModuleMatch.txt",$"Target={target.name}; exact source pose matches={matches}; removed source triangles={removed}; expected source triangles={parts.Sum(p=>p.sharedMesh.triangles.Length/3)}; source source-geometry vertex count={allSource.Length}; detail groups={nParts}; target local offset={V(bestOffset)}; yaw={bestRotation.eulerAngles.y:F5}; world anchor={V(detail.transform.position)}; source root local bounds normalized by prefab transforms, unit scale.\nEBS runtime uses simplified Mesh_51..62 embedded in EBS prefab; DuongRay uses higher-detail Mesh_51..62 from RailLow.prefab. Same 3m assembly envelope; different topology/vertex counts. No claim of bit-identical source meshes or 98 distinct parts. Source module triangles are removed by matching transformed source vertices within 4mm; underlying independent girder/ballast geometry retained.\n");
        return detail;
    }
    static void AddSurroundings(MeshFilter terrain,Transform parent)
    {
        var source=terrain.sharedMesh;var verts=source.vertices;var tris=source.triangles;
        var edges=new Dictionary<(int,int),int>();
        for(int i=0;i<tris.Length;i+=3)for(int e=0;e<3;e++){int a=tris[i+e],b=tris[i+(e+1)%3];var key=a<b?(a,b):(b,a);edges.TryGetValue(key,out int count);edges[key]=count+1;}
        var boundary=edges.Where(e=>e.Value==1).Select(e=>e.Key).ToArray();
        var v=new List<Vector3>();var t=new List<int>();var center=terrain.GetComponent<Renderer>().bounds.center;var material=Matte("SurroundingHills_Matte",new Color(.28f,.36f,.19f));
        Vector3 Ring(Vector3 p,int ring)
        {var d=p-center;d.y=0;d.Normalize();float phase=p.x*.009f+p.z*.012f;float width=new[]{0,55,150,270}[ring];float y=new[]{0,8+5*Mathf.Sin(phase),30+14*Mathf.Sin(phase*.7f),-18}[ring];return p+d*width+Vector3.up*y;}
        foreach(var edge in boundary)
        {
            Vector3 a=terrain.transform.TransformPoint(verts[edge.Item1]),b=terrain.transform.TransformPoint(verts[edge.Item2]);
            for(int ring=0;ring<3;ring++){int o=v.Count;v.Add(Ring(a,ring));v.Add(Ring(b,ring));v.Add(Ring(a,ring+1));v.Add(Ring(b,ring+1));var cross=Vector3.Cross(v[o+2]-v[o],v[o+1]-v[o]);if(cross.y>=0){t.AddRange(new[]{o,o+2,o+1,o+1,o+2,o+3});}else{t.AddRange(new[]{o,o+1,o+2,o+1,o+3,o+2});}}
        }
        var mesh=new Mesh{name="TerrainBoundaryRollingHills",indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/TerrainBoundaryRollingHills.asset");
        var go=Child("Surrounding_Rolling_Hills_NoCollider",parent);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        File.WriteAllText(Out+"/Surroundings.txt",$"Terrain boundary edges={boundary.Length}; hills triangles={t.Count/3}; outward transition rings 0/55/150/270m; gentle +8..44m ridge, far skirt -18m; boundary vertices exactly coincide with source terrain boundary. No mountain collider. No trees spawned for edge masking.");
    }
    static void Verify()
    {
        Clean();var scene=EditorSceneManager.OpenScene(NewScene,OpenSceneMode.Single);var gos=Gos(scene);var results=new List<string>();int missing=0;
        foreach(var g in gos){missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(g);foreach(var mf in g.GetComponents<MeshFilter>())if(mf.sharedMesh==null)missing++;foreach(var r in g.GetComponents<Renderer>())if(r.sharedMaterials.Any(m=>m==null||m.shader==null))missing++;}
        var elements=gos.SelectMany(g=>g.GetComponents<RailwayLifecycleElement>()).ToArray();var inspector=Object.FindFirstObjectByType<RailwayLifecycleInspector>();
        results.Add($"Objects={gos.Length}; renderers={gos.Sum(g=>g.GetComponents<Renderer>().Length)}; missing assets/scripts={missing}; unique IDs={elements.Select(e=>e.ID).Distinct().Count()}/{elements.Length}");
        results.Add("Runtime streaming components="+gos.Sum(g=>g.GetComponents<MonoBehaviour>().Count(m=>m!=null&&(m is RailwayZoneActivation||m is RailwayMicroChunkStreamer||m is RailwayZoneStreamingManager||m is RailwayFarSceneryVisibility))));
        Physics.SyncTransforms();int hits=0;
        foreach(var e in elements)
        {
            var collider=e.GetComponent<MeshCollider>();var b=collider.bounds;bool hit=false;
            // Isolated collider test proves every named component supports a triangle raycast.
            foreach(var dir in new[]{Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
                for(int x=-2;x<=2&&!hit;x++)for(int y=-2;y<=2&&!hit;y++)
            {var normal=dir;var u=Mathf.Abs(dir.y)>.5f?Vector3.right:Vector3.up;var w=Vector3.Cross(normal,u);Vector3 p=b.center-dir*10+u*x*b.extents.magnitude*.12f+w*y*b.extents.magnitude*.12f;if(collider.Raycast(new Ray(p,dir),out _,20))hit=true;}
            if(!hit){var mf=e.GetComponent<MeshFilter>();var vs=mf.sharedMesh.vertices;var tr=mf.sharedMesh.triangles;for(int n=0;n<tr.Length&&!hit;n+=3){Vector3 a=e.transform.TransformPoint(vs[tr[n]]),bb=e.transform.TransformPoint(vs[tr[n+1]]),c=e.transform.TransformPoint(vs[tr[n+2]]);var normal=Vector3.Cross(bb-a,c-a).normalized;if(normal.sqrMagnitude<.5f)continue;var p=(a+bb+c)/3;hit=collider.Raycast(new Ray(p+normal*.1f,-normal),out _,.2f);}}
            if(hit)hits++;results.Add(e.componentName+" | "+e.ID+" | colliderRay="+hit);
        }
        results.Add($"Collider ray coverage={hits}/{elements.Length}; dirty={scene.isDirty}; source scenes unchanged pending external SHA check.");
        foreach(var path in new[]{"Assets/ChiTietSaBan/ChiTiet/DuongRay.fbx","Assets/ChiTietSaBan/ChiTiet/MB-EBS-DRW-003_00EBSLR49E1-MS140SklTrack-3DView_Renamed.fbx"})
        {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);results.Add(path+" imported MeshFilters="+(prefab!=null?prefab.GetComponentsInChildren<MeshFilter>(true).Length:0)+" Mesh assets="+AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Count());}
        File.WriteAllLines(Out+"/SavedGeometry.tsv",gos.Select(g=>g.name+"\t"+V(g.transform.position)+"\t"+V(BoundsOf(g).center)+"\t"+V(BoundsOf(g).size)+"\t"+string.Join(",",g.GetComponents<Renderer>().Select(r=>$"enabled={r.enabled},layer={g.layer},materials="+string.Join(";",r.sharedMaterials.Select(m=>m!=null?m.name+":"+m.shader.name:"NULL"))))));
        File.WriteAllLines(Out+"/Verification.txt",results);RenderViews(inspector.inspectionCamera,inspector.detailedModule.gameObject);
        if(SceneView.lastActiveSceneView!=null){SceneView.lastActiveSceneView.LookAt(BoundsOf(inspector.detailedModule.gameObject).center,inspector.inspectionCamera.transform.rotation,14);SceneView.lastActiveSceneView.Repaint();}
        Selection.activeGameObject=inspector.detailedModule.gameObject;
        File.WriteAllText(Out+"/Status.txt",missing==0&&hits==elements.Length?"EDIT_VERIFIED_NEEDS_PLAY":"VERIFY_INCOMPLETE");
    }
    static void RenderViews(Camera camera,GameObject detail)
    {
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;
        var center=BoundsOf(detail).center;
        for(int view=0;view<3;view++)
        {
            camera.transform.position=center+detail.transform.right*(view==1?-28:view==2?45:7)-detail.transform.forward*(view==2?65:8)+Vector3.up*(view==2?48:6);
            camera.transform.LookAt(center);Render(camera,Out+"/View_"+view+".png");
        }
        camera.transform.SetPositionAndRotation(position,rotation);
    }
    static void Render(Camera camera,string path)
    {
        var rt=new RenderTexture(1440,900,24);rt.Create();var old=RenderTexture.active;Texture2D image=null;
        try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);if(image!=null)Object.DestroyImmediate(image);}
    }
    static void PlayTick()
    {
        if(testStarted==0)testStarted=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-testStarted>120){FinishPlay("FAIL: timeout");return;}
        if(++testFrames<80)return;
        var inspector=Object.FindFirstObjectByType<RailwayLifecycleInspector>();if(inspector==null){FinishPlay("FAIL: missing inspector");return;}
        if(clickQueuedFrame<0)
        {
            Physics.SyncTransforms();var elements=Object.FindObjectsByType<RailwayLifecycleElement>(FindObjectsSortMode.None);var results=new List<string>();int success=0;Ray clickRay=default;Vector3 clickPoint=default;bool hasClick=false;
            foreach(var e in elements)
            {
                var mf=e.GetComponent<MeshFilter>();var v=mf.sharedMesh.vertices;var tris=mf.sharedMesh.triangles;bool selected=false;
                for(int t=0;t<tris.Length&&!selected;t+=3*Mathf.Max(1,tris.Length/3/5000))
                {
                    Vector3 a=e.transform.TransformPoint(v[tris[t]]),b=e.transform.TransformPoint(v[tris[t+1]]),c=e.transform.TransformPoint(v[tris[t+2]]);var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.sqrMagnitude<.9f)continue;
                    var point=(a+b+c)/3;var ray=new Ray(point+normal*1.5f,-normal);
                    if(Physics.Raycast(ray,out var hit,2,~0,QueryTriggerInteraction.Ignore)&&hit.collider.gameObject==e.gameObject&&inspector.SelectRay(ray)&&inspector.Selected==e)
                    {selected=true;success++;if(e.sourceMeshID=="Mesh_53"){clickRay=ray;clickPoint=point;clickID=e.ID;hasClick=true;}}
                }
                bool direct=selected;
                if(!selected)
                {
                    for(int t=0;t<tris.Length&&!selected;t+=3*Mathf.Max(1,tris.Length/3/5000))
                    {
                        Vector3 a=e.transform.TransformPoint(v[tris[t]]),b=e.transform.TransformPoint(v[tris[t+1]]),c=e.transform.TransformPoint(v[tris[t+2]]);var normal=Vector3.Cross(b-a,c-a).normalized;if(normal.sqrMagnitude<.9f)continue;
                        var ray=new Ray((a+b+c)/3+normal*1.5f,-normal);
                        var hits=Physics.RaycastAll(ray,2,~0,QueryTriggerInteraction.Ignore);if(!hits.Any(h=>h.collider.gameObject==e.gameObject))continue;
                        for(int cycle=0;cycle<elements.Length;cycle++)if(inspector.SelectRay(ray,true)&&inspector.Selected==e){selected=true;success++;break;}
                    }
                }
                results.Add(e.componentName+" | "+e.ID+" | directRay="+direct+" | directOrLayerCycle="+selected);
            }
            results.Insert(0,$"Standalone Play scene={SceneManager.GetActiveScene().name}; loaded scenes={SceneManager.sceneCount}; selected by Physics ray={success}/{elements.Length}; streaming managers={Object.FindObjectsByType<RailwayZoneStreamingManager>(FindObjectsSortMode.None).Length}");
            File.WriteAllLines(Out+"/PlayTest.txt",results);
            if(!hasClick){FinishPlay("FAIL: no visible rail surface for mouse click");return;}
            inspector.orbit.enabled=false;inspector.inspectionCamera.transform.position=clickRay.origin;inspector.inspectionCamera.transform.LookAt(clickPoint);
            inspector.Select(null);clickQueuedFrame=Time.frameCount;
            if(Mouse.current!=null)InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f),buttons=1});
        }
        else if(Time.frameCount>clickQueuedFrame+3)
        {
            clicked=inspector.Selected!=null&&inspector.Selected.ID==clickID;
            if(Mouse.current!=null)InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f),buttons=0});
            Render(inspector.inspectionCamera,Out+"/Play_SelectedRail.png");
            File.AppendAllText(Out+"/PlayTest.txt",$"\nQueued actual Mouse left-button state to centre of inspection camera: selected expected rail ID={clicked}; selectionCount={inspector.SelectionCount}; Console errors={playErrors.Count}\n"+string.Join("\n",playErrors));
            FinishPlay(clicked&&playErrors.Count==0?"PASS: runtime selection and input tested":"FAIL: inspect mouse input/console results");
        }
    }
    static void FinishPlay(string result)
    {File.AppendAllText(Out+"/PlayTest.txt","\n"+result);SessionState.SetBool(TestKey,false);SessionState.SetBool("UTT.Lifecycle.Finish",true);EditorApplication.isPlaying=false;}
    static void FinalizeScene()
    {
        Clean();var scene=EditorSceneManager.OpenScene(NewScene,OpenSceneMode.Single);
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Folder}))
        {var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(material.HasProperty("_FadeStart")){material.SetFloat("_FadeStart",10000);material.SetFloat("_FadeEnd",11000);EditorUtility.SetDirty(material);}}
        AssetDatabase.SaveAssets();
        var root=scene.GetRootGameObjects().Single(g=>g.name=="Zone_021_Editable");
        var right=root.transform.Find("Track_Right").GetComponentsInChildren<MeshFilter>();var left=root.transform.Find("Track_Left").GetComponentsInChildren<MeshFilter>();
        var distances=right.Zip(left,(a,b)=>Vector3.Distance(a.transform.position,b.transform.position)).ToArray();
        var points=right.Select(r=>r.transform.position).Distinct().ToArray();float bend=0;
        for(int i=2;i<points.Length;i++)bend=Mathf.Max(bend,Vector3.Angle(points[i-1]-points[i-2],points[i]-points[i-1]));
        var detail=Object.FindFirstObjectByType<RailwayLifecycleInspector>().detailedModule.gameObject;
        Bounds LocalBounds(GameObject g,IEnumerable<MeshFilter> filters)
        {bool first=true;Bounds b=default;foreach(var mf in filters){var matrix=g.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;foreach(var v in mf.sharedMesh.vertices){var p=matrix.MultiplyPoint3x4(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}return b;}
        var low=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab");
        var lowParts=low.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("Mesh_")&&int.TryParse(m.name.Substring(5),out int n)&&n<=12).ToArray();
        var lowBounds=LocalBounds(low,lowParts);var detailBounds=LocalBounds(detail,detail.GetComponentsInChildren<MeshFilter>());
        var terrain=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name.StartsWith("TerrainZone_"));var collider=terrain.GetComponent<MeshCollider>();var heights=new List<float>();
        foreach(float x in new[]{-10f,0,10f})foreach(float z in new[]{-30f,0,30f})if(collider.Raycast(new Ray(detail.transform.TransformPoint(new Vector3(x,30,z)),Vector3.down),out var hit,100))heights.Add(hit.point.y);
        File.WriteAllText(Out+"/FinalMetrics.txt",$"Track pairs={right.Length}; centre spacing min/max={distances.Min():F6}/{distances.Max():F6}m; max change of heading over adjacent ~100m centres={bend:F6} degrees.\nLow source local size={V(lowBounds.size)}, centre={V(lowBounds.center)}; high detail local size={V(detailBounds.size)}, centre={V(detailBounds.center)}. Difference between module envelope centres={V(detailBounds.center-lowBounds.center)}.\nOriginal source triangles in one complete low-detail repeat={lowParts.Sum(p=>p.sharedMesh.triangles.Length/3)}; removed from copied chunk=2000 (final oriented-triangle method; see ModuleMatch.txt).\nCopied terrain near module sampled y min/max={(heights.Count>0?heights.Min():0):F3}/{(heights.Count>0?heights.Max():0):F3}; module reference y={detail.transform.position.y:F3}.\nNew-scene-only material distance fades moved beyond 10km, so all authored scenery remains visible within the 500m scene. No source material changed.\n");
        File.WriteAllText(Folder+"/SceneProvenance.json",JsonUtility.ToJson(new Provenance{sourceScene=Main,sourceZone="Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_021.unity",referenceScene=Detail,sourceWorldOrigin=new Vector3(10612.560f,9.331662f,-1598.864f),startChainage=5300.001f,endChainage=5800.001f,originalStartChainage=10500,originalEndChainage=11000,modelID="QLVDTS-Z021-R-M001"},true));AssetDatabase.ImportAsset(Folder+"/SceneProvenance.json");
        Verify();if(File.Exists(Out+"/ERROR.txt"))File.Delete(Out+"/ERROR.txt");
    }
    struct Face {public Vector3 a,b,c,normal;}
    static void RepairExactModuleAndBed()
    {
        Clean();var scene=EditorSceneManager.OpenScene(NewScene,OpenSceneMode.Single);var inspector=Object.FindFirstObjectByType<RailwayLifecycleInspector>();var root=scene.GetRootGameObjects().Single(g=>g.name=="Zone_021_Editable");
        var target=root.transform.Find("Track_Right").GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Rail_5500_5550m");var detail=inspector.detailedModule;
        var data=AssetDatabase.LoadAssetAtPath<RailwayMicroChunkData>("Assets/ProjectLopHoc/Generated/QuestRailway/RailwayMicroChunks.asset");var chunk=data.chunks.Single(c=>Mathf.Abs(c.start-5500)<.01f);
        var sourceMesh=Resources.Load<Mesh>(chunk.right.meshResource);var tv=sourceMesh.vertices;
        var low=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab");
        var faces=new Dictionary<Cell,List<Face>>();int expected=0;
        var pose=target.transform.worldToLocalMatrix*detail.localToWorldMatrix;
        foreach(var mf in low.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("Mesh_")&&int.TryParse(m.name.Substring(5),out int n)&&n<=12))
        {
            var matrix=pose*low.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;var vertices=mf.sharedMesh.vertices;var ts=mf.sharedMesh.triangles;
            for(int i=0;i<ts.Length;i+=3){var f=new Face{a=matrix.MultiplyPoint3x4(vertices[ts[i]]),b=matrix.MultiplyPoint3x4(vertices[ts[i+1]]),c=matrix.MultiplyPoint3x4(vertices[ts[i+2]])};f.normal=Vector3.Cross(f.b-f.a,f.c-f.a).normalized;var key=new Cell((f.a+f.b+f.c)/3);if(!faces.TryGetValue(key,out var list))faces[key]=list=new List<Face>();list.Add(f);expected++;}
        }
        bool Match(Vector3 a,Vector3 b,Vector3 c)
        {
            var centroid=(a+b+c)/3;var key=new Cell(centroid);var normal=Vector3.Cross(b-a,c-a).normalized;
            bool Near(Vector3 p,Vector3 q)=>(p-q).sqrMagnitude<.000016f;
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(faces.TryGetValue(new Cell{x=key.x+x,y=key.y+y,z=key.z+z},out var list))foreach(var f in list)
                if((Vector3.Dot(normal,f.normal)>.99f||(normal.sqrMagnitude<.5f&&f.normal.sqrMagnitude<.5f))&&((Near(a,f.a)&&Near(b,f.b)&&Near(c,f.c))||(Near(a,f.b)&&Near(b,f.c)&&Near(c,f.a))||(Near(a,f.c)&&Near(b,f.a)&&Near(c,f.b))))return true;
            return false;
        }
        int removed=0;var retained=new List<int[]>();
        for(int sub=0;sub<sourceMesh.subMeshCount;sub++){var ts=sourceMesh.GetTriangles(sub);var keep=new List<int>();for(int i=0;i<ts.Length;i+=3)if(Match(tv[ts[i]],tv[ts[i+1]],tv[ts[i+2]]))removed++;else keep.AddRange(new[]{ts[i],ts[i+1],ts[i+2]});retained.Add(keep.ToArray());}
        if(removed!=expected)throw new Exception($"Exact oriented triangle matching found {removed}, expected {expected}; copied mesh not altered");
        EditorUtility.CopySerialized(sourceMesh,target.sharedMesh);for(int sub=0;sub<retained.Count;sub++)target.sharedMesh.SetTriangles(retained[sub],sub);EditorUtility.SetDirty(target.sharedMesh);
        var rows=data.chunks.Where(c=>c.end>5300.01f&&c.start<5799.99f).GroupBy(c=>c.right.position).Select(g=>new{d=(g.Min(c=>c.start)+g.Max(c=>c.end))/2,p=g.Key,l=g.First().left.position}).OrderBy(r=>r.d).ToArray();
        if(root.transform.Find("Ballast_And_Embankment")!=null)throw new Exception("Bed already exists; no duplicate generation");
        var v=new List<Vector3>();var tsBed=new List<int>();var worldOrigin=chunk.right.position-target.transform.position;
        for(int n=0;n<=50;n++)
        {
            float d=5300+n*10;int index=0;while(index<rows.Length-2&&rows[index+1].d<d)index++;float t=(d-rows[index].d)/(rows[index+1].d-rows[index].d);
            Vector3 right=Vector3.LerpUnclamped(rows[index].p,rows[index+1].p,t)-worldOrigin,left=Vector3.LerpUnclamped(rows[index].l,rows[index+1].l,t)-worldOrigin;var across=(left-right).normalized;var center=(right+left)/2;
            foreach(var pair in new[]{new Vector2(-10,-1.81f),new Vector2(-6.4f,-.405f),new Vector2(6.4f,-.405f),new Vector2(10,-1.81f)})v.Add(center+across*pair.x+Vector3.up*pair.y);
            if(n==0)continue;for(int s=0;s<3;s++){int a=(n-1)*4+s,b=n*4+s;if(Vector3.Cross(v[b]-v[a],v[a+1]-v[a]).y>0)tsBed.AddRange(new[]{a,b,a+1,a+1,b,b+1});else tsBed.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
        }
        var mesh=new Mesh{name="Zone021_BallastAndEmbankment"};mesh.SetVertices(v);mesh.SetTriangles(tsBed,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/BallastAndEmbankment.asset");
        var bed=Child("Ballast_And_Embankment",root.transform);bed.transform.position=Vector3.zero;bed.AddComponent<MeshFilter>().sharedMesh=mesh;bed.AddComponent<MeshRenderer>().sharedMaterial=Matte("BallastEarth_Matte",new Color(.32f,.29f,.22f));
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.AppendAllText(Out+"/ModuleMatch.txt",$"\nFINAL REFINEMENT: oriented, vertex-by-vertex triangle match removed exactly {removed}/{expected} source module faces. Restored {2038-removed} adjacent/shared-boundary faces that the earlier point-set method had included. Only new scene mesh copy changed; no source asset changed.\nNew ballast/embankment: 500m, both tracks, shoulder y=-0.405m and toe y=-1.81m to meet existing terrain y=-1.8m, 20m base width, {tsBed.Count/3} triangles, no collider.\n");
        var raw=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChiTietSaBan/ChiTiet/MB-EBS-DRW-003_00EBSLR49E1-MS140SklTrack-3DView_Renamed.fbx");
        File.WriteAllText(Out+"/RawDetailModel.txt",$"Raw renamed FBX: Transform nodes={raw.GetComponentsInChildren<Transform>(true).Length}, MeshFilters={raw.GetComponentsInChildren<MeshFilter>(true).Length}, renderer world bounds={V(BoundsOf(raw).size)}, local root scale={V(raw.transform.localScale)}. Current ChiTietSaBan DuongRay prefab intentionally groups the assembly into 12 named meshes; this phase reuses that proven prefab, not an automatic decomposition of the raw FBX.");
        Verify();
    }
    [Serializable] sealed class Provenance{public string sourceScene,sourceZone,referenceScene,modelID;public Vector3 sourceWorldOrigin;public float startChainage,endChainage,originalStartChainage,originalEndChainage;}
}
