using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class RailwayFarSettlementJob
{
    static RailwayFarSettlementJob()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        const string path="Temp/RailwayFarSettlement.request";
        if(!File.Exists(path)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        try{File.Delete(path);RailwayEnvironmentDensity.MoveSceneryToMountainFeet();}
        catch(Exception e){File.WriteAllText("Temp/RailwayFarSettlement_ERROR.txt",e.ToString());Debug.LogException(e);}
    }
}

internal static partial class RailwayEnvironmentDensity
{
    const string FarFolder="Assets/ProjectLopHoc/Generated/QuestFarSettlement";
    [Serializable] sealed class FarManifest {public int trees,houses,renderers;public long trianglesBefore,trianglesAfter;public float minimumCenterlineDistance=99999;public List<Placement> placements=new List<Placement>();}
    public static void MoveSceneryToMountainFeet()
    {
        var current=SceneManager.GetActiveScene();if(current.isDirty)throw new InvalidOperationException("Unsaved scene, not discarding it");string previous=current.path;
        var main=current.name=="YenTuHaLongXanh"?current:EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity");
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var route=new Route(manager.trainMover.splineRoute);
        var original=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/Manifest.json"));var sectors=JsonUtility.FromJson<SectorManifest>(File.ReadAllText(SectorFolder+"/Manifest.json"));
        var report=new FarManifest();var scenes=new List<Scene>();var roots=new Dictionary<string,Transform>();var sampleRoot=new GameObject("Editor ground samplers"){hideFlags=HideFlags.HideAndDontSave};var samplers=new List<MeshCollider>();
        void AddSampler(MeshFilter filter)
        {if(filter.sharedMesh==null)return;var go=new GameObject("Temporary terrain sampler"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(sampleRoot.transform);go.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);go.transform.localScale=filter.transform.lossyScale;go.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;samplers.Add(go.GetComponent<MeshCollider>());}
        bool Sample(Vector3 p,out float height)
        {height=float.NegativeInfinity;var ray=new Ray(new Vector3(p.x,300,p.z),Vector3.down);foreach(var c in samplers)if(c.Raycast(ray,out var hit,650))height=Mathf.Max(height,hit.point.y);return !float.IsNegativeInfinity(height);}
        try
        {
            Directory.CreateDirectory(FarFolder);AssetDatabase.Refresh();
            var material=AssetDatabase.LoadAssetAtPath<Material>(FarFolder+"/FarCluster.mat");if(material==null){material=new Material(Shader.Find("UTT/Environment Cluster Fade"));AssetDatabase.CreateAsset(material,FarFolder+"/FarCluster.mat");}
            material.SetFloat("_FadeStart",280);material.SetFloat("_FadeEnd",380);material.enableInstancing=true;EditorUtility.SetDirty(material);
            var backdrop=Find(main,"RailwayMountainBackdrop");if(backdrop!=null)foreach(var mf in backdrop.GetComponentsInChildren<MeshFilter>(true))AddSampler(mf);
            foreach(var zone in manager.zones)
            {
                var scene=EditorSceneManager.OpenScene(zone.scenePath,OpenSceneMode.Additive);scenes.Add(scene);var root=Find(scene,"RuntimeZoneContent");roots[zone.zoneId]=root;
                foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))if(mf.name.StartsWith("TerrainZone_")||mf.name.StartsWith("Mountain_SourceRidge_"))AddSampler(mf);
            }
            Physics.SyncTransforms();
            var placed=new List<(Vector3 position,float radius)>();
            foreach(var zone in manager.zones)
            {
                var root=roots[zone.zoneId];var placements=original.placements.Where(p=>p.zone==zone.zoneId).Concat(sectors.addedHouses.Where(p=>p.zone==zone.zoneId)).ToList();if(placements.Count==0)continue;
                var geometry=new Geometry();Vector3 origin=route.At((zone.startChainage+zone.endChainage)*.5f,out _);int ordinal=0;
                foreach(var placement in placements)
                {
                    bool house=placement.kind=="House";int side=placement.lateral<0?-1:1;float preferred=(house?150:130)+(ordinal%4)*20;float chosenScore=float.PositiveInfinity;Vector3 chosen=default;float selectedDistance=placement.chainage,selectedLateral=0;bool found=false;
                    for(int attempt=0;attempt<20;attempt++)
                    {
                        float distance=Mathf.Clamp(placement.chainage+(attempt/5-1)*8,zone.startChainage+8,zone.endChainage-8);float lateral=side*Mathf.Clamp(preferred+(attempt%5-2)*12,house?145:125,235)+(side<0?-6.400022f:0);
                        Vector3 candidate=route.At(distance,out var f)+Vector3.Cross(Vector3.up,f).normalized*lateral;
                        if(!Sample(candidate,out float y))continue;candidate.y=y;
                        if(!Sample(candidate+Vector3.right*5,out float yx)||!Sample(candidate+Vector3.forward*5,out float yz))continue;
                        float slope=Mathf.Max(Mathf.Abs(yx-y),Mathf.Abs(yz-y))/5;
                        float radius=house?10:4;if(placed.Any(q=>(q.position-candidate).sqrMagnitude<(q.radius+radius)*(q.radius+radius)))continue;
                        if(house&&(slope>.42f||y>route.At(distance,out _).y+14))continue;
                        // Prefer a low, gently sloping mountain foot, with closeness
                        // to the requested band as a secondary criterion.
                        float score=slope*12+Mathf.Abs(Mathf.Abs(lateral)-preferred)*.025f+(y>7&&y<25?-1:0);
                        if(score<chosenScore){chosenScore=score;chosen=candidate;selectedDistance=distance;selectedLateral=lateral;found=true;}
                    }
                    if(!found)throw new InvalidOperationException("No safe distant ground for "+zone.zoneId+" "+placement.kind+" "+placement.chainage);
                    placement.position=chosen;placement.chainage=selectedDistance;placement.lateral=selectedLateral;placed.Add((chosen,house?10:4));
                    var forward=route.At(selectedDistance,out var tangent);route.Nearest(chosen,out float clearance);report.minimumCenterlineDistance=Mathf.Min(report.minimumCenterlineDistance,clearance);
                    if(house){AddFarHouse(geometry,chosen-origin,Quaternion.LookRotation(Vector3.Cross(Vector3.up,tangent).normalized*-side,Vector3.up),placement.height,placement.variant);report.houses++;}
                    else {AddTree(geometry,chosen-origin,placement.height,placement.variant,new System.Random(491+ordinal));report.trees++;}
                    report.placements.Add(placement);ordinal++;
                }
                // Bake the existing town's street farther out with the houses.
                if(zone.endChainage>=5100&&zone.startChainage<=6300)
                    for(float d=Mathf.Max(5100,zone.startChainage);d<Mathf.Min(6300,zone.endChainage);d+=15)
                    {
                        float end=Mathf.Min(Mathf.Min(6300,zone.endChainage),d+15);foreach(float x in new[]{174f,-180.400022f})
                        {Vector3 p=route.At(d,out var f)+Vector3.Cross(Vector3.up,f).normalized*x,q=route.At(end,out var g)+Vector3.Cross(Vector3.up,g).normalized*x;if(!Sample(p,out float yp)||!Sample(q,out float yq))continue;p.y=yp+.07f;q.y=yq+.07f;var width=Vector3.Cross(Vector3.up,(q-p).normalized)*2;Quad(geometry,p-width-origin,q-width-origin,q+width-origin,p+width-origin,new Color(.23f,.24f,.20f));}
                    }
                foreach(string name in new[]{Owned,"Experience_ResidentialCluster","Experience_ResidentialStreets"})
                {
                    var old=root.Find(name);if(old==null)continue;var mf=old.GetComponent<MeshFilter>();if(mf!=null)report.trianglesBefore+=mf.sharedMesh.triangles.Length/3;var renderer=old.GetComponent<MeshRenderer>();if(renderer!=null)renderer.enabled=false;
                }
                // Reuse the existing vegetation root and its activation entry.
                CreateMeshObject(root.gameObject.scene,root,Owned,origin,geometry,material,FarFolder+"/"+zone.zoneId+"_FarCluster.asset");
                var batch=root.Find(Owned);batch.GetComponent<MeshRenderer>().enabled=true;if(batch.GetComponent<RailwayFarSceneryVisibility>()==null)batch.gameObject.AddComponent<RailwayFarSceneryVisibility>();
                var activation=root.GetComponent<RailwayZoneActivation>();var stages=activation.chunks.Where(g=>g!=null).ToList();if(!stages.Contains(batch.gameObject))stages.Add(batch.gameObject);activation.chunks=stages.ToArray();batch.gameObject.SetActive(false);report.trianglesAfter+=geometry.t.Count/3;report.renderers++;
                EditorSceneManager.SaveScene(root.gameObject.scene);
            }
            File.WriteAllText(Folder+"/Manifest.json",JsonUtility.ToJson(original,true));File.WriteAllText(SectorFolder+"/Manifest.json",JsonUtility.ToJson(sectors,true));File.WriteAllText(FarFolder+"/Manifest.json",JsonUtility.ToJson(report,true));AssetDatabase.SaveAssets();
            File.WriteAllText("Temp/RailwayFarSettlement_Result.txt",$"Saved authoring only; NO Play Mode/performance/visual test as requested. Trees={report.trees},houses={report.houses}; minimum centreline distance={report.minimumCenterlineDistance:F1}m; preferred bands trees125..235m,houses145..235m, left side includes6.400022m track spacing. Ground sampled from actual terrain/mountain meshes. Decorative triangles{report.trianglesBefore}->{report.trianglesAfter}; visible batches={report.renderers}; near-house/street source outputs disabled, not deleted; original assets retained. Shader fade280..380m plus conservative batch culling every.25s. Both source placement manifests updated.");
        }
        finally
        {UnityEngine.Object.DestroyImmediate(sampleRoot);foreach(var scene in scenes)if(scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);if(previous!=main.path)EditorSceneManager.OpenScene(previous,OpenSceneMode.Single);}
    }
    static void AddFarHouse(Geometry g,Vector3 p,Quaternion rotation,float height,int variant)
    {
        float width=variant==2?11:12,depth=variant==1?10:9,wall=height*.7f;
        Color color=variant==1?new Color(.64f,.63f,.51f):new Color(.72f,.70f,.60f);g.Box(p+Vector3.up*(wall*.5f+.2f),rotation,new Vector3(width,wall,depth),color);
        Vector3 P(float x,float y,float z)=>p+rotation*new Vector3(x,y+.2f,z);Color roof=new Color(.39f,.20f,.16f);
        Vector3 a=P(-width*.55f,wall,-depth*.57f),b=P(width*.55f,wall,-depth*.57f),c=P(0,height,-depth*.57f),d=P(-width*.55f,wall,depth*.57f),e=P(width*.55f,wall,depth*.57f),f=P(0,height,depth*.57f);
        Quad(g,a,d,f,c,roof);Quad(g,b,c,f,e,roof);Quad(g,a,c,b,b,roof);Quad(g,d,e,f,f,roof);
        for(int row=0;row<(variant==2?2:1);row++)for(int column=-1;column<=1;column++)
        {
            Vector3 center=P(column*width*.26f,wall*(row==0?.38f:.77f),depth*.501f);g.Box(center,rotation,new Vector3(1.25f,1.25f,.05f),new Color(.20f,.30f,.34f));
        }
        if(variant==1)g.Box(P(0,wall*.6f,depth*.65f),rotation,new Vector3(width*.6f,.15f,3),roof);
    }
}
