using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class ShowroomGeometryV3
{
    public const string ScenePath = "Assets/ProjectLopHoc/Scenes/Showroom.unity";
    public const string Output = "Documentation/ShowroomGeometryV3";
    public const string AssetsFolder = "Assets/ProjectLopHoc/Generated/ShowroomGeometryV3";
    public sealed class Use
    {
        public MeshRenderer renderer; public Mesh mesh; public string category; public bool active, instanced;
        public long Triangles => SubmittedTriangles(mesh, renderer.sharedMaterials.Length);
    }
    static Scene Scene()
    {
        var scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Open saved Showroom in Edit Mode first.");
        if (scene.isDirty) throw new Exception("Showroom has unsaved edits; refusing to overwrite.");
        return scene;
    }
    static IEnumerable<T> Components<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    public static long Triangles(Mesh mesh, int lod = 0)
    {
        long n = 0; if (mesh == null) return n;
        for (int s = 0; s < mesh.subMeshCount; s++) if (mesh.GetTopology(s) == MeshTopology.Triangles) n += mesh.GetIndexCount(s, lod) / 3;
        return n;
    }
    public static long SubmittedTriangles(Mesh mesh, int materials)
    {
        long n = 0; if (mesh == null) return n;
        for (int slot = 0; slot < materials; slot++) { int s = Math.Min(slot, mesh.subMeshCount - 1); if (s >= 0 && mesh.GetTopology(s) == MeshTopology.Triangles) n += mesh.GetIndexCount(s, 0) / 3; }
        return n;
    }
    static string Category(MeshRenderer renderer)
    {
        var layer = renderer.GetComponentInParent<UTT.SaBan.SaBanLayer>();
        if (layer != null) return layer.key;
        var path = PathOf(renderer.transform).ToLowerInvariant();
        if (path.Contains("train") || path.Contains("metro") || path.Contains("thsr") || path.Contains("tau")) return "train";
        return "khac";
    }
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static List<Use> Uses(Scene scene)
    {
        var instanced = new HashSet<MeshRenderer>(Components<ShowroomInstancedVisualGroup>(scene).Where(g => g.enabled && g.data != null).SelectMany(g => g.sources).Where(r => r != null));
        var lowerLods = new HashSet<Renderer>(Components<LODGroup>(scene).SelectMany(g => g.GetLODs().Skip(1)).SelectMany(l => l.renderers));
        return Components<MeshRenderer>(scene).Where(r => r.GetComponent<MeshFilter>()?.sharedMesh != null).Select(r => new Use { renderer = r, mesh = r.GetComponent<MeshFilter>().sharedMesh, category = Category(r), active = r.enabled && r.gameObject.activeInHierarchy && !lowerLods.Contains(r), instanced = instanced.Contains(r) }).ToList();
    }
    static string Csv(object o) => "\"" + Convert.ToString(o, CultureInfo.InvariantCulture).Replace("\"", "\"\"") + "\"";
    static string GuidId(Mesh mesh)
    {
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id); return guid + "_" + id;
    }
    static string Hash(string path) { using (var h = SHA256.Create()) using (var f = File.OpenRead(path)) return BitConverter.ToString(h.ComputeHash(f)); }
    [MenuItem("UTT/Showroom Geometry V3/1 Audit")]
    public static string Audit()
    {
        var scene = Scene(); var uses = Uses(scene); Directory.CreateDirectory(Output);
        var groups = uses.GroupBy(u => new { u.mesh, u.category }).OrderByDescending(g => g.Where(u => u.active).Sum(u => u.Triangles)).ToArray();
        var rows = new List<string> { "Category,Mesh,Asset,GUID_LocalID,UniqueMeshVertices,UniqueMeshTriangles,Submeshes,MeshLODCount,RendererCount,ActiveInstances,InstancedSources,WeightedActiveVertices,WeightedActiveTriangles,Materials" };
        foreach (var g in groups)
            rows.Add(string.Join(",", new object[] { g.Key.category, g.Key.mesh.name, AssetDatabase.GetAssetPath(g.Key.mesh), GuidId(g.Key.mesh), g.Key.mesh.vertexCount, Triangles(g.Key.mesh), g.Key.mesh.subMeshCount, g.Key.mesh.lodCount, g.Count(), g.Count(u => u.active), g.Count(u => u.instanced), g.Where(u => u.active).Sum(u => (long)u.mesh.vertexCount), g.Where(u => u.active).Sum(u => u.Triangles), string.Join(" | ", g.SelectMany(u => u.renderer.sharedMaterials).Where(m => m != null).Select(m => AssetDatabase.GetAssetPath(m)).Distinct()) }.Select(Csv)));
        File.WriteAllLines(Output + "/Showroom_Geometry_Audit.csv", rows);
        var report = new List<string> { "# Showroom Geometry Audit V3", "", "Unity: " + Application.unityVersion, "", "These are active geometry submission estimates for one color pass with all active objects visible, not measured triangles/frame. Instanced sources are counted once, not again per batch. Actual stereo, shadows, extra cameras, frustum/occlusion and shader passes change frame counters. Extra material slots repeat the last submesh as Unity does. Unique asset counts are separate.", "", "|Group|Unique meshes|Renderers|Active instances|Vertices × active instances|Triangles × active submissions|Materials|Submeshes (unique)|", "|---|---:|---:|---:|---:|---:|---:|---:|" };
        foreach (string key in new[] { "rail", "terrain", "nature", "bridge", "tunnel", "station", "train", "khac" })
        {
            var g = uses.Where(u => u.category == key).ToArray();
            report.Add($"|{key}|{g.Select(u => u.mesh).Distinct().Count()}|{g.Length}|{g.Count(u => u.active)}|{g.Where(u => u.active).Sum(u => (long)u.mesh.vertexCount)}|{g.Where(u => u.active).Sum(u => u.Triangles)}|{g.SelectMany(u => u.renderer.sharedMaterials).Where(m => m != null).Distinct().Count()}|{g.Select(u => u.mesh).Distinct().Sum(m => m.subMeshCount)}|");
        }
        report.AddRange(new[] { "", "## Top 20 weighted mesh/category bottlenecks", "", "|Group|Mesh|Active instances|Triangles per mesh|Weighted triangles|", "|---|---|---:|---:|---:|" });
        report.AddRange(groups.Take(20).Select(g => $"|{g.Key.category}|{g.Key.mesh.name}|{g.Count(u => u.active)}|{Triangles(g.Key.mesh)}|{g.Where(u => u.active).Sum(u => u.Triangles)}|"));
        report.AddRange(new[] { "", "LODGroup count: " + Components<LODGroup>(scene).Count(), "Mesh assets with embedded Mesh LOD: " + uses.Select(u => u.mesh).Distinct().Count(m => m.lodCount > 1), "Custom instancing originally calls DrawMeshInstanced at full detail, 390 batches / 49,074 sources. GPU instancing does not reduce per-instance triangles.", "Total active weighted geometry triangles (one pass): " + uses.Where(u => u.active).Sum(u => u.Triangles), "", "No native Terrain or SkinnedMeshRenderer was found in this saved Showroom (reconfirm on subsequent audits)." });
        File.WriteAllLines(Output + "/Showroom_Geometry_Audit.md", report);
        var protectedPaths = Directory.GetFiles("Assets/ProjectLopHoc/Scenes", "*.unity", SearchOption.TopDirectoryOnly).Where(p => p.Replace('\\', '/') != ScenePath).Concat(Directory.GetFiles("Assets/ProjectLopHoc/Generated/RuntimeZones", "*.unity", SearchOption.AllDirectories)).Concat(uses.Select(u => AssetDatabase.GetAssetPath(u.mesh))).Concat(uses.SelectMany(u => u.renderer.sharedMaterials).Where(m => m != null).Select(AssetDatabase.GetAssetPath)).Where(File.Exists).Distinct().ToArray();
        // Hash ledger only. No backups or source asset copies.
        if (!File.Exists(Output + "/Protected.tsv")) File.WriteAllLines(Output + "/Protected.tsv", protectedPaths.Select(p => p + "\t" + Hash(p)));
        if (!File.Exists(Output + "/SceneIdentity.tsv")) File.WriteAllLines(Output + "/SceneIdentity.tsv", Components<Transform>(scene).Select(t => GlobalObjectId.GetGlobalObjectIdSlow(t) + "\t" + EditorJsonUtility.ToJson(t)));
        return "Audit saved. Mesh/category rows=" + groups.Length + "; active weighted triangles=" + uses.Where(u => u.active).Sum(u => u.Triangles);
    }
}
