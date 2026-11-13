using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

public static class RuntimeZoneSceneBuilder
{
    private const string MainScenePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    private const string ZoneFolder = "Assets/ProjectLopHoc/Generated/RuntimeZones";
    private const string MeshFolder = ZoneFolder + "/Meshes";
    private const string MapManifestPath = "Assets/ProjectLopHoc/Generated/StaticRailwayMap/StaticRailwayMapMvp_manifest.json";
    private const float TrackSpacing = 6.400022f;
    private const float PoleSpacing = 45f;
    private const int WireSides = 6;
    private const int WireStepsPerSpan = 12;
    private const float WireSagMeters = 0.22f;
    private const float WireRadiusMeters = 0.025f;

    private sealed class TransferItem
    {
        public GameObject source;
        public float chainage;
        public string group;
        public bool moveLeftTrack;
        public bool fixedFacility;
    }

    private sealed class PathTable
    {
        public float[] t;
        public float[] distance;
        public float length;
    }

    private struct Sample
    {
        public Vector3 position;
        public Vector3 tangent;
        public Vector3 up;
        public Vector3 right;
    }

    [MenuItem("Tools/Runtime Streaming/1 - Build Additive Zone Scenes")]
    public static void BuildZoneScenes()
    {
        EnsureNotPlaying();
        Scene main = RequireMainScene();
        var manifest = ReadManifest();
        Transform map = FindTransform(main, "RailwayStaticMap");
        Transform sourceRoot = map != null ? map.Find("Baked/Chunks/StaticRailwayMVP") : null;
        Transform environment = map != null ? map.Find("Editable/Generated/TerrainEnvironment_MVP") : null;
        Transform routeTransform = FindTransform(main, "Rail_Spline_Route");
        SplineContainer route = routeTransform != null ? routeTransform.GetComponent<SplineContainer>() : null;
        if (map == null || sourceRoot == null || environment == null || route == null)
            throw new InvalidOperationException("Required live roots are missing: RailwayStaticMap, active StaticRailwayMVP, TerrainEnvironment_MVP, or Rail_Spline_Route.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ZoneFolder + "/Zone_000.unity") != null)
            throw new InvalidOperationException("RuntimeZones/Zone_000 already exists. Refusing to overwrite zone scenes.");

        float zoneLength = 500f;
        int zoneCount = Mathf.CeilToInt(manifest.routeLength / zoneLength);
        PathTable table = BuildPathTable(route);
        List<TransferItem> items = GatherItems(sourceRoot, environment, manifest, table, route);
        Directory.CreateDirectory(Path.GetFullPath(ZoneFolder));
        Directory.CreateDirectory(Path.GetFullPath(MeshFolder));
        AssetDatabase.Refresh();

        int transferred = 0;
        int rendererCount = 0;
        Material wireMaterial = FindWireMaterial(sourceRoot);
        if (wireMaterial == null) throw new InvalidOperationException("Contact-wire material could not be resolved from the current static map.");
        float wireHeight = FindWireHeight();
        SceneManager.SetActiveScene(main);
        var createdScenePaths = new List<string>();
        var createdMeshPaths = new List<string>();

        try
        {
            for (int zoneIndex = 0; zoneIndex < zoneCount; zoneIndex++)
            {
                float start = zoneIndex * zoneLength;
                float end = Mathf.Min(table.length, start + zoneLength);
                string zoneId = $"Zone_{zoneIndex:D3}";
                string scenePath = ZoneFolder + "/" + zoneId + ".unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
                    throw new InvalidOperationException("Refusing to overwrite existing scene " + scenePath);

                Scene zoneScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                zoneScene.name = zoneId;
                SceneManager.SetActiveScene(zoneScene);
                var root = new GameObject("RuntimeZoneContent");
                var categoryRoots = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (TransferItem item in items)
                {
                    if (item.chainage < start - 0.001f || item.chainage >= end + 0.001f) continue;
                    Transform category = GetCategoryRoot(zoneScene, root.transform, categoryRoots, item.group);
                    GameObject clone = UnityEngine.Object.Instantiate(item.source);
                    clone.name = item.source.name;
                    clone.transform.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(clone, zoneScene);
                    clone.transform.SetParent(category, true);
                    if (item.moveLeftTrack)
                    {
                        Sample sample = Evaluate(table, route, Mathf.Clamp(item.chainage, 0f, table.length));
                        clone.transform.position -= sample.right * (2f * TrackSpacing);
                    }
                    if (item.fixedFacility) ShiftSecondBridgeTrack(clone.transform, route, table);
                    transferred++;
                    rendererCount += clone.GetComponentsInChildren<Renderer>(true).Length;
                }

                CreateZoneWire(zoneScene, root.transform, route, table, start, end, zoneIndex, wireMaterial, wireHeight, createdMeshPaths);

                EditorSceneManager.MarkSceneDirty(zoneScene);
                createdScenePaths.Add(scenePath);
                if (!EditorSceneManager.SaveScene(zoneScene, scenePath))
                    throw new IOException("Unity failed to save " + scenePath);
                EditorSceneManager.CloseScene(zoneScene, true);
                SceneManager.SetActiveScene(main);
            }
        }
        catch
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != main && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
            foreach (string path in createdScenePaths) AssetDatabase.DeleteAsset(path);
            foreach (string path in createdMeshPaths) AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            SceneManager.SetActiveScene(main);
            throw;
        }

        AddZonesToBuildSettings(zoneCount);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);
        File.WriteAllText("F:/UnityTemp/YenTu_RuntimeZones_Build.txt",
            $"Created {zoneCount} additive zone scenes, {transferred} copied source objects, {rendererCount} copied renderers.\n" +
            $"Route={manifest.routeLength:F2} m; zoneLength={zoneLength:F0} m; trackSpacing={TrackSpacing:F6} m; wireHeight={wireHeight:F2} m.\n" +
            "Sources remain in the main scene; no static source object or source asset was deleted or disabled.\n" +
            "TunnelMountainGroups: none registered; route-side terrain audit found no mountain crossing requiring a tunnel.\n" +
            $"Bridge fixed facility: {manifest.bridgeStart:F0}-{manifest.bridgeStart + manifest.bridgeLength:F0} m.\n");
        Debug.Log($"Runtime zone scenes created: {zoneCount} scenes, {transferred} copied source objects. Main static map is untouched.");
    }

    [MenuItem("Tools/Runtime Streaming/2 - Add Streaming Manager (Keep Static Map)")]
    public static void AddStreamingManager()
    {
        EnsureNotPlaying();
        Scene main = RequireMainScene();
        var manifest = ReadManifest();
        int zoneCount = Mathf.CeilToInt(manifest.routeLength / 500f);
        for (int i = 0; i < zoneCount; i++)
        {
            string path = ZoneFolder + $"/Zone_{i:D3}.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("Missing zone scene; build all zones first: " + path);
        }

        GameObject managerObject = GameObject.Find("RailwayRuntimeStreaming");
        if (managerObject == null)
        {
            managerObject = new GameObject("RailwayRuntimeStreaming");
            SceneManager.MoveGameObjectToScene(managerObject, main);
        }
        var manager = managerObject.GetComponent<RailwayZoneStreamingManager>();
        if (manager == null) manager = Undo.AddComponent<RailwayZoneStreamingManager>(managerObject);
        manager.trainMover = FindTransform(main, "Train_THSR_700T")?.GetComponent<TrainSplineMover>();
        manager.routeLengthMeters = manifest.routeLength;
        manager.zoneLengthMeters = 500f;
        manager.loadAheadMeters = 650f;
        manager.keepBehindMeters = 150f;
        manager.updateIntervalSeconds = 0.25f;
        manager.logZoneTransitions = true;
        manager.zones.Clear();
        for (int i = 0; i < zoneCount; i++)
        {
            float start = i * 500f;
            manager.zones.Add(new RailwayZoneStreamingManager.ZoneRecord
            {
                zoneId = $"Zone_{i:D3}",
                startChainage = start,
                endChainage = Mathf.Min(manifest.routeLength, start + 500f),
                scenePath = ZoneFolder + $"/Zone_{i:D3}.unity"
            });
        }

        int bridgeZoneA = Mathf.Clamp(Mathf.FloorToInt(manifest.bridgeStart / 500f), 0, zoneCount - 1);
        int bridgeZoneB = Mathf.Clamp(Mathf.FloorToInt((manifest.bridgeStart + manifest.bridgeLength - 0.001f) / 500f), 0, zoneCount - 1);
        var bridgeZones = bridgeZoneA == bridgeZoneB
            ? new[] { $"Zone_{bridgeZoneA:D3}" }
            : new[] { $"Zone_{bridgeZoneA:D3}", $"Zone_{bridgeZoneB:D3}" };
        manager.fixedInfrastructure.Clear();
        manager.fixedInfrastructure.Add(new RailwayZoneStreamingManager.FixedInfrastructureRecord
        {
            id = "Bridge_Main",
            kind = "Bridge",
            startChainage = manifest.bridgeStart,
            endChainage = manifest.bridgeStart + manifest.bridgeLength,
            zoneId = bridgeZones[0],
            zoneIds = bridgeZones,
            mountainAssetReference = "",
            tunnelAssetReference = ""
        });

        AddZonesToBuildSettings(zoneCount);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);
        Debug.Log($"RailwayZoneStreamingManager configured for {zoneCount} zones. Static map remains active; use step 3 to switch for Play Mode validation.");
    }

    [MenuItem("Tools/Runtime Streaming/3 - Activate Streaming for Play Mode Validation")]
    public static void ActivateStreamingForPlayMode()
    {
        EnsureNotPlaying();
        Scene main = RequireMainScene();
        var manager = GameObject.Find("RailwayRuntimeStreaming")?.GetComponent<RailwayZoneStreamingManager>();
        if (manager == null || manager.zones.Count == 0) throw new InvalidOperationException("Build zones and add the manager first.");
        Transform map = FindTransform(main, "RailwayStaticMap");
        Transform staticRoot = map.Find("Baked/Chunks/StaticRailwayMVP");
        Transform environment = map.Find("Editable/Generated/TerrainEnvironment_MVP");
        if (staticRoot == null || environment == null) throw new InvalidOperationException("Static output roots were not found.");

        Undo.RecordObject(staticRoot.gameObject, "Temporarily Disable Static Railway Map for Streaming Test");
        staticRoot.gameObject.SetActive(false);
        foreach (string child in new[] { "Mesh_Transitions_100m_Chunks", "Rural_Roads_100m_Chunks", "Baked_Vegetation_100m_Chunks", "Existing_Houses_Deterministic_Placement", "River_Valley_At_Existing_Bridge" })
        {
            Transform go = environment.Find(child);
            if (go == null) continue;
            Undo.RecordObject(go.gameObject, "Temporarily Disable Static Environment for Streaming Test");
            go.gameObject.SetActive(false);
        }
        manager.enabled = true;
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);
        Debug.Log("Streaming enabled for Play Mode validation. The original terrain, source objects, inactive older map roots, and all source assets are still retained.");
    }

    [MenuItem("Tools/Runtime Streaming/4 - Restore Static Map Visibility")]
    public static void RestoreStaticMapVisibility()
    {
        EnsureNotPlaying();
        Scene main = RequireMainScene();
        Transform map = FindTransform(main, "RailwayStaticMap");
        Transform staticRoot = map != null ? map.Find("Baked/Chunks/StaticRailwayMVP") : null;
        Transform environment = map != null ? map.Find("Editable/Generated/TerrainEnvironment_MVP") : null;
        if (staticRoot != null) staticRoot.gameObject.SetActive(true);
        if (environment != null)
            foreach (string child in new[] { "Mesh_Transitions_100m_Chunks", "Rural_Roads_100m_Chunks", "Baked_Vegetation_100m_Chunks", "Existing_Houses_Deterministic_Placement", "River_Valley_At_Existing_Bridge" })
            {
                Transform go = environment.Find(child);
                if (go != null) go.gameObject.SetActive(true);
            }
        var manager = GameObject.Find("RailwayRuntimeStreaming")?.GetComponent<RailwayZoneStreamingManager>();
        if (manager != null) manager.enabled = false;
        EditorSceneManager.MarkSceneDirty(main);
        EditorSceneManager.SaveScene(main);
        Debug.Log("Static visibility restored and runtime manager disabled.");
    }

    [MenuItem("Tools/Runtime Streaming/6 - Remove Replaced Static Outputs")]
    public static void RemoveReplacedStaticOutputs()
    {
        EnsureNotPlaying();
        Scene main = RequireMainScene();
        var manifest = ReadManifest();
        var manager = GameObject.Find("RailwayRuntimeStreaming")?.GetComponent<RailwayZoneStreamingManager>();
        int expectedZones = Mathf.CeilToInt(manifest.routeLength / 500f);
        if (manager == null || !manager.enabled || manager.zones.Count != expectedZones)
            throw new InvalidOperationException("Refusing cleanup: the configured runtime streaming manager is missing, disabled, or incomplete.");
        for (int i = 0; i < expectedZones; i++)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ZoneFolder + $"/Zone_{i:D3}.unity") == null)
                throw new InvalidOperationException("Refusing cleanup: a runtime zone scene is missing.");

        Transform map = FindTransform(main, "RailwayStaticMap");
        Transform chunks = map != null ? map.Find("Baked/Chunks") : null;
        Transform generated = map != null ? map.Find("Editable/Generated") : null;
        Transform environment = generated != null ? generated.Find("TerrainEnvironment_MVP") : null;
        Transform terrain = environment != null ? environment.Find("Terrain") : null;
        if (chunks == null || generated == null || environment == null || terrain == null)
            throw new InvalidOperationException("Refusing cleanup: required map/terrain roots were not found.");
        Transform terrainObject = terrain.Find("YenTu_Region_Terrain_EditableCopy");
        if (terrainObject == null) throw new InvalidOperationException("Refusing cleanup: editable Terrain object is missing.");

        var remove = new List<GameObject>();
        foreach (Transform child in chunks)
            if (child.name == "StaticRailwayMVP" || child.name.StartsWith("StaticRailwayMVP_Previous_", StringComparison.Ordinal))
                remove.Add(child.gameObject);
        foreach (string name in new[] { "PrototypeTrack", "DiagnosticPreview" })
        {
            Transform item = generated.Find(name);
            if (item != null) remove.Add(item.gameObject);
        }
        foreach (string name in new[] { "Mesh_Transitions_100m_Chunks", "Rural_Roads_100m_Chunks", "Baked_Vegetation_100m_Chunks", "Existing_Houses_Deterministic_Placement", "River_Valley_At_Existing_Bridge" })
        {
            Transform item = environment.Find(name);
            if (item != null) remove.Add(item.gameObject);
        }

        if (remove.Count == 0) throw new InvalidOperationException("No replaced static outputs were found; nothing was removed.");
        int removedCount = remove.Count;
        foreach (GameObject item in remove) Undo.DestroyObjectImmediate(item);
        EditorSceneManager.MarkSceneDirty(main);
        if (!EditorSceneManager.SaveScene(main)) throw new IOException("Unity failed to save the cleaned main scene.");
        Debug.Log($"Removed {removedCount} replaced static output roots. Terrain, manual objects, source assets, spline, train, XR, and runtime zone scenes were retained.");
    }

    [MenuItem("Tools/Runtime Streaming/5 - Validate Zone Scene Assets")]
    public static void ValidateZoneScenes()
    {
        EnsureNotPlaying();
        Scene main = RequireMainScene();
        var manifest = ReadManifest();
        int count = Mathf.CeilToInt(manifest.routeLength / 500f);
        int missing = 0;
        int missingScripts = 0;
        int missingMeshes = 0;
        long bytes = 0;
        int totalObjects = 0;
        int totalRenderers = 0;
        var report = new System.Text.StringBuilder();
        report.AppendLine("RUNTIME ZONE SCENE VALIDATION");
        report.AppendLine($"Route={manifest.routeLength:F3}m ZoneLength=500m Zones={count}");
        for (int i = 0; i < count; i++)
        {
            string path = ZoneFolder + $"/Zone_{i:D3}.unity";
            if (!File.Exists(Path.GetFullPath(path))) { Debug.LogError("Missing zone scene: " + path); missing++; continue; }
            bytes += new FileInfo(Path.GetFullPath(path)).Length;
            Scene zone = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (!zone.IsValid() || !zone.isLoaded) { Debug.LogError("Could not open zone scene " + path); missing++; continue; }
            GameObject[] roots = zone.GetRootGameObjects();
            GameObject[] objects = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).Distinct().ToArray();
            int renderers = 0;
            foreach (GameObject go in objects)
            {
                renderers += go.GetComponents<Renderer>().Length;
                foreach (Component component in go.GetComponents<Component>()) if (component == null) missingScripts++;
                foreach (MeshFilter filter in go.GetComponents<MeshFilter>()) if (filter.sharedMesh == null) missingMeshes++;
            }
            bool hasContentRoot = roots.Any(r => r.name == "RuntimeZoneContent");
            bool hasRight = objects.Any(g => g.name == "Track_Right");
            bool hasLeft = objects.Any(g => g.name == "Track_Left");
            bool hasWires = objects.Any(g => g.name == "Continuous_Contact_Wire_Zone");
            bool hasBridge = i == Mathf.FloorToInt(manifest.bridgeStart / 500f) && objects.Any(g => g.name == "Bridge_Main");
            if (!hasContentRoot || !hasRight || !hasLeft || !hasWires || (i == Mathf.FloorToInt(manifest.bridgeStart / 500f) && !hasBridge))
                missing++;
            totalObjects += objects.Length;
            totalRenderers += renderers;
            report.AppendLine($"{path}: objects={objects.Length} renderers={renderers} Track_Right={hasRight} Track_Left={hasLeft} wires={hasWires} bridge={hasBridge}");
            EditorSceneManager.CloseScene(zone, true);
            SceneManager.SetActiveScene(main);
        }
        var manager = GameObject.Find("RailwayRuntimeStreaming")?.GetComponent<RailwayZoneStreamingManager>();
        bool managerOkay = manager != null && manager.enabled && manager.zones.Count == count && manager.trainMover != null;
        int buildScenes = EditorBuildSettings.scenes.Count(s => s.path.StartsWith(ZoneFolder + "/", StringComparison.Ordinal));
        report.AppendLine($"SUMMARY: missingZoneChecks={missing} missingScripts={missingScripts} missingMeshes={missingMeshes} objects={totalObjects} renderers={totalRenderers} yamlBytes={bytes} managerReady={managerOkay} buildSettingsEntries={buildScenes}");
        report.AppendLine("Main-scene generated static outputs were cleaned after Play Mode streaming validation. Editable Terrain, source assets, and manual objects remain.");
        File.WriteAllText("F:/UnityTemp/YenTu_RuntimeZones_Validation.txt", report.ToString());
        Debug.Log($"Zone validation: expected={count}, failed zone checks={missing}, missing scripts={missingScripts}, missing meshes={missingMeshes}, objects={totalObjects}, renderers={totalRenderers}, sceneYamlBytes={bytes}, managerReady={managerOkay}, Build Settings entries={buildScenes}. Report: F:/UnityTemp/YenTu_RuntimeZones_Validation.txt");
    }

    private static List<TransferItem> GatherItems(Transform staticRoot, Transform environment, StaticRailwayMapMvpBuilder.Manifest manifest, PathTable table, SplineContainer route)
    {
        var result = new List<TransferItem>();
        AddChunkChildren(result, staticRoot.Find("Tracks/Track_A"), "Track_Right", false, table);
        AddChunkChildren(result, staticRoot.Find("Tracks/Track_B"), "Track_Left", true, table);
        Transform overhead = staticRoot.Find("Overhead_Electrification");
        if (overhead != null)
            foreach (Transform child in overhead)
                if (child.name.StartsWith("PoleChunk_", StringComparison.Ordinal))
                    result.Add(new TransferItem { source = child.gameObject, chainage = ChainageFromChunkName(child.name, table.length), group = "Catenary_SingleMasts" });

        Transform bridge = staticRoot.Find("Bridge_Main");
        if (bridge != null)
            result.Add(new TransferItem { source = bridge.gameObject, chainage = manifest.bridgeStart + manifest.bridgeLength * 0.5f, group = "FixedInfrastructure_Bridge", fixedFacility = true });

        Transform stations = staticRoot.Find("Stations");
        if (stations != null)
            foreach (Transform child in stations)
            {
                bool isStart = child.name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0;
                result.Add(new TransferItem { source = child.gameObject, chainage = isStart ? 0f : table.length - 0.01f, group = isStart ? "FixedInfrastructure_Stations_Start" : "FixedInfrastructure_Stations_End" });
            }

        AddEnvironmentChunks(result, environment.Find("Mesh_Transitions_100m_Chunks"), "TerrainTransitions", table);
        AddEnvironmentChunks(result, environment.Find("Rural_Roads_100m_Chunks"), "Roads", table);
        AddEnvironmentChunks(result, environment.Find("Baked_Vegetation_100m_Chunks"), "Vegetation", table);
        Transform houses = environment.Find("Existing_Houses_Deterministic_Placement");
        if (houses != null)
            foreach (Transform house in houses)
            {
                float chainage = ParseMetersSuffix(house.name, table.length);
                if (float.IsNaN(chainage)) chainage = NearestChainage(table, route, house.position);
                result.Add(new TransferItem { source = house.gameObject, chainage = chainage, group = "Houses" });
            }
        Transform river = environment.Find("River_Valley_At_Existing_Bridge");
        if (river != null)
            result.Add(new TransferItem { source = river.gameObject, chainage = manifest.bridgeStart + manifest.bridgeLength * 0.5f, group = "FixedInfrastructure_Bridge" });

        if (result.Any(i => i.source == null || float.IsNaN(i.chainage)))
            throw new InvalidOperationException("Could not resolve chainage for one or more static outputs; no source content was changed.");
        return result;
    }

    private static void AddChunkChildren(List<TransferItem> result, Transform source, string group, bool moveLeftTrack, PathTable table)
    {
        if (source == null) throw new InvalidOperationException("Missing static track source " + group);
        foreach (Transform child in source)
            result.Add(new TransferItem { source = child.gameObject, chainage = ChainageFromChunkName(child.name, table.length), group = group, moveLeftTrack = moveLeftTrack });
    }

    private static void AddEnvironmentChunks(List<TransferItem> result, Transform source, string group, PathTable table)
    {
        if (source == null) throw new InvalidOperationException("Missing environment output " + group);
        foreach (Transform child in source)
            result.Add(new TransferItem { source = child.gameObject, chainage = ChainageFromChunkName(child.name, table.length), group = group });
    }

    private static float ChainageFromChunkName(string name, float routeLength)
    {
        var meterRange = Regex.Match(name, @"(\d+(?:\.\d+)?)m_to_(\d+(?:\.\d+)?)m", RegexOptions.IgnoreCase);
        if (meterRange.Success && float.TryParse(meterRange.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float start) &&
            float.TryParse(meterRange.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float end)) return (start + end) * 0.5f;
        var chunk = Regex.Match(name, @"Chunk_(\d{1,4})", RegexOptions.IgnoreCase);
        if (chunk.Success && int.TryParse(chunk.Groups[1].Value, out int index)) return Mathf.Min(routeLength - 0.01f, index * 100f + 50f);
        return float.NaN;
    }

    private static float ParseMetersSuffix(string name, float routeLength)
    {
        var match = Regex.Match(name, @"_(\d+(?:\.\d+)?)m(?:$|_)", RegexOptions.IgnoreCase);
        return match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)
            ? Mathf.Clamp(value, 0f, routeLength - 0.01f) : float.NaN;
    }

    private static Transform GetCategoryRoot(Scene scene, Transform root, Dictionary<string, Transform> cache, string category)
    {
        if (cache.TryGetValue(category, out Transform found)) return found;
        var go = new GameObject(category);
        go.transform.SetParent(root, false);
        cache.Add(category, go.transform);
        return go.transform;
    }

    private static void ShiftSecondBridgeTrack(Transform bridge, SplineContainer route, PathTable table)
    {
        foreach (Transform child in bridge.GetComponentsInChildren<Transform>(true))
        {
            bool secondTrack = child.name.StartsWith("Pier_1_", StringComparison.Ordinal) || child.name.StartsWith("BridgeDeck_1_", StringComparison.Ordinal);
            if (!secondTrack) continue;
            Sample sample = Evaluate(table, route, NearestChainage(table, route, child.position));
            child.position -= sample.right * (2f * TrackSpacing);
        }
    }

    private static void CreateZoneWire(Scene scene, Transform root, SplineContainer route, PathTable table,
        float zoneStart, float zoneEnd, int zoneIndex, Material material, float height, List<string> createdMeshPaths)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var normals = new List<Vector3>();
        float firstSpan = Mathf.Floor(zoneStart / PoleSpacing) * PoleSpacing;
        for (int track = 0; track < 2; track++)
        {
            float lateral = track == 0 ? 0f : -TrackSpacing;
            for (float spanStart = firstSpan; spanStart < zoneEnd; spanStart += PoleSpacing)
            {
                float spanEnd = Mathf.Min(table.length, spanStart + PoleSpacing);
                for (int step = 1; step <= WireStepsPerSpan; step++)
                {
                    float a = spanStart + (spanEnd - spanStart) * ((step - 1f) / WireStepsPerSpan);
                    float b = spanStart + (spanEnd - spanStart) * (step / (float)WireStepsPerSpan);
                    float mid = (a + b) * 0.5f;
                    if (mid < zoneStart || mid >= zoneEnd || b <= 0f || a >= table.length) continue;
                    int baseIndex = vertices.Count;
                    AddWireRing(vertices, normals, route, table, Mathf.Clamp(a, 0f, table.length), spanStart, lateral, height);
                    AddWireRing(vertices, normals, route, table, Mathf.Clamp(b, 0f, table.length), spanStart, lateral, height);
                    for (int side = 0; side < WireSides; side++)
                    {
                        int next = (side + 1) % WireSides;
                        int v0 = baseIndex + side;
                        int v1 = baseIndex + next;
                        int v2 = baseIndex + WireSides + side;
                        int v3 = baseIndex + WireSides + next;
                        triangles.Add(v0); triangles.Add(v2); triangles.Add(v1);
                        triangles.Add(v1); triangles.Add(v2); triangles.Add(v3);
                    }
                }
            }
        }
        if (vertices.Count == 0) return;
        var mesh = new Mesh { name = $"Runtime_Catenary_Zone_{zoneIndex:D3}" };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.SetNormals(normals);
        mesh.RecalculateBounds();
        string meshPath = MeshFolder + $"/ContactWire_Zone_{zoneIndex:D3}.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
            throw new InvalidOperationException("Wire mesh already exists; refusing to overwrite: " + meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        createdMeshPaths.Add(meshPath);
        var wire = new GameObject("Continuous_Contact_Wire_Zone");
        SceneManager.MoveGameObjectToScene(wire, scene);
        wire.transform.SetParent(root, false);
        wire.AddComponent<MeshFilter>().sharedMesh = mesh;
        wire.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void AddWireRing(List<Vector3> vertices, List<Vector3> normals, SplineContainer route, PathTable table,
        float chainage, float spanStart, float lateral, float height)
    {
        Sample sample = Evaluate(table, route, chainage);
        float u = Mathf.Clamp01((chainage - spanStart) / PoleSpacing);
        Vector3 center = sample.position + sample.right * lateral + sample.up * (height - 4f * WireSagMeters * u * (1f - u));
        for (int side = 0; side < WireSides; side++)
        {
            float angle = side * Mathf.PI * 2f / WireSides;
            Vector3 radial = (sample.right * Mathf.Cos(angle) + sample.up * Mathf.Sin(angle)).normalized;
            vertices.Add(center + radial * WireRadiusMeters);
            normals.Add(radial);
        }
    }

    private static Material FindWireMaterial(Transform staticRoot)
    {
        Transform overhead = staticRoot.Find("Overhead_Electrification");
        Transform wire = overhead != null ? overhead.Find("Continuous_Contact_Wires_Track_A_B") : null;
        Material fromScene = wire != null ? wire.GetComponent<MeshRenderer>()?.sharedMaterial : null;
        if (fromScene != null) return fromScene;
        return AssetDatabase.LoadAssetAtPath<Material>("Assets/ProjectLopHoc/Generated/StaticRailwayMap/Materials/StaticRailway_ContactWire.mat");
    }

    private static float FindWireHeight()
    {
        GameObject pole = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/pole.prefab");
        if (pole == null) return 7.5f;
        Renderer[] renderers = pole.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return 7.5f;
        Bounds bounds = renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        return Mathf.Clamp(bounds.size.y * 0.72f, 5.5f, 8.5f);
    }

    private static PathTable BuildPathTable(SplineContainer route)
    {
        float estimated = Mathf.Max(route.CalculateLength(), 1f);
        int count = Mathf.Clamp(Mathf.CeilToInt(estimated * 2f), 4096, 200000);
        var table = new PathTable { t = new float[count + 1], distance = new float[count + 1] };
        Vector3 previous = ToVector3(route.EvaluatePosition(0f));
        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;
            Vector3 p = ToVector3(route.EvaluatePosition(t));
            table.t[i] = t;
            if (i > 0) table.distance[i] = table.distance[i - 1] + Vector3.Distance(previous, p);
            previous = p;
        }
        table.length = table.distance[count];
        return table;
    }

    private static Sample Evaluate(PathTable table, SplineContainer route, float chainage)
    {
        chainage = Mathf.Clamp(chainage, 0f, table.length);
        int index = Array.BinarySearch(table.distance, chainage);
        if (index < 0) index = ~index;
        index = Mathf.Clamp(index, 1, table.distance.Length - 1);
        float span = table.distance[index] - table.distance[index - 1];
        float u = span > 0f ? (chainage - table.distance[index - 1]) / span : 0f;
        float t = Mathf.Lerp(table.t[index - 1], table.t[index], u);
        Vector3 local = ToVector3(route.EvaluatePosition(t));
        Vector3 localTangent = ToVector3(route.EvaluateTangent(t));
        Vector3 localUp = ToVector3(route.EvaluateUpVector(t));
        Vector3 tangent = route.transform.TransformDirection(localTangent).normalized;
        Vector3 up = route.transform.TransformDirection(localUp).normalized;
        if (tangent.sqrMagnitude < 0.000001f) tangent = Vector3.forward;
        if (up.sqrMagnitude < 0.000001f) up = Vector3.up;
        Vector3 right = Vector3.Cross(up, tangent).normalized;
        return new Sample { position = route.transform.TransformPoint(local), tangent = tangent, up = up, right = right };
    }

    private static float NearestChainage(PathTable table, SplineContainer route, Vector3 position)
    {
        float best = float.PositiveInfinity;
        float bestDistance = 0f;
        for (int i = 0; i < table.distance.Length; i += 8)
        {
            Vector3 p = route.transform.TransformPoint(ToVector3(route.EvaluatePosition(table.t[i])));
            float sqr = (p - position).sqrMagnitude;
            if (sqr < best) { best = sqr; bestDistance = table.distance[i]; }
        }
        float lo = Mathf.Max(0f, bestDistance - 10f);
        float hi = Mathf.Min(table.length, bestDistance + 10f);
        for (float d = lo; d <= hi; d += 0.5f)
        {
            Sample sample = Evaluate(table, route, d);
            float sqr = (sample.position - position).sqrMagnitude;
            if (sqr < best) { best = sqr; bestDistance = d; }
        }
        return bestDistance;
    }

    private static Vector3 ToVector3(Unity.Mathematics.float3 value) => new Vector3(value.x, value.y, value.z);

    private static void AddZonesToBuildSettings(int count)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        for (int i = 0; i < count; i++)
        {
            string path = ZoneFolder + $"/Zone_{i:D3}.unity";
            if (scenes.Any(s => string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase))) continue;
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static IEnumerable<Scene> GetLoadedGeneratedScenes()
    {
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path.StartsWith(ZoneFolder + "/", StringComparison.Ordinal)) yield return scene;
        }
    }

    private static StaticRailwayMapMvpBuilder.Manifest ReadManifest()
    {
        string absolute = Path.GetFullPath(MapManifestPath);
        if (!File.Exists(absolute)) throw new FileNotFoundException("Static railway manifest not found.", absolute);
        var manifest = JsonUtility.FromJson<StaticRailwayMapMvpBuilder.Manifest>(File.ReadAllText(absolute));
        if (manifest == null || manifest.routeLength <= 0f || manifest.chunkLength <= 0f)
            throw new InvalidDataException("Static railway manifest is missing valid route data.");
        return manifest;
    }

    private static Transform FindTransform(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name) return root.transform;
            Transform found = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (found != null) return found;
        }
        return null;
    }

    private static Scene RequireMainScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != MainScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh as the active Scene before using this tool.");
        return scene;
    }

    private static void EnsureNotPlaying()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before changing zone assets or Scene data.");
    }
}
