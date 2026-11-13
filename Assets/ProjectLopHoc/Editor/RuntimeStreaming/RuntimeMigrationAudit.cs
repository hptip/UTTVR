using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace YenTuHaLongXanh.RuntimeStreamingMigration
{
    public static class RuntimeMigrationAudit
    {
        private const string ScenePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
        private const string OutputPath = "F:/UnityTemp/YenTu_RuntimeMigration_Audit.txt";

        [MenuItem("Tools/Runtime Streaming/Run Migration Audit")]
        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("READ-ONLY RUNTIME MIGRATION AUDIT");
            sb.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            sb.AppendLine("Scene: " + ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var all = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).Distinct().ToArray();
            sb.AppendLine("RootCount=" + roots.Length + " GameObjectCount=" + all.Length);
            sb.AppendLine("ROOTS:");
            foreach (var root in roots.OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase))
                sb.AppendLine($"  {root.name} active={root.activeSelf} childCount={root.transform.childCount} components={string.Join(",", root.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name))}");

            sb.AppendLine("STATIC MAP TREE (depth <= 4):");
            var map = all.FirstOrDefault(g => g.name == "RailwayStaticMap");
            if (map != null) AppendTree(sb, map.transform, 0, 4);
            sb.AppendLine("RENDERER COUNTS:");
            foreach (var root in roots)
                sb.AppendLine($"  {root.name}: rendererCount={root.GetComponentsInChildren<Renderer>(true).Length} meshFilterCount={root.GetComponentsInChildren<MeshFilter>(true).Length} terrainCount={root.GetComponentsInChildren<Terrain>(true).Length}");

            sb.AppendLine("STREAMERS:");
            foreach (var go in all.Where(g => g.GetComponent("DynamicTrackStreamer") != null || g.GetComponent("TunnelStreamer") != null || g.GetComponent("SceneryStreamer") != null || g.GetComponent("StationStreamer") != null))
            {
                sb.AppendLine($"  {Hierarchy(go.transform)} active={go.activeInHierarchy}");
                foreach (var c in go.GetComponents<Component>().Where(c => c != null && (c.GetType().Name.EndsWith("Streamer", StringComparison.Ordinal) || c.GetType().Name == "TrainSplineMover")))
                    sb.AppendLine($"    {c.GetType().FullName} enabled={(c is Behaviour b ? b.enabled.ToString() : "n/a")}");
            }

            sb.AppendLine("KEY OBJECT COUNTS:");
            foreach (var token in new[] { "StaticRailwayMVP", "TerrainEnvironment_MVP", "PrototypeTrack", "DiagnosticPreview", "Station", "Bridge", "Tunnel", "Mountain", "River", "Road", "House", "Tree", "Vegetation", "Catenary", "PoleChunk", "TrackChunk" })
            {
                var matches = all.Where(g => g.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                if (matches.Length > 0) sb.AppendLine($"  {token}: {matches.Length}; sample={string.Join(" | ", matches.Take(6).Select(g => Hierarchy(g.transform)))}");
            }

            sb.AppendLine("TERRAINS:");
            foreach (var t in all.SelectMany(g => g.GetComponents<Terrain>()).Where(t => t != null))
            {
                var d = t.terrainData;
                sb.AppendLine($"  {Hierarchy(t.transform)} active={t.gameObject.activeInHierarchy} pos={t.transform.position} size={(d != null ? d.size.ToString() : "null")} resolution={(d != null ? d.heightmapResolution : 0)} asset={(d != null ? AssetDatabase.GetAssetPath(d) : "")}");
            }

            var spline = all.SelectMany(g => g.GetComponents<SplineContainer>()).FirstOrDefault(s => s != null && (s.name.Contains("Rail_Spline_Route") || s.gameObject.name.Contains("Rail_Spline_Route")));
            if (spline == null) spline = all.SelectMany(g => g.GetComponents<SplineContainer>()).FirstOrDefault(s => s != null);
            sb.AppendLine("SPLINE:");
            if (spline != null)
            {
                float len = spline.CalculateLength();
                sb.AppendLine($"  path={Hierarchy(spline.transform)} world={spline.transform.position} rot={spline.transform.rotation.eulerAngles} scale={spline.transform.lossyScale} length={len} knots={spline.Spline.Count}");
                var terrains = all.SelectMany(g => g.GetComponents<Terrain>()).Where(t => t != null && t.terrainData != null).ToArray();
                for (float chain = 0; chain <= len; chain += 500f)
                {
                    float t = len > 0 ? chain / len : 0;
                    var p3 = spline.EvaluatePosition(t);
                    var tan3 = spline.EvaluateTangent(t);
                    Vector3 p = new Vector3(p3.x, p3.y, p3.z);
                    Vector3 tan = new Vector3(tan3.x, tan3.y, tan3.z).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, tan).normalized;
                    var heights = new StringBuilder();
                    foreach (var tr in terrains)
                    {
                        foreach (float side in new[] { -30f, -15f, 0f, 15f, 30f })
                        {
                            Vector3 q = p + right * side;
                            var b = tr.terrainData.bounds;
                            Vector3 local = q - tr.transform.position;
                            if (local.x >= 0 && local.z >= 0 && local.x <= b.size.x && local.z <= b.size.z)
                                heights.Append($" {side:+0;-0;0}:{tr.SampleHeight(q) + tr.transform.position.y:F2}");
                        }
                    }
                    if (heights.Length > 0) sb.AppendLine($"  chain={chain:F0} route={p} terrainY[{heights}]");
                }
            }
            else sb.AppendLine("  NONE FOUND");

            sb.AppendLine("PREFAB ASSETS:");
            foreach (var path in new[] {
                "Assets/ProjectLopHoc/Prefabs/Tunnel.prefab",
                "Assets/ProjectLopHoc/Prefabs/Mountain_Peak.prefab",
                "Assets/ProjectLopHoc/Prefabs/Mountain_GreenHill.prefab",
                "Assets/ProjectLopHoc/Prefabs/MountainRidge.prefab",
                "Assets/ProjectLopHoc/Prefabs/Train Ride.prefab",
                "Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { sb.AppendLine("  MISSING " + path); continue; }
                var rends = prefab.GetComponentsInChildren<Renderer>(true);
                var bounds = rends.Length == 0 ? new Bounds(prefab.transform.position, Vector3.zero) : rends.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
                sb.AppendLine($"  {path}; renderers={rends.Length}; worldBounds={bounds.size}");
                foreach (var r in rends.Take(12)) sb.AppendLine($"    {r.name} type={r.GetType().Name} bounds={r.bounds.size} localPos={r.transform.localPosition}");
            }

            File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log("Runtime migration audit written: " + OutputPath);
        }

        private static string Hierarchy(Transform t)
        {
            var names = new System.Collections.Generic.Stack<string>();
            while (t != null) { names.Push(t.name); t = t.parent; }
            return string.Join("/", names);
        }

        private static int AppendTree(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            int descendants = 0;
            for (int i = 0; i < t.childCount; i++) descendants += 1 + CountDescendants(t.GetChild(i));
            string components = string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name));
            sb.AppendLine($"  {new string(' ', depth * 2)}{t.name} active={t.gameObject.activeSelf} children={t.childCount} descendants={descendants} pos={t.position} comps={components}");
            if (depth < maxDepth)
                for (int i = 0; i < t.childCount; i++) AppendTree(sb, t.GetChild(i), depth + 1, maxDepth);
            return descendants;
        }

        private static int CountDescendants(Transform t)
        {
            int count = t.childCount;
            for (int i = 0; i < t.childCount; i++) count += CountDescendants(t.GetChild(i));
            return count;
        }
    }
}
