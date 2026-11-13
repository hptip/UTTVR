using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

/// <summary>
/// Editor-only, reversible prototype baker for a short double-track segment.
/// Generated prefab instances are kept under RailwayStaticMap/Editable/Generated.
/// </summary>
public sealed class StaticRailwayMapPrototypeWindow : EditorWindow
{
    private const string ScenePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    private const string RailPrefabPath = "Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab";
    private const float DefaultSpacing = 4.5f;
    private const float EndpointMargin = 100f;

    private enum TrackSide { LocalRight, LocalLeft }

    [SerializeField] private SplineContainer route;
    [SerializeField] private GameObject railPrefab;
    [SerializeField] private float startDistance = 100f;
    [SerializeField] private float segmentLength = 100f;
    // Intentionally unset: the prefab includes several overlapping sub-meshes, so its
    // overall bounds are not sufficient evidence for the repeat interval.
    [SerializeField] private float placementStep;
    [SerializeField] private float effectiveRepeatLength;
    [SerializeField] private bool effectiveRepeatLengthVerified;
    [SerializeField] private float diagnosticLaneSpacing = 40f;
    [SerializeField] private float customPreviewStep;
    [SerializeField] private bool inspectPrefabGeometry;
    [SerializeField] private int windowStateVersion;
    [SerializeField] private float trackCenterSpacing = DefaultSpacing;
    [SerializeField] private TrackSide trackBSide = TrackSide.LocalRight;
    [SerializeField] private float trackALateralOffset;
    [SerializeField] private float trackBLateralOffset;
    [SerializeField] private float verticalOffset;
    [SerializeField] private float yawOffset;
    [SerializeField] private float provisionalClearance = 1f;
    private Vector2 scrollPosition;

    private RouteLut lut;
    private AssetMeasurement trainMeasurement;
    private AssetMeasurement railMeasurement;
    private bool showRailMeshDetails;
    private bool previewEnabled;
    private float bestSegmentEnd;
    private float bestSegmentCurvature;
    private float bestSegmentGradeChange;
    private string status = "Ready. No Scene objects have been generated yet.";
    private MessageType statusType = MessageType.Info;

    [MenuItem("Tools/Static Railway Map/Prototype Generator")]
    private static void Open()
    {
        var window = GetWindow<StaticRailwayMapPrototypeWindow>();
        window.titleContent = new GUIContent("Railway Prototype");
        window.minSize = new Vector2(440f, 620f);
        window.TryAutoFind();
        window.Show();
    }

    [MenuItem("Tools/Static Railway Map/Create Visual A-B-C Preview (C = girder-center candidate 6.182 m)")]
    private static void CreateVisualPreviewCandidate()
    {
        StaticRailwayMapPrototypeWindow window = GetWindow<StaticRailwayMapPrototypeWindow>();
        window.TryAutoFind();
        window.MeasureAssets();
        if (window.lut == null) window.RebuildLut();
        if (window.customPreviewStep <= 0.01f) window.customPreviewStep = 6.182f;
        window.CreateDiagnosticPreview();
        window.Show();
    }

    private void OnEnable()
    {
        // The previous tool stored 3 m / bounds-derived steps as though they were
        // authoritative. Clear those persisted values once so the user must enter a
        // measured repeat before generating a replacement segment.
        if (windowStateVersion < 1)
        {
            placementStep = 0f;
            effectiveRepeatLength = 0f;
            effectiveRepeatLengthVerified = false;
            windowStateVersion = 1;
        }
        TryAutoFind();
        SceneView.duringSceneGui += DrawPreview;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DrawPreview;
    }

