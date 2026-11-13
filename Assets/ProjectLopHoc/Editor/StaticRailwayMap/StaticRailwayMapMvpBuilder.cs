using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

/// <summary>Editor-only tools for the static railway MVP. Source prefabs remain untouched.</summary>
public static class StaticRailwayMapMvpBuilder
{
    private static bool _suppressGeneratedObjectUndo;

    public const string ScenePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    public const string RailPath = "Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab";
    public const string PolePath = "Assets/ProjectLopHoc/Prefabs/pole.prefab";
    public const string PierPath = "Assets/ProjectLopHoc/Prefabs/Pier Column.prefab";
    public const string DeckPath = "Assets/ProjectLopHoc/Prefabs/Deck Girder.prefab";
    public const string StationPath = "Assets/ProjectLopHoc/Prefabs/Train Ride.prefab";
    public const string GeneratedRootPath = "Assets/ProjectLopHoc/Generated/StaticRailwayMap";
    public const string ManifestPath = GeneratedRootPath + "/StaticRailwayMapMvp_manifest.json";

    [Serializable]
    public sealed class Manifest
    {
        public string scenePath;
        public string routeObject;
        public string railPrefab;
        public string polePrefab;
        public string pierPrefab;
        public string deckPrefab;
        public string stationPrefab;
        public float routeLength;
        public float placementStep;
        public float trackSpacing;
        public float chunkLength;
        public float poleSpacing;
        public float supportSpacing;
        public float bridgeStart;
        public float bridgeLength;
        public int totalChunks;
        public int modulesPerTrack;
        public int poles;
        public int supports;
        public int stations;
        public string tunnelPolicy;
        public string buildId;
    }

    private sealed class PathTable
    {
        public float Length;
        public float[] T;
        public float[] Distance;
    }

