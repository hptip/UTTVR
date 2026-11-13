using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UTT.SaBan;
using Object=UnityEngine.Object;

public static partial class ShowroomPerformanceAudit
{
    sealed class Candidate
    {
        public MeshRenderer r;public Mesh mesh;public Material mat;public Matrix4x4 local;public string key;
    }
    static HashSet<Object> SerializedSceneTargets()
    {
        var refs=new HashSet<Object>();
        foreach(var b in Nodes(Scene()).SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(b=>b!=null&&!(b is ShowroomInstancedVisualGroup)))
        {
            var so=new SerializedObject(b);var p=so.GetIterator();
            while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null&&!EditorUtility.IsPersistent(p.objectReferenceValue))refs.Add(p.objectReferenceValue);
        }
        return refs;
    }
    static List<Candidate> Candidates(SaBanLayer layer,Dictionary<string,int> reasons)
    {
        var result=new List<Candidate>();var displayRoot=layer.GetComponentInParent<SaBanRoot>();var referenced=SerializedSceneTargets();
        foreach(var r in layer.GetComponentsInChildren<MeshRenderer>(true))
        {
            string skip=null;var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;var mats=r.sharedMaterials;
            if(!r.enabled||!r.gameObject.activeInHierarchy)skip="inactive/disabled";
            else if(mesh==null)skip="no mesh";
            else if(mesh.subMeshCount!=1||mats.Length!=1||mats[0]==null)skip="multiple submeshes/materials";
            else if(mats[0].shader.name!="Universal Render Pipeline/Lit"&&mats[0].shader.name!="Universal Render Pipeline/Simple Lit"&&mats[0].shader.name!="Universal Render Pipeline/Unlit")skip="shader not explicitly validated for instancing";
            else if(mats[0].renderQueue>2500)skip="transparent sorting";
            else if(r.HasPropertyBlock())skip="per-object material properties";
            else if(referenced.Contains(r)||referenced.Contains(r.gameObject)||referenced.Contains(r.transform)||referenced.Contains(r.GetComponent<MeshFilter>()))skip="serialized runtime reference";
            else if(r.renderingLayerMask!=1)skip="custom rendering layer mask";
            else if(r.lightmapIndex>=0&&r.lightmapIndex<65534)skip="lightmapped";
            else if(r.transform.localToWorldMatrix.determinant<=0)skip="mirrored transform";
            else if(r.GetComponentInParent<LODGroup>()!=null)skip="existing LOD";
            else if(r.GetComponentInParent<Animator>()!=null||r.GetComponentInParent<Animation>()!=null)skip="animated";
            else
            {
                for(var p=r.transform;p!=null&&p!=layer.transform;p=p.parent)
                    if(p.GetComponent<Collider>()!=null||p.GetComponents<MonoBehaviour>().Any(b=>b!=null)){skip="script/collider-controlled object";break;}
            }
            if(skip!=null){reasons[skip]=reasons.TryGetValue(skip,out int n)?n+1:1;continue;}
            var pos=displayRoot.transform.InverseTransformPoint(r.bounds.center);int x=Mathf.FloorToInt(pos.x/2f),z=Mathf.FloorToInt(pos.z/2f);
            string key=mesh.GetInstanceID()+"|"+mats[0].GetInstanceID()+"|"+x+"|"+z+"|"+(int)r.shadowCastingMode+"|"+r.receiveShadows+"|"+r.gameObject.layer+"|"+r.renderingLayerMask;
            result.Add(new Candidate{r=r,mesh=mesh,mat=mats[0],local=layer.transform.worldToLocalMatrix*r.localToWorldMatrix,key=key});
        }
        return result;
    }
    static void PlanInstancing()
    {
        var root=Root();var lines=new List<string>{"Light probes="+(LightmapSettings.lightProbes==null?0:LightmapSettings.lightProbes.count),"Reflection probes="+Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length,"Supports instancing="+SystemInfo.supportsInstancing};
        foreach(var layer in root.GetLayers().Where(l=>l.key!="train"&&l.key!="khac"))
        {
            var reasons=new Dictionary<string,int>();var candidates=Candidates(layer,reasons);var groups=candidates.GroupBy(c=>c.key).Where(g=>g.Count()>=3).ToArray();
            lines.Add(layer.key+" | candidates="+candidates.Count+" | eligible repeated="+groups.Sum(g=>g.Count())+" | batches="+groups.Sum(g=>(g.Count()+249)/250));
            lines.AddRange(reasons.Select(p=>" skip | "+p.Key+"="+p.Value));
            lines.AddRange(candidates.GroupBy(c=>c.mesh).OrderByDescending(g=>g.Count()).Take(6).Select(g=>" mesh | "+g.Count()+" | "+g.Key.name+" | "+AssetDatabase.GetAssetPath(g.Key)));
        }
        File.WriteAllLines(Dir+"/InstancingPlan.txt",lines);
    }
    static void ApplyInstancing(string key)
    {
        var scene=Scene();if(scene.isDirty)throw new Exception("Unsaved Showroom changes; not overwriting");
        var layer=Root().GetLayers().Single(l=>l.key==key);
        if(layer.GetComponent<ShowroomInstancedVisualGroup>()!=null)throw new Exception("This layer is already optimized; refusing duplicate output");
        if(key=="train"||key=="khac")throw new Exception("Dynamic or visibility-controlled layers are excluded");
        if(LightmapSettings.lightProbes!=null&&LightmapSettings.lightProbes.count>0)throw new Exception("Baked light probes require separate per-instance SH preservation; not applying");
        if(Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Length>0)throw new Exception("Reflection probe matching requires validation; not applying");
        var reasons=new Dictionary<string,int>();var candidates=Candidates(layer,reasons);var groups=candidates.GroupBy(c=>c.key).Where(g=>g.Count()>=3).ToArray();
        if(groups.Length==0)throw new Exception("No safe repeated meshes in layer "+key);
        Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
        var materials=new Dictionary<Material,Material>();var batches=new List<ShowroomInstancedVisualData.Batch>();var sources=new List<MeshRenderer>();
        double maxError=0;long triangleCount=0;
        foreach(var grouping in groups)
        {
            var items=grouping.ToArray();var first=items[0];
            if(!materials.TryGetValue(first.mat,out var material))
            {
                string path=Folder+"/Materials/"+key+"_"+materials.Count.ToString("D3")+".mat";
                if(File.Exists(path))throw new Exception("Asset already exists: "+path);
                material=Object.Instantiate(first.mat);material.name="ShowroomInstanced_"+first.mat.name;material.enableInstancing=true;AssetDatabase.CreateAsset(material,path);materials[first.mat]=material;
            }
            for(int offset=0;offset<items.Length;offset+=250)
            {
                var slice=items.Skip(offset).Take(250).ToArray();
                batches.Add(new ShowroomInstancedVisualData.Batch{mesh=first.mesh,material=material,localMatrices=slice.Select(c=>c.local).ToArray(),shadows=first.r.shadowCastingMode,receiveShadows=first.r.receiveShadows,layer=first.r.gameObject.layer});
                foreach(var c in slice)
                {
                    sources.Add(c.r);triangleCount+=Tris(c.mesh);
                    var a=c.r.localToWorldMatrix;var b=layer.transform.localToWorldMatrix*c.local;
                    var bounds=c.mesh.bounds;
                    for(int j=0;j<8;j++){var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((j&1)==0?-1:1,(j&2)==0?-1:1,(j&4)==0?-1:1));maxError=Math.Max(maxError,Vector3.Distance(a.MultiplyPoint3x4(p),b.MultiplyPoint3x4(p)));}
                }
            }
        }
        if(maxError>.00005)throw new Exception("Transform equivalence tolerance exceeded: "+maxError);
        var data=ScriptableObject.CreateInstance<ShowroomInstancedVisualData>();data.batches=batches.ToArray();data.originalRendererCount=sources.Count;AssetDatabase.CreateAsset(data,Folder+"/"+key+".asset");
        var component=Undo.AddComponent<ShowroomInstancedVisualGroup>(layer.gameObject);component.data=data;component.sources=sources.ToArray();EditorUtility.SetDirty(component);
        if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        foreach(var material in materials.Values)AssetDatabase.SaveAssetIfDirty(material);AssetDatabase.SaveAssetIfDirty(data);
        Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Dir+"/Applied_"+key+".txt","Applied runtime GPU instancing to existing geometry only. Sources="+sources.Count+"; batches="+batches.Count+"; triangles unchanged="+triangleCount+"; max world-space corner error="+maxError.ToString("R")+"; original renderers and all transforms retained, enabled in Editor; materials copied="+materials.Count);
    }
}
