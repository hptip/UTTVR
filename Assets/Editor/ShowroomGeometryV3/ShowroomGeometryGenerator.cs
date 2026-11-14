using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static partial class ShowroomGeometryV3
{
    const string ProfilePath = AssetsFolder + "/ShowroomQuestGeometry.asset";
    static Mesh Compact(Mesh mesh, int lod)
    {
        var map = new Dictionary<int, int>(); var old = new List<int>(); var indices = new int[mesh.subMeshCount][];
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            indices[s] = mesh.GetIndices(s, lod, true);
            for (int i = 0; i < indices[s].Length; i++)
            {
                int source = indices[s][i];
                if (!map.TryGetValue(source, out int target)) { target = old.Count; map[source] = target; old.Add(source); }
                indices[s][i] = target;
            }
        }
        var result = new Mesh { name = mesh.name + "_LOD" + lod, indexFormat = old.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        var vertices = mesh.vertices; result.vertices = old.Select(i => vertices[i]).ToArray();
        var normals = mesh.normals; if (normals.Length == vertices.Length) result.normals = old.Select(i => normals[i]).ToArray();
        var tangents = mesh.tangents; if (tangents.Length == vertices.Length) result.tangents = old.Select(i => tangents[i]).ToArray();
        var colors = mesh.colors; if (colors.Length == vertices.Length) result.colors = old.Select(i => colors[i]).ToArray();
        for (int channel = 0; channel < 8; channel++)
        {
            var uv = new List<Vector4>(); mesh.GetUVs(channel, uv);
            if (uv.Count == vertices.Length) result.SetUVs(channel, old.Select(i => uv[i]).ToList());
        }
        result.subMeshCount = mesh.subMeshCount;
        for (int s = 0; s < indices.Length; s++) result.SetIndices(indices[s], mesh.GetTopology(s), s, false);
        result.RecalculateBounds(); return result;
    }
    static float BoundsError(Mesh original, Mesh simplified)
    {
        var a = original.bounds; var b = simplified.bounds;
        return Mathf.Max((a.min - b.min).magnitude, (a.max - b.max).magnitude) / Mathf.Max(.00001f, a.size.magnitude);
    }
    static bool Valid(Mesh original, Mesh simplified)
    {
        if (simplified.vertexCount < 3 || simplified.subMeshCount != original.subMeshCount || BoundsError(original, simplified) > .02f) return false;
        for (int s = 0; s < original.subMeshCount; s++) if (original.GetIndexCount(s, 0) > 0 && simplified.GetIndexCount(s, 0) == 0) return false;
        return simplified.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z));
    }
    static ShowroomGeometryProfile LoadProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<ShowroomGeometryProfile>(ProfilePath);
        if (profile != null) return profile;
        Directory.CreateDirectory(AssetsFolder + "/Meshes"); AssetDatabase.Refresh();
        profile = ScriptableObject.CreateInstance<ShowroomGeometryProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); return profile;
    }
    // Work is resumable per source asset. Does not mutate any source or scene.
    [MenuItem("UTT/Showroom Geometry V3/2 Generate next priority mesh")]
    public static string GenerateNext() => Generate(1);
    public static string Generate(int maxMeshes)
    {
        var scene = Scene(); var uses = Uses(scene); var profile = LoadProfile();
        var existing = new HashSet<Mesh>(profile.entries.Select(e => e.original));
        var candidates = uses.Where(u => u.active && EditorUtility.IsPersistent(u.mesh)).GroupBy(u => u.mesh).Where(g => !existing.Contains(g.Key))
            .OrderByDescending(g => g.Sum(u => u.Triangles)).Take(maxMeshes).ToArray();
        var entries = profile.entries.ToList();
        foreach (var g in candidates)
        {
            var mesh = g.Key; long originalCount = Triangles(mesh);
            var entry = new ShowroomGeometryProfile.Entry { original = mesh, originalTriangles = originalCount, category = g.First().category, generator = "Unity 6.3 MeshLodUtility; extracted compact physical LOD meshes (no runtime simplification)" };
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out entry.sourceGuid, out long localId); entry.sourceLocalId = localId.ToString();
            Mesh copy = null; var levels = new Dictionary<int, Mesh>();
            try
            {
                bool compatible = mesh.blendShapeCount == 0 && mesh.bindposes.Length == 0 && Enumerable.Range(0, mesh.subMeshCount).All(s => mesh.GetTopology(s) == MeshTopology.Triangles);
                if (originalCount >= 48 && compatible)
                {
                    copy = Object.Instantiate(mesh); MeshLodUtility.GenerateMeshLods(copy, (MeshLodUtility.LodGenerationFlags)0, 10);
                    for (int quality = 0; quality < 3; quality++)
                    {
                        double ratio = new[] { .5, .25, .1 }[quality];
                        int selected = 0;
                        foreach (int level in Enumerable.Range(0, copy.lodCount).OrderBy(l => Math.Abs(Math.Log(Math.Max(1, Triangles(copy, l)) / (double)originalCount / ratio))))
                        {
                            if (level == 0) break;
                            if (!levels.TryGetValue(level, out var candidate)) { candidate = Compact(copy, level); levels[level] = candidate; }
                            if (Valid(mesh, candidate)) { selected = level; break; }
                        }
                        Mesh output = selected == 0 ? mesh : levels[selected];
                        if (output != mesh && !EditorUtility.IsPersistent(output))
                        {
                            string path = AssetsFolder + "/Meshes/" + GuidId(mesh) + "_L" + selected + ".asset";
                            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                            if (saved != null) { Object.DestroyImmediate(output); levels[selected] = output = saved; }
                            else { AssetDatabase.CreateAsset(output, path); AssetDatabase.SaveAssetIfDirty(output); }
                        }
                        if (quality == 0) { entry.quality50 = output; entry.triangles50 = Triangles(output); entry.boundsError50 = BoundsError(mesh, output); }
                        if (quality == 1) { entry.quality25 = output; entry.triangles25 = Triangles(output); entry.boundsError25 = BoundsError(mesh, output); }
                        if (quality == 2) { entry.quality10 = output; entry.triangles10 = Triangles(output); entry.boundsError10 = BoundsError(mesh, output); }
                    }
                }
                else { entry.generator = "Original retained: tiny mesh or unsupported deformation/topology"; entry.quality50 = entry.quality25 = entry.quality10 = mesh; entry.triangles50 = entry.triangles25 = entry.triangles10 = originalCount; }
                entries.Add(entry); profile.entries = entries.ToArray(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);
                File.AppendAllText(Output + "/GenerationProgress.txt", mesh.name + " | " + originalCount + " -> " + entry.triangles50 + "/" + entry.triangles25 + "/" + entry.triangles10 + " | weighted=" + g.Sum(u => u.Triangles) + "\n");
            }
            finally
            {
                if (copy != null) Object.DestroyImmediate(copy);
                foreach (var candidate in levels.Values.Distinct()) if (candidate != null && !EditorUtility.IsPersistent(candidate)) Object.DestroyImmediate(candidate);
            }
        }
        WriteOptimizationTable(profile, uses);
        return "Generated " + candidates.Length + " source entries; total=" + profile.entries.Length;
    }
    static void WriteOptimizationTable(ShowroomGeometryProfile profile, List<Use> uses)
    {
        var rows = new List<string> { "Mesh,Category,Source,OriginalTriangles,Triangles50,Triangles25,Triangles10,Ratio50,Ratio25,Ratio10,BoundsError50,BoundsError25,BoundsError10,Generator" };
        foreach (var e in profile.entries.Where(e=>e.original!=null))
            rows.Add(string.Join(",", new object[] { e.original.name, e.category, AssetDatabase.GetAssetPath(e.original), e.originalTriangles, e.triangles50, e.triangles25, e.triangles10, e.triangles50 / (double)Math.Max(1,e.originalTriangles), e.triangles25 / (double)Math.Max(1,e.originalTriangles), e.triangles10 / (double)Math.Max(1,e.originalTriangles), e.boundsError50, e.boundsError25, e.boundsError10, e.generator }.Select(Csv)));
        File.WriteAllLines(Output + "/Showroom_Optimized_Meshes.csv", rows);
        var totals = new List<string>();
        foreach (var mode in new[] { ShowroomGeometryMode.Original, ShowroomGeometryMode.Quality50, ShowroomGeometryMode.Quality25, ShowroomGeometryMode.Quality10 })
            totals.Add(mode + " | active weighted triangles=" + uses.Where(u => u.active).Sum(u => SubmittedTriangles(profile.Resolve(u.mesh, mode), u.renderer.sharedMaterials.Length)) + " | active weighted vertices=" + uses.Where(u => u.active).Sum(u => (long)profile.Resolve(u.mesh, mode).vertexCount));
        File.WriteAllLines(Output + "/GeometryEstimates.txt", totals);
    }
    [MenuItem("UTT/Showroom Geometry V3/3 Apply Quest profile to Showroom")]
    public static string Apply()
    {
        var scene = Scene(); var profile = AssetDatabase.LoadAssetAtPath<ShowroomGeometryProfile>(ProfilePath);
        if (profile == null || profile.entries.Length == 0) throw new Exception("Generate and inspect priority meshes first");
        if(!File.Exists(Output+"/SilhouetteValidation.csv") || File.GetLastWriteTimeUtc(Output+"/SilhouetteValidation.csv") < File.GetLastWriteTimeUtc(Output+"/GenerationProgress.txt")) throw new Exception("Run ValidateSilhouettes after generation before applying the profile");
        var groupSources = new HashSet<MeshFilter>(Components<ShowroomInstancedVisualGroup>(scene).SelectMany(g => g.sources).Where(r => r != null).Select(r => r.GetComponent<MeshFilter>()));
        var host = Components<UTT.SaBan.SaBanRoot>(scene).Single().gameObject;
        var controller = host.GetComponent<ShowroomGeometryController>() ?? Undo.AddComponent<ShowroomGeometryController>(host);
        Undo.RecordObject(controller, "Configure Android-only Showroom geometry"); controller.profile = profile;
        var originals = new HashSet<Mesh>(profile.entries.Select(e => e.original));
        controller.targets = Components<MeshFilter>(scene).Where(f => originals.Contains(f.sharedMesh) && !groupSources.Contains(f)).ToArray();
        controller.mode = ShowroomGeometryMode.DeviceDefault; EditorUtility.SetDirty(controller);
        if (host.GetComponent<ShowroomGeometryBenchmark>() == null) Undo.AddComponent<ShowroomGeometryBenchmark>(host);
        Undo.FlushUndoRecordObjects(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Saved Showroom; non-instanced targets=" + controller.targets.Length + "; original Edit/Desktop meshes preserved; Android=" + profile.androidDefault;
    }
    public static string VerifyProtected()
    {
        var changed = File.ReadAllLines(Output + "/Protected.tsv").Select(r => r.Split('\t')).Where(r => Hash(r[0]) != r[1]).ToArray();
        const string transientId="GlobalObjectId_V1-0-00000000000000000000000000000000-0-0";
        var scene = Scene(); var current = Components<Transform>(scene).Where(t=>GlobalObjectId.GetGlobalObjectIdSlow(t).ToString()!=transientId).ToDictionary(t => GlobalObjectId.GetGlobalObjectIdSlow(t).ToString(), t => EditorJsonUtility.ToJson(t));
        var ledger=File.ReadAllLines(Output+"/SceneIdentity.tsv").Where(r=>!r.StartsWith(transientId+"\t")).ToArray();
        string Stable(string json)=>System.Text.RegularExpressions.Regex.Replace(json,"\"m_Children\":\\[.*?\\],", "");
        int identityChanges = ledger.Count(r => { int tab = r.IndexOf('\t'); return !current.TryGetValue(r.Substring(0, tab), out var json) || Stable(json) != Stable(r.Substring(tab + 1)); });
        string result = "Protected source/other-scene files changed=" + changed.Length + "; original Transform identity/serialization changed=" + identityChanges;
        File.WriteAllText(Output + "/Integrity.txt", result + "\n" + string.Join("\n", changed.Select(r => r[0]))); return result;
    }
    public static string RestoreUILayoutCaches()
    {
        var scene=Scene();var ledger=File.ReadAllLines(Output+"/SceneIdentity.tsv").Where(r=>!r.StartsWith("GlobalObjectId_V1-0-")).ToDictionary(r=>r.Substring(0,r.IndexOf('\t')),r=>r.Substring(r.IndexOf('\t')+1));int count=0;
        foreach(var rect in Components<RectTransform>(scene))
            if(ledger.TryGetValue(GlobalObjectId.GetGlobalObjectIdSlow(rect).ToString(),out var json)&&json!=EditorJsonUtility.ToJson(rect))
            {Undo.RecordObject(rect,"Restore pre-test UI layout cache");EditorJsonUtility.FromJsonOverwrite(json,rect);EditorUtility.SetDirty(rect);count++;}
        if(count>0){Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
        return "Restored original UI layout caches="+count;
    }
}
