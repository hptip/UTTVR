using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Whitelisted obsolete generated Mesh assets only. Never deletes scenes/source models.
[InitializeOnLoad]
public static class UnusedRailMeshAudit
{
    const string Folder="Assets/ProjectLopHoc/Generated/StaticRailwayMap/Meshes/";
    const string Request="Temp/UnusedRailMesh.request";
    const string Out="F:/UnityTemp/UnusedRailMesh";
    static readonly string[] Batches={"20261110_122011","20261110_175136","20261110_180615","20261110_182530"};
    static bool busy;
    [Serializable] public class Item {public string path,guid;public long bytes,ticks;public string[] reasons;}
    [Serializable] public class FileStamp {public string path,hash;}
    [Serializable] public class Plan {public string status;public Item[] candidates;public string[] dirtyScenes;public FileStamp[] protectedFiles;public long safeBytes;public int roots,rawFiles;}
    static UnusedRailMeshAudit(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(busy||!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        Directory.CreateDirectory(Out);
        if(EditorApplication.isPlayingOrWillChangePlaymode){File.WriteAllText(Out+"/Status.txt","BLOCKED: Play Mode active; no asset changes");return;}
        string mode=File.ReadAllText(Request).Trim();if(mode!="audit"&&mode!="delete"&&mode!="verify")return;
        File.Delete(Request);busy=true;
        try{if(mode=="verify")Verify();else{var plan=Audit();if(mode=="delete")Delete(plan);}}
        catch(Exception e){File.WriteAllText(Out+"/ERROR.txt",e.ToString());File.WriteAllText(Out+"/Status.txt","ERROR - stopped");Debug.LogException(e);}finally{busy=false;}
    }
    static string Hash(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
    static string Project=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Full(string p)=>Path.GetFullPath(Path.Combine(Project,p));
    static IEnumerable<string> DiskFiles(string p)=>Directory.Exists(p)?Directory.EnumerateFiles(p,"*",SearchOption.AllDirectories):Enumerable.Empty<string>();
    static bool Whitelisted(string p)=>p.StartsWith(Folder,StringComparison.Ordinal)&&Batches.Any(b=>Path.GetFileName(p).StartsWith("SRM_MVP_"+b+"_",StringComparison.Ordinal))&&p.EndsWith(".asset",StringComparison.Ordinal);
    static Plan Audit()
    {
        File.WriteAllText(Out+"/Status.txt","Building fresh dependency graph; no deletion");
        var all=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(p)).ToArray();
        var items=all.Where(Whitelisted).Select(p=>new Item{path=p,guid=AssetDatabase.AssetPathToGUID(p),bytes=new FileInfo(p).Length,ticks=File.GetLastWriteTimeUtc(p).Ticks}).ToArray();
        var byPath=items.ToDictionary(i=>i.path);var byGuid=items.Where(i=>!string.IsNullOrEmpty(i.guid)).ToDictionary(i=>i.guid,StringComparer.OrdinalIgnoreCase);
        var reasons=items.ToDictionary(i=>i.path,i=>new HashSet<string>());
        Action<string,string> protect=(p,r)=>{if(reasons.TryGetValue(p,out var set))set.Add(r);};
        foreach(var i in items)
        {
            if(!File.Exists(i.path+".meta")||string.IsNullOrEmpty(i.guid))protect(i.path,"Missing meta/GUID");
            if(AssetDatabase.GetMainAssetTypeAtPath(i.path)!=typeof(Mesh))protect(i.path,"Not a Mesh main asset");
            if(i.path.Contains("/Resources/")||!string.IsNullOrEmpty(AssetDatabase.GetImplicitAssetBundleName(i.path)))protect(i.path,"Resources or AssetBundle membership");
        }
        // Root every other asset, including inactive scenes, prefabs, metadata and editor assets.
        var roots=all.Where(p=>!byPath.ContainsKey(p)).ToArray();
        foreach(var dependency in AssetDatabase.GetDependencies(roots,true))protect(dependency,"Referenced by another project asset (fresh AssetDatabase closure)");
        var dirtyScenes=new List<string>();
        for(int k=0;k<SceneManager.sceneCount;k++)
        {
            var scene=SceneManager.GetSceneAt(k);if(scene.isDirty)dirtyScenes.Add(scene.path);
            if(scene.isLoaded)foreach(var dependency in EditorUtility.CollectDependencies(scene.GetRootGameObjects().Cast<Object>().ToArray()))protect(AssetDatabase.GetAssetPath(dependency),"Referenced by loaded scene object");
        }
        foreach(var asset in Resources.FindObjectsOfTypeAll<Object>())
            if(asset!=null&&EditorUtility.IsPersistent(asset)&&EditorUtility.IsDirty(asset))
                foreach(var dep in EditorUtility.CollectDependencies(new[]{asset}))protect(AssetDatabase.GetAssetPath(dep),"Referenced by dirty in-memory persistent asset");
        if(all.Any(p=>p.Contains("/AddressableAssetsData/")))foreach(var i in items)protect(i.path,"Addressables configuration present: folder-level ownership needs separate review");
        File.WriteAllText(Out+"/Status.txt","Checking GUID strings, importer meta, code and metadata paths");
        var hex=new Regex(@"(?<![a-fA-F0-9])([a-fA-F0-9]{32})(?![a-fA-F0-9])",RegexOptions.Compiled);
        var textExtensions=new HashSet<string>(new[]{".cs",".json",".csv",".txt",".unity",".prefab",".asset",".mat",".controller",".overrideController",".anim",".meta",".xml",".yaml",".yml",".inputactions",".asmdef",".shader",".cginc",".hlsl",".uxml",".uss",".fusion"},StringComparer.OrdinalIgnoreCase);
        int rawFiles=0;
        foreach(var file in DiskFiles("Assets").Concat(DiskFiles("Packages")).Concat(DiskFiles("ProjectSettings")))
        {
            string p=file.Replace('\\','/');string basePath=p.EndsWith(".meta")?p.Substring(0,p.Length-5):p;
            if(byPath.ContainsKey(basePath)||p=="Assets/Editor/UnusedRailMeshAudit.cs"||!textExtensions.Contains(Path.GetExtension(p)))continue;
            // Native Mesh buffers are geometry; their actual object dependencies were checked above.
            if(!p.EndsWith(".meta")&&p.EndsWith(".asset")&&AssetDatabase.GetMainAssetTypeAtPath(p)==typeof(Mesh))continue;
            string text=File.ReadAllText(p);rawFiles++;
            foreach(Match match in hex.Matches(text))if(byGuid.TryGetValue(match.Groups[1].Value,out var target))protect(target.path,"GUID string in "+p);
            foreach(var batch in Batches)
                if(text.Contains(batch))foreach(var target in items.Where(i=>i.path.Contains(batch)))protect(target.path,"Batch/path/name string in "+p);
        }
        foreach(var item in items)item.reasons=reasons[item.path].OrderBy(r=>r).ToArray();
        var protectedFiles=all.Where(p=>p.EndsWith(".unity")||p.EndsWith(".cs")||p.EndsWith(".json")).Concat(new[]{"Packages/manifest.json","Packages/packages-lock.json"})
            .Where(File.Exists).Distinct().Select(p=>new FileStamp{path=p,hash=Hash(p)}).ToArray();
        var plan=new Plan{status=dirtyScenes.Count>0?"DIRTY_SCENE_BLOCKS_DELETE":"AUDITED",candidates=items,dirtyScenes=dirtyScenes.ToArray(),protectedFiles=protectedFiles,roots=roots.Length,rawFiles=rawFiles,safeBytes=items.Where(i=>i.reasons.Length==0).Sum(i=>i.bytes)};
        File.WriteAllText(Out+"/Plan.json",JsonUtility.ToJson(plan,true));
        File.WriteAllLines(Out+"/Plan.tsv",new[]{"Path\tGUID\tBytes\tDecision\tReasons"}.Concat(items.Select(i=>i.path+"\t"+i.guid+"\t"+i.bytes+"\t"+(i.reasons.Length==0?"SAFE_CURRENT_PROJECT":"KEEP")+"\t"+string.Join(" | ",i.reasons))));
        File.WriteAllText(Out+"/Status.txt",$"{plan.status}; candidates={items.Length}, safe={items.Count(i=>i.reasons.Length==0)}, safeGiB={plan.safeBytes/1073741824.0:F3}, kept={items.Count(i=>i.reasons.Length>0)}, rawFiles={rawFiles}, dirtyScenes={dirtyScenes.Count}");
        return plan;
    }
    static void Delete(Plan plan)
    {
        if(plan.dirtyScenes.Length>0)throw new InvalidOperationException("Unsaved scenes; no deletion");
        var safe=plan.candidates.Where(i=>i.reasons.Length==0).ToArray();
        foreach(var i in safe)
        {
            string full=Full(i.path);string expected=Full(Folder)+Path.DirectorySeparatorChar;
            // Trim separator because Full(Folder) may already include it.
            expected=Full(Folder).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!Whitelisted(i.path)||!full.StartsWith(expected,StringComparison.OrdinalIgnoreCase)||AssetDatabase.AssetPathToGUID(i.path)!=i.guid||new FileInfo(full).Length!=i.bytes||File.GetLastWriteTimeUtc(full).Ticks!=i.ticks)
                throw new InvalidOperationException("Candidate changed/escaped whitelist: "+i.path);
        }
        File.WriteAllText(Out+"/DeletionImpact.txt","Only unreferenced Mesh outputs of four obsolete static bake IDs. Current 184700 generation, all 58 zones, source prefabs, spline, runtime resources/metadata and all other files retained. Old intermediate static results cannot be reopened from these deleted files; regeneration/restoration would require original sources/backups. No backup created.");
        var removed=new List<string>();long bytes=0;
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach(var i in safe)
            {
                if(!AssetDatabase.DeleteAsset(i.path))throw new IOException("DeleteAsset failed: "+i.path);
                removed.Add(i.path);bytes+=i.bytes;
                if(removed.Count%100==0)File.WriteAllText(Out+"/Status.txt",$"Deleting verified assets: {removed.Count}/{safe.Length}");
            }
        }
        finally{AssetDatabase.StopAssetEditing();File.WriteAllLines(Out+"/Deleted.txt",removed);File.WriteAllText(Out+"/DeletedSummary.txt",$"Deleted={removed.Count}; assetBytes={bytes}; GiB={bytes/1073741824.0:F3}; meta paired by AssetDatabase.DeleteAsset; no other deletion.");}
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var changed=plan.protectedFiles.Where(f=>!File.Exists(f.path)||Hash(f.path)!=f.hash).Select(f=>f.path).ToArray();
        File.WriteAllLines(Out+"/ProtectedFileChanges.txt",changed);
        var residual=safe.Where(i=>File.Exists(i.path)||File.Exists(i.path+".meta")).Select(i=>i.path).ToArray();
        File.WriteAllLines(Out+"/ResidualFiles.txt",residual);
        File.WriteAllText(Out+"/Status.txt",$"DELETED; count={removed.Count}; protectedFileChanges={changed.Length}; residual={residual.Length}; run verification before claiming success");
    }
    static void Verify()
    {
        var main=SceneManager.GetSceneByPath("Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene blocks reload verification");
        main=EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity",OpenSceneMode.Single);
        var manager=Object.FindFirstObjectByType<RailwayZoneStreamingManager>();var micro=Object.FindFirstObjectByType<RailwayMicroChunkStreamer>();
        var results=new List<string>();int failures=0;
        var zonePaths=Directory.GetFiles("Assets/ProjectLopHoc/Generated/RuntimeZones","*.unity").Select(p=>p.Replace('\\','/')).ToArray();
        foreach(var path in new[]{main.path}.Concat(zonePaths))
        {
            bool isMain=path==main.path;var scene=isMain?main:EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            var gos=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
            int missingScript=gos.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount);
            int missingMesh=gos.Sum(g=>g.GetComponents<MeshFilter>().Count(m=>m.sharedMesh==null&&m.GetComponent<MeshRenderer>()!=null&&m.GetComponent<MeshRenderer>().enabled&&g.activeInHierarchy));
            int missingPrefab=gos.Count(g=>PrefabUtility.GetPrefabAssetType(g)==PrefabAssetType.MissingAsset);
            results.Add(path+$" | missingScript={missingScript}, activeMissingMesh={missingMesh}, missingPrefab={missingPrefab}");failures+=missingScript+missingMesh+missingPrefab;
            if(!isMain)EditorSceneManager.CloseScene(scene,true);
        }
        int resourceMeshes=0;
        if(manager==null||micro==null||micro.data==null||manager.trainMover==null||manager.trainMover.splineRoute==null||manager.trainMover.frontCar==null||manager.trainMover.rearCar==null)failures++;
        else foreach(var chunk in micro.data.chunks)foreach(var track in new[]{chunk.right,chunk.left})
        {var mesh=Resources.Load<Mesh>(track.meshResource);resourceMeshes++;if(mesh==null){failures++;results.Add("Missing Resources mesh: "+track.meshResource);}else Resources.UnloadAsset(mesh);}
        results.Add($"Resource mesh checks={resourceMeshes}; zones on disk={zonePaths.Length}; live zones={manager?.zones.Count}; failures={failures}");
        File.WriteAllLines(Out+"/Verification.txt",results);File.WriteAllText(Out+"/Status.txt",failures==0?"ASSET_VERIFICATION_PASS_NEEDS_PLAY_TEST":"ASSET_VERIFICATION_FAILED");
    }
}
