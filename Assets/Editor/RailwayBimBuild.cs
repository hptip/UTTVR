using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static partial class RailwayBimBuild
{
    public const string Folder="Assets/ProjectLopHoc/Generated/RailwayLifecycleBim";
    const string ScenePath="Assets/ProjectLopHoc/Scenes/QuanLyVongDoiTaiSan.unity";
    const string RawPath="Assets/ChiTietSaBan/ChiTiet/MB-EBS-DRW-003_00EBSLR49E1-MS140SklTrack-3DView_Renamed.fbx";
    static RailwayBimBuild(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        const string request="Temp/RailwayBimBuild.request";if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string command=File.ReadAllText(request).Trim();File.Delete(request);
        try{for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene: "+SceneManager.GetSceneAt(i).path);if(command=="survey")Survey();else if(command=="catalog")BuildCatalog();else if(command=="setup")Setup();}
        catch(Exception e){File.WriteAllText("Temp/RailwayBimBuild_ERROR.txt",e.ToString());Debug.LogException(e);}
    }
    public static List<int[]> Connected(Mesh mesh)
    {
        var vertices=mesh.vertices;var parent=Enumerable.Range(0,vertices.Length).ToArray();
        int Root(int n){while(parent[n]!=n){parent[n]=parent[parent[n]];n=parent[n];}return n;}
        void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[b]=a;}
        var weld=new Dictionary<Vector3Int,int>();for(int i=0;i<vertices.Length;i++){var v=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(v.x*100000),Mathf.RoundToInt(v.y*100000),Mathf.RoundToInt(v.z*100000));if(weld.TryGetValue(key,out int other))Join(i,other);else weld[key]=i;}
        var tris=mesh.triangles;for(int i=0;i<tris.Length;i+=3){Join(tris[i],tris[i+1]);Join(tris[i],tris[i+2]);}
        var groups=new Dictionary<int,List<int>>();for(int i=0;i<tris.Length;i+=3){int key=Root(tris[i]);if(!groups.TryGetValue(key,out var list))groups[key]=list=new List<int>();list.AddRange(new[]{tris[i],tris[i+1],tris[i+2]});}
        return groups.Values.Select(l=>l.ToArray()).ToList();
    }
    static string V(Vector3 v)=>$"{v.x:F6},{v.y:F6},{v.z:F6}";
    static void Survey()
    {
        var raw=AssetDatabase.LoadAssetAtPath<GameObject>(RawPath);var rootFilter=raw.GetComponentsInChildren<MeshFilter>(true).First();var report=new List<string>();
        foreach(var mf in new[]{rootFilter,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChiTietSaBan/ChiTiet/DuongRay.prefab").GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="ThanhRay")})
        {
            var groups=Connected(mf.sharedMesh);var matrix=mf.transform.localToWorldMatrix;report.Add(mf.name+" connected="+groups.Count);
            foreach(var group in groups){var ps=group.Distinct().Select(i=>matrix.MultiplyPoint3x4(mf.sharedMesh.vertices[i])).ToArray();var b=new Bounds(ps[0],Vector3.zero);foreach(var p in ps)b.Encapsulate(p);report.Add("triangles="+group.Length/3+" size="+V(b.size)+" center="+V(b.center));}
        }
        File.WriteAllLines("Temp/BimGeometrySurvey.txt",report);
    }
    struct Face{public Vector3 a,b,c,n;}
    sealed class Geo
    {
        public List<Vector3> v=new List<Vector3>(),n=new List<Vector3>();public List<Color32> colors=new List<Color32>();public List<int> t=new List<int>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 na,Vector3 nb,Vector3 nc,Color32 ca,Color32 cb,Color32 cc){int i=v.Count;v.AddRange(new[]{a,b,c});n.AddRange(new[]{na,nb,nc});colors.AddRange(new[]{ca,cb,cc});t.AddRange(new[]{i,i+1,i+2});}
        public Mesh Mesh(string name){var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetNormals(n);m.SetColors(colors);m.SetTriangles(t,0);m.RecalculateBounds();return m;}
    }
    struct Pose{public Vector3 p;public Quaternion r;public float global;}
    static Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/.01f),Mathf.FloorToInt(p.y/.01f),Mathf.FloorToInt(p.z/.01f));
    static Dictionary<Vector3Int,List<Vector3>> Points(IEnumerable<Vector3> ps){var d=new Dictionary<Vector3Int,List<Vector3>>();foreach(var p in ps){var k=Key(p);if(!d.TryGetValue(k,out var l))d[k]=l=new List<Vector3>();l.Add(p);}return d;}
    static bool Has(Dictionary<Vector3Int,List<Vector3>> map,Vector3 p){var k=Key(p);for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(map.TryGetValue(k+new Vector3Int(x,y,z),out var vs)&&vs.Any(v=>(v-p).sqrMagnitude<.000036f))return true;return false;}
    static void Save(Mesh mesh,string name){AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");}
    static void BuildCatalog()
    {
        if(File.Exists(Folder+"/Catalog.asset"))throw new Exception("Catalog already exists; refusing overwrite");Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var scene=SceneManager.GetActiveScene();if(scene.path!=ScenePath)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var oldTarget=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>(true)).Single(m=>m.name=="Rail_5500_5550m"&&m.transform.parent.name=="Track_Right");
        var data=AssetDatabase.LoadAssetAtPath<RailwayMicroChunkData>("Assets/ProjectLopHoc/Generated/QuestRailway/RailwayMicroChunks.asset");var selected=data.chunks.Where(c=>c.end>5300.01f&&c.start<5799.99f).ToArray();
        var origin=selected.Single(c=>Mathf.Abs(c.start-5500)<.01f).right.position-oldTarget.transform.position;
        var low=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab");var filters=low.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("Mesh_")&&int.TryParse(m.name.Substring(5),out int n)&&n<=12).ToArray();
        var slab=filters.Single(m=>m.name=="Mesh_12");var slabMat=low.transform.worldToLocalMatrix*slab.transform.localToWorldMatrix;var slabV=slab.sharedMesh.vertices.Select(slabMat.MultiplyPoint3x4).ToArray();var slabT=slab.sharedMesh.triangles;Vector3 sa=default,sb=default,sc=default;
        for(int i=0;i<slabT.Length;i+=3){sa=slabV[slabT[i]];sb=slabV[slabT[i+1]];sc=slabV[slabT[i+2]];if(Mathf.Abs(sa.y-slabV.Max(p=>p.y))<.001f&&Vector3.Cross(sb-sa,sc-sa).magnitude>5)break;}
        var faces=new Dictionary<Vector3Int,List<Face>>();
        foreach(var mf in filters){var mat=low.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;var v=mf.sharedMesh.vertices.Select(mat.MultiplyPoint3x4).ToArray();var ts=mf.sharedMesh.triangles;for(int i=0;i<ts.Length;i+=3){var f=new Face{a=v[ts[i]],b=v[ts[i+1]],c=v[ts[i+2]]};f.n=Vector3.Cross(f.b-f.a,f.c-f.a).normalized;var k=Key((f.a+f.b+f.c)/3);if(!faces.TryGetValue(k,out var list))faces[k]=list=new List<Face>();list.Add(f);}}
        bool FaceMatch(Vector3 a,Vector3 b,Vector3 c)
        {
            var k=Key((a+b+c)/3);var normal=Vector3.Cross(b-a,c-a).normalized;bool Near(Vector3 p,Vector3 q)=>(p-q).sqrMagnitude<.000036f;
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)if(faces.TryGetValue(k+new Vector3Int(x,y,z),out var fs))foreach(var f in fs)if((Vector3.Dot(normal,f.n)>.99f||(normal.sqrMagnitude<.5f&&f.n.sqrMagnitude<.5f))&&((Near(a,f.a)&&Near(b,f.b)&&Near(c,f.c))||(Near(a,f.b)&&Near(b,f.c)&&Near(c,f.a))||(Near(a,f.c)&&Near(b,f.a)&&Near(c,f.b))))return true;return false;
        }
        var blocks=new List<RailwayBimCatalog.Block>();var allSegments=new List<RailwayBimCatalog.Segment>();var report=new List<string>();var support=new Geo();
        var matCopy=Object.Instantiate(selected[0].right.materials[0]);AssetDatabase.CreateAsset(matCopy,Folder+"/OverviewRail.mat");
        foreach(bool right in new[]{true,false})
        {
            string track=right?"TR":"TL";var all=new Geo();
            foreach(var record in selected.Select(c=>right?c.right:c.left))
            {
                var mesh=Resources.Load<Mesh>(record.meshResource);var matrix=Matrix4x4.TRS(record.position-origin,record.rotation,record.scale);var vs=mesh.vertices;var ns=mesh.normals;var cs=mesh.colors32;int start=all.v.Count;
                all.v.AddRange(vs.Select(matrix.MultiplyPoint3x4));all.n.AddRange(ns.Length==vs.Length?ns.Select(matrix.MultiplyVector):vs.Select(_=>Vector3.up));all.colors.AddRange(cs.Length==vs.Length?cs:vs.Select(_=>(Color32)Color.gray));all.t.AddRange(mesh.triangles.Select(i=>i+start));
            }
            var pointMap=Points(all.v);var poses=new List<Pose>();Vector3 forward=(selected.Last().right.position-selected.First().right.position).normalized;
            for(int i=0;i<all.t.Count;i+=3)
            {
                Vector3 a=all.v[all.t[i]],b=all.v[all.t[i+1]],c=all.v[all.t[i+2]];if(Mathf.Abs(Vector3.Cross(b-a,c-a).normalized.y)<.999f)continue;var tri=new[]{a,b,c};
                for(int k=0;k<3;k++)for(int rev=0;rev<2;rev++)
                {
                    var ta=tri[k];var tb=tri[(k+(rev==0?1:2))%3];var tc=tri[(k+(rev==0?2:1))%3];if(Mathf.Abs((tb-ta).magnitude-(sb-sa).magnitude)>.006f||Mathf.Abs((tc-ta).magnitude-(sc-sa).magnitude)>.006f)continue;
                    var rot=Quaternion.AngleAxis(Vector3.SignedAngle(sb-sa,tb-ta,Vector3.up),Vector3.up);if(Vector3.Dot(rot*Vector3.forward,forward)<.9f)continue;var p=ta-rot*sa;
                    if((rot*sc+p-tc).sqrMagnitude>.000036f||poses.Any(q=>(q.p-p).sqrMagnitude<.0004f)||slabV.Any(v=>!Has(pointMap,rot*v+p)))continue;
                    var reference=selected.OrderBy(ch=>((right?ch.right.position:ch.left.position)-origin-p).sqrMagnitude).First();var refPos=(right?reference.right.position:reference.left.position)-origin;
                    var same=selected.Where(ch=>(right?ch.right.position:ch.left.position)==(right?reference.right.position:reference.left.position)).ToArray();float mid=(same.Min(ch=>ch.start)+same.Max(ch=>ch.end))*.5f;
                    float global=Mathf.Round((data.originalStart+mid+Vector3.Dot(p-refPos,rot*Vector3.forward))/3)*3;
                    poses.Add(new Pose{p=p,r=rot,global=global});
                }
            }
            poses=poses.OrderBy(p=>p.global).ToList();if(poses.Count<160||poses.Select(p=>p.global).Distinct().Count()!=poses.Count)throw new Exception(track+" unsafe module pose count/duplicates: "+poses.Count);
            var buckets=poses.Select(_=>new Geo()).ToArray();var centers=poses.Select(p=>p.p+p.r*new Vector3(.07362f,.255f,1.5f)).ToArray();int assigned=0;
            for(int i=0;i<all.t.Count;i+=3)
            {
                int ia=all.t[i],ib=all.t[i+1],ic=all.t[i+2];Vector3 a=all.v[ia],b=all.v[ib],c=all.v[ic],center=(a+b+c)/3;int first=-1,second=-1;float d1=float.MaxValue,d2=float.MaxValue;
                for(int j=0;j<centers.Length;j++){float d=(centers[j]-center).sqrMagnitude;if(d<d1){second=first;d2=d1;first=j;d1=d;}else if(d<d2){second=j;d2=d;}}
                int owner=-1;foreach(int j in new[]{first,second})if(j>=0){var inv=Quaternion.Inverse(poses[j].r);if(FaceMatch(inv*(a-poses[j].p),inv*(b-poses[j].p),inv*(c-poses[j].p))){owner=j;break;}}
                if(owner>=0){var inv=Quaternion.Inverse(poses[owner].r);buckets[owner].Triangle(inv*(a-poses[owner].p),inv*(b-poses[owner].p),inv*(c-poses[owner].p),inv*all.n[ia],inv*all.n[ib],inv*all.n[ic],all.colors[ia],all.colors[ib],all.colors[ic]);assigned++;}
                else support.Triangle(a,b,c,all.n[ia],all.n[ib],all.n[ic],all.colors[ia],all.colors[ib],all.colors[ic]);
            }
            for(int i=0;i<poses.Count;i++)
            {
                int s=Mathf.FloorToInt((poses[i].global-10500)/30)+1;string segment=$"Z021-{track}-S{s:D3}";string id=segment+$"-B{(int)((poses[i].global-10500)%30/3)+1:D3}";
                var mesh=buckets[i].Mesh(id);if(mesh.triangles.Length/3<1800)throw new Exception(id+" incomplete physical module: "+mesh.triangles.Length/3);Save(mesh,id);
                var b=new RailwayBimCatalog.Block{id=id,segmentID=segment,track=track,start=poses[i].global-data.originalStart,end=poses[i].global-data.originalStart+3,position=poses[i].p,rotation=poses[i].r,overview=mesh,materials=new[]{matCopy}};
                var bounds=new Bounds(b.position+b.rotation*mesh.bounds.center,Vector3.zero);foreach(var v in mesh.vertices)bounds.Encapsulate(b.position+b.rotation*v);b.bounds=bounds;blocks.Add(b);
            }
            report.Add(track+" modules="+poses.Count+" assigned triangles="+assigned+" expected="+poses.Count*2000+" min/max per module="+buckets.Min(g=>g.t.Count/3)+"/"+buckets.Max(g=>g.t.Count/3));
        }
        foreach(var group in blocks.GroupBy(b=>b.segmentID))
        {
            var mesh=new Mesh{name=group.Key,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Select(b=>new CombineInstance{mesh=b.overview,transform=Matrix4x4.TRS(b.position,b.rotation,Vector3.one)}).ToArray(),true,true,false);Save(mesh,group.Key);
            allSegments.Add(new RailwayBimCatalog.Segment{id=group.Key,track=group.First().track,start=group.Min(b=>b.start),end=group.Max(b=>b.end),bounds=mesh.bounds,overview=mesh,materials=new[]{matCopy}});
        }
        var supportMesh=support.Mesh("ExistingGirderSupportGeometry");Save(supportMesh,"ExistingGirderSupportGeometry");
        var catalog=ScriptableObject.CreateInstance<RailwayBimCatalog>();catalog.blocks=blocks.ToArray();catalog.segments=allSegments.ToArray();catalog.parts=BuildParts();AssetDatabase.CreateAsset(catalog,Folder+"/Catalog.asset");AssetDatabase.SaveAssets();
        report.Add($"Segments={catalog.segments.Length}; blocks={catalog.blocks.Length}; physical parts per block={catalog.parts.Length}; total components={catalog.blocks.Length*catalog.parts.Length}; retained support triangles={support.t.Count/3}; no scene changes.");File.WriteAllLines("Temp/BimCatalogBuild.txt",report);
    }
    static RailwayBimCatalog.Part[] BuildParts()
    {
        var raw=AssetDatabase.LoadAssetAtPath<GameObject>(RawPath);var filters=raw.GetComponentsInChildren<MeshFilter>(true);var rootFilter=filters.First();var world=new List<Vector3>();foreach(var mf in filters)world.AddRange(mf.sharedMesh.vertices.Select(mf.transform.localToWorldMatrix.MultiplyPoint3x4));
        var yaw=Quaternion.Euler(0,90,0);var rotated=world.Select(p=>yaw*p).ToArray();var bounds=new Bounds(rotated[0],Vector3.zero);foreach(var p in rotated)bounds.Encapsulate(p);var offset=new Vector3(.0736206f-bounds.center.x,-bounds.min.y,1.5f-bounds.center.z);
        var all=new List<RailwayBimCatalog.Part>();var materials=new Dictionary<Material,Material>();
        string PhysicalType(string name)=>name.Contains("BuLong")?"Bolt":name.Contains("ThanhNgang")&&!name.Contains("_")?"Sleeper":name.Contains("KepSKL")?"Fastener":name.Contains("ChotNhua")?"Anchor":name.Contains("VongDem")?"Washer":name.Contains("Dem")?"Pad":name.Contains("TamDan")?"GuidePlate":name.Contains("TamDe")?"BasePlate":name.Contains("KhoiDo")?"Support":"SteelDetail";
        foreach(var mf in filters)
        {
            var groups=mf==rootFilter?Connected(mf.sharedMesh):new List<int[]>{mf.sharedMesh.triangles};int gIndex=0;
            foreach(var group in groups)
            {
                var matrix=Matrix4x4.TRS(offset,yaw,Vector3.one)*mf.transform.localToWorldMatrix;var sourceV=mf.sharedMesh.vertices;var sourceN=mf.sharedMesh.normals;var unique=group.Distinct().ToArray();var indices=unique.Select((index,i)=>new{index,i}).ToDictionary(p=>p.index,p=>p.i);var vs=unique.Select(i=>matrix.MultiplyPoint3x4(sourceV[i])).ToArray();var ns=unique.Select(i=>matrix.inverse.transpose.MultiplyVector(sourceN[i]).normalized).ToArray();var ts=group.Select(i=>indices[i]).ToArray();if(matrix.determinant<0)for(int i=0;i<ts.Length;i+=3){int t=ts[i];ts[i]=ts[i+1];ts[i+1]=t;}
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.vertices=vs;mesh.normals=ns;mesh.triangles=ts;mesh.RecalculateBounds();
                string type=PhysicalType(mf.name),name=mf.name;
                if(mf==rootFilter){if(mesh.bounds.size.z>2.99f&&mesh.bounds.size.x<.2f){type="Rail";name=mesh.bounds.center.x<.0736f?"Ray_Left":"Ray_Right";}else{type="Slab";name="Concrete_Slab";}}
                else if(mf.name.StartsWith("ThanhNgangBetongDucSan_")&&mf.name.Split('_').Length==2)type="Sleeper";
                string key=$"C{all.Count+1:D3}";mesh.name=key+"_"+name;Save(mesh,"Part_"+key);
                var mats=mf.GetComponent<Renderer>().sharedMaterials.Select(m=>{if(!materials.TryGetValue(m,out var copy)){copy=Object.Instantiate(m);Color c=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.HasProperty("_Color")?m.GetColor("_Color"):Color.gray;copy.shader=Shader.Find("Universal Render Pipeline/Lit");copy.SetColor("_BaseColor",new Color(c.r,c.g,c.b,1));copy.SetFloat("_Surface",0);copy.SetFloat("_Smoothness",.15f);copy.SetFloat("_Metallic",0);copy.renderQueue=-1;copy.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");AssetDatabase.CreateAsset(copy,Folder+"/PartMaterial_"+materials.Count+".mat");materials[m]=copy;}return copy;}).ToArray();
                all.Add(new RailwayBimCatalog.Part{key=key,name=name,type=type,sourcePath=mf.name+(mf==rootFilter?"/connected_"+gIndex:""),mesh=mesh,materials=mats,bounds=mesh.bounds});gIndex++;
            }
        }
        if(all.Count!=99||all.Count(p=>p.type=="Rail")!=2||all.Count(p=>p.type=="Sleeper")!=3)throw new Exception("Unexpected source physical mapping");
        File.WriteAllLines("Temp/BimPhysicalParts.tsv",all.Select(p=>p.key+"\t"+p.name+"\t"+p.type+"\t"+V(p.bounds.size)+"\t"+V(p.bounds.center)));
        return all.ToArray();
    }
}
