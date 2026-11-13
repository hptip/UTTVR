using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Profiling;

[InitializeOnLoad]
internal static class RailwayPerformanceAudit
{
    const string Request = "Temp/RailwayPerformanceAudit.request";
    const string Main = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Output = "Temp/RailwayPerformanceAudit.txt";
    static RailwayPerformanceAudit() => EditorApplication.update += Tick;
    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        File.Delete(Request);
        var report = new StringBuilder(DateTime.Now.ToString("O") + "\n");
        try
        {
            long trees = 0, renderers = 0, objects = 0, colliders = 0;
            var materials = new HashSet<Material>();
            var terrainData = new HashSet<TerrainData>();
            var paths = new List<string> { Main };
            for (int z = 0; z < 58; z++) paths.Add($"Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_{z:D3}.unity");
            foreach (string path in paths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                int n = 0, r = 0, c = 0, ti = 0, vegetationMeshes = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    n += root.GetComponentsInChildren<Transform>(true).Length;
                    c += root.GetComponentsInChildren<Collider>(true).Length;
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                    {
                        r++;
                        foreach (var material in renderer.sharedMaterials) if (material != null) materials.Add(material);
                        if (renderer.name.StartsWith("VegetationChunk_")) vegetationMeshes++;
                    }
                    foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                        if (terrain.terrainData != null)
                        {
                            ti += terrain.terrainData.treeInstanceCount;
                            terrainData.Add(terrain.terrainData);
                            report.AppendLine($"Terrain {terrain.name}: source={AssetDatabase.GetAssetPath(terrain.terrainData)}, trees={terrain.terrainData.treeInstanceCount}, detailDensity={terrain.detailObjectDensity}, detailDistance={terrain.detailObjectDistance}, heightmap={terrain.terrainData.heightmapResolution}");
                        }
                }
                report.AppendLine($"{path}: GO={n}, Renderer={r}, Collider={c}, TerrainTrees={ti}, BakedVegetationMeshes={vegetationMeshes}");
                objects += n; renderers += r; colliders += c; trees += ti;
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            report.AppendLine($"TOTAL authored: GO={objects}, Renderer={renderers}, Collider={colliders}, TerrainTreeInstances={trees}, uniqueMaterials={materials.Count}, uniqueTerrainData={terrainData.Count}. These are authored counts, not simultaneously active runtime counts.");
            report.AppendLine("Baked vegetation trees must be counted from connected trunk geometry; mesh count is not tree count. CPU/GPU cause not established by object counts.");
        }
        catch (Exception e) { report.AppendLine("ERROR: " + e); }
        File.WriteAllText(Output, report.ToString());
        Debug.Log("[RailwayPerformanceAudit] " + Output);
    }
}