    private struct Sample
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public Vector3 Up;
        public Vector3 Right => Vector3.Cross(Up, Tangent).normalized;
    }

    private sealed class MaterialBucket
    {
        public Material Material;
        public readonly List<CombineInstance> Instances = new List<CombineInstance>();
        public int VertexCount;
    }

    [MenuItem("Tools/Static Railway Map/Generate Full Static Railway MVP")]
    private static void GenerateFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        SplineContainer route = FindRoute(scene);
        GameObject rail = AssetDatabase.LoadAssetAtPath<GameObject>(RailPath);
        GameObject pole = AssetDatabase.LoadAssetAtPath<GameObject>(PolePath);
        GameObject pier = AssetDatabase.LoadAssetAtPath<GameObject>(PierPath);
        GameObject deck = AssetDatabase.LoadAssetAtPath<GameObject>(DeckPath);
        GameObject station = AssetDatabase.LoadAssetAtPath<GameObject>(StationPath);
        Build(scene, route, rail, pole, pier, deck, station, 6.400022f, 3f, 100f, 45f, 36f, 180f);
    }

    [MenuItem("Tools/Static Railway Map/Flatten Route Profile To One Height (Preserve XZ)")]
    private static void FlattenRouteProfileToOneHeight()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open YenTuHaLongXanh before flattening its rail route.");

        SplineContainer route = FindRoute(scene);
        if (route == null || route.Spline == null || route.Spline.Count < 2)
            throw new InvalidOperationException("Rail_Spline_Route must contain at least two knots.");

        Spline spline = route.Spline;
        int knotCount = spline.Count;
        var originalKnots = new BezierKnot[knotCount];
        float meanHeight = 0f;
        for (int i = 0; i < knotCount; i++)
        {
            originalKnots[i] = spline[i];
            meanHeight += originalKnots[i].Position.y;
        }
        meanHeight /= knotCount;

        Undo.RecordObject(route, "Flatten Rail Route Profile");
        for (int i = 0; i < knotCount; i++)
        {
            BezierKnot knot = originalKnots[i];
            Vector3 horizontalForward = Vector3.zero;
            Vector3 localOut = new Vector3(knot.TangentOut.x, knot.TangentOut.y, knot.TangentOut.z);
            Quaternion oldRotation = new Quaternion(knot.Rotation.value.x, knot.Rotation.value.y,
                knot.Rotation.value.z, knot.Rotation.value.w);
            if (localOut.sqrMagnitude > 0.000001f)
                horizontalForward = Vector3.ProjectOnPlane(oldRotation * localOut, Vector3.up);

            if (horizontalForward.sqrMagnitude < 0.000001f)
            {
                Vector3 previous = new Vector3(originalKnots[Mathf.Max(0, i - 1)].Position.x, 0f,
                    originalKnots[Mathf.Max(0, i - 1)].Position.z);
                Vector3 next = new Vector3(originalKnots[Mathf.Min(knotCount - 1, i + 1)].Position.x, 0f,
                    originalKnots[Mathf.Min(knotCount - 1, i + 1)].Position.z);
                horizontalForward = next - previous;
            }
            if (horizontalForward.sqrMagnitude < 0.000001f)
                throw new InvalidOperationException($"Could not determine horizontal heading at spline knot {i}.");

            Vector3 horizontalIn = Vector3.ProjectOnPlane(oldRotation * (Vector3)knot.TangentIn, Vector3.up);
            Vector3 horizontalOut = Vector3.ProjectOnPlane(oldRotation * (Vector3)knot.TangentOut, Vector3.up);
            knot.Position = new float3(knot.Position.x, meanHeight, knot.Position.z);
            Quaternion horizontalRotation = Quaternion.LookRotation(horizontalForward.normalized, Vector3.up);
            knot.Rotation = new quaternion(horizontalRotation.x, horizontalRotation.y,
                horizontalRotation.z, horizontalRotation.w);
            Quaternion inverseHorizontalRotation = Quaternion.Inverse(horizontalRotation);
            Vector3 localHorizontalIn = inverseHorizontalRotation * horizontalIn;
            Vector3 localHorizontalOut = inverseHorizontalRotation * horizontalOut;
            knot.TangentIn = new float3(localHorizontalIn.x, 0f, localHorizontalIn.z);
            knot.TangentOut = new float3(localHorizontalOut.x, 0f, localHorizontalOut.z);
            spline[i] = knot;
        }

        EditorUtility.SetDirty(route);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = route.gameObject;
        Debug.Log($"Rail route flattened: {knotCount} knots set to Y={meanHeight:F3}; horizontal XZ path preserved.");
    }

    public static Manifest Build(Scene scene, SplineContainer route, GameObject railPrefab,
        GameObject polePrefab, GameObject pierPrefab, GameObject deckPrefab, GameObject stationPrefab,
        float trackSpacing, float placementStep, float chunkLength, float poleSpacing,
        float supportSpacing, float bridgeLength)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh before generating the full static map.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Exit Prefab Mode before generating the Scene map.");
        if (route == null || railPrefab == null || polePrefab == null || pierPrefab == null || stationPrefab == null)
            throw new InvalidOperationException("Route, EBS rail, pole, pier, and Train Ride prefab references are required.");
        if (placementStep <= 0f || chunkLength <= 0f || poleSpacing <= 0f || supportSpacing <= 0f)
            throw new InvalidOperationException("All spacing values must be positive.");

        EnsureAssetFolder(GeneratedRootPath);
        EnsureAssetFolder(GeneratedRootPath + "/Meshes");
        string buildId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string previousManifestText = File.Exists(ToAbsoluteAssetPath(ManifestPath))
            ? File.ReadAllText(ToAbsoluteAssetPath(ManifestPath)) : null;
        Manifest previousManifest = !string.IsNullOrEmpty(previousManifestText)
            ? JsonUtility.FromJson<Manifest>(previousManifestText) : null;
        Transform chunksRoot = GetOrCreatePath(scene, "RailwayStaticMap/Baked/Chunks");
        Transform staticRoot = CreateChild(chunksRoot, "StaticRailwayMVP_Staging_" + buildId, scene);
        var createdAssetPaths = new List<string>();
        var undo = BeginUndo("Generate Full Static Railway MVP");
        bool previousUndoSuppression = _suppressGeneratedObjectUndo;
        _suppressGeneratedObjectUndo = true;
        try
        {
            PathTable table = BuildPathTable(route);
            int totalChunks = Mathf.CeilToInt(table.Length / chunkLength);
            int modulesPerTrack = 0;
            int poleCount = 0;
            int supportCount = 0;
            Bounds railBounds = PrefabBounds(railPrefab);

            Transform tracks = CreateChild(staticRoot, "Tracks", scene);
            Transform trackA = CreateChild(tracks, "Track_A", scene);
            Transform trackB = CreateChild(tracks, "Track_B", scene);
            for (int trackIndex = 0; trackIndex < 2; trackIndex++)
            {
                string trackName = trackIndex == 0 ? "Track_A" : "Track_B";
                float lateralOffset = trackIndex == 0 ? 0f : trackSpacing;
                Transform parent = trackIndex == 0 ? trackA : trackB;
                int trackModules = 0;
                for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    float start = chunkIndex * chunkLength;
                    float end = Mathf.Min(table.Length, start + chunkLength);
                    Transform chunk = BuildRailChunk(route, table, railPrefab, parent, scene, createdAssetPaths,
                        trackName, chunkIndex, start, end, placementStep, lateralOffset, buildId);
                    trackModules += Mathf.CeilToInt((end - start) / placementStep - 0.00001f);
                    if (EditorUtility.DisplayCancelableProgressBar("Static Railway MVP", $"{trackName}: chunk {chunkIndex + 1}/{totalChunks}",
                            (trackIndex * totalChunks + chunkIndex + 1f) / (totalChunks * 2f)))
                        throw new OperationCanceledException("Rail chunk generation cancelled by user.");
                }
                if (trackIndex == 0) modulesPerTrack = Mathf.CeilToInt(table.Length / placementStep);
            }

            Transform overhead = CreateChild(staticRoot, "Overhead_Electrification", scene);
            float wireHeight = Mathf.Clamp(PrefabBounds(polePrefab).size.y * 0.72f, 5.5f, 8.5f);
            for (int chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                float start = chunkIndex * chunkLength;
                float end = Mathf.Min(table.Length, start + chunkLength);
                BuildPoleChunk(route, table, polePrefab, overhead, scene, createdAssetPaths, chunkIndex,
                    start, end, poleSpacing, trackSpacing, wireHeight, buildId, ref poleCount);
            }
            BuildContactWires(route, table, overhead, scene, createdAssetPaths, poleSpacing,
                trackSpacing, wireHeight, 0.22f, buildId);

            float bridgeStart = FindLowCurvatureSegment(route, table, Mathf.Min(bridgeLength, table.Length - 200f));
            float actualBridgeLength = Mathf.Min(bridgeLength, table.Length - bridgeStart);
            Transform bridge = CreateChild(staticRoot, "Bridge_Main", scene);
            GameObject bridgeMarker = CreateSceneObject(scene, "BridgeSegment_Chainage_" + FormatMeters(bridgeStart), bridge);
            bridgeMarker.transform.position = Evaluate(route, table, bridgeStart + actualBridgeLength * 0.5f).Position;
            bridgeMarker.transform.rotation = Quaternion.LookRotation(Evaluate(route, table, bridgeStart + actualBridgeLength * 0.5f).Tangent,
                Evaluate(route, table, bridgeStart + actualBridgeLength * 0.5f).Up);
            bridgeMarker.name = $"Bridge_Main_{bridgeStart:F0}m_to_{bridgeStart + actualBridgeLength:F0}m";

            // The rail prefab already supplies the rail/deck envelope. Deck Girder is added only as
            // the bridge's secondary support deck, using its own measured envelope as an initial step.
            if (deckPrefab != null)
            {
                Bounds deckBounds = PrefabBounds(deckPrefab);
                float deckStep = Mathf.Max(3f, deckBounds.size.z);
                Transform girders = CreateChild(bridge, "Deck_Girders", scene);
                for (int trackIndex = 0; trackIndex < 2; trackIndex++)
                {
                    float offset = trackIndex == 0 ? 0f : trackSpacing;
                    int n = 0;
                    for (float d = bridgeStart; d < bridgeStart + actualBridgeLength; d += deckStep)
                    {
                        Sample p = Evaluate(route, table, d);
                        InstantiatePrefab(scene, deckPrefab, girders,
                            p.Position + p.Up * -0.8f + p.Right * offset,
                            Quaternion.LookRotation(p.Tangent, p.Up), $"BridgeDeck_{trackIndex}_{n++:D3}");
                    }
                }
            }

            Transform supports = CreateChild(bridge, "Supports_36m", scene);
            for (float d = bridgeStart; d <= bridgeStart + actualBridgeLength + 0.001f; d += supportSpacing)
            {
                Sample p = Evaluate(route, table, Mathf.Min(d, table.Length));
                for (int trackIndex = 0; trackIndex < 2; trackIndex++)
                {
                    float offset = trackIndex == 0 ? 0f : trackSpacing;
                    InstantiatePrefab(scene, pierPrefab, supports, p.Position + p.Right * offset,
                        Quaternion.LookRotation(p.Tangent, p.Up), $"Pier_{trackIndex}_{FormatMeters(d)}");
                    supportCount++;
                }
            }

            Transform stations = CreateChild(staticRoot, "Stations", scene);
            Sample startSample = Evaluate(route, table, 0f);
            Sample endSample = Evaluate(route, table, table.Length);
            InstantiatePrefab(scene, stationPrefab, stations,
                startSample.Position + startSample.Up * 3.2f + startSample.Tangent * 15f,
                Quaternion.LookRotation(startSample.Tangent, startSample.Up), "Station_Start_TrainRide");
            InstantiatePrefab(scene, stationPrefab, stations,
                endSample.Position + endSample.Up * 3.2f - endSample.Tangent * 15f,
                Quaternion.LookRotation(endSample.Tangent, endSample.Up), "Station_End_TrainRide");

            var manifest = new Manifest
            {
                scenePath = scene.path,
                routeObject = route.name,
                railPrefab = AssetDatabase.GetAssetPath(railPrefab),
                polePrefab = AssetDatabase.GetAssetPath(polePrefab),
                pierPrefab = AssetDatabase.GetAssetPath(pierPrefab),
                deckPrefab = deckPrefab != null ? AssetDatabase.GetAssetPath(deckPrefab) : "",
                stationPrefab = AssetDatabase.GetAssetPath(stationPrefab),
                routeLength = table.Length,
                placementStep = placementStep,
                trackSpacing = trackSpacing,
                chunkLength = chunkLength,
                poleSpacing = poleSpacing,
                supportSpacing = supportSpacing,
                bridgeStart = bridgeStart,
                bridgeLength = actualBridgeLength,
                totalChunks = totalChunks,
                modulesPerTrack = modulesPerTrack,
                poles = poleCount,
                supports = supportCount,
                stations = 2,
                tunnelPolicy = "No fixed mountain segment was identified; periodic TunnelStreamer is left for explicit deactivation after scene review.",
                buildId = buildId
            };
            File.WriteAllText(ToAbsoluteAssetPath(ManifestPath), JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(ManifestPath);
            // Promote only after every chunk and supporting system has been generated.
            // Keep any previous version in the Scene for rollback; never overwrite it.
            Transform previous = chunksRoot.Find("StaticRailwayMVP");
            if (previous != null)
            {
                Undo.RecordObject(previous, "Preserve Previous Static Railway MVP");
                previous.gameObject.SetActive(false);
                string previousId = previousManifest != null && !string.IsNullOrEmpty(previousManifest.buildId)
                    ? previousManifest.buildId : "Unknown_" + buildId;
                previous.name = "StaticRailwayMVP_Previous_" + previousId;
            }
            Undo.RecordObject(staticRoot, "Promote Static Railway MVP");
            staticRoot.name = "StaticRailwayMVP";
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeTransform = staticRoot;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"Static Railway MVP generated: {table.Length:F2}m, {totalChunks} chunks, {modulesPerTrack} modules/track, {poleCount} poles, {supportCount} bridge supports, 2 stations. Build {buildId}.");
            return manifest;
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            if (staticRoot != null) UnityEngine.Object.DestroyImmediate(staticRoot.gameObject);
            foreach (string path in createdAssetPaths) AssetDatabase.DeleteAsset(path);
            if (previousManifestText != null)
            {
                File.WriteAllText(ToAbsoluteAssetPath(ManifestPath), previousManifestText);
                AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceSynchronousImport);
            }
            else if (File.Exists(ToAbsoluteAssetPath(ManifestPath)))
            {
                File.Delete(ToAbsoluteAssetPath(ManifestPath));
                AssetDatabase.Refresh();
            }
            throw;
        }
        finally
        {
            _suppressGeneratedObjectUndo = previousUndoSuppression;
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(undo);
        }
    }

    public static void RegenerateChunkFromManifest(Scene scene, string trackName, int chunkIndex)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh before regenerating a chunk.");
        if (trackName != "Track_A" && trackName != "Track_B") throw new InvalidOperationException("Track must be Track_A or Track_B.");
        if (!File.Exists(ToAbsoluteAssetPath(ManifestPath))) throw new FileNotFoundException("Static Railway MVP manifest was not found.", ManifestPath);
        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ToAbsoluteAssetPath(ManifestPath)));
        if (chunkIndex < 0 || chunkIndex >= manifest.totalChunks) throw new ArgumentOutOfRangeException(nameof(chunkIndex));
        SplineContainer route = FindRoute(scene);
        GameObject rail = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.railPrefab);
        if (route == null || rail == null) throw new InvalidOperationException("Could not resolve the route or source rail from the manifest.");

        PathTable table = BuildPathTable(route);
        Transform root = FindStaticRoot(scene);
        Transform parent = root != null ? root.Find("Tracks/" + trackName) : null;
        if (parent == null) throw new InvalidOperationException("The generated track hierarchy does not exist.");
        var createdAssets = new List<string>();
        int undo = BeginUndo("Regenerate One Static Railway Chunk");
        Transform replacement = null;
        try
        {
            float start = chunkIndex * manifest.chunkLength;
            float end = Mathf.Min(table.Length, start + manifest.chunkLength);
            float offset = trackName == "Track_A" ? 0f : manifest.trackSpacing;
            replacement = BuildRailChunk(route, table, rail, parent, scene, createdAssets, trackName,
                chunkIndex, start, end, manifest.placementStep, offset, manifest.buildId + "_regen_" + Guid.NewGuid().ToString("N"));
            Transform old = parent.Find($"Chunk_{chunkIndex:D4}");
            if (old != null && old != replacement) Undo.DestroyObjectImmediate(old.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }
        catch
        {
            Undo.RevertAllDownToGroup(undo);
            foreach (string path in createdAssets) AssetDatabase.DeleteAsset(path);
            throw;
        }
        finally { Undo.CollapseUndoOperations(undo); }
    }

    public static void DeactivateSupersededStreamersAndDiagnostics(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh before switching to the static map.");
        Transform root = FindStaticRoot(scene);
        if (root == null || root.Find("Tracks/Track_A") == null || root.Find("Tracks/Track_B") == null)
            throw new InvalidOperationException("The full static map is not present. Review it before disabling any streamer.");

        int disabled = 0;
        foreach (GameObject go in scene.GetRootGameObjects())
        foreach (MonoBehaviour component in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || !component.enabled) continue;
            string className = component.GetType().Name;
            if (className == "DynamicTrackStreamer" || className == "RuntimeTrackStreamer" ||
                className == "TunnelStreamer" || className == "StationStreamer")
            {
                Undo.RecordObject(component, "Disable Superseded Railway Streamer");
                component.enabled = false;
                EditorUtility.SetDirty(component);
                disabled++;
            }
        }

        Transform editableGenerated = scene.GetRootGameObjects().Select(x => x.transform)
            .FirstOrDefault(x => x.name == "RailwayStaticMap")?.Find("Editable/Generated");
        if (editableGenerated != null)
        {
            foreach (string name in new[] { "PrototypeTrack", "DiagnosticPreview" })
            {
                Transform diagnostic = editableGenerated.Find(name);
                if (diagnostic == null) continue;
                Undo.RecordObject(diagnostic.gameObject, "Hide Superseded Railway Diagnostics");
                diagnostic.gameObject.SetActive(false);
                EditorUtility.SetDirty(diagnostic.gameObject);
            }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Static-map activation saved. Disabled {disabled} railway/tunnel/station streamer component(s); old PrototypeTrack and DiagnosticPreview were hidden, not deleted. SceneryStreamer, train, XR, and multiplayer were preserved.");
    }

    public static void RemovePreviousGeneratedMvp(Scene scene)
    {
        if (scene.path != ScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh before removing a previous generated map.");
        Transform mapRoot = scene.GetRootGameObjects().Select(x => x.transform).FirstOrDefault(x => x.name == "RailwayStaticMap");
        Transform chunks = mapRoot != null ? mapRoot.Find("Baked/Chunks") : null;
        if (chunks == null) throw new InvalidOperationException("Static railway chunk hierarchy was not found.");
        var oldRoots = new List<Transform>();
        for (int i = 0; i < chunks.childCount; i++)
        {
            Transform child = chunks.GetChild(i);
            if (child.name.StartsWith("StaticRailwayMVP_Previous_", StringComparison.Ordinal)) oldRoots.Add(child);
        }
        if (oldRoots.Count == 0) throw new InvalidOperationException("There are no previous generated map roots to clean up.");
        foreach (Transform oldRoot in oldRoots)
        {
            string buildId = oldRoot.name.Substring("StaticRailwayMVP_Previous_".Length);
            string generatedMeshFolder = ToAbsoluteAssetPath(GeneratedRootPath + "/Meshes").Replace('\\', '/');
            var ownedMeshPaths = oldRoot.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null)
                .Select(filter => AssetDatabase.GetAssetPath(filter.sharedMesh).Replace('\\', '/'))
                .Where(path => path.StartsWith(GeneratedRootPath + "/Meshes/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).ToList();
            Undo.DestroyObjectImmediate(oldRoot.gameObject);
            foreach (string file in Directory.GetFiles(generatedMeshFolder, "SRM_MVP_" + buildId + "_*.asset"))
            {
                string assetPath = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
                if (!ownedMeshPaths.Contains(assetPath, StringComparer.Ordinal)) ownedMeshPaths.Add(assetPath);
            }
            foreach (string assetPath in ownedMeshPaths) AssetDatabase.DeleteAsset(assetPath);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Removed {oldRoots.Count} inactive previous static map(s) and their generated mesh assets; source assets and PrototypeTrack/DiagnosticPreview/ManualObjects were preserved.");
    }

    private static Transform BuildRailChunk(SplineContainer route, PathTable table, GameObject prefab,
        Transform parent, Scene scene, List<string> createdAssets, string trackName, int chunkIndex,
        float start, float end, float step, float lateralOffset, string buildId)
    {
        Sample chunkCenter = Evaluate(route, table, (start + end) * 0.5f);
        Vector3 chunkOrigin = chunkCenter.Position + chunkCenter.Right * lateralOffset;
        Transform chunk = CreateChild(parent, $"Chunk_{chunkIndex:D4}", scene);
        chunk.position = chunkOrigin;
        chunk.rotation = Quaternion.identity;
        var buckets = new List<MaterialBucket>();
        Transform prefabRoot = prefab.transform;
        Matrix4x4 rootInverse = prefabRoot.worldToLocalMatrix;
        MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
        var repeatedTrackFilters = new List<MeshFilter>();
        var longDeckFilters = new List<MeshFilter>();
        foreach (MeshFilter filter in filters)
        {
            if (IsNestedDeckGirderFilter(filter)) longDeckFilters.Add(filter);
            else repeatedTrackFilters.Add(filter);
        }

        Bounds longDeckBounds = longDeckFilters.Count > 0
            ? PrefabBounds(prefabRoot, longDeckFilters)
            : default;
        if (longDeckFilters.Count == 0 || longDeckBounds.size.z <= step * 1.5f)
            throw new InvalidOperationException("Could not isolate the nested Deck Girder repeat geometry from the 3 m rail/sleeper module; generation stopped to avoid repeating an unmeasured long mesh.");

        int modules = 0;
        float firstChainage = Mathf.Ceil(start / step - 0.00001f) * step;
        for (float chainage = firstChainage; chainage < end - 0.0001f; chainage += step)
        {
            Sample sample = Evaluate(route, table, chainage);
            Quaternion rotation = Quaternion.LookRotation(sample.Tangent, sample.Up);
            Matrix4x4 instance = Matrix4x4.TRS(sample.Position + sample.Right * lateralOffset, rotation, prefabRoot.localScale);
            AddPrefabMeshesToBuckets(repeatedTrackFilters, rootInverse, instance, chunkOrigin, buckets);
            modules++;
        }

        // The rail and sleeper module repeats every 3 m. The nested Deck Girder
        // spans about 17.5 m, so including it in every 3 m instance overlays
        // several long girders and produces a corrugated visual edge. Keep the
        // original prefab untouched and bake its nested girder at its measured
        // repeat length instead.
        if (longDeckFilters.Count > 0 && longDeckBounds.size.z > 0.01f)
        {
            float repeatLength = longDeckBounds.size.z;
            float firstPivotChainage = Mathf.Ceil((start - longDeckBounds.center.z) / repeatLength - 0.00001f) * repeatLength;
            for (float pivotChainage = firstPivotChainage; pivotChainage < end - 0.0001f; pivotChainage += repeatLength)
            {
                Sample sample = Evaluate(route, table, Mathf.Max(0f, pivotChainage));
                Quaternion rotation = Quaternion.LookRotation(sample.Tangent, sample.Up);
                Matrix4x4 instance = Matrix4x4.TRS(sample.Position + sample.Right * lateralOffset, rotation, prefabRoot.localScale);
                AddPrefabMeshesToBuckets(longDeckFilters, rootInverse, instance, chunkOrigin, buckets);
            }
        }

        Mesh combined = CombineMaterialBuckets(buckets, $"SRM_MVP_{buildId}_{trackName}_Chunk_{chunkIndex:D4}", out Material[] outputMaterials);
        string meshPath = CreateMeshAsset(combined, createdAssets);
        chunk.gameObject.AddComponent<MeshFilter>().sharedMesh = combined;
        MeshRenderer targetRenderer = chunk.gameObject.AddComponent<MeshRenderer>();
        targetRenderer.sharedMaterials = outputMaterials;
        targetRenderer.shadowCastingMode = ShadowCastingMode.On;
        targetRenderer.receiveShadows = true;
        EditorUtility.SetDirty(chunk.gameObject);
        AssetDatabase.ImportAsset(meshPath, ImportAssetOptions.ForceSynchronousImport);
        return chunk;
    }

    private static bool IsNestedDeckGirderFilter(MeshFilter filter)
    {
        GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(filter.gameObject);
        if (instanceRoot == null) return false;
        GameObject sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
        return sourceRoot != null && AssetDatabase.GetAssetPath(sourceRoot) == DeckPath;
    }

    private static void AddPrefabMeshesToBuckets(List<MeshFilter> filters, Matrix4x4 rootInverse,
        Matrix4x4 instance, Vector3 chunkOrigin, List<MaterialBucket> buckets)
    {
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null) continue;
            Material[] sourceMaterials = renderer.sharedMaterials;
            Matrix4x4 matrix = Matrix4x4.Translate(-chunkOrigin) * instance * rootInverse * filter.transform.localToWorldMatrix;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                Material material = sourceMaterials.Length > 0 ? sourceMaterials[Mathf.Min(submesh, sourceMaterials.Length - 1)] : null;
                MaterialBucket bucket = buckets.FirstOrDefault(x => x.Material == material);
                if (bucket == null)
                {
                    bucket = new MaterialBucket { Material = material };
                    buckets.Add(bucket);
                }
                bucket.Instances.Add(new CombineInstance { mesh = mesh, subMeshIndex = submesh, transform = matrix });
                bucket.VertexCount += mesh.GetIndexCount(submesh) > 0 ? mesh.vertexCount : 0;
            }
        }
    }

    private static void BuildPoleChunk(SplineContainer route, PathTable table, GameObject polePrefab,
        Transform overhead, Scene scene, List<string> createdAssets, int chunkIndex, float start,
        float end, float spacing, float trackSpacing, float height, string buildId, ref int totalPoles)
    {
        Sample center = Evaluate(route, table, (start + end) * 0.5f);
        Vector3 origin = center.Position + center.Right * 2f;
        Transform chunk = CreateChild(overhead, $"PoleChunk_{chunkIndex:D4}", scene);
        chunk.position = origin;
        var buckets = new List<MaterialBucket>();
        Transform root = polePrefab.transform;
        Matrix4x4 rootInverse = root.worldToLocalMatrix;
        MeshFilter[] filters = polePrefab.GetComponentsInChildren<MeshFilter>(true);
        int poleIndex = Mathf.CeilToInt(start / spacing);
        for (float d = poleIndex * spacing; d < end - 0.0001f; d += spacing)
        {
            Sample sample = Evaluate(route, table, d);
            Vector3 position = sample.Position + sample.Right * 2f + sample.Up * 0.35f;
            Quaternion rotation = Quaternion.LookRotation(sample.Tangent, sample.Up) * Quaternion.Euler(0f, -90f, 0f);
            Matrix4x4 instance = Matrix4x4.TRS(position, rotation, root.localScale);
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null) continue;
                Material[] sourceMaterials = renderer.sharedMaterials;
                Matrix4x4 matrix = Matrix4x4.Translate(-origin) * instance * rootInverse * filter.transform.localToWorldMatrix;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Material material = sourceMaterials.Length > 0 ? sourceMaterials[Mathf.Min(submesh, sourceMaterials.Length - 1)] : null;
                    MaterialBucket bucket = buckets.FirstOrDefault(x => x.Material == material);
                    if (bucket == null)
                    {
                        bucket = new MaterialBucket { Material = material };
                        buckets.Add(bucket);
                    }
                    bucket.Instances.Add(new CombineInstance { mesh = mesh, subMeshIndex = submesh, transform = matrix });
                    bucket.VertexCount += mesh.vertexCount;
                }
            }
            totalPoles++;
        }

        Mesh combined = CombineMaterialBuckets(buckets, $"SRM_MVP_{buildId}_Poles_Chunk_{chunkIndex:D4}", out Material[] outputMaterials);
        string path = CreateMeshAsset(combined, createdAssets);
        chunk.gameObject.AddComponent<MeshFilter>().sharedMesh = combined;
        chunk.gameObject.AddComponent<MeshRenderer>().sharedMaterials = outputMaterials;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
    }

    private static Mesh CombineMaterialBuckets(List<MaterialBucket> buckets, string meshName, out Material[] materials)
    {
        if (buckets.Count == 0) throw new InvalidOperationException($"No renderable source geometry was found for {meshName}.");
        var temporaryMeshes = new List<Mesh>(buckets.Count);
        var combines = new CombineInstance[buckets.Count];
        materials = new Material[buckets.Count];
        int totalVertices = 0;
        for (int i = 0; i < buckets.Count; i++)
        {
            MaterialBucket bucket = buckets[i];
            Mesh temporary = new Mesh { name = meshName + "_MaterialPart_" + i.ToString("D2", CultureInfo.InvariantCulture) };
            if (bucket.VertexCount > 65535) temporary.indexFormat = IndexFormat.UInt32;
            temporary.CombineMeshes(bucket.Instances.ToArray(), true, true, false);
            temporary.RecalculateBounds();
            temporaryMeshes.Add(temporary);
            combines[i] = new CombineInstance { mesh = temporary, subMeshIndex = 0, transform = Matrix4x4.identity };
            materials[i] = bucket.Material;
            totalVertices += temporary.vertexCount;
        }
        Mesh result = new Mesh { name = meshName };
        if (totalVertices > 65535) result.indexFormat = IndexFormat.UInt32;
        result.CombineMeshes(combines, false, false, false);
        result.RecalculateBounds();
        foreach (Mesh temporary in temporaryMeshes) UnityEngine.Object.DestroyImmediate(temporary);
        return result;
    }

    private static void BuildContactWires(SplineContainer route, PathTable table, Transform parent, Scene scene,
        List<string> createdAssets, float poleSpacing, float trackSpacing, float height, float sag, string buildId)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int sides = 6;
        const int stepsPerSpan = 12;
        const float radius = 0.025f;
        int spanCount = Mathf.CeilToInt(table.Length / poleSpacing);
        for (int track = 0; track < 2; track++)
        {
            float lateral = track == 0 ? 0f : trackSpacing;
            for (int span = 0; span < spanCount; span++)
            {
                float a = span * poleSpacing;
                float b = Mathf.Min(table.Length, a + poleSpacing);
                if (b <= a) continue;
                int firstRing = -1;
                for (int i = 0; i <= stepsPerSpan; i++)
                {
                    float u = i / (float)stepsPerSpan;
                    float d = Mathf.Lerp(a, b, u);
                    Sample sample = Evaluate(route, table, d);
                    Vector3 center = sample.Position + sample.Right * lateral + sample.Up * (height - 4f * sag * u * (1f - u));
                    Vector3 right = sample.Right;
                    Vector3 up = sample.Up;
                    if (firstRing < 0) firstRing = vertices.Count / sides;
                    for (int side = 0; side < sides; side++)
                    {
                        float angle = side * Mathf.PI * 2f / sides;
                        vertices.Add(center + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius);
                    }
                    if (i == 0) continue;
                    int previousRing = vertices.Count / sides - 2;
                    int currentRing = previousRing + 1;
                    for (int side = 0; side < sides; side++)
                    {
                        int next = (side + 1) % sides;
                        int v0 = previousRing * sides + side;
                        int v1 = previousRing * sides + next;
                        int v2 = currentRing * sides + side;
                        int v3 = currentRing * sides + next;
                        triangles.Add(v0); triangles.Add(v2); triangles.Add(v1);
                        triangles.Add(v1); triangles.Add(v2); triangles.Add(v3);
                    }
                }
            }
        }
        Mesh wireMesh = new Mesh { name = "SRM_MVP_ContactWires" };
        if (vertices.Count > 65535) wireMesh.indexFormat = IndexFormat.UInt32;
        wireMesh.SetVertices(vertices);
        wireMesh.SetTriangles(triangles, 0);
        wireMesh.RecalculateNormals();
        wireMesh.RecalculateBounds();
        string meshAsset = CreateMeshAsset(wireMesh, createdAssets);
        Material material = CreateWireMaterial();
        GameObject wireObject = CreateSceneObject(scene, "Continuous_Contact_Wires_Track_A_B", parent);
        wireObject.AddComponent<MeshFilter>().sharedMesh = wireMesh;
        wireObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        AssetDatabase.ImportAsset(meshAsset, ImportAssetOptions.ForceSynchronousImport);
    }

    private static Material CreateWireMaterial()
    {
        const string path = GeneratedRootPath + "/Materials";
        EnsureAssetFolder(path);
        string materialPath = path + "/StaticRailway_ContactWire.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material != null) return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
        if (shader == null) throw new InvalidOperationException("No compatible built-in or URP shader was found for contact wires.");
        material = new Material(shader) { name = "StaticRailway_ContactWire" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.18f, 0.18f, 0.19f, 1f));
        else if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.18f, 0.18f, 0.19f, 1f));
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static Transform GetOrCreatePath(Scene scene, string path)
    {
        string[] segments = path.Split('/');
        Transform parent = null;
        foreach (string segment in segments)
        {
            Transform next = parent != null ? parent.Find(segment) : scene.GetRootGameObjects().Select(x => x.transform).FirstOrDefault(x => x.name == segment);
            if (next == null) next = CreateChild(parent, segment, scene);
            parent = next;
        }
        return parent;
    }

    private static Transform FindStaticRoot(Scene scene)
    {
        return scene.GetRootGameObjects().Select(x => x.transform).FirstOrDefault(x => x.name == "RailwayStaticMap")
            ?.Find("Baked/Chunks/StaticRailwayMVP");
    }

    private static SplineContainer FindRoute(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<SplineContainer>()
            .FirstOrDefault(x => x != null && x.gameObject.scene == scene && x.name == "Rail_Spline_Route");
    }

    private static Transform CreateChild(Transform parent, string name, Scene scene)
    {
        GameObject go = CreateSceneObject(scene, name, parent);
        return go.transform;
    }

    private static GameObject CreateSceneObject(Scene scene, string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        if (!scene.IsValid()) throw new InvalidOperationException("Invalid target Scene.");
        SceneManager.MoveGameObjectToScene(go, scene);
        if (_suppressGeneratedObjectUndo)
        {
            if (parent != null) go.transform.SetParent(parent, true);
        }
        else
        {
            Undo.RegisterCreatedObjectUndo(go, "Create Static Railway MVP Object");
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "Parent Static Railway MVP Object");
        }
        return go;
    }

    private static GameObject InstantiatePrefab(Scene scene, GameObject prefab, Transform parent,
        Vector3 position, Quaternion rotation, string name)
    {
        GameObject go = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (go == null) throw new InvalidOperationException($"Could not instantiate prefab '{prefab.name}'.");
        if (_suppressGeneratedObjectUndo)
        {
            if (parent != null) go.transform.SetParent(parent, true);
        }
        else
        {
            Undo.RegisterCreatedObjectUndo(go, "Instantiate Static Railway MVP Prefab");
            if (parent != null) Undo.SetTransformParent(go.transform, parent, "Parent Static Railway MVP Prefab");
        }
        go.name = name;
        go.transform.SetPositionAndRotation(position, rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go;
    }

    private static PathTable BuildPathTable(SplineContainer route)
    {
        float estimatedLength = Mathf.Max(route.CalculateLength(), 1f);
        int sampleCount = Mathf.Clamp(Mathf.CeilToInt(estimatedLength * 2f), 4096, 200000);
        PathTable table = new PathTable { T = new float[sampleCount + 1], Distance = new float[sampleCount + 1] };
        Vector3 previous = route.EvaluatePosition(0f);
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Vector3 p = route.EvaluatePosition(t);
            table.T[i] = t;
            if (i > 0) table.Distance[i] = table.Distance[i - 1] + Vector3.Distance(previous, p);
            previous = p;
        }
        table.Length = table.Distance[sampleCount];
        return table;
    }

    private static Sample Evaluate(SplineContainer route, PathTable table, float distance)
    {
        distance = Mathf.Clamp(distance, 0f, table.Length);
        int index = Array.BinarySearch(table.Distance, distance);
        if (index < 0) index = ~index;
        index = Mathf.Clamp(index, 1, table.Distance.Length - 1);
        float lower = table.Distance[index - 1];
        float upper = table.Distance[index];
        float fraction = upper > lower ? (distance - lower) / (upper - lower) : 0f;
        float t = Mathf.Lerp(table.T[index - 1], table.T[index], fraction);
        Vector3 localPos = route.EvaluatePosition(t);
        Vector3 localTangent = route.EvaluateTangent(t);
        Vector3 localUp = route.EvaluateUpVector(t);
        Vector3 tangent = route.transform.TransformDirection(localTangent).normalized;
        Vector3 up = route.transform.TransformDirection(localUp).normalized;
        if (tangent.sqrMagnitude < 0.000001f) tangent = Vector3.forward;
        if (up.sqrMagnitude < 0.000001f) up = Vector3.up;
        return new Sample { Position = route.transform.TransformPoint(localPos), Tangent = tangent, Up = up };
    }

    private static float FindLowCurvatureSegment(SplineContainer route, PathTable table, float length)
    {
        const float endpointMargin = 100f;
        const float scanStep = 10f;
        const float analysisStep = 5f;
        if (table.Length <= length + 2f * endpointMargin) return Mathf.Max(0f, (table.Length - length) * 0.5f);
        float bestScore = float.PositiveInfinity;
        float bestStart = endpointMargin;
        for (float start = endpointMargin; start + length <= table.Length - endpointMargin; start += scanStep)
        {
            Sample previous = default;
            float prevGrade = 0f;
            float maxTurn = 0f;
            float maxGradeDelta = 0f;
            bool hasPrevious = false;
            for (float d = start; d <= start + length + 0.001f; d += analysisStep)
            {
                Sample current = Evaluate(route, table, Mathf.Min(d, start + length));
                float vertical = Vector3.Dot(current.Tangent, current.Up);
                float horizontal = Vector3.ProjectOnPlane(current.Tangent, current.Up).magnitude;
                float grade = Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;
                if (hasPrevious)
                {
                    maxTurn = Mathf.Max(maxTurn, Vector3.Angle(previous.Tangent, current.Tangent));
                    maxGradeDelta = Mathf.Max(maxGradeDelta, Mathf.Abs(grade - prevGrade));
                }
                previous = current;
                prevGrade = grade;
                hasPrevious = true;
            }
            float score = maxTurn + maxGradeDelta;
            if (score < bestScore) { bestScore = score; bestStart = start; }
        }
        return bestStart;
    }

    private static Bounds PrefabBounds(GameObject prefab)
    {
        return PrefabBounds(prefab.transform, prefab.GetComponentsInChildren<MeshFilter>(true));
    }

    private static Bounds PrefabBounds(Transform root, IReadOnlyList<MeshFilter> filters)
    {
        Matrix4x4 inverse = root.worldToLocalMatrix;
        bool hasBounds = false;
        Bounds bounds = default;
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null) continue;
            Matrix4x4 matrix = inverse * filter.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3(x, y, z));
                Vector3 point = matrix.MultiplyPoint3x4(corner);
                if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                else bounds.Encapsulate(point);
            }
        }
        if (!hasBounds) throw new InvalidOperationException($"Prefab geometry under '{root.name}' has no readable mesh bounds.");
        return bounds;
    }

    private static string CreateMeshAsset(Mesh mesh, List<string> createdPaths)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(GeneratedRootPath + "/Meshes/" + mesh.name + ".asset");
        AssetDatabase.CreateAsset(mesh, path);
        createdPaths.Add(path);
        return path;
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        string absolute = ToAbsoluteAssetPath(assetPath);
        if (!Directory.Exists(absolute)) Directory.CreateDirectory(absolute);
        AssetDatabase.Refresh();
    }

    private static string ToAbsoluteAssetPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));
    }

    private static int BeginUndo(string name)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
        return group;
    }

    private static string FormatMeters(float value)
    {
        return ((long)Math.Round(value * 1000d, MidpointRounding.AwayFromZero)).ToString("D10", CultureInfo.InvariantCulture);
    }
}
