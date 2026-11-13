using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

internal static partial class RailwayEnvironmentDensity
{
    const string SectorFolder="Assets/ProjectLopHoc/Generated/QuestExperienceSectors";
    [Serializable] sealed class SectorManifest
    {
        public float routeLength,subgradeStart=300,subgradeEnd=800,overheadStart=3000,overheadEnd=3500,settlementStart=5100,settlementEnd=6300;
        public int additionalHouses,totalHousesBefore=40,totalHousesAfter,portalMasts,retiredSingleMastRenderers;
        public List<Placement> addedHouses=new List<Placement>();
        public List<string> assets=new List<string>();
    }
    public static void ExpandSettlementAndEngineering(bool residentialOnly=false)
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var route=new Route(manager.trainMover.splineRoute);
        Directory.CreateDirectory(SectorFolder);AssetDatabase.Refresh();
        var manifest=new SectorManifest{routeLength=manager.trainMover.splineRoute.CalculateLength()};
        var original=JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder+"/Manifest.json"));
        var models=new[]{ReadHouse("Assets/ProjectLopHoc/YenTuFix/house_small.fbx",0),ReadHouse("Assets/ProjectLopHoc/YenTuFix/house_porch.fbx",1),ReadHouse("Assets/ProjectLopHoc/YenTuFix/house_two_storey.fbx",2)};
        var mast=ReadHouse("Assets/ProjectLopHoc/YenTuFix/catenary portal mast.glb",0);
        if(mast.t.Length/3>12000)throw new InvalidOperationException("Portal too heavy");
        foreach(var zone in manager.zones.Where(z=>z.endChainage>=300&&z.startChainage<=6400))
        {
            bool residential=zone.endChainage>=5100&&zone.startChainage<=6300;
            bool subgrade=!residentialOnly&&zone.endChainage>300&&zone.startChainage<800;
            bool overhead=!residentialOnly&&zone.endChainage>3000&&zone.startChainage<3500;
            if(!residential&&!subgrade&&!overhead)continue;
            var scene=EditorSceneManager.OpenScene(zone.scenePath,OpenSceneMode.Additive);
            try
            {
                var root=Find(scene,"RuntimeZoneContent");Vector3 origin=route.At((zone.startChainage+zone.endChainage)*.5f,out _);
                if(residential)
                {
                    Physics.SyncTransforms();var houses=new Geometry();var roads=new Geometry();
                    int ordinal=0;
                    for(float d=5120;d<=6280;d+=40)
                    {
                        for(int side=-1;side<=1;side+=2)for(int row=0;row<2;row++)
                        {
                            float chainage=d+(row==1?12:0),lateral=side*(32+row*25)+(side<0?-6.400022f:0);
                            if(chainage<zone.startChainage||chainage>=zone.endChainage)continue;
                            Vector3 p=route.At(chainage,out var f)+Vector3.Cross(Vector3.up,f).normalized*lateral;p.y=GroundY(scene,p);
                            // Explicit deterministic rows are deliberately denser than the
                            // earlier countryside distribution. Skip source-house overlaps.
                            if(original.placements.Any(q=>q.kind=="House"&&(q.position-p).sqrMagnitude<22*22))continue;
                            int variant=(ordinal+++(int)d/40)%3;var house=models[variant];float scale=Mathf.Min((variant==2?10:8)/house.bounds.size.y,16/Mathf.Max(house.bounds.size.x,house.bounds.size.z));
                            AppendHouse(houses,house,p-origin,Quaternion.LookRotation(f,Vector3.up)*Quaternion.Euler(0,side<0?90:-90,0),scale);
                            manifest.addedHouses.Add(new Placement{zone=zone.zoneId,kind="House",chainage=chainage,lateral=lateral,position=p,height=house.bounds.size.y*scale,variant=variant});manifest.additionalHouses++;
                        }
                    }
                    for(float d=Mathf.Max(5100,zone.startChainage);d<Mathf.Min(6300,zone.endChainage);d+=10)
                    {
                        float n=Mathf.Min(10,Mathf.Min(6300,zone.endChainage)-d);Vector3 p=route.At(d+n*.5f,out var f);var right=Vector3.Cross(Vector3.up,f).normalized;
                        foreach(float side in new[]{44f,-50.400022f}){Vector3 r=p+right*side;r.y=GroundY(scene,r)+.04f;roads.Box(r-origin,Quaternion.LookRotation(f,Vector3.up),new Vector3(5,.08f,n+.1f),new Color(.20f,.21f,.19f));}
                    }
                    SaveSector(scene,root,zone.zoneId,"ResidentialCluster",origin,houses,manifest);SaveSector(scene,root,zone.zoneId,"ResidentialStreets",origin,roads,manifest);
                }
                if(subgrade)
                {
                    var civil=new Geometry();
                    float a=Mathf.Max(300,zone.startChainage),b=Mathf.Min(800,zone.endChainage);
                    float bottom=RailBottom(manager,route,550);
                    Vector3 Civil(float d,float x,float y){Vector3 p=route.At(d,out var f);return p+Vector3.Cross(Vector3.up,f).normalized*x+Vector3.up*(bottom+y)-origin;}
                    float Height(float d,float x)
                    {
                        float ramp=Mathf.SmoothStep(0,1,Mathf.InverseLerp(300,350,d))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(750,800,d)));
                        if(x>12&&x<72)return Mathf.Max(0,Mathf.Min((x-12)/30,(72-x)/30))*14*ramp;
                        return 0;
                    }
                    // Existing working terrain follows the civil profile; the track
                    // elevation never follows the new slopes.
                    foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("TerrainZone_")))
                    {
                        var mesh=filter.sharedMesh;var vertices=mesh.vertices;bool changed=false;
                        for(int i=0;i<vertices.Length;i++){Vector3 p=filter.transform.TransformPoint(vertices[i]);float d=route.Nearest(p,out _);if(d<a||d>b)continue;Vector3 center=route.At(d,out var f);float x=Vector3.Dot(p-center,Vector3.Cross(Vector3.up,f).normalized);if(x<12||x>72)continue;p.y=Mathf.Max(p.y,center.y+bottom+Height(d,x)-.15f);vertices[i]=filter.transform.InverseTransformPoint(p);changed=true;}
                        if(changed){mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);var mc=filter.GetComponent<MeshCollider>();if(mc!=null){mc.sharedMesh=null;mc.sharedMesh=mesh;}}
                    }
                    for(float d=a;d<b;d+=10)
                    {
                        float end=Mathf.Min(b,d+10);Vector3 p=route.At((d+end)*.5f,out var f);var rotation=Quaternion.LookRotation(f,Vector3.up);var right=Vector3.Cross(Vector3.up,f).normalized;
                        civil.Box(p+right*(-3.200011f)+Vector3.up*(bottom-.6f)-origin,rotation,new Vector3(23,1.2f,end-d+.15f),new Color(.43f,.38f,.28f));
                        foreach(var pair in new[]{(12f,22f),(22f,32f),(32f,42f),(42f,57f),(57f,72f)})
                            Quad(civil,Civil(d,pair.Item1,Height(d,pair.Item1)),Civil(end,pair.Item1,Height(end,pair.Item1)),Civil(end,pair.Item2,Height(end,pair.Item2)),Civil(d,pair.Item2,Height(d,pair.Item2)),new Color(.34f,.37f,.25f));
                        // Repeated diagonal concrete lattice, drainage toe and crest.
                        foreach(float x in new[]{12f,22f,32f,42f})Beam(civil,Civil(d,x,Height(d,x)+.1f),Civil(end,x,Height(end,x)+.1f),.25f,new Color(.64f,.64f,.58f));
                        foreach(float x in new[]{12f,22f,32f}){Beam(civil,Civil(d,x,Height(d,x)+.12f),Civil(end,x+10,Height(end,x+10)+.12f),.25f,new Color(.64f,.64f,.58f));Beam(civil,Civil(d,x+10,Height(d,x+10)+.12f),Civil(end,x,Height(end,x)+.12f),.25f,new Color(.64f,.64f,.58f));}
                    }
                    SaveSector(scene,root,zone.zoneId,"SubgradeReinforcedSlope",origin,civil,manifest);
                }
                if(overhead)
                {
                    var geometry=new Geometry();float a=Mathf.Max(3000,zone.startChainage),b=Mathf.Min(3500,zone.endChainage);
                    foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("PoleChunk_")))
                    {float d=route.Nearest(r.bounds.center,out _);if(d>=3000&&d<=3500&&r.enabled){r.enabled=false;manifest.retiredSingleMastRenderers++;}}
                    bool wideX=mast.bounds.size.x>mast.bounds.size.z;float width=wideX?mast.bounds.size.x:mast.bounds.size.z;
                    for(float d=3000;d<=3500;d+=40)
                    {
                        if(d<a||d>=b)continue;Vector3 p=route.At(d,out var f);var rotation=Quaternion.LookRotation(f,Vector3.up);int first=geometry.v.Count;
                        foreach(var vertex in mast.v)
                        {
                            Vector3 q=vertex-mast.bounds.center;float x=(wideX?q.x:-q.z)/width*24,z=(wideX?q.z:q.x)/width*24,y=(vertex.y-mast.bounds.min.y)/mast.bounds.size.y*10;
                            geometry.v.Add(p+rotation*new Vector3(x-3.200011f,y,z)-origin);geometry.c.Add(new Color(.50f,.53f,.52f));
                        }
                        foreach(int t in mast.t)geometry.t.Add(first+t);manifest.portalMasts++;
                    }
                    // Retain existing continuous contact-wire meshes. Add messenger
                    // spans/droppers and tension bracing instead of duplicate contacts.
                    for(float d=a;d<b;d+=5)
                    {
                        float end=Mathf.Min(b,d+5);Vector3 p=route.At(d,out var f),q=route.At(end,out var g);var right=Vector3.Cross(Vector3.up,f).normalized;var other=Vector3.Cross(Vector3.up,g).normalized;
                        foreach(float x in new[]{0f,-6.400022f})
                        {
                            float y=7.6f-.35f*Mathf.Sin(Mathf.PI*Mathf.Repeat(d-3000,40)/40),y2=7.6f-.35f*Mathf.Sin(Mathf.PI*Mathf.Repeat(end-3000,40)/40);
                            Beam(geometry,p+right*x+Vector3.up*y-origin,q+other*x+Vector3.up*y2-origin,.045f,new Color(.19f,.22f,.21f));
                            if(Mathf.RoundToInt(d)%10==0)Beam(geometry,p+right*x+Vector3.up*6.5f-origin,p+right*x+Vector3.up*y-origin,.035f,new Color(.22f,.24f,.23f));
                        }
                    }
                    SaveSector(scene,root,zone.zoneId,"OverheadPortalSector",origin,geometry,manifest);
                }
                EditorSceneManager.SaveScene(scene);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        manifest.totalHousesAfter=40+manifest.additionalHouses;File.WriteAllText(SectorFolder+"/Manifest.json",JsonUtility.ToJson(manifest,true));AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        File.WriteAllText("Temp/RailwayExperience_Applied.txt",$"Route kept={manifest.routeLength:F3}m; zones={manager.zones.Count}; houses added={manifest.additionalHouses}, total={manifest.totalHousesAfter}; portals={manifest.portalMasts}; old single-mast renderers disabled={manifest.retiredSingleMastRenderers}; subgrade300..800; overhead3000..3500; residential5100..6300. No source prefab, train/XR/spline/build changes.");
    }
    static float RailBottom(RailwayZoneStreamingManager manager,Route route,float d)
    {var track=manager.GetComponent<RailwayMicroChunkStreamer>().data.chunks.First(c=>c.start<=d&&c.end>=d).right;var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/ProjectLopHoc/Generated/QuestRailway/Resources/"+track.meshResource+".asset");return track.position.y+mesh.bounds.min.y*track.scale.y-route.At(d,out _).y;}
    static void Quad(Geometry g,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color)
    {int n=g.v.Count;g.v.AddRange(new[]{a,b,c,d});for(int i=0;i<4;i++)g.c.Add(color);g.t.AddRange(new[]{n,n+1,n+2,n,n+2,n+3,n+2,n+1,n,n+3,n+2,n});}
    static void Beam(Geometry g,Vector3 a,Vector3 b,float width,Color color)
    {if((b-a).sqrMagnitude<.00001f)return;g.Box((a+b)*.5f,Quaternion.LookRotation(b-a),new Vector3(width,width,Vector3.Distance(a,b)),color);}
    static void SaveSector(Scene scene,Transform root,string zone,string name,Vector3 origin,Geometry geometry,SectorManifest manifest)
    {
        if(geometry.t.Count==0)return;
        // Discard unreferenced source-model vertices before serializing the mesh.
        var compact=new Geometry();var map=new Dictionary<int,int>();foreach(int index in geometry.t){if(!map.TryGetValue(index,out int n)){n=compact.v.Count;map.Add(index,n);compact.v.Add(geometry.v[index]);compact.c.Add(geometry.c[index]);}compact.t.Add(n);}
        string path=SectorFolder+"/"+zone+"_"+name+".asset";
        CreateMeshObject(scene,root,"Experience_"+name,origin,compact,AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectLopHoc/Generated/QuestRailway/RailwayFlatColour.mat"),path);
        var go=root.Find("Experience_"+name).gameObject;var activation=root.GetComponent<RailwayZoneActivation>();var stages=activation.chunks.Where(g=>g!=null).ToList();if(!stages.Contains(go))stages.Add(go);activation.chunks=stages.ToArray();go.SetActive(false);manifest.assets.Add(path);
    }
    public static void AuditExperienceSectors()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var route=new Route(manager.trainMover.splineRoute);
        var manifest=JsonUtility.FromJson<SectorManifest>(File.ReadAllText(SectorFolder+"/Manifest.json"));int missing=0,groups=0,portalGroups=0,disabled=0;long triangles=0,vertices=0;
        foreach(var zone in manager.zones)
        {
            var scene=EditorSceneManager.OpenScene(zone.scenePath,OpenSceneMode.Additive);
            try
            {
                foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                {
                    missing+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if(t.name.StartsWith("TunnelMountainGroup_"))groups++;
                    if(t.name=="Experience_OverheadPortalSector")portalGroups++;
                    var filter=t.GetComponent<MeshFilter>();if(filter!=null&&filter.sharedMesh==null)throw new InvalidOperationException("Missing mesh "+t.name);
                    if(filter!=null&&(t.name.StartsWith("Experience_")||t.GetComponentInParent<TrainSplineMover>()!=null)){vertices+=filter.sharedMesh.vertexCount;triangles+=filter.sharedMesh.triangles.Length/3;}
                    var r=t.GetComponent<MeshRenderer>();if(r!=null&&r.name.StartsWith("PoleChunk_")&&!r.enabled){float d=route.Nearest(r.bounds.center,out _);if(d>=3000&&d<=3500)disabled++;}
                }
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        manifest.portalMasts=portalGroups==2?13:0;manifest.retiredSingleMastRenderers=disabled;manifest.assets=AssetDatabase.FindAssets("t:Mesh",new[]{SectorFolder}).Select(AssetDatabase.GUIDToAssetPath).ToList();
        File.WriteAllText(SectorFolder+"/Manifest.json",JsonUtility.ToJson(manifest,true));AssetDatabase.Refresh();
        File.WriteAllText("Temp/RailwayExperience_Audit.txt",$"Reopened17 saved zones: tunnel groups={groups}; registry tunnel records={manager.fixedInfrastructure.Count(f=>f.kind=="TunnelMountain")}; portalMeshGroups={portalGroups} with authored13 portal structures; retired single-mast renderers={disabled}; missingScripts={missing}; new experience vertices={vertices},triangles={triangles}; assets={manifest.assets.Count}; houses40+{manifest.additionalHouses}={manifest.totalHousesAfter}; routeLength={manifest.routeLength:F3}m. Existing rail chunks and contacts retained.");
        if(missing!=0||groups!=2||portalGroups!=2)throw new InvalidOperationException("Experience sector audit failed");
    }
    public static void AlignMessengerToContactWire()
    {
        var manager=UnityEngine.Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var route=new Route(manager.trainMover.splineRoute);int changed=0;
        foreach(var zone in manager.zones.Where(z=>z.endChainage>3000&&z.startChainage<3500))
        {
            var scene=EditorSceneManager.OpenScene(zone.scenePath,OpenSceneMode.Additive);
            try
            {
                var root=Find(scene,"RuntimeZoneContent");var wire=root.Find("Continuous_Contact_Wire_Zone").GetComponent<MeshFilter>();var wirePoints=wire.sharedMesh.vertices.Select(wire.transform.TransformPoint).ToArray();
                float ContactY(Vector3 point){float best=float.PositiveInfinity,y=0;foreach(var q in wirePoints){float sq=(q.x-point.x)*(q.x-point.x)+(q.z-point.z)*(q.z-point.z);if(sq<best){best=sq;y=q.y;}}return y;}
                var filter=root.Find("Experience_OverheadPortalSector").GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var geometry=new Geometry();geometry.v.AddRange(mesh.vertices);geometry.c.AddRange(mesh.colors32);geometry.t.AddRange(mesh.triangles);
                for(int i=0;i<geometry.v.Count;i++)if(geometry.c[i].r<100)
                {
                    Vector3 p=filter.transform.TransformPoint(geometry.v[i]);float desired=ContactY(p);float d=route.Nearest(p,out _);float originalBase=route.At(d,out _).y+6.5f;
                    p.y+=desired-originalBase;geometry.v[i]=filter.transform.InverseTransformPoint(p);changed++;
                }
                for(float d=3000;d<=3500;d+=40)
                {
                    if(d<zone.startChainage||d>=zone.endChainage)continue;Vector3 p=route.At(d,out var f);var right=Vector3.Cross(Vector3.up,f).normalized;
                    foreach(float x in new[]{0f,-6.400022f}){Vector3 q=p+right*x;Vector3 lower=new Vector3(q.x,ContactY(q)+1.1f,q.z),upper=q+Vector3.up*9.8f;Beam(geometry,filter.transform.InverseTransformPoint(lower),filter.transform.InverseTransformPoint(upper),.05f,new Color(.5f,.53f,.52f));}
                }
                var replacement=geometry.Mesh(mesh.name);EditorUtility.CopySerialized(replacement,mesh);UnityEngine.Object.DestroyImmediate(replacement);EditorUtility.SetDirty(mesh);EditorSceneManager.SaveScene(scene);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        AssetDatabase.SaveAssets();File.WriteAllText("Temp/RailwayExperience_WireAlignment.txt",$"Aligned {changed} messenger/dropper vertices to sampled original continuous contact mesh heights; source contacts untouched. Added13x2 supports from portal crossarms to messenger. Both existing zone sector mesh references preserved.");
    }
}
