using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

/// <summary>Creates a deterministic, editable terrain and environment around the saved static railway.</summary>
public static class StaticRailwayTerrainEnvironmentMvp
{
    private const string ScenePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    private const string TerrainPrefabPath = "Assets/ProjectLopHoc/Prefabs/SaBan_YenTu_HaLong_Miniature.prefab";
    private const string TerrainDataPath = "Assets/New Terrain.asset";
    private const string TerrainSatelliteLayerPath = "Assets/ProjectLopHoc/Art/YenTu_Satellite_Layer.terrainlayer";
    private const string TerrainSatelliteTexturePath = "Assets/ProjectLopHoc/Art/Terrain_Satellite_Texture.png";
    private const string SoilTexturePath = "Assets/ADG_Textures/ground_vol1/ground1/ground1_Diffuse.tga";
    private const string HouseSmallPath = "Assets/ProjectLopHoc/YenTuFix/house_small.fbx";
    private const string HousePorchPath = "Assets/ProjectLopHoc/YenTuFix/house_porch.fbx";
    private const string HouseTallPath = "Assets/ProjectLopHoc/YenTuFix/house_two_storey.fbx";
    private const string RailwayPath = "RailwayStaticMap/Baked/Chunks/StaticRailwayMVP";
    private const string OutputPath = "Assets/ProjectLopHoc/Generated/StaticRailwayEnvironmentMVP";
    private const string SceneRootName = "TerrainEnvironment_MVP";
    private const float ChunkLength = 100f;
    private const int TerrainResolution = 2049;
    private const int TerrainAlphaResolution = 512;
    private const float TrackCenterOffset = 3.200011f;

    [Serializable]
    private sealed class RailwayManifest
    {
        public float bridgeStart;
        public float bridgeLength;
        public float trackSpacing;
        public float routeLength;
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
        public Vector3 Right => new Vector3(Tangent.z, 0f, -Tangent.x).normalized;
    }

    [MenuItem("Tools/Static Railway Map/Generate Terrain and Environment MVP")]
    private static void Generate()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh before generating the terrain environment.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before generating saved terrain.");
        if (FindPath(scene, "RailwayStaticMap/Editable/Generated/" + SceneRootName) != null)
            throw new InvalidOperationException("TerrainEnvironment_MVP already exists. It was left untouched.");