    private void OnGUI()
    {
        if (windowStateVersion < 2)
        {
            placementStep = 0f;
            effectiveRepeatLength = 0f;
            effectiveRepeatLengthVerified = false;
            windowStateVersion = 2;
        }
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.LabelField("Static Railway Map — 100 m Double-Track Prototype", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Editor-only tool. It does not run in Play Mode and does not edit the source spline, train, or existing streamers.", MessageType.Info);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Route Settings", EditorStyles.boldLabel);
            route = (SplineContainer)EditorGUILayout.ObjectField("Spline Container", route, typeof(SplineContainer), true);
            railPrefab = (GameObject)EditorGUILayout.ObjectField("Rail Prefab", railPrefab, typeof(GameObject), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Auto Find")) TryAutoFind();
                if (GUILayout.Button("Rebuild Route LUT")) RebuildLut();
            }
            if (lut != null)
                EditorGUILayout.LabelField("Route length (world units)", lut.Length.ToString("N2", CultureInfo.InvariantCulture));

            startDistance = EditorGUILayout.FloatField("Start Distance", startDistance);
            segmentLength = Mathf.Max(1f, EditorGUILayout.FloatField("Segment Length", segmentLength));
            placementStep = Mathf.Max(0f, EditorGUILayout.FloatField("Placement Step", placementStep));
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Double Track Settings", EditorStyles.boldLabel);
            trackCenterSpacing = Mathf.Max(0f, EditorGUILayout.FloatField("Track Center Spacing", trackCenterSpacing));
            trackBSide = (TrackSide)EditorGUILayout.EnumPopup("Track B Side", trackBSide);
            trackALateralOffset = EditorGUILayout.FloatField("Track A Lateral Offset", trackALateralOffset);
            trackBLateralOffset = EditorGUILayout.FloatField("Track B Lateral Offset", trackBLateralOffset);
            verticalOffset = EditorGUILayout.FloatField("Vertical Offset (along spline up)", verticalOffset);
            yawOffset = EditorGUILayout.FloatField("Yaw Offset", yawOffset);
            provisionalClearance = Mathf.Max(0f, EditorGUILayout.FloatField("Provisional clearance input", provisionalClearance));
            EditorGUILayout.HelpBox("4.5 units is the requested prototype starting value, not a verified safe spacing. Any recommendation remains provisional until the rail module and train orientation are visually confirmed.", MessageType.Warning);
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Test Segment", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Find Best Test Segment")) FindBestTestSegment();
                if (GUILayout.Button(previewEnabled ? "Hide Preview" : "Preview Route")) TogglePreview();
            }
            if (lut != null && segmentLength > 0f)
            {
                float end = Mathf.Min(startDistance + segmentLength, lut.Length);
                EditorGUILayout.LabelField("Start / End", $"{startDistance:F2} / {end:F2}");
                EditorGUILayout.LabelField("Available segment length", $"{Mathf.Max(0f, end - startDistance):F2}");
                if (bestSegmentEnd > 0f)
                {
                    EditorGUILayout.LabelField("Best scan curvature (max tangent change)", $"{bestSegmentCurvature:F4}° per 5 m sample");
                    EditorGUILayout.LabelField("Best scan grade change", $"{bestSegmentGradeChange:F4}° per 5 m sample");
                }
                if (startDistance < EndpointMargin || end > lut.Length - EndpointMargin)
                    EditorGUILayout.HelpBox("The current test segment is close to a route endpoint.", MessageType.Warning);
            }
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Asset Geometry", EditorStyles.boldLabel);
            if (GUILayout.Button("Inspect Rail Prefab (Prefab Mode)")) OpenRailPrefabForInspection();
            if (GUILayout.Button("Measure Assets")) MeasureAssets();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Prefab Top View")) FramePrefabGeometry(true);
                if (GUILayout.Button("Prefab Side View")) FramePrefabGeometry(false);
            }
            DrawMeasurement("Train_THSR_700T", trainMeasurement);
            DrawMeasurement("EBS_Rail_2000_Prefab", railMeasurement);
            if (railMeasurement != null && railMeasurement.Renderers.Count > 0)
            {
                inspectPrefabGeometry = EditorGUILayout.Toggle("Inspect Prefab Geometry (Scene View)", inspectPrefabGeometry);
                showRailMeshDetails = EditorGUILayout.Foldout(showRailMeshDetails, "Per-mesh geometry and child transforms", true);
                if (showRailMeshDetails)
                {
                    foreach (RendererMeasurement rendererMeasurement in railMeasurement.Renderers)
                    {
                        bool notable = rendererMeasurement.RootBounds.size.z >= 3f ||
                                       rendererMeasurement.Name == "Mesh_11" || rendererMeasurement.Name == "Mesh_12";
                        EditorGUILayout.LabelField(notable ? $"★ {rendererMeasurement.Name}" : rendererMeasurement.Name,
                            notable ? EditorStyles.boldLabel : EditorStyles.miniBoldLabel);
                        EditorGUILayout.LabelField($"Child local P {rendererMeasurement.LocalPosition:F3}  R {rendererMeasurement.LocalRotation:F1}  S {rendererMeasurement.LocalScale:F3}");
                        EditorGUILayout.LabelField($"Root bounds center {rendererMeasurement.RootBounds.center:F3}  size {rendererMeasurement.RootBounds.size:F3}");
                        EditorGUILayout.LabelField($"Mesh local Z {rendererMeasurement.MeshLocalZLength:F3} m  | root-space Z span {rendererMeasurement.RootBounds.size.z:F3} m");
                    }
                }
            }
            float? recommendation = CalculateRecommendedSpacing();
            if (recommendation.HasValue)
            {
                EditorGUILayout.LabelField("Provisional recommended center spacing", $"{recommendation.Value:F3} units");
                if (GUILayout.Button("Apply Recommended Spacing"))
                {
                    trackCenterSpacing = recommendation.Value;
                    SetStatus("Applied measured-envelope spacing with the displayed provisional clearance. Visual validation is still required.", MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Recommended spacing", "Not verified — using provisional 4.5 units.");
                trackCenterSpacing = EditorGUILayout.FloatField("Prototype spacing", trackCenterSpacing);
            }

            if (railMeasurement != null && railMeasurement.HasBounds)
            {
                float actualMeshLength = railMeasurement.LocalBounds.size.z;
                float pivotOffset = railMeasurement.LocalBounds.center.z;
                EditorGUILayout.LabelField("Whole Prefab AABB Envelope (local Z)", $"{actualMeshLength:F3} m");
                EditorGUILayout.LabelField("Bounds endpoints from root pivot", $"{railMeasurement.LocalBounds.min.z:F3} to {railMeasurement.LocalBounds.max.z:F3} m");
                EditorGUILayout.LabelField("Pivot Offset (bounds center from root)", $"{pivotOffset:F3} m");
                EditorGUILayout.HelpBox("The prefab contains multiple renderer meshes with different longitudinal spans. The whole-prefab AABB envelope is not itself proof of a repeat length; inspect mesh endpoints and joints before marking a repeat verified.", MessageType.Warning);
                effectiveRepeatLength = EditorGUILayout.FloatField("Effective Repeat Length", effectiveRepeatLength);
                effectiveRepeatLengthVerified = EditorGUILayout.Toggle("Verified from mesh endpoints", effectiveRepeatLengthVerified);
                using (new EditorGUI.DisabledScope(!effectiveRepeatLengthVerified || effectiveRepeatLength <= 0.01f))
                {
                    if (GUILayout.Button("Use Measured Repeat Length"))
                    {
                        placementStep = effectiveRepeatLength;
                        SetStatus($"Placement Step set to verified repeat length {placementStep:F3} m.", MessageType.Info);
                    }
                }

                float boundsDelta = actualMeshLength - placementStep;
                float repeatDelta = effectiveRepeatLength - placementStep;
                EditorGUILayout.LabelField("Placement Step", $"{placementStep:F3} m");
                EditorGUILayout.LabelField("Estimated whole-prefab bounds overlap / gap", boundsDelta >= 0f
                    ? $"Overlap {boundsDelta:F3} m (AABB only)"
                    : $"Gap {Mathf.Abs(boundsDelta):F3} m (AABB only)");
                if (effectiveRepeatLengthVerified && effectiveRepeatLength > 0.01f)
                    EditorGUILayout.LabelField("Repeat estimate overlap / gap", repeatDelta >= 0f
                        ? $"Overlap {repeatDelta:F3} m"
                        : $"Gap {Mathf.Abs(repeatDelta):F3} m");
                if (boundsDelta > 0.05f)
                    EditorGUILayout.HelpBox("Bounds overlap is only an AABB estimate. It does not prove that the actual meshes intersect.", MessageType.Warning);
                if (effectiveRepeatLength > 0.01f && placementStep > effectiveRepeatLength + 0.05f)
                    EditorGUILayout.HelpBox("Placement Step exceeds the verified repeat length; a geometric gap is expected between consecutive modules.", MessageType.Warning);
                if (effectiveRepeatLength > 0.01f && placementStep < effectiveRepeatLength - 0.05f)
                    EditorGUILayout.HelpBox("Placement Step is shorter than the verified repeat length; modules will overlap along the route.", MessageType.Warning);
                if (Mathf.Abs(pivotOffset) > 0.05f)
                    EditorGUILayout.HelpBox($"The bounds center is shifted {pivotOffset:F3} m along local Z. Generated modules are centered on the chainage sample by compensating this pivot offset.", MessageType.Info);
            }
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Diagnostic Preview (does not replace PrototypeTrack)", EditorStyles.boldLabel);
            diagnosticLaneSpacing = Mathf.Max(10f, EditorGUILayout.FloatField("Diagnostic lane separation", diagnosticLaneSpacing));
            customPreviewStep = Mathf.Max(0f, EditorGUILayout.FloatField("Preview C custom step (m)", customPreviewStep));
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(route == null || railPrefab == null || lut == null || railMeasurement == null || !railMeasurement.HasBounds || customPreviewStep <= 0.01f))
                    if (GUILayout.Button("Create / Refresh A-B-C Preview")) CreateDiagnosticPreview();
                if (GUILayout.Button("Clear Diagnostic Preview")) ClearDiagnosticPreview();
            }
            EditorGUILayout.HelpBox("A = 3 m, B = 17.526 m, C = the custom value above. Preview creation does not require a verified repeat length and never changes PrototypeTrack or ManualObjects. Generate Prototype still requires verified spacing.", MessageType.Info);
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Generated Prototype", EditorStyles.boldLabel);
            int count = GetPlacementCount();
            EditorGUILayout.LabelField("Prefab instances per track", count.ToString(CultureInfo.InvariantCulture));
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!CanGenerate()))
                    if (GUILayout.Button("Generate Test Segment")) GenerateTestSegment();
                if (GUILayout.Button("Clear Generated Test Segment")) ClearGeneratedTestSegment();
            }
            if (GUILayout.Button("Select Generated Objects")) SelectGeneratedObjects();
        }

        EditorGUILayout.HelpBox(status, statusType);
        EditorGUILayout.EndScrollView();
    }

    private void TryAutoFind()
    {
        if (route == null)
        {
            Scene active = SceneManager.GetActiveScene();
            route = Resources.FindObjectsOfTypeAll<SplineContainer>()
                .FirstOrDefault(x => x != null && x.gameObject.scene == active && x.name == "Rail_Spline_Route");
        }

        if (railPrefab == null)
            railPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RailPrefabPath);

        if (route != null) RebuildLut();
        SetStatus(route != null && railPrefab != null ? "Route and rail prefab found." : "Auto Find could not resolve all references. Assign them above.",
            route != null && railPrefab != null ? MessageType.Info : MessageType.Warning);
    }

    private void RebuildLut()
    {
        if (route == null || route.Spline == null || route.Spline.Count < 2)
        {
            lut = null;
            SetStatus("Assign a valid SplineContainer with at least two knots.", MessageType.Error);
            return;
        }

        lut = RouteLut.Build(route);
        SetStatus($"Route LUT rebuilt: {lut.Length:F2} world units, {lut.TValues.Length:N0} samples.", MessageType.Info);
        SceneView.RepaintAll();
    }

    private bool CanGenerate()
    {
        return route != null && railPrefab != null && lut != null && segmentLength > 0f && placementStep > 0f &&
               effectiveRepeatLengthVerified && effectiveRepeatLength > 0.01f &&
               startDistance >= 0f && startDistance + segmentLength <= lut.Length + 0.001f;
    }

    private int GetPlacementCount()
    {
        if (placementStep <= 0f || segmentLength <= 0f) return 0;
        return Mathf.Max(0, Mathf.CeilToInt(segmentLength / placementStep - 0.00001f));
    }

    private void FindBestTestSegment()
    {
        if (route == null || railPrefab == null) TryAutoFind();
        if (lut == null) RebuildLut();
        if (lut == null) return;

        float length = Mathf.Min(segmentLength, lut.Length - EndpointMargin * 2f);
        if (length <= 0f)
        {
            SetStatus("Route is too short for the requested segment and endpoint margins.", MessageType.Error);
            return;
        }

        const float candidateStep = 10f;
        const float analysisStep = 5f;
        float bestScore = float.PositiveInfinity;
        float bestStart = EndpointMargin;
        float bestTurn = 0f;
        float bestGradeDelta = 0f;

        for (float candidate = EndpointMargin; candidate + length <= lut.Length - EndpointMargin; candidate += candidateStep)
        {
            Vector3 previousTangent = Vector3.zero;
            float previousGrade = 0f;
            float maxTurn = 0f;
            float maxGradeDelta = 0f;
            bool havePrevious = false;

            for (float d = candidate; d <= candidate + length + 0.001f; d += analysisStep)
            {
                RouteSample sample = lut.Evaluate(route, Mathf.Min(d, candidate + length));
                Vector3 tangent = sample.Tangent.normalized;
                float vertical = Vector3.Dot(tangent, sample.Up.normalized);
                float horizontal = Vector3.ProjectOnPlane(tangent, sample.Up.normalized).magnitude;
                float grade = Mathf.Atan2(vertical, horizontal) * Mathf.Rad2Deg;

                if (havePrevious)
                {
                    maxTurn = Mathf.Max(maxTurn, Vector3.Angle(previousTangent, tangent));
                    maxGradeDelta = Mathf.Max(maxGradeDelta, Mathf.Abs(grade - previousGrade));
                }
                previousTangent = tangent;
                previousGrade = grade;
                havePrevious = true;
            }

            float score = maxTurn + maxGradeDelta;
            if (score < bestScore)
            {
                bestScore = score;
                bestStart = candidate;
                bestTurn = maxTurn;
                bestGradeDelta = maxGradeDelta;
            }
        }

        startDistance = bestStart;
        bestSegmentEnd = Mathf.Min(bestStart + length, lut.Length);
        bestSegmentCurvature = bestTurn;
        bestSegmentGradeChange = bestGradeDelta;
        SetStatus($"Best candidate found at {startDistance:F2}–{bestSegmentEnd:F2} (score {bestScore:F5}). Review in Scene View before generating.", MessageType.Info);
        previewEnabled = true;
        FramePreview();
        Repaint();
    }

    private void TogglePreview()
    {
        if (lut == null) RebuildLut();
        if (lut == null) return;
        previewEnabled = !previewEnabled;
        if (previewEnabled) FramePreview();
        SceneView.RepaintAll();
    }

    private void FramePreview()
    {
        if (route == null || lut == null) return;
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;
        RouteSample a = lut.Evaluate(route, Mathf.Clamp(startDistance, 0f, lut.Length));
        RouteSample b = lut.Evaluate(route, Mathf.Clamp(startDistance + segmentLength, 0f, lut.Length));
        Vector3 center = (a.Position + b.Position) * 0.5f;
        view.LookAt(center, Quaternion.LookRotation(Vector3.up, Vector3.forward), Mathf.Max(40f, segmentLength * 0.85f));
    }

    private void DrawPreview(SceneView view)
    {
        if (inspectPrefabGeometry) DrawPrefabGeometry(view);
        if (!previewEnabled || route == null || lut == null) return;
        float end = Mathf.Min(startDistance + segmentLength, lut.Length);
        if (end <= startDistance) return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
        DrawRoutePreview(startDistance, end, 0f, new Color(0.2f, 0.9f, 1f, 1f));
        DrawRoutePreview(startDistance, end, trackBSide == TrackSide.LocalRight ? trackCenterSpacing : -trackCenterSpacing, new Color(1f, 0.7f, 0.1f, 1f));
        RouteSample first = lut.Evaluate(route, startDistance);
        RouteSample last = lut.Evaluate(route, end);
        Handles.Label(first.Position, $"Start {startDistance:F1} m");
        Handles.Label(last.Position, $"End {end:F1} m");
    }

    private void DrawPrefabGeometry(SceneView view)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null || stage.prefabContentsRoot == null || railPrefab == null || railMeasurement == null ||
            !string.Equals(stage.assetPath, AssetDatabase.GetAssetPath(railPrefab), StringComparison.OrdinalIgnoreCase))
            return;

        Transform root = stage.prefabContentsRoot.transform;
        Matrix4x4 previousMatrix = Handles.matrix;
        Color previousColor = Handles.color;
        Handles.matrix = root.localToWorldMatrix;
        try
        {
            for (int i = 0; i < railMeasurement.Renderers.Count; i++)
            {
                RendererMeasurement item = railMeasurement.Renderers[i];
                bool notable = item.RootBounds.size.z >= 3f || item.Name == "Mesh_11" || item.Name == "Mesh_12";
                Color color = item.Name == "Mesh_11" ? new Color(1f, 0.35f, 0.15f, 1f) :
                              item.Name == "Mesh_12" ? new Color(0.2f, 0.85f, 1f, 1f) :
                              Color.HSVToRGB((i * 0.6180339f) % 1f, 0.65f, 1f);
                Handles.color = color;
                Handles.DrawWireCube(item.RootBounds.center, item.RootBounds.size);
                if (notable)
                {
                    int labelRow = i / 2;
                    float side = i % 2 == 0 ? -1f : 1f;
                    Vector3 labelPosition = item.RootBounds.center +
                                            Vector3.right * side * (item.RootBounds.extents.x + 1.5f + labelRow * 0.6f) +
                                            Vector3.up * (item.RootBounds.extents.y + 0.2f + labelRow * 0.8f);
                    Handles.Label(labelPosition, $"{item.Name}  Z {item.RootBounds.size.z:F3} m");
                }
            }
        }
        finally
        {
            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
        }
    }

    private void DrawRoutePreview(float start, float end, float lateral, Color color)
    {
        const int previewSegments = 40;
        Handles.color = color;
        Vector3 previous = Vector3.zero;
        for (int i = 0; i <= previewSegments; i++)
        {
            float distance = Mathf.Lerp(start, end, i / (float)previewSegments);
            RouteSample sample = lut.Evaluate(route, distance);
            Quaternion rotation = Quaternion.LookRotation(sample.Tangent, sample.Up);
            Vector3 point = sample.Position + (rotation * Vector3.right) * (lateral + (lateral == 0f ? trackALateralOffset : trackALateralOffset + trackBLateralOffset));
            point += sample.Up * verticalOffset;
            if (i > 0) Handles.DrawLine(previous, point, 3f);
            previous = point;
        }
    }

    private void MeasureAssets()
    {
        GameObject train = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/Train_THSR_700T.prefab");
        trainMeasurement = MeasureAsset(train);
        railMeasurement = MeasureAsset(railPrefab);
        if (trainMeasurement.HasBounds && railMeasurement.HasBounds)
            SetStatus("Asset bounds measured from mesh-local bounds transformed into each prefab root space. Forward axis is an assumption pending visual inspection.", MessageType.Warning);
        else
            SetStatus("One or more asset bounds are unavailable. They are shown as Not verified; prototype generation remains available.", MessageType.Warning);
    }

    private void OpenRailPrefabForInspection()
    {
        if (railPrefab == null)
        {
            SetStatus("Assign the rail prefab before opening Prefab Mode.", MessageType.Warning);
            return;
        }

        string path = AssetDatabase.GetAssetPath(railPrefab);
        if (string.IsNullOrEmpty(path))
        {
            SetStatus("The assigned rail prefab has no asset path and cannot be opened in Prefab Mode.", MessageType.Error);
            return;
        }

        PrefabStageUtility.OpenPrefab(path);
        EditorApplication.delayCall += FrameOpenedRailPrefab;
        SetStatus($"Opened '{path}' in Prefab Mode for visual inspection. No prefab changes were made.", MessageType.Info);
    }

    private static void FrameOpenedRailPrefab()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage == null || stage.prefabContentsRoot == null) return;
        Selection.activeGameObject = stage.prefabContentsRoot;
        FramePrefabGeometry(true);
        SceneView.RepaintAll();
    }

    private static void FramePrefabGeometry(bool topView)
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        SceneView view = SceneView.lastActiveSceneView;
        if (stage == null || stage.prefabContentsRoot == null || view == null) return;
        Transform root = stage.prefabContentsRoot.transform;
        view.pivot = root.position + root.forward * 1.55f;
        view.rotation = topView
            ? Quaternion.LookRotation(-root.up, root.forward)
            : Quaternion.LookRotation(root.right, root.up);
        view.size = 13f;
        view.orthographic = true;
        view.Repaint();
    }

    private static AssetMeasurement MeasureAsset(GameObject asset)
    {
        var result = new AssetMeasurement { AssetName = asset != null ? asset.name : "Not found" };
        if (asset == null) return result;

        Transform root = asset.transform;
        Matrix4x4 rootInverse = root.worldToLocalMatrix;
        bool hasBounds = false;
        Bounds combined = default;

        foreach (MeshFilter filter in asset.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            result.MeshFilterCount++;
            Matrix4x4 toRoot = rootInverse * filter.transform.localToWorldMatrix;
            Bounds rendererBounds = TransformBounds(filter.sharedMesh.bounds, toRoot);
            result.Renderers.Add(new RendererMeasurement(filter.name, rendererBounds, filter.transform.localPosition,
                filter.transform.localRotation.eulerAngles, filter.transform.localScale, filter.sharedMesh.bounds.size.z));
            EncapsulateTransformedBounds(ref combined, ref hasBounds, filter.sharedMesh.bounds, toRoot);
        }

        foreach (SkinnedMeshRenderer skin in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            result.SkinnedMeshRendererCount++;
            Matrix4x4 toRoot = rootInverse * skin.transform.localToWorldMatrix;
            result.Renderers.Add(new RendererMeasurement(skin.name, TransformBounds(skin.localBounds, toRoot),
                skin.transform.localPosition, skin.transform.localRotation.eulerAngles, skin.transform.localScale, skin.localBounds.size.z));
            EncapsulateTransformedBounds(ref combined, ref hasBounds, skin.localBounds, toRoot);
        }

        result.MeshRendererCount = asset.GetComponentsInChildren<MeshRenderer>(true).Length;
        result.ColliderCount = asset.GetComponentsInChildren<Collider>(true).Length;
        result.NonUnitScaleCount = asset.GetComponentsInChildren<Transform>(true)
            .Count(t => !ApproximatelyOne(t.localScale));
        result.HasBounds = hasBounds;
        result.RootPosition = root.localPosition;
        result.RootRotation = root.localRotation.eulerAngles;
        result.RootScale = root.localScale;
        if (hasBounds) result.LocalBounds = combined;
        return result;
    }

    private static bool ApproximatelyOne(Vector3 scale)
    {
        return Mathf.Abs(scale.x - 1f) < 0.0001f && Mathf.Abs(scale.y - 1f) < 0.0001f && Mathf.Abs(scale.z - 1f) < 0.0001f;
    }

    private static void EncapsulateTransformedBounds(ref Bounds target, ref bool hasTarget, Bounds source, Matrix4x4 matrix)
    {
        Vector3 center = source.center;
        Vector3 extents = source.extents;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
            Vector3 transformed = matrix.MultiplyPoint3x4(corner);
            if (!hasTarget)
            {
                target = new Bounds(transformed, Vector3.zero);
                hasTarget = true;
            }
            else target.Encapsulate(transformed);
        }
    }

    private static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
    {
        bool hasBounds = false;
        Bounds result = default;
        EncapsulateTransformedBounds(ref result, ref hasBounds, source, matrix);
        return result;
    }

    private static void DrawMeasurement(string label, AssetMeasurement measurement)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        if (measurement == null || !measurement.HasBounds)
        {
            EditorGUILayout.LabelField("Bounds", "Not verified");
            return;
        }

        EditorGUILayout.LabelField("Local root-space bounds size", measurement.LocalBounds.size.ToString("F3"));
        EditorGUILayout.LabelField("Local root-space bounds center", measurement.LocalBounds.center.ToString("F3"));
        EditorGUILayout.LabelField("Local dimensions X / Y / Z", $"{measurement.LocalBounds.size.x:F3} / {measurement.LocalBounds.size.y:F3} / {measurement.LocalBounds.size.z:F3}");
        EditorGUILayout.LabelField("Root position / rotation / scale", $"{measurement.RootPosition} / {measurement.RootRotation} / {measurement.RootScale}");
        EditorGUILayout.LabelField("MeshFilter / MeshRenderer / Skinned / Collider", $"{measurement.MeshFilterCount} / {measurement.MeshRendererCount} / {measurement.SkinnedMeshRendererCount} / {measurement.ColliderCount}");
        EditorGUILayout.LabelField("Non-unit transforms", measurement.NonUnitScaleCount.ToString(CultureInfo.InvariantCulture));
    }

    private float? CalculateRecommendedSpacing()
    {
        if (trainMeasurement == null || railMeasurement == null || !trainMeasurement.HasBounds || !railMeasurement.HasBounds)
            return null;

        // Train width assumes the TrainSplineMover's +Z forward convention; the clearance is user-editable and provisional.
        float trainEnvelope = trainMeasurement.LocalBounds.size.x;
        float moduleEnvelope = railMeasurement.LocalBounds.size.x;
        return Mathf.Max(trainEnvelope + provisionalClearance, moduleEnvelope + provisionalClearance);
    }

    private void GenerateTestSegment()
    {
        if (!CanGenerate())
        {
            SetStatus("Cannot generate: assign valid references, verify Effective Repeat Length, and keep the complete segment inside the route.", MessageType.Error);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            SetStatus($"Open the main scene first. Active scene is '{scene.path}'. No objects were created.", MessageType.Error);
            return;
        }

        int undoGroup = BeginUndoGroup("Generate Static Railway Prototype");
        try
        {
            Transform[] hierarchy = EnsureHierarchy(scene);
            Transform trackA = hierarchy[0];
            Transform trackB = hierarchy[1];
            List<Placement> placementsA = new List<Placement>();
            List<Placement> placementsB = new List<Placement>();
            int count = GetPlacementCount();
            float sideSign = trackBSide == TrackSide.LocalRight ? 1f : -1f;

            for (int i = 0; i < count; i++)
            {
                float localDistance = placementStep * (i + 0.5f);
                float chainage = startDistance + localDistance;
                RouteSample sample = lut.Evaluate(route, chainage);
                Quaternion baseRotation = Quaternion.LookRotation(sample.Tangent, sample.Up);
                Quaternion rotation = baseRotation * Quaternion.Euler(0f, yawOffset, 0f);
                Vector3 right = baseRotation * Vector3.right;
                Vector3 pivotCompensation = rotation * Vector3.forward * GetPivotOffset();
                Vector3 basePosition = sample.Position + sample.Up * verticalOffset + right * trackALateralOffset - pivotCompensation;
                Vector3 positionB = basePosition + right * (sideSign * trackCenterSpacing + trackBLateralOffset);

                placementsA.Add(new Placement(StableName("A", chainage), basePosition, rotation));
                placementsB.Add(new Placement(StableName("B", chainage), positionB, rotation));
            }

            UpdateGeneratedTrack(trackA, placementsA, scene);
            UpdateGeneratedTrack(trackB, placementsB, scene);
            EditorSceneManager.MarkSceneDirty(scene);
            SetStatus($"Generated {placementsA.Count} Track A and {placementsB.Count} Track B prefab instances from {startDistance:F2} to {startDistance + segmentLength:F2}.", MessageType.Info);
            previewEnabled = true;
            SceneView.RepaintAll();
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(exception);
            SetStatus("Generate failed. The current Undo group was reverted; see Console for the exception.", MessageType.Error);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static int BeginUndoGroup(string name)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
        return group;
    }

    private static Transform[] EnsureHierarchy(Scene scene)
    {
        Transform root = FindSceneRoot(scene, "RailwayStaticMap");
        if (root == null) root = CreateSceneObject(scene, "RailwayStaticMap", null).transform;

        Transform editable = GetOrCreateChild(root, "Editable");
        Transform generated = GetOrCreateChild(editable, "Generated");
        Transform prototype = GetOrCreateChild(generated, "PrototypeTrack");
        Transform trackA = GetOrCreateChild(prototype, "Track_A");
        Transform trackB = GetOrCreateChild(prototype, "Track_B");
        GetOrCreateChild(editable, "ManualObjects");
        Transform baked = GetOrCreateChild(root, "Baked");
        GetOrCreateChild(baked, "Chunks");
        GetOrCreateChild(root, "GeneratedMetadata");
        return new[] { trackA, trackB };
    }

    private static Transform FindSceneRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().Select(x => x.transform).FirstOrDefault(x => x.name == name);
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).name == name) return parent.GetChild(i);

        GameObject child = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(child, "Create Static Railway Hierarchy");
        Undo.SetTransformParent(child.transform, parent, "Parent Static Railway Hierarchy");
        return child.transform;
    }

    private static GameObject CreateSceneObject(Scene scene, string name, Transform parent)
    {
        var go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "Create Static Railway Hierarchy");
        if (parent != null) Undo.SetTransformParent(go.transform, parent, "Parent Static Railway Hierarchy");
        return go;
    }

    private void UpdateGeneratedTrack(Transform parent, List<Placement> placements, Scene scene)
    {
        var existing = new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (!IsGeneratedRail(child)) continue;
            if (!existing.TryGetValue(child.name, out List<Transform> duplicates))
            {
                duplicates = new List<Transform>();
                existing.Add(child.name, duplicates);
            }
            duplicates.Add(child);
        }

        var used = new HashSet<Transform>();
        foreach (Placement placement in placements)
        {
            Transform instance = null;
            if (existing.TryGetValue(placement.Name, out List<Transform> candidates))
            {
                instance = candidates.FirstOrDefault(x => !used.Contains(x));
                if (instance != null)
                {
                    used.Add(instance);
                    Undo.RecordObject(instance, "Update Static Railway Prototype Transform");
                    instance.position = placement.Position;
                    instance.rotation = placement.Rotation;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                }
            }

            if (instance == null)
            {
                Transform collision = parent.Find(placement.Name);
                if (collision != null && !IsGeneratedRail(collision))
                {
                    Debug.LogWarning($"Skipped '{placement.Name}' because an object with that name is not a generated instance of the assigned source prefab.", collision);
                    continue;
                }

                GameObject created = PrefabUtility.InstantiatePrefab(railPrefab, scene) as GameObject;
                if (created == null) throw new InvalidOperationException("Unity could not instantiate the assigned rail prefab.");
                Undo.RegisterCreatedObjectUndo(created, "Create Static Rail Prefab Instance");
                Undo.SetTransformParent(created.transform, parent, "Parent Static Rail Prefab Instance");
                Undo.RecordObject(created, "Name Static Rail Prefab Instance");
                Undo.RecordObject(created.transform, "Position Static Rail Prefab Instance");
                created.name = placement.Name;
                created.transform.position = placement.Position;
                created.transform.rotation = placement.Rotation;
                PrefabUtility.RecordPrefabInstancePropertyModifications(created);
                PrefabUtility.RecordPrefabInstancePropertyModifications(created.transform);
                used.Add(created.transform);
            }
        }

        foreach (List<Transform> candidates in existing.Values)
        foreach (Transform obsolete in candidates)
            if (obsolete != null && !used.Contains(obsolete)) Undo.DestroyObjectImmediate(obsolete.gameObject);
    }

    private bool IsGeneratedRail(Transform candidate)
    {
        if (candidate == null || railPrefab == null) return false;
        if (!candidate.name.StartsWith("Rail_", StringComparison.Ordinal) &&
            !candidate.name.StartsWith("SRM_DIAG_", StringComparison.Ordinal) &&
            !candidate.name.StartsWith("SRM_PREVIEW_", StringComparison.Ordinal)) return false;
        string expectedPath = AssetDatabase.GetAssetPath(railPrefab);
        if (string.IsNullOrEmpty(expectedPath)) return false;

        string instancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(candidate.gameObject);
        if (!string.IsNullOrEmpty(instancePath) &&
            string.Equals(instancePath, expectedPath, StringComparison.OrdinalIgnoreCase))
            return true;

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject);
        string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        return string.Equals(sourcePath, expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string StableName(string track, float chainage)
    {
        long millimeters = (long)Math.Round(chainage * 1000.0, MidpointRounding.AwayFromZero);
        return $"Rail_{track}_C{millimeters:D10}";
    }

    private float GetPivotOffset()
    {
        return railMeasurement != null && railMeasurement.HasBounds ? railMeasurement.LocalBounds.center.z : 0f;
    }

    private void CreateSpacingComparison()
    {
        CreateDiagnosticPreview();
    }

    private void CreateDiagnosticPreview()
    {
        if (route == null || railPrefab == null || lut == null || railMeasurement == null || !railMeasurement.HasBounds || customPreviewStep <= 0.01f)
        {
            SetStatus("Assign the route and prefab, measure the prefab, and enter a positive custom step for Preview C.", MessageType.Error);
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            SetStatus($"Open the YenTuHaLongXanh scene first. Active scene is '{scene.path}'. No diagnostic objects were created.", MessageType.Error);
            return;
        }

        float maxSpan = Mathf.Max(3f, Mathf.Max(17.526f, customPreviewStep)) * 3f;
        if (startDistance < 0f || startDistance + maxSpan > lut.Length)
        {
            SetStatus($"The chosen 3-module preview needs {maxSpan:F3} m of route from Start Distance {startDistance:F3}; choose a valid low-curvature section.", MessageType.Error);
            return;
        }

        int undoGroup = BeginUndoGroup("Create Static Railway Spacing Diagnostics");
        try
        {
            Transform diagnosticsRoot = EnsureDiagnosticPreviewHierarchy(scene);
            CreateDiagnosticVariant(diagnosticsRoot, scene, "Preview_A_Step_3m", "A", 3f, 0);
            CreateDiagnosticVariant(diagnosticsRoot, scene, "Preview_B_Step_17_526m", "B", 17.526f, 1);
            CreateDiagnosticVariant(diagnosticsRoot, scene, "Preview_C_Custom", "C", customPreviewStep, 2);
            EditorSceneManager.MarkSceneDirty(scene);
            SetStatus($"Created/refreshed 3 instances per preview group: A 3.000 m, B 17.526 m, C {customPreviewStep:F3} m. PrototypeTrack and ManualObjects were preserved.", MessageType.Info);
            SceneView.RepaintAll();
            Selection.activeTransform = diagnosticsRoot;
            FrameDiagnosticPreview(diagnosticsRoot, Mathf.Max(3f, Mathf.Max(17.526f, customPreviewStep)) * 3f);
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(exception);
            SetStatus("Spacing diagnostics failed and were reverted. See Console for details.", MessageType.Error);
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    private Transform EnsureDiagnosticPreviewHierarchy(Scene scene)
    {
        Transform root = FindSceneRoot(scene, "RailwayStaticMap");
        if (root == null) root = CreateSceneObject(scene, "RailwayStaticMap", null).transform;
        Transform editable = GetOrCreateChild(root, "Editable");
        Transform generated = GetOrCreateChild(editable, "Generated");
        return GetOrCreateChild(generated, "DiagnosticPreview");
    }

    private void FrameDiagnosticPreview(Transform diagnosticsRoot, float routeSpan)
    {
        if (diagnosticsRoot == null || route == null || lut == null) return;
        bool hasBounds = false;
        Bounds combined = default;
        foreach (Renderer renderer in diagnosticsRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds)
            {
                combined = renderer.bounds;
                hasBounds = true;
            }
            else combined.Encapsulate(renderer.bounds);
        }
        if (!hasBounds) return;

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null) return;
        RouteSample sample = lut.Evaluate(route, Mathf.Clamp(startDistance + routeSpan * 0.5f, 0f, lut.Length));
        view.pivot = combined.center;
        view.rotation = Quaternion.LookRotation(-sample.Up, sample.Tangent);
        view.orthographic = true;
        view.size = Mathf.Max(15f, routeSpan * 0.62f);
        view.Repaint();
    }

    private void CreateDiagnosticVariant(Transform diagnosticsRoot, Scene scene, string groupName, string variant, float step, int laneIndex)
    {
        if (step <= 0.01f) throw new InvalidOperationException($"Variant {variant} has an invalid repeat length.");

        Transform variantRoot = GetOrCreateChild(diagnosticsRoot, groupName);
        for (int i = variantRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = variantRoot.GetChild(i);
            if (IsDiagnosticPrefab(child)) Undo.DestroyObjectImmediate(child.gameObject);
        }

        float lateral = diagnosticLaneSpacing * (laneIndex + 1);
        for (int i = 0; i < 3; i++)
        {
            float chainage = startDistance + step * (i + 0.5f);
            RouteSample sample = lut.Evaluate(route, chainage);
            Quaternion rotation = Quaternion.LookRotation(sample.Tangent, sample.Up) * Quaternion.Euler(0f, yawOffset, 0f);
            Quaternion baseRotation = Quaternion.LookRotation(sample.Tangent, sample.Up);
            Vector3 position = sample.Position + sample.Up * verticalOffset + baseRotation * Vector3.right * lateral -
                               rotation * Vector3.forward * GetPivotOffset();
            GameObject created = PrefabUtility.InstantiatePrefab(railPrefab, scene) as GameObject;
            if (created == null) throw new InvalidOperationException($"Unity could not instantiate diagnostic variant {variant}.");
            Undo.RegisterCreatedObjectUndo(created, "Create Static Railway Diagnostic Instance");
            Undo.SetTransformParent(created.transform, variantRoot, "Parent Static Railway Diagnostic Instance");
            Undo.RecordObject(created, "Name Static Railway Diagnostic Instance");
            Undo.RecordObject(created.transform, "Position Static Railway Diagnostic Instance");
            long chainageMillimeters = (long)Math.Round(chainage * 1000.0, MidpointRounding.AwayFromZero);
            created.name = $"SRM_PREVIEW_{variant}_C{chainageMillimeters:D10}";
            created.transform.position = position;
            created.transform.rotation = rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(created);
            PrefabUtility.RecordPrefabInstancePropertyModifications(created.transform);
        }
    }

    private bool IsDiagnosticPrefab(Transform candidate)
    {
        return candidate != null &&
               (candidate.name.StartsWith("SRM_PREVIEW_", StringComparison.Ordinal) || candidate.name.StartsWith("SRM_DIAG_", StringComparison.Ordinal)) &&
               IsGeneratedRail(candidate);
    }

    private void ClearDiagnosticPreview()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform root = FindSceneRoot(scene, "RailwayStaticMap");
        Transform diagnosticsRoot = root != null ? root.Find("Editable/Generated/DiagnosticPreview") : null;
        if (diagnosticsRoot == null)
        {
            SetStatus("No DiagnosticPreview branch exists. Nothing was removed.", MessageType.Info);
            return;
        }

        int undoGroup = BeginUndoGroup("Clear Static Railway Spacing Diagnostics");
        try
        {
            int removed = 0;
            ClearMarkedDiagnosticChildren(diagnosticsRoot, ref removed);
            if (removed > 0) EditorSceneManager.MarkSceneDirty(scene);
            SetStatus($"Cleared {removed} marked preview instances under DiagnosticPreview. PrototypeTrack, ManualObjects, and unmarked children were preserved.", MessageType.Info);
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(undoGroup);
            Debug.LogException(exception);
            SetStatus("Clearing diagnostics failed and was reverted. See Console for details.", MessageType.Error);
        }
        finally { Undo.CollapseUndoOperations(undoGroup); }
    }

    private void ClearMarkedDiagnosticChildren(Transform parent, ref int removed)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (IsDiagnosticPrefab(child))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                removed++;
            }
            else ClearMarkedDiagnosticChildren(child, ref removed);
        }
    }

    private void ClearGeneratedTestSegment()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform root = FindSceneRoot(scene, "RailwayStaticMap");
        Transform editable = root != null ? root.Find("Editable") : null;
        Transform generated = editable != null ? editable.Find("Generated") : null;
        Transform prototype = generated != null ? generated.Find("PrototypeTrack") : null;
        if (prototype == null)
        {
            SetStatus("No prototype track hierarchy exists. Nothing was removed.", MessageType.Info);
            return;
        }

        int group = BeginUndoGroup("Clear Generated Static Railway Prototype");
        try
        {
            int removed = 0;
            foreach (string trackName in new[] { "Track_A", "Track_B" })
            {
                Transform track = prototype.Find(trackName);
                if (track == null) continue;
                for (int i = track.childCount - 1; i >= 0; i--)
                {
                    Transform child = track.GetChild(i);
                    if (!IsGeneratedRail(child)) continue;
                    Undo.DestroyObjectImmediate(child.gameObject);
                    removed++;
                }
            }

            if (removed > 0) EditorSceneManager.MarkSceneDirty(scene);
            SetStatus($"Cleared {removed} generated rail instances. ManualObjects, Baked, source objects and unrelated children were preserved.", MessageType.Info);
        }
        catch (Exception exception)
        {
            Undo.RevertAllDownToGroup(group);
            Debug.LogException(exception);
            SetStatus("Clear failed and the current Undo group was reverted.", MessageType.Error);
        }
        finally { Undo.CollapseUndoOperations(group); }
    }

    private void SelectGeneratedObjects()
    {
        Scene scene = SceneManager.GetActiveScene();
        Transform root = FindSceneRoot(scene, "RailwayStaticMap");
        Transform prototype = root != null ? root.Find("Editable/Generated/PrototypeTrack") : null;
        if (prototype == null)
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
            SetStatus("No generated prototype hierarchy exists.", MessageType.Info);
            return;
        }

        var selection = new List<UnityEngine.Object>();
        string firstCandidateDiagnostic = null;
        foreach (string trackName in new[] { "Track_A", "Track_B" })
        {
            Transform track = prototype.Find(trackName);
            if (track == null) continue;
            for (int i = 0; i < track.childCount; i++)
            {
                Transform child = track.GetChild(i);
                if (IsGeneratedRail(child)) selection.Add(child.gameObject);
                else if (firstCandidateDiagnostic == null)
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    firstCandidateDiagnostic = $"Candidate '{child.name}', expected '{AssetDatabase.GetAssetPath(railPrefab)}', nearest '{PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject)}', source '{(source != null ? AssetDatabase.GetAssetPath(source) : "null")}', prefab instance {PrefabUtility.IsPartOfPrefabInstance(child.gameObject)}.";
                }
            }
        }
        Selection.objects = selection.ToArray();
        if (selection.Count > 0) EditorGUIUtility.PingObject(selection[0]);
        SetStatus(selection.Count > 0
                ? $"Selected {selection.Count} generated prefab instances."
                : $"Selected 0 generated prefab instances. {firstCandidateDiagnostic ?? "No Rail_ candidate was found under Track_A/Track_B."}",
            selection.Count > 0 ? MessageType.Info : MessageType.Warning);
    }

    private void SetStatus(string message, MessageType type)
    {
        status = message;
        statusType = type;
        Repaint();
    }

    private sealed class AssetMeasurement
    {
        public string AssetName;
        public bool HasBounds;
        public Bounds LocalBounds;
        public readonly List<RendererMeasurement> Renderers = new List<RendererMeasurement>();
        public int MeshFilterCount;
        public int MeshRendererCount;
        public int SkinnedMeshRendererCount;
        public int ColliderCount;
        public int NonUnitScaleCount;
        public Vector3 RootPosition;
        public Vector3 RootRotation;
        public Vector3 RootScale;
    }

    private sealed class RendererMeasurement
    {
        public readonly string Name;
        public readonly Bounds RootBounds;
        public readonly Vector3 LocalPosition;
        public readonly Vector3 LocalRotation;
        public readonly Vector3 LocalScale;
        public readonly float MeshLocalZLength;

        public RendererMeasurement(string name, Bounds rootBounds, Vector3 localPosition, Vector3 localRotation, Vector3 localScale, float meshLocalZLength)
        {
            Name = name;
            RootBounds = rootBounds;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            MeshLocalZLength = meshLocalZLength;
        }
    }

    private struct Placement
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public Placement(string name, Vector3 position, Quaternion rotation)
        {
            Name = name;
            Position = position;
            Rotation = rotation;
        }
    }

    private struct RouteSample
    {
        public float Chainage;
        public Vector3 Position;
        public Vector3 Tangent;
        public Vector3 Up;
    }

    private sealed class RouteLut
    {
        public float[] TValues { get; private set; }
        public float[] Distances { get; private set; }
        public float Length { get; private set; }

        public static RouteLut Build(SplineContainer container)
        {
            float estimatedLength = Mathf.Max(container.CalculateLength(), 1f);
            int sampleCount = Mathf.Clamp(Mathf.CeilToInt(estimatedLength * 2f), 4096, 200000);
            var table = new RouteLut
            {
                TValues = new float[sampleCount + 1],
                Distances = new float[sampleCount + 1]
            };

            Vector3 previous = container.EvaluatePosition(0f);
            for (int i = 0; i <= sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                Vector3 position = container.EvaluatePosition(t);
                table.TValues[i] = t;
                if (i > 0) table.Distances[i] = table.Distances[i - 1] + Vector3.Distance(previous, position);
                previous = position;
            }
            table.Length = table.Distances[sampleCount];
            return table;
        }

        public RouteSample Evaluate(SplineContainer container, float chainage)
        {
            chainage = Mathf.Clamp(chainage, 0f, Length);
            int index = Array.BinarySearch(Distances, chainage);
            if (index < 0) index = ~index;
            index = Mathf.Clamp(index, 1, Distances.Length - 1);

            float lowerDistance = Distances[index - 1];
            float upperDistance = Distances[index];
            float fraction = upperDistance > lowerDistance ? (chainage - lowerDistance) / (upperDistance - lowerDistance) : 0f;
            float t = Mathf.Lerp(TValues[index - 1], TValues[index], fraction);
            Vector3 tangent = ((Vector3)container.EvaluateTangent(t)).normalized;
            Vector3 up = ((Vector3)container.EvaluateUpVector(t)).normalized;
            if (tangent.sqrMagnitude < 0.000001f) tangent = Vector3.forward;
            if (up.sqrMagnitude < 0.000001f) up = Vector3.up;

            return new RouteSample
            {
                Chainage = chainage,
                Position = container.EvaluatePosition(t),
                Tangent = tangent,
                Up = up
            };
        }
    }
}
