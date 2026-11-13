using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class RailwayVegetationReduction
{
    static RailwayVegetationReduction() => EditorApplication.update += Tick;
    static void Tick()
    {
        const string request = "Temp/RailwayVegetationReduction.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(request);
        var report = new StringBuilder();
        int before = 0, after = 0, removed = 0;
        try
        {
            for (int z = 0; z < 58; z++)
            {
                string path = $"Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_{z:D3}.unity";
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                foreach (var root in scene.GetRootGameObjects())
                {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var match = Regex.Match(filter.name, @"^VegetationChunk_(\d+)$");
                    if (!match.Success || filter.sharedMesh == null) continue;
                    var mesh = filter.sharedMesh;
                    // This project's seven-sided trunks have 14 triangles per tree.
                    int indices = mesh.GetTriangles(0).Length;
                    if (mesh.subMeshCount != 2 || indices % 42 != 0)
                        throw new InvalidOperationException("Unrecognized vegetation geometry: " + filter.name);
                    int trees = indices / 42;
                    before += trees;
                    int chunk = int.Parse(match.Groups[1].Value);
                    bool keep = ((chunk / 3) * 13 % 10) < 2;
                    if (!keep)
                    {
                        UnityEngine.Object.DestroyImmediate(filter.gameObject);
                        removed++;
                    }
                    else
                    {
                        after += trees;
                        var renderer = filter.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                        }
                    }
                }
                // Stage only leaf mesh objects. Fixed infrastructure keeps its complete
                // authored hierarchy; terrain/rail remain immediately available here.
                var staged = new System.Collections.Generic.List<GameObject>();
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    bool scenery = false;
                    for (var parent = filter.transform.parent; parent != null; parent = parent.parent)
                        if (parent.name == "Vegetation" || parent.name == "Catenary_SingleMasts" || parent.name == "Roads") scenery = true;
                    if (!scenery || !filter.gameObject.activeSelf || filter.transform.childCount > 0) continue;
                    staged.Add(filter.gameObject);
                    filter.gameObject.SetActive(false);
                }
                if (staged.Count > 0)
                {
                    var activation = root.GetComponent<RailwayZoneActivation>();
                    if (activation == null) activation = root.AddComponent<RailwayZoneActivation>();
                    activation.chunks = staged.ToArray();
                }
                }
                EditorSceneManager.SaveScene(scene);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            report.AppendLine($"Baked trees before={before}, after={after}, removed={before-after}, removedVegetationObjects={removed}; reduction={(before>0 ? (1.0-after/(double)before)*100:0):F2}%.");
            report.AppendLine("Retained contiguous three-chunk groves in deterministic sparse regions; no regeneration on load. Source meshes/materials/prefabs untouched. TerrainTreeInstances=0 in baseline, detailDistance=0 already. Houses, bridge, roads and overhead lines retained.");
        }
        catch(Exception e) { report.AppendLine("ERROR: " + e); }
        File.WriteAllText("Temp/RailwayVegetationReduction.txt",report.ToString());
    }
}