        SplineContainer route = FindRoute(scene);
        Transform staticRoot = FindPath(scene, RailwayPath);
        GameObject terrainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerrainPrefabPath);
        TerrainData sourceData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        TerrainLayer sourceGrass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainSatelliteLayerPath);
        Texture2D soilTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SoilTexturePath);
        GameObject[] houses =
        {
            AssetDatabase.LoadAssetAtPath<GameObject>(HouseSmallPath),
            AssetDatabase.LoadAssetAtPath<GameObject>(HousePorchPath),
            AssetDatabase.LoadAssetAtPath<GameObject>(HouseTallPath)
        };
        if (route == null || staticRoot == null || terrainPrefab == null || sourceData == null || sourceGrass == null || soilTexture == null)
            throw new InvalidOperationException("Required route, active static railway, terrain source, or ground textures are missing.");
        if (houses.Any(h => h == null)) throw new InvalidOperationException("One of the existing YenTuFix house models could not be loaded.");

        EnsureFolder(OutputPath);
        EnsureFolder(OutputPath + "/Terrain");
        EnsureFolder(OutputPath + "/Layers");
        EnsureFolder(OutputPath + "/Materials");
        EnsureFolder(OutputPath + "/Meshes");
        string terrainAssetPath = OutputPath + "/Terrain/YenTu_StaticEnvironment_TerrainData.asset";
        string grassLayerPath = OutputPath + "/Layers/YenTu_Grass.terrainlayer";
        string soilLayerPath = OutputPath + "/Layers/YenTu_ExposedSoil.terrainlayer";
        if (AssetDatabase.LoadMainAssetAtPath(terrainAssetPath) != null || AssetDatabase.LoadMainAssetAtPath(grassLayerPath) != null ||
            AssetDatabase.LoadMainAssetAtPath(soilLayerPath) != null)
            throw new InvalidOperationException("Generated terrain assets already exist; no source or generated asset was overwritten.");

        GameObject root = null;
        TerrainData generatedData = null;
        var createdAssets = new List<string>();
        bool success = false;
        try
        {
            Transform parent = FindPath(scene, "RailwayStaticMap/Editable/Generated");
            if (parent == null) throw new InvalidOperationException("RailwayStaticMap/Editable/Generated was not found.");
            root = new GameObject(SceneRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Generate Terrain and Environment MVP");
            root.transform.SetParent(parent, false);

            TerrainLayer grass = CreateLayer(sourceGrass, null, "YenTu_GeographicGround", new Vector2(sourceData.size.x, sourceData.size.z), OutputPath + "/Layers/YenTu_Grass.terrainlayer", createdAssets);
            TerrainLayer soil = CreateLayer(sourceGrass, soilTexture, "YenTu_ExposedSoil", new Vector2(26f, 26f), OutputPath + "/Layers/YenTu_ExposedSoil.terrainlayer", createdAssets);
            Material grassMaterial = CreateMaterial("Environment_Grass", grass.diffuseTexture, Color.white, OutputPath + "/Materials/Environment_Grass.mat", createdAssets);
            Material roadMaterial = CreateMaterial("Environment_RuralRoad", soilTexture, new Color(0.52f, 0.46f, 0.34f), OutputPath + "/Materials/Environment_RuralRoad.mat", createdAssets);
            Material trunkMaterial = CreateMaterial("Environment_TreeTrunks", null, new Color(0.24f, 0.15f, 0.085f), OutputPath + "/Materials/Environment_TreeTrunks.mat", createdAssets);
            Material canopyMaterial = CreateMaterial("Environment_TreeCanopy", null, new Color(0.17f, 0.34f, 0.17f), OutputPath + "/Materials/Environment_TreeCanopy.mat", createdAssets);
            Material waterMaterial = CreateMaterial("Environment_RiverWater", null, new Color(0.08f, 0.34f, 0.40f), OutputPath + "/Materials/Environment_RiverWater.mat", createdAssets);

            PathTable table = BuildPathTable(route);
            RailwayManifest manifest = ReadManifest();
            float bridgeStart = Mathf.Clamp(manifest.bridgeStart, 0f, table.Length);
            float bridgeLength = Mathf.Clamp(manifest.bridgeLength, 0f, table.Length - bridgeStart);
            float bridgeMiddle = bridgeStart + bridgeLength * 0.5f;
            Bounds trackBounds = GetTrackBounds(staticRoot);
            float trackFloor = trackBounds.min.y;
            float gradedGroundY = trackFloor - 0.35f;
            Transform bridgeRoot = staticRoot.Find("Bridge_Main");
            float supportBottom = GetRendererMinY(bridgeRoot != null ? bridgeRoot.Find("Supports_36m") : null, gradedGroundY - 1f);
            float waterY = Mathf.Min(gradedGroundY - 10f, supportBottom - 1.25f);
            float valleyFloorY = waterY - 2f;

            GameObject sourceContents = PrefabUtility.LoadPrefabContents(TerrainPrefabPath);
            Vector3 sourceTerrainPosition;
            Terrain sourceTerrain;
            try
            {
                sourceTerrain = sourceContents.GetComponent<Terrain>();
                if (sourceTerrain == null || sourceTerrain.terrainData != sourceData)
                    throw new InvalidOperationException("The existing terrain prefab does not reference the expected TerrainData.");
                sourceTerrainPosition = sourceContents.transform.position;
            }
            finally { PrefabUtility.UnloadPrefabContents(sourceContents); }

            generatedData = UnityEngine.Object.Instantiate(sourceData);
            generatedData.name = "YenTu_StaticEnvironment_TerrainData";
            float[,] sourceHeights = sourceData.GetHeights(0, 0, sourceData.heightmapResolution, sourceData.heightmapResolution);
            float sourceMinY = float.PositiveInfinity;
            float sourceMaxY = float.NegativeInfinity;
            for (int z = 0; z < sourceHeights.GetLength(0); z++)
            for (int x = 0; x < sourceHeights.GetLength(1); x++)
            {
                float y = sourceTerrainPosition.y + sourceHeights[z, x] * sourceData.size.y;
                sourceMinY = Mathf.Min(sourceMinY, y);
                sourceMaxY = Mathf.Max(sourceMaxY, y);
            }
            float terrainBaseY = Mathf.Min(sourceMinY, valleyFloorY) - 2f;
            float terrainTopY = Mathf.Max(sourceMaxY, trackFloor + 10f) + 2f;
            float terrainHeight = Mathf.Max(100f, terrainTopY - terrainBaseY);
            generatedData.heightmapResolution = TerrainResolution;
            generatedData.size = new Vector3(sourceData.size.x, terrainHeight, sourceData.size.z);

            float[,] workingHeights = ResampleHeights(sourceHeights, sourceData.heightmapResolution, TerrainResolution,
                sourceTerrainPosition.y, sourceData.size.y, terrainBaseY, terrainHeight);
            ApplyRailCorridorAndBridgeValley(workingHeights, generatedData.size, sourceTerrainPosition, terrainBaseY,
                terrainHeight, route, table, bridgeMiddle, trackFloor, gradedGroundY, waterY, valleyFloorY);
            generatedData.SetHeights(0, 0, workingHeights);
            generatedData.terrainLayers = new[] { grass, soil };
            PaintTerrainLayers(generatedData, workingHeights);
            AssetDatabase.CreateAsset(generatedData, terrainAssetPath);
            createdAssets.Add(terrainAssetPath);

            Transform terrainGroup = CreateGroup(root.transform, "Terrain");
            GameObject terrainObject = PrefabUtility.InstantiatePrefab(terrainPrefab, scene) as GameObject;
            if (terrainObject == null) throw new InvalidOperationException("Could not instantiate the existing terrain prefab.");
            terrainObject.name = "YenTu_Region_Terrain_EditableCopy";
            terrainObject.transform.SetParent(terrainGroup, true);
            terrainObject.transform.position = new Vector3(sourceTerrainPosition.x, terrainBaseY, sourceTerrainPosition.z);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            TerrainCollider terrainCollider = terrainObject.GetComponent<TerrainCollider>();
            terrain.terrainData = generatedData;
            if (terrainCollider != null) terrainCollider.terrainData = generatedData;
            terrain.drawInstanced = true;
            terrain.drawTreesAndFoliage = false;
            terrain.treeDistance = 0f;
            terrain.detailObjectDistance = 0f;
            terrain.heightmapPixelError = 14f;
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain.transform);

            Transform transitions = CreateGroup(root.transform, "Mesh_Transitions_100m_Chunks");
            Transform roads = CreateGroup(root.transform, "Rural_Roads_100m_Chunks");
            Transform vegetation = CreateGroup(root.transform, "Baked_Vegetation_100m_Chunks");
            Transform settlements = CreateGroup(root.transform, "Existing_Houses_Deterministic_Placement");
            Transform water = CreateGroup(root.transform, "River_Valley_At_Existing_Bridge");

            CreateRiver(route, table, bridgeMiddle, water, waterMaterial, waterY, createdAssets);
            int chunks = Mathf.CeilToInt(table.Length / ChunkLength);
            int treeCount = 0;
            int roadSegments = 0;
            for (int index = 0; index < chunks; index++)
            {
                float start = index * ChunkLength;
                float end = Mathf.Min(table.Length, start + ChunkLength);
                Sample center = Evaluate(route, table, (start + end) * 0.5f);
                Vector3 origin = center.Position + center.Right * TrackCenterOffset;
                BuildChunkMeshes(index, start, end, route, table, terrain, origin, transitions, roads, vegetation,
                    grassMaterial, roadMaterial, trunkMaterial, canopyMaterial, createdAssets, ref treeCount, ref roadSegments,
                    bridgeMiddle, bridgeLength);
                if ((index + 1) % 8 == 0 || index + 1 == chunks)
                    EditorUtility.DisplayProgressBar("Terrain and Environment MVP", $"Baking fixed 100 m environment chunks ({index + 1}/{chunks})", (index + 1f) / chunks);
            }

            int houseCount = PlaceHouses(route, table, terrain, houses, settlements, bridgeMiddle, bridgeLength);
            DisableReplacedStreamers(scene);
            WriteManifest(table.Length, TerrainResolution, chunks, treeCount, roadSegments, houseCount, bridgeStart, bridgeLength,
                trackFloor, gradedGroundY, waterY, terrainBaseY, terrainTopY);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
            EditorUtility.ClearProgressBar();
            success = true;
            Debug.Log($"Terrain and Environment MVP generated: {table.Length:F1}m route, {chunks} editable 100m chunks, " +
                      $"{TerrainResolution} heightmap, {treeCount} baked trees, {roadSegments} road segments, {houseCount} houses; " +
                      $"existing bridge {bridgeStart:F0}-{bridgeStart + bridgeLength:F0}m, rail floor {trackFloor:F2}m, " +
                      $"ground {gradedGroundY:F2}m, river {waterY:F2}m. No rail or train was changed.");
        }
        catch
        {
            EditorUtility.ClearProgressBar();
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            foreach (string asset in createdAssets.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(asset);
            if (generatedData != null && !createdAssets.Contains(terrainAssetPath)) UnityEngine.Object.DestroyImmediate(generatedData);
            AssetDatabase.Refresh();
            throw;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            if (success) AssetDatabase.Refresh();
        }
    }

    [MenuItem("Tools/Static Railway Map/Inspect Environment MVP Inputs")]
    private static void InspectInputs()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh before inspecting the environment inputs.");
        GameObject contents = PrefabUtility.LoadPrefabContents(TerrainPrefabPath);
        try
        {
            Terrain terrain = contents.GetComponent<Terrain>();
            TerrainData data = terrain.terrainData;
            float[,] heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
            float min = 1f, max = 0f;
            for (int z = 0; z < heights.GetLength(0); z += 4)
            for (int x = 0; x < heights.GetLength(1); x += 4)
            { min = Mathf.Min(min, heights[z, x]); max = Mathf.Max(max, heights[z, x]); }
            Transform activeRoot = FindPath(scene, RailwayPath);
            Bounds trackBounds = GetTrackBounds(activeRoot);
            string bridgeName = activeRoot.Find("Bridge_Main").Cast<Transform>().Select(t => t.name).FirstOrDefault(n => n.Contains("m_to_"));
            Debug.Log($"Terrain MVP inputs: data={data.name}, size={data.size}, heightmap={data.heightmapResolution}, " +
                      $"worldPosition={contents.transform.position}, layers={data.terrainLayers.Length}, " +
                      $"heightRangeWorld=[{contents.transform.position.y + min * data.size.y:F2}, " +
                      $"{contents.transform.position.y + max * data.size.y:F2}], trackWorldBounds={trackBounds}, activeBridge={bridgeName}.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    [MenuItem("Tools/Static Railway Map/Preview Terrain MVP From Track")]
    private static void PreviewFromTrack()
    {
        Scene scene = SceneManager.GetActiveScene();
        SplineContainer route = FindRoute(scene);
        if (scene.path != ScenePath || route == null) throw new InvalidOperationException("Open YenTuHaLongXanh before previewing its terrain.");
        PathTable table = BuildPathTable(route);
        Sample sample = Evaluate(route, table, Mathf.Min(900f, table.Length * 0.5f));
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) throw new InvalidOperationException("A Scene View must be open to preview the terrain.");
        Quaternion rotation = Quaternion.LookRotation((sample.Tangent - Vector3.up * 0.06f).normalized, Vector3.up);
        view.LookAt(sample.Position + Vector3.up * 2.5f + sample.Tangent * 22f, rotation, 22f);
        Debug.Log("Terrain MVP Scene View framed at rail height near chainage 900m.");
    }

    [MenuItem("Tools/Static Railway Map/Validate Terrain Environment MVP")]
    private static void ValidateEnvironment()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh before validating its terrain environment.");
        SplineContainer route = FindRoute(scene);
        Transform environment = FindPath(scene, "RailwayStaticMap/Editable/Generated/" + SceneRootName);
        Transform staticRoot = FindPath(scene, RailwayPath);
        if (route == null || environment == null || staticRoot == null) throw new InvalidOperationException("The route, environment, or active static railway is missing.");
        Terrain terrain = environment.GetComponentInChildren<Terrain>(true);
        if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("The generated Terrain or its TerrainData is missing.");
        PathTable table = BuildPathTable(route);
        float railFloor = GetTrackBounds(staticRoot).min.y;
        float minClearance = float.PositiveInfinity;
        float maxTerrainUnderRail = float.NegativeInfinity;
        int sampleCount = 0, possibleIntersections = 0;
        for (float d = 0f; d <= table.Length; d += 10f)
        {
            Sample sample = Evaluate(route, table, d);
            for (int offset = -1; offset <= 1; offset++)
            {
                Vector3 point = sample.Position + sample.Right * (TrackCenterOffset + offset * 3.2f);
                float ground = TerrainHeight(terrain, point);
                float clearance = railFloor - ground;
                minClearance = Mathf.Min(minClearance, clearance);
                maxTerrainUnderRail = Mathf.Max(maxTerrainUnderRail, ground);
                if (ground >= railFloor - 0.1f) possibleIntersections++;
                sampleCount++;
            }
        }

        int missingComponents = environment.GetComponentsInChildren<Component>(true).Count(component => component == null);
        int missingMeshes = environment.GetComponentsInChildren<MeshFilter>(true).Count(filter => filter.sharedMesh == null);
        int missingMaterials = environment.GetComponentsInChildren<MeshRenderer>(true).Count(renderer => renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null));
        var manifest = ReadManifest();
        float[,,] firstAlpha = terrain.terrainData.GetAlphamaps(0, 0, 1, 1);
        TerrainLayer[] terrainLayers = terrain.terrainData.terrainLayers;
        string layerInfo = string.Join("; ", terrainLayers.Select(layer => $"{layer.name}:{(layer.diffuseTexture != null ? layer.diffuseTexture.name : "NO_TEXTURE")}"));
        Debug.Log($"Terrain Environment MVP validation: {table.Length:F1}m route, {sampleCount} rail-area height samples, " +
                  $"minimum clearance={minClearance:F3}m, highest terrain under rail={maxTerrainUnderRail:F3}m, " +
                  $"possible terrain/rail intersections={possibleIntersections}, missing components={missingComponents}, " +
                  $"missing meshes={missingMeshes}, missing materials={missingMaterials}, terrain heightmap={terrain.terrainData.heightmapResolution}, " +
                  $"terrainLayers={layerInfo}, alpha00=({firstAlpha[0, 0, 0]:F2},{firstAlpha[0, 0, 1]:F2}), " +
                  $"bridge={manifest.bridgeStart:F0}-{manifest.bridgeStart + manifest.bridgeLength:F0}m, " +
                  $"dynamicTrack={IsEnabled(scene, "/Rail_Spline_Route", "DynamicTrackStreamer")}, " +
                  $"scenery={IsEnabled(scene, "/Scenery_Manager", "SceneryStreamer")}, " +
                  $"tunnels={IsEnabled(scene, "/Tunnel_Manager", "TunnelStreamer")}, " +
                  $"stations={IsEnabled(scene, "/Station_Manager", "StationStreamer")}.");
    }

    [MenuItem("Tools/Static Railway Map/Repair Terrain Surface Texture")]
    private static void RepairTerrainSurfaceTexture()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new InvalidOperationException("Open YenTuHaLongXanh before updating its terrain surface.");
        Transform environment = FindPath(scene, "RailwayStaticMap/Editable/Generated/" + SceneRootName);
        if (environment == null) throw new InvalidOperationException("TerrainEnvironment_MVP was not found.");
        Terrain terrain = environment.GetComponentInChildren<Terrain>(true);
        TerrainLayer grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(OutputPath + "/Layers/YenTu_Grass.terrainlayer");
        TerrainLayer soil = AssetDatabase.LoadAssetAtPath<TerrainLayer>(OutputPath + "/Layers/YenTu_ExposedSoil.terrainlayer");
        Texture2D satellite = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainSatelliteTexturePath);
        Material transitionMaterial = AssetDatabase.LoadAssetAtPath<Material>(OutputPath + "/Materials/Environment_Grass.mat");
        if (terrain == null || grass == null || soil == null || satellite == null || transitionMaterial == null)
            throw new InvalidOperationException("The generated Terrain, texture, or surface materials are missing.");

        Undo.RecordObject(terrain, "Repair Terrain Surface Texture");
        Undo.RecordObject(terrain.terrainData, "Repair Terrain Surface Texture");
        grass.diffuseTexture = satellite;
        grass.normalMapTexture = null;
        grass.tileSize = new Vector2(terrain.terrainData.size.x, terrain.terrainData.size.z);
        EditorUtility.SetDirty(grass);
        terrain.terrainData.terrainLayers = new[] { grass, soil };
        EditorUtility.SetDirty(terrain.terrainData);

        Undo.RecordObject(transitionMaterial, "Repair Terrain Surface Texture");
        if (transitionMaterial.HasProperty("_BaseMap")) transitionMaterial.SetTexture("_BaseMap", satellite);
        if (transitionMaterial.HasProperty("_MainTex")) transitionMaterial.SetTexture("_MainTex", satellite);
        if (transitionMaterial.HasProperty("_BaseColor")) transitionMaterial.SetColor("_BaseColor", Color.white);
        if (transitionMaterial.HasProperty("_Color")) transitionMaterial.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(transitionMaterial);

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        Transform transitions = environment.Find("Mesh_Transitions_100m_Chunks");
        int updatedMeshes = 0;
        if (transitions != null)
        foreach (MeshFilter filter in transitions.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null) continue;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = filter.transform.TransformPoint(vertices[i]);
                uvs[i] = new Vector2((world.x - origin.x) / size.x, (world.z - origin.z) / size.z);
            }
            mesh.uv = uvs;
            EditorUtility.SetDirty(mesh);
            updatedMeshes++;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Terrain surface texture repaired with the existing YenTu-HaLong satellite image; {updatedMeshes} transition meshes now align to the full terrain map.");
    }

    private static bool IsEnabled(Scene scene, string path, string type)
    {
        GameObject go = scene.GetRootGameObjects().FirstOrDefault(x => "/" + x.name == path);
        return go != null && go.GetComponent(type) is Behaviour behaviour && behaviour.enabled;
    }

    private static void ApplyRailCorridorAndBridgeValley(float[,] heights, Vector3 terrainSize, Vector3 terrainOrigin,
        float terrainBaseY, float terrainHeight, SplineContainer route, PathTable table, float bridgeMiddle,
        float trackFloor, float groundY, float waterY, float valleyFloorY)
    {
        int res = heights.GetLength(0);
        float cellX = terrainSize.x / (res - 1f);
        float cellZ = terrainSize.z / (res - 1f);
        var nearest = new float[res, res];
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++) nearest[z, x] = float.PositiveInfinity;

        const float paintRadius = 270f;
        for (float d = 0f; d <= table.Length; d += 10f)
        {
            Sample sample = Evaluate(route, table, d);
            int cx = Mathf.RoundToInt((sample.Position.x - terrainOrigin.x) / cellX);
            int cz = Mathf.RoundToInt((sample.Position.z - terrainOrigin.z) / cellZ);
            int rx = Mathf.CeilToInt(paintRadius / cellX);
            int rz = Mathf.CeilToInt(paintRadius / cellZ);
            for (int z = Mathf.Max(0, cz - rz); z <= Mathf.Min(res - 1, cz + rz); z++)
            for (int x = Mathf.Max(0, cx - rx); x <= Mathf.Min(res - 1, cx + rx); x++)
            {
                float dx = terrainOrigin.x + x * cellX - sample.Position.x;
                float dz = terrainOrigin.z + z * cellZ - sample.Position.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance < nearest[z, x]) nearest[z, x] = distance;
            }
        }

        Sample bridge = Evaluate(route, table, bridgeMiddle);
        Vector3 right = bridge.Right;
        Vector3 tangent = bridge.Tangent;
        for (int z = 0; z < res; z++)
        {
            float worldZ = terrainOrigin.z + z * cellZ;
            for (int x = 0; x < res; x++)
            {
                float worldX = terrainOrigin.x + x * cellX;
                float originalY = terrainBaseY + heights[z, x] * terrainHeight;
                float d = nearest[z, x];
                float adjustedY = originalY;
                if (d <= paintRadius)
                {
                    float blend = Smooth01(Mathf.InverseLerp(9f, paintRadius, d));
                    adjustedY = Mathf.Lerp(groundY, originalY, blend);
                }

                Vector3 delta = new Vector3(worldX - bridge.Position.x, 0f, worldZ - bridge.Position.z);
                float along = Mathf.Abs(Vector3.Dot(delta, tangent));
                float across = Mathf.Abs(Vector3.Dot(delta, right));
                float valleyWeight = (1f - Smooth01(Mathf.InverseLerp(155f, 390f, along))) *
                                     (1f - Smooth01(Mathf.InverseLerp(560f, 760f, across)));
                adjustedY = Mathf.Lerp(adjustedY, valleyFloorY, valleyWeight);
                heights[z, x] = Mathf.Clamp01((adjustedY - terrainBaseY) / terrainHeight);
            }
            if (z % 64 == 0)
                EditorUtility.DisplayProgressBar("Terrain and Environment MVP", $"Shaping 2049 heightmap ({z}/{res})", 0.05f + 0.22f * z / res);
        }
    }

    private static float[,] ResampleHeights(float[,] source, int sourceRes, int targetRes, float sourceBaseY,
        float sourceHeight, float targetBaseY, float targetHeight)
    {
        float[,] result = new float[targetRes, targetRes];
        float scale = (sourceRes - 1f) / (targetRes - 1f);
        for (int z = 0; z < targetRes; z++)
        {
            float sourceZ = z * scale;
            int z0 = Mathf.FloorToInt(sourceZ), z1 = Mathf.Min(sourceRes - 1, z0 + 1);
            float fz = sourceZ - z0;
            for (int x = 0; x < targetRes; x++)
            {
                float sourceX = x * scale;
                int x0 = Mathf.FloorToInt(sourceX), x1 = Mathf.Min(sourceRes - 1, x0 + 1);
                float fx = sourceX - x0;
                float h0 = Mathf.Lerp(source[z0, x0], source[z0, x1], fx);
                float h1 = Mathf.Lerp(source[z1, x0], source[z1, x1], fx);
                float worldY = sourceBaseY + Mathf.Lerp(h0, h1, fz) * sourceHeight;
                result[z, x] = Mathf.Clamp01((worldY - targetBaseY) / targetHeight);
            }
        }
        return result;
    }

    private static void PaintTerrainLayers(TerrainData data, float[,] heights)
    {
        data.alphamapResolution = TerrainAlphaResolution;
        var alpha = new float[TerrainAlphaResolution, TerrainAlphaResolution, 2];
        int heightRes = heights.GetLength(0);
        float cellX = data.size.x / (heightRes - 1f);
        float cellZ = data.size.z / (heightRes - 1f);
        for (int z = 0; z < TerrainAlphaResolution; z++)
        {
            float hz = z * (heightRes - 1f) / (TerrainAlphaResolution - 1f);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(hz), 0, heightRes - 1);
            int z1 = Mathf.Min(z0 + 1, heightRes - 1);
            for (int x = 0; x < TerrainAlphaResolution; x++)
            {
                float hx = x * (heightRes - 1f) / (TerrainAlphaResolution - 1f);
                int x0 = Mathf.Clamp(Mathf.FloorToInt(hx), 0, heightRes - 1);
                int x1 = Mathf.Min(x0 + 1, heightRes - 1);
                float center = heights[z0, x0];
                float dx = (heights[z0, x1] - heights[z0, x0]) * data.size.y / Mathf.Max(0.01f, cellX * (x1 - x0));
                float dz = (heights[z1, x0] - heights[z0, x0]) * data.size.y / Mathf.Max(0.01f, cellZ * (z1 - z0));
                if (x0 == x1) dx = 0f;
                if (z0 == z1) dz = 0f;
                float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
                float soil = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24f, 43f, slope));
                if (center < 0.08f) soil = Mathf.Max(soil, 0.22f);
                alpha[z, x, 0] = 1f - soil;
                alpha[z, x, 1] = soil;
            }
            if (z % 64 == 0)
                EditorUtility.DisplayProgressBar("Terrain and Environment MVP", $"Painting grass and exposed slopes ({z}/{TerrainAlphaResolution})", 0.29f + 0.08f * z / TerrainAlphaResolution);
        }
        data.SetAlphamaps(0, 0, alpha);
    }

    private static void BuildChunkMeshes(int chunkIndex, float start, float end, SplineContainer route, PathTable table,
        Terrain terrain, Vector3 origin, Transform transitionParent, Transform roadParent, Transform vegetationParent,
        Material grassMaterial, Material roadMaterial, Material trunkMaterial, Material canopyMaterial,
        List<string> createdAssets, ref int treeCount, ref int roadSegments, float bridgeMiddle, float bridgeLength)
    {
        var vertices = new List<Vector3>(1400);
        var uvs = new List<Vector2>(1400);
        var transitionTriangles = new List<int>(2500);
        var roadTriangles = new List<int>(256);
        var trunkTriangles = new List<int>(1800);
        var canopyTriangles = new List<int>(9000);
        const int rows = 17;
        const int lateralSegments = 18;
        bool bridgeChunk = end >= bridgeMiddle - bridgeLength * 0.5f - 370f && start <= bridgeMiddle + bridgeLength * 0.5f + 370f;
        if (!bridgeChunk)
        for (int side = -1; side <= 1; side += 2)
        {
            int first = vertices.Count;
            for (int row = 0; row < rows; row++)
            {
                float distance = Mathf.Lerp(start, end, row / (float)(rows - 1));
                Sample sample = Evaluate(route, table, distance);
                for (int col = 0; col <= lateralSegments; col++)
                {
                    float lateral = Mathf.Lerp(9f, 225f, col / (float)lateralSegments);
                    Vector3 world = sample.Position + sample.Right * (TrackCenterOffset + side * lateral);
                    world.y = TerrainHeight(terrain, world) + 0.035f;
                    vertices.Add(world - origin);
                    Vector3 terrainOrigin = terrain.transform.position;
                    Vector3 terrainSize = terrain.terrainData.size;
                    uvs.Add(new Vector2((world.x - terrainOrigin.x) / terrainSize.x,
                        (world.z - terrainOrigin.z) / terrainSize.z));
                }
            }
            AddGridTriangles(transitionTriangles, first, rows, lateralSegments + 1, side > 0);
        }

        if (!bridgeChunk)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int first = vertices.Count;
                for (int row = 0; row < rows; row++)
                {
                    float distance = Mathf.Lerp(start, end, row / (float)(rows - 1));
                    Sample sample = Evaluate(route, table, distance);
                    Vector3 center = sample.Position + sample.Right * (TrackCenterOffset + side * 145f);
                    Vector3 left = center - sample.Right * 3f;
                    Vector3 right = center + sample.Right * 3f;
                    left.y = TerrainHeight(terrain, left) + 0.07f;
                    right.y = TerrainHeight(terrain, right) + 0.07f;
                    vertices.Add(left - origin); uvs.Add(new Vector2(0f, distance / 18f));
                    vertices.Add(right - origin); uvs.Add(new Vector2(1f, distance / 18f));
                }
                AddGridTriangles(roadTriangles, first, rows, 2, true);
                roadSegments++;
            }
        }

        var random = new System.Random(1739 + chunkIndex * 7919);
        if (!bridgeChunk)
        {
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 8; i++)
            {
                float chainage = Mathf.Lerp(start + 4f, end - 4f, (i + 0.5f) / 8f) + (float)(random.NextDouble() - 0.5) * 8f;
                if (chainage < 0f || chainage > table.Length) continue;
                Sample sample = Evaluate(route, table, chainage);
                float lateral = 48f + (float)random.NextDouble() * 168f;
                if (Mathf.Abs(lateral - 145f) < 12f) lateral += 24f;
                Vector3 world = sample.Position + sample.Right * (TrackCenterOffset + side * lateral);
                world.y = TerrainHeight(terrain, world) + 0.02f;
                float height = Mathf.Lerp(7.5f, 14.5f, (float)random.NextDouble());
                float radius = height * Mathf.Lerp(0.18f, 0.28f, (float)random.NextDouble());
                AddTree(vertices, uvs, trunkTriangles, canopyTriangles, world - origin, height, radius, (float)random.NextDouble());
                treeCount++;
            }
        }

        Mesh mesh = new Mesh { name = $"Environment_Chunk_{chunkIndex:D4}_Source" };
        if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.subMeshCount = 4;
        mesh.SetTriangles(transitionTriangles, 0, true); mesh.SetTriangles(roadTriangles, 1, true);
        mesh.SetTriangles(trunkTriangles, 2, true); mesh.SetTriangles(canopyTriangles, 3, true);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();

        Mesh transitionMesh = CreateSubmeshView(mesh, 0, $"TerrainTransition_Chunk_{chunkIndex:D4}");
        SaveMesh(transitionMesh, OutputPath + $"/Meshes/TerrainTransition_Chunk_{chunkIndex:D4}.asset", createdAssets);
        GameObject transition = new GameObject($"Chunk_{chunkIndex:D4}_{start:F0}m_to_{end:F0}m");
        transition.transform.SetParent(transitionParent, false); transition.transform.position = origin;
        transition.AddComponent<MeshFilter>().sharedMesh = transitionMesh;
        transition.AddComponent<MeshRenderer>().sharedMaterial = grassMaterial;
        transition.isStatic = true;

        Mesh roadMesh = CreateSubmeshView(mesh, 1, $"RuralRoad_Chunk_{chunkIndex:D4}");
        SaveMesh(roadMesh, OutputPath + $"/Meshes/RuralRoad_Chunk_{chunkIndex:D4}.asset", createdAssets);
        GameObject roadGroup = new GameObject($"RoadChunk_{chunkIndex:D4}");
        roadGroup.transform.SetParent(roadParent, false); roadGroup.transform.position = origin;
        roadGroup.AddComponent<MeshFilter>().sharedMesh = roadMesh;
        roadGroup.AddComponent<MeshRenderer>().sharedMaterial = roadMaterial;
        roadGroup.isStatic = true;

        Mesh vegetationMesh = CreateVegetationView(mesh, $"Vegetation_Chunk_{chunkIndex:D4}");
        SaveMesh(vegetationMesh, OutputPath + $"/Meshes/Vegetation_Chunk_{chunkIndex:D4}.asset", createdAssets);
        GameObject vegetationChunk = new GameObject($"VegetationChunk_{chunkIndex:D4}");
        vegetationChunk.transform.SetParent(vegetationParent, false); vegetationChunk.transform.position = origin;
        vegetationChunk.AddComponent<MeshFilter>().sharedMesh = vegetationMesh;
        vegetationChunk.AddComponent<MeshRenderer>().sharedMaterials = new[] { trunkMaterial, canopyMaterial };
        vegetationChunk.isStatic = true;
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.DisplayProgressBar("Terrain and Environment MVP", $"Creating scene chunk {chunkIndex + 1}/{Mathf.CeilToInt(table.Length / ChunkLength)}", 0.37f + 0.58f * ((chunkIndex + 1f) / Mathf.CeilToInt(table.Length / ChunkLength)));
    }

    private static Mesh CreateSubmeshView(Mesh source, int submesh, string name)
    {
        var mesh = new Mesh { name = name };
        if (source.vertexCount > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = source.vertices;
        mesh.uv = source.uv;
        mesh.subMeshCount = 1;
        mesh.SetTriangles(source.GetTriangles(submesh), 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateVegetationView(Mesh source, string name)
    {
        var mesh = new Mesh { name = name };
        if (source.vertexCount > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = source.vertices;
        mesh.uv = source.uv;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(source.GetTriangles(2), 0, true);
        mesh.SetTriangles(source.GetTriangles(3), 1, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddGridTriangles(List<int> triangles, int start, int rows, int columns, bool upward)
    {
        for (int row = 0; row < rows - 1; row++)
        for (int col = 0; col < columns - 1; col++)
        {
            int a = start + row * columns + col;
            int b = a + columns;
            int c = a + 1;
            int d = b + 1;
            if (upward) { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(c); triangles.Add(b); triangles.Add(d); }
            else { triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(c); triangles.Add(d); triangles.Add(b); }
        }
    }

    private static void AddTree(List<Vector3> vertices, List<Vector2> uv, List<int> trunkTriangles,
        List<int> canopyTriangles, Vector3 position, float height, float radius, float shape)
    {
        const int sides = 7;
        int first = vertices.Count;
        float trunkHeight = height * 0.38f;
        for (int ring = 0; ring < 2; ring++)
        {
            float y = ring == 0 ? 0f : trunkHeight;
            float r = ring == 0 ? radius * 0.18f : radius * 0.12f;
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                vertices.Add(position + new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r));
                uv.Add(new Vector2(side / (float)sides, ring));
            }
        }
        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            trunkTriangles.Add(first + side); trunkTriangles.Add(first + sides + side); trunkTriangles.Add(first + next);
            trunkTriangles.Add(first + next); trunkTriangles.Add(first + sides + side); trunkTriangles.Add(first + sides + next);
        }

        if (shape < 0.32f)
        {
            AddCone(vertices, uv, canopyTriangles, position + Vector3.up * (height * 0.22f), height * 0.78f, radius, sides);
        }
        else
        {
            AddCone(vertices, uv, canopyTriangles, position + Vector3.up * (height * 0.25f), height * 0.58f, radius, sides);
            AddCone(vertices, uv, canopyTriangles, position + Vector3.up * (height * 0.48f), height * 0.43f, radius * 0.72f, sides);
            AddCone(vertices, uv, canopyTriangles, position + Vector3.up * (height * 0.67f), height * 0.29f, radius * 0.46f, sides);
        }
    }

    private static void AddCone(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Vector3 baseCenter,
        float height, float radius, int sides)
    {
        int start = vertices.Count;
        vertices.Add(baseCenter);
        uv.Add(Vector2.zero);
        for (int side = 0; side < sides; side++)
        {
            float angle = side * Mathf.PI * 2f / sides;
            vertices.Add(baseCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            uv.Add(new Vector2(side / (float)sides, 0f));
        }
        int tip = vertices.Count;
        vertices.Add(baseCenter + Vector3.up * height);
        uv.Add(new Vector2(0.5f, 1f));
        for (int side = 0; side < sides; side++)
        {
            int a = start + 1 + side;
            int b = start + 1 + (side + 1) % sides;
            triangles.Add(start); triangles.Add(a); triangles.Add(b);
            triangles.Add(a); triangles.Add(tip); triangles.Add(b);
        }
    }

    private static int PlaceHouses(SplineContainer route, PathTable table, Terrain terrain, GameObject[] housePrefabs,
        Transform parent, float bridgeMiddle, float bridgeLength)
    {
        int count = 0;
        int group = 0;
        for (float d = 350f; d < table.Length - 350f; d += 820f, group++)
        {
            if (Mathf.Abs(d - bridgeMiddle) < bridgeLength * 0.5f + 900f) continue;
            Sample sample = Evaluate(route, table, d);
            int side = (group % 2 == 0) ? 1 : -1;
            for (int houseIndex = 0; houseIndex < 2; houseIndex++)
            {
                float lateral = 155f + houseIndex * 56f + ((group * 17 + houseIndex * 11) % 19);
                Vector3 position = sample.Position + sample.Right * (TrackCenterOffset + side * lateral);
                position.y = TerrainHeight(terrain, position);
                GameObject prefab = housePrefabs[(group + houseIndex) % housePrefabs.Length];
                GameObject house = PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene) as GameObject;
                if (house == null) continue;
                house.name = $"RuralHouse_{count + 1:D3}_{(int)d:D5}m";
                house.transform.SetParent(parent, true);
                house.transform.SetPositionAndRotation(position, Quaternion.LookRotation(sample.Tangent, Vector3.up));
                Renderer[] renderers = house.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    float lowest = renderers.Min(r => r.bounds.min.y);
                    house.transform.position += Vector3.up * (position.y - lowest);
                }
                house.isStatic = true;
                count++;
            }
        }
        return count;
    }

    private static void CreateRiver(SplineContainer route, PathTable table, float chainage,
        Transform parent, Material material, float waterY, List<string> createdAssets)
    {
        Sample center = Evaluate(route, table, chainage);
        Vector3 origin = center.Position + center.Right * TrackCenterOffset;
        const int steps = 64;
        const float halfLength = 680f;
        const float halfWidth = 38f;
        var vertices = new List<Vector3>((steps + 1) * 2);
        var uvs = new List<Vector2>((steps + 1) * 2);
        var triangles = new List<int>(steps * 6);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float cross = Mathf.Lerp(-halfLength, halfLength, t);
            Vector3 riverCenter = origin + center.Right * cross + center.Tangent * (Mathf.Sin(cross * 0.0045f) * 11f + Mathf.Sin(cross * 0.013f) * 3f);
            Vector3 a = riverCenter - center.Tangent * halfWidth;
            Vector3 b = riverCenter + center.Tangent * halfWidth;
            a.y = waterY; b.y = waterY;
            vertices.Add(a - origin); vertices.Add(b - origin);
            uvs.Add(new Vector2(0f, t * 6f)); uvs.Add(new Vector2(1f, t * 6f));
            if (i == 0) continue;
            int previous = (i - 1) * 2;
            int current = i * 2;
            triangles.Add(previous); triangles.Add(previous + 1); triangles.Add(current);
            triangles.Add(current); triangles.Add(previous + 1); triangles.Add(current + 1);
        }
        Mesh mesh = new Mesh { name = "ExistingBridge_River_And_Water" };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string path = OutputPath + "/Meshes/ExistingBridge_River_And_Water.asset";
        AssetDatabase.CreateAsset(mesh, path); createdAssets.Add(path);
        GameObject river = new GameObject("River_Crossing_Below_Existing_Bridge");
        river.transform.SetParent(parent, false); river.transform.position = origin;
        river.AddComponent<MeshFilter>().sharedMesh = mesh;
        river.AddComponent<MeshRenderer>().sharedMaterial = material;
        river.isStatic = true;
    }

    private static void DisableReplacedStreamers(Scene scene)
    {
        SetBehaviourEnabled(scene, "/Rail_Spline_Route", "DynamicTrackStreamer", false);
        SetBehaviourEnabled(scene, "/Scenery_Manager", "SceneryStreamer", false);
        SetBehaviourEnabled(scene, "/Tunnel_Manager", "TunnelStreamer", false);
        SetBehaviourEnabled(scene, "/Station_Manager", "StationStreamer", false);
    }

    private static void SetBehaviourEnabled(Scene scene, string path, string type, bool enabled)
    {
        GameObject go = scene.GetRootGameObjects().FirstOrDefault(x => "/" + x.name == path);
        if (go == null) return;
        Component component = go.GetComponent(type);
        if (!(component is Behaviour behaviour) || behaviour.enabled == enabled) return;
        Undo.RecordObject(behaviour, $"Disable replaced {type}");
        behaviour.enabled = enabled;
        EditorUtility.SetDirty(behaviour);
    }

    private static RailwayManifest ReadManifest()
    {
        const string path = "Assets/ProjectLopHoc/Generated/StaticRailwayMap/StaticRailwayMapMvp_manifest.json";
        string absolute = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        if (!File.Exists(absolute)) throw new FileNotFoundException("The static railway manifest is required to locate its active bridge.", absolute);
        RailwayManifest manifest = JsonUtility.FromJson<RailwayManifest>(File.ReadAllText(absolute));
        if (manifest == null || manifest.bridgeLength <= 0f) throw new InvalidOperationException("Static railway manifest has no valid bridge span.");
        return manifest;
    }

    private static Bounds GetTrackBounds(Transform staticRoot)
    {
        Transform tracks = staticRoot.Find("Tracks");
        Renderer[] renderers = tracks != null ? tracks.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("The active static railway has no track renderers.");
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private static float GetRendererMinY(Transform root, float fallback)
    {
        if (root == null) return fallback;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        return renderers.Length == 0 ? fallback : renderers.Min(r => r.bounds.min.y);
    }

    private static TerrainLayer CreateLayer(TerrainLayer source, Texture2D texture, string name, Vector2 tileSize,
        string path, List<string> createdAssets)
    {
        TerrainLayer layer = UnityEngine.Object.Instantiate(source);
        layer.name = name;
        if (texture != null) layer.diffuseTexture = texture;
        layer.tileSize = tileSize;
        layer.tileOffset = Vector2.zero;
        layer.metallic = 0f;
        layer.smoothness = 0.05f;
        AssetDatabase.CreateAsset(layer, path);
        createdAssets.Add(path);
        return layer;
    }

    private static Material CreateMaterial(string name, Texture texture, Color color, string path, List<string> createdAssets)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No URP Lit or Standard shader is available.");
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", name.Contains("Water") ? 0.55f : 0.1f);
        if (name.Contains("Water")) material.renderQueue = (int)RenderQueue.Geometry;
        AssetDatabase.CreateAsset(material, path);
        createdAssets.Add(path);
        return material;
    }

    private static void SaveMesh(Mesh mesh, string path, List<string> createdAssets)
    {
        AssetDatabase.CreateAsset(mesh, path);
        createdAssets.Add(path);
    }

    private static Transform CreateGroup(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static float TerrainHeight(Terrain terrain, Vector3 world)
    {
        Vector3 p = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        world.x = Mathf.Clamp(world.x, p.x + 0.5f, p.x + size.x - 0.5f);
        world.z = Mathf.Clamp(world.z, p.z + 0.5f, p.z + size.z - 0.5f);
        return terrain.SampleHeight(world) + p.y;
    }

    private static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    private static void WriteManifest(float routeLength, int heightmap, int chunks, int trees, int roads, int houses,
        float bridgeStart, float bridgeLength, float railFloor, float groundY, float waterY, float terrainMin, float terrainMax)
    {
        string path = OutputPath + "/EnvironmentMvp_manifest.json";
        string absolute = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        string json = "{\n" +
                      $"  \"scene\": \"{ScenePath}\",\n" +
                      "  \"terrainSource\": \"" + TerrainPrefabPath + "\",\n" +
                      $"  \"routeLengthMeters\": {routeLength.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                      $"  \"heightmapResolution\": {heightmap},\n" +
                      $"  \"chunkLengthMeters\": {ChunkLength.ToString(CultureInfo.InvariantCulture)},\n" +
                      $"  \"chunks\": {chunks},\n" +
                      $"  \"treesBaked\": {trees},\n" +
                      $"  \"ruralRoadSegments\": {roads},\n" +
                      $"  \"houses\": {houses},\n" +
                      $"  \"activeBridgeStartMeters\": {bridgeStart.ToString("F2", CultureInfo.InvariantCulture)},\n" +
                      $"  \"activeBridgeLengthMeters\": {bridgeLength.ToString("F2", CultureInfo.InvariantCulture)},\n" +
                      $"  \"railLowestWorldY\": {railFloor.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                      $"  \"gradedGroundWorldY\": {groundY.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                      $"  \"riverSurfaceWorldY\": {waterY.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                      $"  \"terrainWorldYRange\": [{terrainMin.ToString("F3", CultureInfo.InvariantCulture)}, {terrainMax.ToString("F3", CultureInfo.InvariantCulture)}],\n" +
                      "  \"runtimeRandomPlacement\": false\n" + "}\n";
        File.WriteAllText(absolute, json);
        AssetDatabase.ImportAsset(path);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        if (!string.IsNullOrEmpty(parent)) AssetDatabase.CreateFolder(parent, name);
    }

    private static SplineContainer FindRoute(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<SplineContainer>().FirstOrDefault(x => x != null && x.gameObject.scene == scene && x.name == "Rail_Spline_Route");
    }

    private static Transform FindPath(Scene scene, string path)
    {
        Transform current = null;
        foreach (string part in path.Split('/'))
        {
            current = current == null
                ? scene.GetRootGameObjects().Select(go => go.transform).FirstOrDefault(t => t.name == part)
                : current.Find(part);
            if (current == null) return null;
        }
        return current;
    }

    private static PathTable BuildPathTable(SplineContainer route)
    {
        float length = Mathf.Max(route.CalculateLength(), 1f);
        int count = Mathf.Clamp(Mathf.CeilToInt(length * 1.5f), 4096, 150000);
        var table = new PathTable { T = new float[count + 1], Distance = new float[count + 1] };
        Vector3 previous = route.EvaluatePosition(0f);
        for (int i = 0; i <= count; i++)
        {
            float t = i / (float)count;
            Vector3 current = route.EvaluatePosition(t);
            table.T[i] = t;
            if (i > 0) table.Distance[i] = table.Distance[i - 1] + Vector3.Distance(previous, current);
            previous = current;
        }
        table.Length = table.Distance[count];
        return table;
    }

    private static Sample Evaluate(SplineContainer route, PathTable table, float distance)
    {
        distance = Mathf.Clamp(distance, 0f, table.Length);
        int index = Array.BinarySearch(table.Distance, distance);
        if (index < 0) index = ~index;
        index = Mathf.Clamp(index, 1, table.Distance.Length - 1);
        float lower = table.Distance[index - 1], upper = table.Distance[index];
        float fraction = upper > lower ? (distance - lower) / (upper - lower) : 0f;
        float t = Mathf.Lerp(table.T[index - 1], table.T[index], fraction);
        Vector3 tangent = route.transform.TransformDirection(route.EvaluateTangent(t)).normalized;
        tangent.y = 0f;
        if (tangent.sqrMagnitude < 0.000001f) tangent = Vector3.forward;
        return new Sample { Position = route.transform.TransformPoint(route.EvaluatePosition(t)), Tangent = tangent.normalized };
    }
}
