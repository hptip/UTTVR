using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Splines;

/// <summary>One explicit, resumable Editor job. Never runs without its request file.</summary>
[InitializeOnLoad]
internal static class QuestRailwayOptimize
{
    const string Main="Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Folder="Assets/ProjectLopHoc/Generated/QuestRailway";
    const string ResourcesFolder=Folder+"/Resources/YenTuQuestRails";
    const string Request="Temp/QuestRailwayOptimize.request";
    static QuestRailwayOptimize()=>EditorApplication.update+=Tick;
    static string Phase {get=>SessionState.GetString("UTT.QuestOptimize.Phase","");set=>SessionState.SetString("UTT.QuestOptimize.Phase",value);}
    static StringBuilder report;
    static void Tick()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        try
        {
            string command=File.ReadAllText(Request).Trim();if(command.Length==0)return;
            File.Delete(Request);
            if(command=="run")Run();
            else if(command=="validate")Validate();
            else if(command=="trainvisuals")BatchTrainVisuals();
            else if(command=="buildlist")UpdateBuildSceneList();
            else if(command=="split50")SplitRailResources();
            else if(command=="finish") { SplitRailResources();UpdateBuildSceneList();Validate(); }
            else if(command=="preparetest") { EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();File.WriteAllText("Temp/QuestRailwayPureProfile.flag","No capture passes in final benchmark");File.WriteAllText("Temp/RailwayProfile.request","questFinal"); }
            else if(command=="facilities")BatchFixedFacilities();
            else if(command=="capturefinal")CaptureFinal();
        }
        catch(IOException e){Debug.LogError("[QuestOptimize] IO: "+e.Message);}
        catch(Exception e){File.WriteAllText("Temp/QuestRailwayOptimize_ERROR.txt",Phase+"\n"+e);Debug.LogException(e);}
        finally{EditorUtility.ClearProgressBar();}
    }
    sealed class RouteTable
    {
        public Vector3[] p;
        public float[] d;
        public float length;
        public RouteTable(SplineContainer route)
        {
            int n=30000;p=new Vector3[n+1];d=new float[n+1];
            for(int i=0;i<=n;i++){p[i]=route.EvaluatePosition(i/(float)n);if(i>0)d[i]=d[i-1]+Vector3.Distance(p[i-1],p[i]);}length=d[n];
        }
        public Vector3 At(float distance,out Vector3 forward)
        {
            int k=Array.BinarySearch(d,Mathf.Clamp(distance,0,length));if(k<0)k=~k;k=Mathf.Clamp(k,1,d.Length-1);
            forward=(p[k]-p[k-1]).normalized;return Vector3.Lerp(p[k-1],p[k],Mathf.InverseLerp(d[k-1],d[k],distance));
        }
        public float Nearest(Vector3 point)
        {
            float best=float.PositiveInfinity,result=0;
            for(int i=0;i<p.Length;i+=10){float v=(p[i]-point).sqrMagnitude;if(v<best){best=v;result=d[i];}}return result;
        }
    }
    [Serializable] sealed class Migration
    {
        public float oldLength,start,end,length;
        public int firstZone,lastZone,railChunks,environmentZones,trees,renderers,colliders;
        public float bridgeStart,bridgeEnd;
    }
    static Transform Find(Scene scene,string name)
    {
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name==name)return t;
        return null;
    }
    static void CaptureFinal()
    {
        var main=SceneManager.GetSceneByPath(Main);var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var streamer=manager.GetComponent<RailwayMicroChunkStreamer>();
        var zone=EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_018.unity",OpenSceneMode.Additive);
        var temporary=new GameObject("Editor verification only");SceneManager.MoveGameObjectToScene(temporary,main);
        try
        {
            foreach(var c in streamer.data.chunks)
            {
                if(c.end<3850||c.start>4700)continue;
                foreach(var track in new[]{c.right,c.left})
                {
                    var go=new GameObject("Rail view");go.transform.SetParent(temporary.transform);go.transform.SetPositionAndRotation(track.position,track.rotation);go.transform.localScale=track.scale;
                    go.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(ResourcesFolder+"/"+Path.GetFileName(track.meshResource)+".asset");go.AddComponent<MeshRenderer>().sharedMaterials=track.materials;
                }
            }
            var route=new RouteTable(streamer.train.splineRoute);Vector3 p=route.At(4170,out var f);Vector3 right=Vector3.Cross(Vector3.up,f);
            var cameraObject=new GameObject("Verification camera");cameraObject.transform.SetParent(temporary.transform);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.stereoTargetEye=StereoTargetEyeMask.None;camera.farClipPlane=1500;camera.cullingMask=~(1<<5);camera.transform.position=p+right*16+Vector3.up*12;camera.transform.LookAt(p+f*85);
            var rt=new RenderTexture(1280,720,24);rt.Create();
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Temp/QuestRailway_Final_Bridge.png",texture.EncodeToPNG());RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            File.WriteAllText("Temp/QuestRenderConfiguration.txt",$"SRPBatcher={UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching}; URP={AssetDatabase.GetAssetPath(pipeline)}; renderScale={pipeline?.renderScale}; MSAA={pipeline?.msaaSampleCount}; shadowDistance={pipeline?.shadowDistance}; no global render/XR settings changed. Vertex colour shader errors={ShaderUtil.ShaderHasError(Shader.Find("UTT/Railway Vertex Colour Matte"))}.");
        }
        finally{UnityEngine.Object.DestroyImmediate(temporary);EditorSceneManager.CloseScene(zone,true);SceneManager.SetActiveScene(main);EditorSceneManager.SaveScene(main);}
    }
    static void BatchFixedFacilities()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();int before=0,after=0;
        foreach(var z in manager.zones)
        {
            var scene=EditorSceneManager.OpenScene(z.scenePath,OpenSceneMode.Additive);
            foreach(string name in new[]{"FixedInfrastructure_Bridge","Station_Start_TrainRide","Station_End_TrainRide"})
            {
                var root=Find(scene,name);if(root==null||root.Find("QuestFacilityVisual")!=null)continue;
                if(PrefabUtility.IsPartOfPrefabInstance(root.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(root.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                foreach(var t in root.GetComponentsInChildren<Transform>(true))
                    if(t!=null&&PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))PrefabUtility.UnpackPrefabInstance(t.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                var buckets=new Dictionary<Material,List<CombineInstance>>();var sources=new List<MeshRenderer>();
                before+=root.GetComponentsInChildren<MeshRenderer>(true).Length;
                foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r=mf.GetComponent<MeshRenderer>();if(r==null||!r.enabled||!r.gameObject.activeInHierarchy||mf.sharedMesh==null||mf.GetComponentInParent<Animator>()!=null)continue;
                    var matrix=root.worldToLocalMatrix*mf.transform.localToWorldMatrix;if(matrix.determinant<=0)continue;
                    var materials=r.sharedMaterials;if(materials.Length<mf.sharedMesh.subMeshCount)continue;
                    for(int i=0;i<mf.sharedMesh.subMeshCount;i++){if(materials[i]==null)continue;if(!buckets.TryGetValue(materials[i],out var list)){list=new List<CombineInstance>();buckets.Add(materials[i],list);}list.Add(new CombineInstance{mesh=mf.sharedMesh,subMeshIndex=i,transform=matrix});}
                    sources.Add(r);
                }
                if(sources.Count<2)continue;
                var meshes=new List<Mesh>();var combines=new List<CombineInstance>();var mats=new List<Material>();
                foreach(var pair in buckets){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(pair.Value.ToArray(),true,true,false);meshes.Add(m);combines.Add(new CombineInstance{mesh=m,transform=Matrix4x4.identity});mats.Add(pair.Key);}
                var result=new Mesh{name=name+"_Quest",indexFormat=IndexFormat.UInt32};result.CombineMeshes(combines.ToArray(),false,false,false);result.RecalculateBounds();AssetDatabase.CreateAsset(result,Folder+"/"+name+"_Combined.asset");
                foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);
                var go=new GameObject("QuestFacilityVisual");SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=result;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=mats.ToArray();renderer.shadowCastingMode=ShadowCastingMode.Off;
                foreach(var r in sources){var mf=r.GetComponent<MeshFilter>();UnityEngine.Object.DestroyImmediate(r);UnityEngine.Object.DestroyImmediate(mf);}
                var nodes=root.GetComponentsInChildren<Transform>(true);for(int n=nodes.Length-1;n>=0;n--){var t=nodes[n];if(t!=root&&t.childCount==0&&t.GetComponents<Component>().Length==1)UnityEngine.Object.DestroyImmediate(t.gameObject);}
                after+=root.GetComponentsInChildren<MeshRenderer>(true).Length;
            }
            EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(scene,true);
        }
        manager.keepBehindMeters=350;EditorUtility.SetDirty(manager);EditorSceneManager.SaveScene(manager.gameObject.scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/QuestFixedFacilityBatch.txt",$"Bridge/station mesh renderers before={before}; after={after}; colliders/scripts and group roots preserved; empty visual leaves removed; source prefabs untouched. Terrain retention=350m beyond train rear envelope.");
        Validate();
    }
    static void SplitRailResources()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var streamer=manager.GetComponent<RailwayMicroChunkStreamer>();var data=streamer.data;
        if(data.chunks.Length>100)throw new InvalidOperationException("50m split already applied; not repeating.");
        var route=new RouteTable(streamer.train.splineRoute);var output=new List<RailwayMicroChunkData.Chunk>();var retired=new List<string>();
        var shader=Shader.Find("UTT/Railway Vertex Colour Matte");if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Rail colour shader failed");
        var material=new Material(shader);material.enableInstancing=true;AssetDatabase.CreateAsset(material,Folder+"/RailwayFlatColour.mat");
        long before=0,after=0;
        foreach(var c in data.chunks)
        {
            float mid=(c.start+c.end)*.5f;Vector3 plane=route.At(mid,out var direction);
            var halves=new[]{new RailwayMicroChunkData.Chunk{start=c.start,end=mid},new RailwayMicroChunkData.Chunk{start=mid,end=c.end}};
            for(int side=0;side<2;side++)
            {
                var track=side==0?c.right:c.left;string oldPath=ResourcesFolder+"/"+Path.GetFileName(track.meshResource)+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(oldPath);var vertices=mesh.vertices;var normals=mesh.normals;
                var matrix=Matrix4x4.TRS(track.position,track.rotation,track.scale);
                var positions=new[]{new List<Vector3>(),new List<Vector3>()};var ns=new[]{new List<Vector3>(),new List<Vector3>()};var colours=new[]{new List<Color32>(),new List<Color32>()};var indices=new[]{new List<int>(),new List<int>()};
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var source=mesh.GetTriangles(sub);before+=source.Length;
                    var maps=new[]{new Dictionary<int,int>(),new Dictionary<int,int>()};
                    Color color=Color.gray;var mat=sub<track.materials.Length?track.materials[sub]:null;
                    if(mat!=null){if(mat.HasProperty("_BaseColor"))color=mat.GetColor("_BaseColor");else if(mat.HasProperty("_Color"))color=mat.GetColor("_Color");}color.a=1;
                    for(int t=0;t<source.Length;t+=3)
                    {
                        Vector3 center=matrix.MultiplyPoint3x4((vertices[source[t]]+vertices[source[t+1]]+vertices[source[t+2]])/3);
                        int half=Vector3.Dot(center-plane,direction)<0?0:1;
                        for(int corner=0;corner<3;corner++)
                        {
                            int old=source[t+corner];if(!maps[half].TryGetValue(old,out int index)){index=positions[half].Count;maps[half].Add(old,index);positions[half].Add(vertices[old]);ns[half].Add(normals.Length==vertices.Length?normals[old]:Vector3.up);colours[half].Add(color);}indices[half].Add(index);
                        }
                    }
                }
                for(int half=0;half<2;half++)
                {
                    string key=track.meshResource+(half==0?"_a":"_b");
                    var result=new Mesh{name=Path.GetFileName(key),indexFormat=IndexFormat.UInt32};result.SetVertices(positions[half]);result.SetNormals(ns[half]);result.SetColors(colours[half]);result.SetTriangles(indices[half],0);result.RecalculateBounds();after+=indices[half].Count;
                    AssetDatabase.CreateAsset(result,ResourcesFolder+"/"+Path.GetFileName(key)+".asset");
                    var record=new RailwayMicroChunkData.Track{meshResource=key,position=track.position,rotation=track.rotation,scale=track.scale,materials=new[]{material}};
                    if(side==0)halves[half].right=record;else halves[half].left=record;
                }
                retired.Add(oldPath);
            }
            output.AddRange(halves);
        }
        if(before!=after)throw new InvalidOperationException("Triangle conservation failed");
        data.chunks=output.ToArray();EditorUtility.SetDirty(data);streamer.poolCapacity=28;streamer.operationsPerFrame=1;streamer.millisecondsPerFrame=.5f;
        var migration=JsonUtility.FromJson<Migration>(File.ReadAllText(Folder+"/Migration.json"));migration.railChunks=output.Count;File.WriteAllText(Folder+"/Migration.json",JsonUtility.ToJson(migration,true));
        EditorUtility.SetDirty(streamer);EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);EditorSceneManager.SaveScene(manager.gameObject.scene);AssetDatabase.SaveAssets();
        var referenced=new HashSet<string>(data.chunks.SelectMany(c=>new[]{c.right.meshResource,c.left.meshResource}));
        foreach(string path in retired)
            if(!referenced.Contains("YenTuQuestRails/"+Path.GetFileNameWithoutExtension(path)))AssetDatabase.DeleteAsset(path);
        File.WriteAllText("Temp/QuestRailway50m.txt",$"Logical 50m rail records={output.Count}; triangle indices before={before}, after={after}; positions unchanged; all triangles assigned exactly once; original source mesh assets retained; only superseded Quest working copies retired. One shared vertex-colour diffuse material; pool capacity=28 pairs; integration operations/frame=1; soft budget=0.5ms.");
    }
    static void UpdateBuildSceneList()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();
        var valid=new HashSet<string>(manager.zones.Select(z=>z.scenePath));
        var scenes=EditorBuildSettings.scenes.Where(s=>!s.path.StartsWith("Assets/ProjectLopHoc/Generated/RuntimeZones/")||valid.Contains(s.path)).ToList();
        foreach(var scene in scenes)if(valid.Contains(scene.path))scene.enabled=true;
        foreach(var z in manager.zones)if(!scenes.Any(s=>s.path==z.scenePath))scenes.Add(new EditorBuildSettingsScene(z.scenePath,true));
        EditorBuildSettings.scenes=scenes.ToArray();
        var profiles=AssetDatabase.FindAssets("t:BuildProfile");int overrides=0;
        foreach(string guid in profiles)
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var asset=AssetDatabase.LoadMainAssetAtPath(path);var serialized=new SerializedObject(asset);
            var sceneList=serialized.FindProperty("m_Scenes");
            if(sceneList==null||!sceneList.isArray)continue;
            bool changed=false;
            for(int i=sceneList.arraySize-1;i>=0;i--)
            {
                var entry=sceneList.GetArrayElementAtIndex(i);var p=entry.FindPropertyRelative("path")??entry.FindPropertyRelative("m_Path");
                if(p!=null&&p.propertyType==SerializedPropertyType.String&&p.stringValue.StartsWith("Assets/ProjectLopHoc/Generated/RuntimeZones/")&&!valid.Contains(p.stringValue)){sceneList.DeleteArrayElementAtIndex(i);changed=true;}
            }
            if(changed){serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(asset);overrides++;}
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Temp/QuestRailwayBuildList.txt",$"Shared build scene list now contains {valid.Count} runtime environment zones; obsolete zone files retained but excluded. BuildProfile assets found={profiles.Length}; overridden scene lists adjusted={overrides}. No build executed; no new profile created.");
    }
    static void BatchTrainVisuals()
    {
        var train=UnityEngine.Object.FindFirstObjectByType<TrainSplineMover>();
        int before=train.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled),disabled=0,batches=0;
        foreach(var car in new[]{train.frontCar,train.rearCar})
        {
            if(car.Find("QuestCombinedVisual")!=null)continue;
            var buckets=new Dictionary<Material,List<CombineInstance>>();var sources=new List<MeshRenderer>();
            foreach(var mf in car.GetComponentsInChildren<MeshFilter>(true))
            {
                var r=mf.GetComponent<MeshRenderer>();if(r==null||!r.enabled||!r.gameObject.activeInHierarchy||mf.sharedMesh==null||mf.GetComponentInParent<Animator>()!=null)continue;
                var matrix=car.worldToLocalMatrix*mf.transform.localToWorldMatrix;if(matrix.determinant<=0)continue;
                bool interactive=false;for(var p=mf.transform;p!=null&&p!=car;p=p.parent)if(p.GetComponent<InteractableObject>()!=null)interactive=true;
                if(interactive)continue;
                var materials=r.sharedMaterials;if(materials.Length<mf.sharedMesh.subMeshCount)continue;
                for(int sub=0;sub<mf.sharedMesh.subMeshCount;sub++)
                {
                    var mat=materials[sub];if(mat==null)continue;
                    if(!buckets.TryGetValue(mat,out var list)){list=new List<CombineInstance>();buckets.Add(mat,list);}
                    list.Add(new CombineInstance{mesh=mf.sharedMesh,subMeshIndex=sub,transform=matrix});
                }
                sources.Add(r);
            }
            if(sources.Count<2)continue;
            var pieces=new List<Mesh>();var combine=new List<CombineInstance>();var outputMaterials=new List<Material>();
            foreach(var pair in buckets)
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true,false);
                pieces.Add(mesh);combine.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.identity});outputMaterials.Add(pair.Key);
            }
            var final=new Mesh{name="QuestTrain_"+batches,indexFormat=IndexFormat.UInt32};final.CombineMeshes(combine.ToArray(),false,false,false);final.RecalculateBounds();
            string path=Folder+"/TrainVisual_"+batches+".asset";AssetDatabase.CreateAsset(final,path);
            foreach(var piece in pieces)UnityEngine.Object.DestroyImmediate(piece);
            var go=new GameObject("QuestCombinedVisual");go.transform.SetParent(car,false);go.AddComponent<MeshFilter>().sharedMesh=final;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=outputMaterials.ToArray();renderer.shadowCastingMode=ShadowCastingMode.Off;
            foreach(var r in sources){r.enabled=false;r.GetComponent<MeshFilter>().sharedMesh=null;disabled++;}batches++;
        }
        EditorSceneManager.MarkSceneDirty(train.gameObject.scene);EditorSceneManager.SaveScene(train.gameObject.scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/QuestTrainVisualBatch.txt",$"Enabled train MeshRenderers before={before}; disabled source renderers={disabled}; combined car renderers={batches}; enabled after={train.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled)}. Original transforms, scripts, colliders, seat and source prefab preserved; mirrored/animated/interactive renderers excluded.");
    }
    static void Run()
    {
        var main=SceneManager.GetSceneByPath(Main);if(!main.isLoaded)main=EditorSceneManager.OpenScene(Main,OpenSceneMode.Single);
        if(main.isDirty)EditorSceneManager.SaveScene(main);
        if(File.Exists(Folder+"/Migration.json"))throw new InvalidOperationException("Migration already exists. Use validate; refusing to shorten twice.");
        Directory.CreateDirectory(ResourcesFolder);Directory.CreateDirectory(Folder+"/TerrainMeshes");AssetDatabase.Refresh();
        var route=Find(main,"Rail_Spline_Route").GetComponent<SplineContainer>();
        var train=UnityEngine.Object.FindFirstObjectByType<TrainSplineMover>();
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();
        if(route==null||train==null||manager==null)throw new InvalidOperationException("Required route/train/manager missing");
        var original=new RouteTable(route);
        var oldTerrain=Find(main,"YenTu_Region_Terrain_EditableCopy").GetComponent<Terrain>();
        if(oldTerrain==null||oldTerrain.terrainData==null)throw new InvalidOperationException("Working terrain missing");
        var terrainData=oldTerrain.terrainData;Vector3 terrainOrigin=oldTerrain.transform.position;
        // Keep the original contiguous middle section containing the actual bridge
        // at 9.32km. This preserves curve geometry rather than compressing rail gauge
        // or merely dropping the far end and losing the bridge.
        Phase="select route";
        var spline=route.Spline;int first=0,last=spline.Count-1;float start=0,end=0,acc=0;
        for(int i=0;i<spline.Count;i++)
        {
            if(i>0)acc+=spline.GetCurveLength(i-1);
            if(acc<=5200){first=i;start=acc;}
            if(acc<=13200){last=i;end=acc;}
        }
        if(first<=0||last-first<100||end-start<7500||end-start>8300)throw new InvalidOperationException("Unsafe route selection");
        var knots=new List<BezierKnot>();for(int i=first;i<=last;i++)knots.Add(spline[i]);
        var shortened=new Spline(knots,false);
        for(int i=0;i<shortened.Count;i++)shortened.SetTangentMode(i,TangentMode.Broken);
        route.Spline=shortened;EditorUtility.SetDirty(route);
        float newLength=route.CalculateLength();
        var migration=new Migration{oldLength=original.length,start=start,end=end,length=newLength,firstZone=Mathf.FloorToInt(start/500),lastZone=Mathf.FloorToInt((end-.01f)/500),bridgeStart=9320-start,bridgeEnd=9500-start};
        report=new StringBuilder($"Old route={original.length:F3}m, retained original chainage {start:F3}..{end:F3}, new={newLength:F3}m, knots={shortened.Count}.\n");
        // New simple shared ground; no satellite texture references in active content.
        var grass=MakeGround();
        var registry=ScriptableObject.CreateInstance<RailwayMicroChunkData>();registry.length=newLength;registry.originalStart=start;registry.originalEnd=end;
        var chunks=new SortedDictionary<int,RailwayMicroChunkData.Chunk>();
        var zones=new List<RailwayZoneStreamingManager.ZoneRecord>();
        for(int z=migration.firstZone;z<=migration.lastZone;z++)
        {
            Phase="zone "+z;EditorUtility.DisplayProgressBar("Quest Railway Optimize",Phase,(z-migration.firstZone)/(float)(migration.lastZone-migration.firstZone+1));
            string path=$"Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_{z:D3}.unity";
            var s=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);var root=Find(s,"RuntimeZoneContent");
            if(root==null)throw new InvalidOperationException("Zone content missing: "+path);
            foreach(string trackName in new[]{"Track_Right","Track_Left"})
            {
                var track=root.Find(trackName);if(track==null)throw new InvalidOperationException("Missing track "+path);
                foreach(Transform child in track)
                {
                    var match=System.Text.RegularExpressions.Regex.Match(child.name,@"Chunk_(\d+)");if(!match.Success)continue;
                    int k=int.Parse(match.Groups[1].Value);if((k+1)*100<start||k*100>end)continue;
                    var mf=child.GetComponent<MeshFilter>();var mr=child.GetComponent<MeshRenderer>();if(mf==null||mr==null||mf.sharedMesh==null)throw new InvalidOperationException("Incomplete rail chunk");
                    string side=trackName=="Track_Right"?"R":"L";string asset=ResourcesFolder+$"/{side}_{k:D4}.asset";
                    if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(mf.sharedMesh),asset))throw new IOException("Could not prepare rail resource "+asset);
                    if(!chunks.TryGetValue(k,out var c)){c=new RailwayMicroChunkData.Chunk{start=Mathf.Max(0,k*100-start),end=Mathf.Min(newLength,(k+1)*100-start)};chunks.Add(k,c);}
                    var t=new RailwayMicroChunkData.Track{meshResource=$"YenTuQuestRails/{side}_{k:D4}",position=child.position,rotation=child.rotation,scale=child.lossyScale,materials=mr.sharedMaterials};
                    if(side=="R")c.right=t;else c.left=t;
                }
                // These are generated scene instances, not source meshes/prefabs.
                UnityEngine.Object.DestroyImmediate(track.gameObject);
            }
            foreach(string category in new[]{"TerrainTransitions","Roads","Vegetation","Catenary_SingleMasts","Houses"})
            {
                var group=root.Find(category);if(group==null)continue;
                foreach(Transform child in group.Cast<Transform>().ToArray())
                {
                    float chainage=original.Nearest(child.position);
                    var match=System.Text.RegularExpressions.Regex.Match(child.name,@"(?:Chunk_)(\d+)");
                    if(match.Success)chainage=int.Parse(match.Groups[1].Value)*100+50;
                    bool outside=chainage<start-50||chainage>end+50;
                    // One sparse three-chunk grove in each kilometre; keep houses
                    // only in countryside/residential zones. All placements fixed.
                    float local=chainage-start;
                    bool vegetation=category=="Vegetation";
                    bool keepTrees=!vegetation || ((Mathf.FloorToInt(local/1000)%3)==0 && local%1000<300);
                    bool keepHouse=category!="Houses" || local<1500 || (local>5000&&local<6500);
                    if(outside||!keepTrees||!keepHouse){UnityEngine.Object.DestroyImmediate(child.gameObject);continue;}
                    foreach(var r in child.GetComponentsInChildren<Renderer>(true))
                    {
                        if(category=="TerrainTransitions"||category=="Roads")r.sharedMaterials=Enumerable.Repeat(grass,r.sharedMaterials.Length).ToArray();
                        if(category=="Vegetation"){r.shadowCastingMode=ShadowCastingMode.Off;foreach(var col in r.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(col);}
                    }
                }
            }
            float a=Mathf.Max(start,z*500),b=Mathf.Min(end,(z+1)*500);
            CreateTerrainRibbon(s,root,original,terrainData,terrainOrigin,a,b,grass,z);
            // Remove old activation references to removed content and re-author a
            // bounded activation list. Rail is now prepared by its independent pool.
            var activation=root.GetComponent<RailwayZoneActivation>();if(activation==null)activation=root.gameObject.AddComponent<RailwayZoneActivation>();
            var staged=new List<GameObject>();
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(mf.name.StartsWith("TerrainZone_"))continue;
                bool optional=false;for(var p=mf.transform.parent;p!=null;p=p.parent)if(p.name=="Vegetation"||p.name=="Roads"||p.name=="Catenary_SingleMasts"||p.name=="Houses")optional=true;
                if(optional&&mf.transform.childCount==0){mf.gameObject.SetActive(false);staged.Add(mf.gameObject);}
            }
            activation.chunks=staged.ToArray();activation.objectsPerFrame=2;activation.millisecondsPerFrame=.5f;
            EditorSceneManager.SaveScene(s);EditorSceneManager.CloseScene(s,true);
            zones.Add(new RailwayZoneStreamingManager.ZoneRecord{zoneId=$"Zone_{z:D3}",scenePath=path,startChainage=a-start,endChainage=b-start});
        }
        foreach(var pair in chunks)if(pair.Value.right==null||pair.Value.left==null)throw new InvalidOperationException("Unpaired rail "+pair.Key);
        registry.chunks=chunks.Values.ToArray();AssetDatabase.CreateAsset(registry,Folder+"/RailwayMicroChunks.asset");
        migration.railChunks=registry.chunks.Length;migration.environmentZones=zones.Count;
        Phase="stations";
        PlaceStation(zones[0].scenePath,route,true);PlaceStation(zones[zones.Count-1].scenePath,route,false);
        // The same two source station prefabs are placed at the shortened ends.
        // The old end-zone assets remain on disk but are absent from runtime registry.
        manager.zones=zones;manager.routeLengthMeters=newLength;manager.loadAheadMeters=650;manager.keepBehindMeters=200;manager.logZoneTransitions=false;
        foreach(var facility in manager.fixedInfrastructure){facility.startChainage-=start;facility.endChainage-=start;}
        manager.fixedInfrastructure.RemoveAll(f=>f.endChainage<0||f.startChainage>newLength);
        var micro=manager.GetComponent<RailwayMicroChunkStreamer>();if(micro==null)micro=manager.gameObject.AddComponent<RailwayMicroChunkStreamer>();micro.data=registry;micro.train=train;
        // No large TerrainData or satellite layer is referenced by the active main
        // scene. Far terrain is now pre-baked low-poly ribbons in the 500m zones.
        if(PrefabUtility.IsPartOfPrefabInstance(oldTerrain.gameObject))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(oldTerrain.gameObject),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        UnityEngine.Object.DestroyImmediate(oldTerrain.GetComponent<TerrainCollider>());
        UnityEngine.Object.DestroyImmediate(oldTerrain);
        Phase="backdrop";
        var backdrop=Find(main,"RailwayMountainBackdrop");int mountainCount=0;
        if(backdrop!=null)foreach(Transform mountain in backdrop.Cast<Transform>().ToArray())
        {
            Bounds bounds=new Bounds();bool firstBound=true;
            foreach(var r in mountain.GetComponentsInChildren<Renderer>(true)){if(firstBound){bounds=r.bounds;firstBound=false;}else bounds.Encapsulate(r.bounds);}
            float nearest=original.Nearest(bounds.center);
            if(nearest<start-700||nearest>end+700){UnityEngine.Object.DestroyImmediate(mountain.gameObject);continue;}
            foreach(var r in mountain.GetComponentsInChildren<Renderer>(true)){r.sharedMaterial=grass;r.shadowCastingMode=ShadowCastingMode.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;}
            foreach(var col in mountain.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(col);
            mountainCount++;
        }
        // Align train's authored root and markers before direct-entry play starts.
        Vector3 startPoint=route.EvaluatePosition(0);Vector3 direction=route.EvaluateTangent(0);
        train.transform.SetPositionAndRotation(startPoint,Quaternion.LookRotation(direction,Vector3.up));
        EditorUtility.SetDirty(manager);EditorUtility.SetDirty(micro);EditorUtility.SetDirty(route);
        SceneManager.SetActiveScene(main);EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);AssetDatabase.SaveAssets();
        File.WriteAllText(Folder+"/Migration.json",JsonUtility.ToJson(migration,true));
        report.AppendLine($"Runtime environment zones={zones.Count}; pooled 100m rail records={registry.chunks.Length}; mountains={mountainCount}; bridge={migration.bridgeStart:F1}..{migration.bridgeEnd:F1}m; two relocated source stations. No tunnel created: no complete validated mountain/tunnel facility identified.");
        File.WriteAllText("Temp/QuestRailwayOptimize_Implementation.txt",report.ToString());Phase="saved";
        Validate();AssetDatabase.Refresh();
    }
    static Material MakeGround()
    {
        var texture=new Texture2D(2,2,TextureFormat.RGB24,false);texture.SetPixels(Enumerable.Repeat(new Color(.27f,.39f,.19f),4).ToArray());texture.Apply();
        AssetDatabase.CreateAsset(texture,Folder+"/GrassColour.asset");
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",0);material.SetFloat("_SpecularHighlights",0);material.SetFloat("_EnvironmentReflections",0);material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");material.enableInstancing=true;
        AssetDatabase.CreateAsset(material,Folder+"/GrassGround.mat");return material;
    }
    static void CreateTerrainRibbon(Scene scene,Transform root,RouteTable route,TerrainData source,Vector3 origin,float a,float b,Material material,int zone)
    {
        const int cols=49;int rows=Mathf.CeilToInt((b-a)/10)+1;
        var vertices=new Vector3[rows*cols];var uv=new Vector2[vertices.Length];var triangles=new int[(rows-1)*(cols-1)*6];int ti=0;
        Vector3 center=route.At((a+b)*.5f,out _);
        for(int row=0;row<rows;row++)
        {
            float d=Mathf.Lerp(a,b,row/(float)(rows-1));var p=route.At(d,out var forward);var right=Vector3.Cross(Vector3.up,forward).normalized;
            for(int col=0;col<cols;col++)
            {
                float lateral=Mathf.Lerp(-700,700,col/(float)(cols-1));var v=p+right*lateral;
                float x=Mathf.Clamp01((v.x-origin.x)/source.size.x),z=Mathf.Clamp01((v.z-origin.z)/source.size.z);
                v.y=origin.y+source.GetInterpolatedHeight(x,z);
                if(Mathf.Abs(lateral)<70)v.y=Mathf.Min(v.y,p.y-1.8f);
                vertices[row*cols+col]=v-center;uv[row*cols+col]=new Vector2(col/(float)(cols-1),row/(float)(rows-1));
                if(row<rows-1&&col<cols-1){int k=row*cols+col;triangles[ti++]=k;triangles[ti++]=k+cols;triangles[ti++]=k+1;triangles[ti++]=k+1;triangles[ti++]=k+cols;triangles[ti++]=k+cols+1;}
            }
        }
        var mesh=new Mesh{name=$"QuestTerrain_{zone:D3}"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+$"/TerrainMeshes/Terrain_{zone:D3}.asset");
        var go=new GameObject($"TerrainZone_{zone:D3}");SceneManager.MoveGameObjectToScene(go,scene);go.transform.SetParent(root);go.transform.position=center;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
    static void PlaceStation(string zonePath,SplineContainer route,bool start)
    {
        var scene=EditorSceneManager.OpenScene(zonePath,OpenSceneMode.Additive);var parent=Find(scene,"RuntimeZoneContent");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(StaticRailwayMapMvpBuilder.StationPath);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name=start?"Station_Start_TrainRide":"Station_End_TrainRide";instance.transform.SetParent(parent);
        float t=start?0:1;Vector3 p=route.EvaluatePosition(t),f=route.EvaluateTangent(t);f.Normalize();instance.transform.SetPositionAndRotation(p+Vector3.up*3.2f+f*(start?15:-15),Quaternion.LookRotation(f,Vector3.up));
        EditorSceneManager.SaveScene(scene);EditorSceneManager.CloseScene(scene,true);
    }
    static void Validate()
    {
        var main=SceneManager.GetSceneByPath(Main);var route=Find(main,"Rail_Spline_Route").GetComponent<SplineContainer>();var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();
        var micro=manager.GetComponent<RailwayMicroChunkStreamer>();var sb=new StringBuilder();
        if(micro==null||micro.data==null)throw new InvalidOperationException("Micro chunk registry missing");
        float length=route.CalculateLength();if(length<6000||length>8300)throw new InvalidOperationException("Unexpected length "+length);
        int duplicateRails=0,terrainObjects=0,stations=0,renderers=0,colliders=0,trees=0;long indices=0;
        foreach(var z in manager.zones)
        {
            var scene=EditorSceneManager.OpenScene(z.scenePath,OpenSceneMode.Additive);
            foreach(var root in scene.GetRootGameObjects())
            {
                foreach(var t in root.GetComponentsInChildren<Transform>(true)){if(t.name=="Track_Right"||t.name=="Track_Left")duplicateRails++;if(t.name.StartsWith("Station_")&&t.name.EndsWith("TrainRide"))stations++;if(t.name.StartsWith("TerrainZone_"))terrainObjects++;}
                renderers+=root.GetComponentsInChildren<Renderer>(true).Length;colliders+=root.GetComponentsInChildren<Collider>(true).Length;
                foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))if(mf.name.StartsWith("VegetationChunk_")&&mf.sharedMesh!=null)trees+=mf.sharedMesh.GetTriangles(0).Length/42;
            }
            EditorSceneManager.CloseScene(scene,true);
        }
        foreach(var c in micro.data.chunks)foreach(var t in new[]{c.right,c.left})
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(ResourcesFolder+"/"+Path.GetFileName(t.meshResource)+".asset");if(mesh==null)throw new InvalidOperationException("Rail mesh missing "+t.meshResource);indices+=mesh.triangles.Length;
        }
        sb.AppendLine($"Length={length:F3}m; zones={manager.zones.Count}; independentRailChunks={micro.data.chunks.Length}; duplicatedZoneTrackRoots={duplicateRails}; terrainMeshes={terrainObjects}; stations={stations}; zoneRenderers={renderers}; zoneColliders={colliders}; trees={trees}; railTriangles={indices/3}.");
        if(stations!=2||duplicateRails!=0)throw new InvalidOperationException("Station/duplicate rail validation failed: "+sb);
        File.WriteAllText("Temp/QuestRailwayOptimize_Validation.txt",sb.ToString());
    }
}
