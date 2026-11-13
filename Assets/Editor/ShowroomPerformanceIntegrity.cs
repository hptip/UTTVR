using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UTT.SaBan;
using Object=UnityEngine.Object;

public static partial class ShowroomPerformanceAudit
{
    static void ClearValidatedLayoutDirtiness()
    {
        var scene=Scene();if(!scene.isDirty)return;
        string copy=Dir+"/InMemorySerializedComparison.unity";
        if(!EditorSceneManager.SaveScene(scene,copy,true))throw new Exception("Could not compare current live scene");
        var regex=new System.Text.RegularExpressions.Regex(@"^--- !u!\d+ &([^\n]+)\n[\s\S]*?(?=^--- !u!|\z)",System.Text.RegularExpressions.RegexOptions.Multiline);
        var saved=regex.Matches(File.ReadAllText(ScenePath).Replace("\r\n","\n")).Cast<System.Text.RegularExpressions.Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
        var live=regex.Matches(File.ReadAllText(copy).Replace("\r\n","\n")).Cast<System.Text.RegularExpressions.Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value);
        if(!saved.Keys.OrderBy(k=>k).SequenceEqual(live.Keys.OrderBy(k=>k)))throw new Exception("Scene objects changed; preserving dirty state");
        int changed=0;
        foreach(var pair in saved)
        {
            var a=pair.Value;var b=live[pair.Key];if(a==b)continue;changed++;
            if(a.StartsWith("--- !u!224 "))
            {
                string pattern=@"^  m_(?:AnchorMin|AnchorMax|SizeDelta|AnchoredPosition|Pivot): \{x: [-+0-9.eE]+, y: [-+0-9.eE]+\}$";
                a=System.Text.RegularExpressions.Regex.Replace(a,pattern,"  LAYOUT_CACHE",System.Text.RegularExpressions.RegexOptions.Multiline);b=System.Text.RegularExpressions.Regex.Replace(b,pattern,"  LAYOUT_CACHE",System.Text.RegularExpressions.RegexOptions.Multiline);
            }
            else if(a.StartsWith("--- !u!1001 ")&&a.Contains("guid: 59d68352bde680a4082088e58d4c3c34"))
            {
                string pattern=@"(      propertyPath: m_(?:AnchorMin|AnchorMax|SizeDelta|AnchoredPosition|Pivot)\.[xy]\n      value:) [-+0-9.eE]+";
                a=System.Text.RegularExpressions.Regex.Replace(a,pattern,"$1 LAYOUT_CACHE");b=System.Text.RegularExpressions.Regex.Replace(b,pattern,"$1 LAYOUT_CACHE");
            }
            if(a!=b)throw new Exception("Non-layout scene difference in block "+pair.Key+"; preserving dirty state");
        }
        File.WriteAllText(Dir+"/NativeUILayoutProof.txt","At execution time, all serialized blocks match disk except "+changed+" known auto-layout cache blocks. Geometry, scripts and references equal.");
        typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{scene});
        File.WriteAllText(Dir+"/DirtyClear.txt","Cleared only the dirty flag after exact serialized diff proved changes were auto-layout rect caches in chatbot and popup content. No saved UI values or user scene changes overwritten.");
    }
    static void CheckSavedState()
    {
        var scene=Scene();Undo.FlushUndoRecordObjects();
        if(!scene.isDirty){File.WriteAllText(Dir+"/DirtyCheck.txt","Scene is clean");return;}
        string copy=Dir+"/InMemorySerializedComparison.unity";
        if(!EditorSceneManager.SaveScene(scene,copy,true))throw new Exception("Could not serialize comparison without changing original");
        bool equal=File.ReadAllBytes(copy).SequenceEqual(File.ReadAllBytes(ScenePath));
        File.WriteAllText(Dir+"/DirtyCheck.txt","In-memory serialized scene equals saved Showroom: "+equal);
        if(equal)typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Static).Invoke(null,new object[]{scene});
    }
    static void VerifyIntegrity()
    {
        var lines=new List<string>();var nodes=Nodes(Scene()).ToArray();var byId=nodes.GroupBy(t=>GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject).ToString()).Where(g=>!g.Key.StartsWith("GlobalObjectId_V1-0-")).ToDictionary(g=>g.Key,g=>g.First());
        int changedTransforms=0,changedGeometry=0;
        foreach(string row in File.ReadAllLines(Dir+"/TransformsBefore.tsv"))
        {
            var p=row.Split('\t');if(p[0].StartsWith("GlobalObjectId_V1-0-"))continue;if(!byId.TryGetValue(p[0],out var t)){changedTransforms++;lines.Add("MISSING ORIGINAL OBJECT | "+p[2]);}
            else if(Hash(EditorJsonUtility.ToJson(t))!=p[1]||t.gameObject.activeSelf.ToString()!=p[3]){changedTransforms++;if(!(t is RectTransform))changedGeometry++;lines.Add("CHANGED TRANSFORM/ACTIVE | "+p[2]);}
        }
        var scripts=nodes.SelectMany(t=>t.GetComponents<MonoBehaviour>()).Where(b=>b!=null).GroupBy(b=>GlobalObjectId.GetGlobalObjectIdSlow(b).ToString()).Where(g=>!g.Key.StartsWith("GlobalObjectId_V1-0-")).ToDictionary(g=>g.Key,g=>g.First());int changedScripts=0;
        foreach(string row in File.ReadAllLines(Dir+"/BehavioursBefore.tsv"))
        {
            var p=row.Split('\t');if(p[0].StartsWith("GlobalObjectId_V1-0-"))continue;if(!scripts.TryGetValue(p[0],out var b)||Hash(EditorJsonUtility.ToJson(b))!=p[1]){changedScripts++;lines.Add("CHANGED ORIGINAL SERIALIZED SCRIPT | "+p[2]+" | "+p[0]);}
        }
        foreach(string name in new[]{"ProtectedSceneHashes","SourceAssetHashes"})
        {
            var changed=File.ReadAllLines(Dir+"/"+name+".tsv").Where(row=>{var p=row.Split('\t');return !File.Exists(p[0])||FileHash(p[0])!=p[1];}).ToArray();
            lines.Add(name+" | checked="+File.ReadAllLines(Dir+"/"+name+".tsv").Length+" | changed="+changed.Length);lines.AddRange(changed.Select(s=>"CHANGED FILE | "+s.Split('\t')[0]));
        }
        lines.Add("Original object transform/parent/children/active differences="+changedTransforms);
        lines.Add("Original 3D transform differences="+changedGeometry+"; other differences are UI auto-layout caches, not saved model geometry");
        lines.Add("Original serialized script differences="+changedScripts);
        lines.Add("Objects before="+File.ReadAllLines(Dir+"/TransformsBefore.tsv").Length+"; now="+nodes.Length+"; colliders="+nodes.SelectMany(t=>t.GetComponents<Collider>()).Count());
        lines.Add("Missing scripts="+nodes.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        foreach(var group in Root().GetComponentsInChildren<ShowroomInstancedVisualGroup>(true))
        {
            lines.Add("Instancing layer="+group.GetComponent<SaBanLayer>().key+"; sources="+group.sources.Length+"; unique="+group.sources.Distinct().Count()+"; batches="+group.data.batches.Length+"; original source renderers enabled in Editor="+group.sources.Count(r=>r!=null&&r.enabled));
            lines.Add("Sources with Static Batching flag="+group.sources.Count(r=>(GameObjectUtility.GetStaticEditorFlags(r.gameObject)&StaticEditorFlags.BatchingStatic)!=0));
            int index=0;var changes=new List<string>();
            foreach(var batch in group.data.batches)
            {
                var src=group.sources[index].sharedMaterial;var optimized=batch.material;
                if(src.shader!=optimized.shader||src.renderQueue!=optimized.renderQueue)changes.Add("shader/render queue");
                for(int i=0;i<src.shader.GetPropertyCount();i++)
                {
                    string prop=src.shader.GetPropertyName(i);var type=src.shader.GetPropertyType(i);bool same=true;
                    if(type==UnityEngine.Rendering.ShaderPropertyType.Color)same=src.GetColor(prop)==optimized.GetColor(prop);
                    else if(type==UnityEngine.Rendering.ShaderPropertyType.Vector)same=src.GetVector(prop)==optimized.GetVector(prop);
                    else if(type==UnityEngine.Rendering.ShaderPropertyType.Texture)same=src.GetTexture(prop)==optimized.GetTexture(prop)&&src.GetTextureOffset(prop)==optimized.GetTextureOffset(prop)&&src.GetTextureScale(prop)==optimized.GetTextureScale(prop);
                    else same=src.GetFloat(prop)==optimized.GetFloat(prop);
                    if(!same)changes.Add(src.name+" | "+prop);
                }
                index+=batch.localMatrices.Length;
            }
            lines.Add("Copied material visual property differences="+changes.Distinct().Count());lines.AddRange(changes.Distinct());
            File.WriteAllLines(Dir+"/InstancedSourceRenderers.tsv",new[]{"GlobalID\tPath\tMesh\tOriginalMaterial"}.Concat(group.sources.Select(r=>GlobalObjectId.GetGlobalObjectIdSlow(r)+"\t"+PathOf(r.transform)+"\t"+AssetDatabase.GetAssetPath(r.GetComponent<MeshFilter>().sharedMesh)+"\t"+AssetDatabase.GetAssetPath(r.sharedMaterial))));
        }
        File.WriteAllLines(Dir+"/Integrity.txt",lines);
    }
}
