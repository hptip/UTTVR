using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

namespace UTT.SaBan.EditorTools
{
    /// <summary>
    /// Tools > UTT > Sa Bàn Builder
    ///  1. Sa Bàn &amp; Tỉ lệ   : tạo bàn, chỉnh tỉ lệ, vừa khít, bảng điều khiển nổi
    ///  2. Chuyển Scene       : chọn một scene (vd YenTuHaLongXanh) và chuyển thành sa bàn
    ///  3. Thêm Model         : nhập thêm prefab / model vào một lớp của sa bàn
    ///  4. Sắp xếp            : đặt dọc ray, nhân bản dọc ray, xếp lưới, đổi lớp
    /// </summary>
    public sealed class SaBanBuilderWindow : EditorWindow
    {
        [System.Serializable]
        private sealed class SourceItem
        {
            public string name;
            public bool include = true;
            public SaBanLayerKind kind = SaBanLayerKind.Custom;
            public bool isRoute;
            public bool isTrain;
            public string note;
            [System.NonSerialized] public GameObject go;
        }

        private enum ImportPlacement { TableCenter = 0, SceneViewPivot = 1, AlongRoute = 2 }

        private static readonly string[] TabNames = { "1. Sa bàn & Tỉ lệ", "2. Chuyển Scene", "3. Thêm Model", "4. Sắp xếp" };

        [SerializeField] private SaBanRoot target;
        [SerializeField] private int tab;
        private Vector2 scroll;

        // ---- Tab 1
        [SerializeField] private string newName = "SaBan_YenTuHaLong";
        [SerializeField] private Vector2 tableSize = new Vector2(3f, 1.4f);
        [SerializeField] private float tableHeight = 0.8f;
        [SerializeField] private float boardThickness = 0.07f;
        [SerializeField] private float legSize = 0.08f;
        [SerializeField] private Color tableColor = new Color(0.72f, 0.55f, 0.36f);
        [SerializeField] private float scaleDenominator = 10000f;
        [SerializeField] private float paddingPercent = 6f;
        [SerializeField] private bool fitRouteOnly = true;
        [SerializeField] private bool panelAutoPosition = true;
        [SerializeField] private float panelHeightAboveTable = 1.35f;
        [SerializeField] private Vector3 panelCustomPosition = new Vector3(0f, 2.2f, 0f);
        [SerializeField] private bool panelFacePlayer = true;
        [SerializeField] private float panelSizeMultiplier = 1f;

        // ---- Tab 2
        [SerializeField] private SceneAsset sourceScene;
        [SerializeField] private List<SourceItem> items = new List<SourceItem>();
        [SerializeField] private BakeOptions bake = new BakeOptions();
        [SerializeField] private bool fitAfterConvert = true;
        [SerializeField] private float trainScaleMultiplier = 1f;
        [SerializeField] private float terrainScaleMultiplier = 1f;
        [SerializeField] private int routePick;
        private Scene _sourceHandle;
        private bool _openedByTool;
        private readonly List<Scene> _openedZoneScenes = new List<Scene>();
        private readonly List<SplineContainer> _routes = new List<SplineContainer>();
        private string _routeSummary = "";
        private string _lastReport = "";

        // ---- Tab 3
        [SerializeField] private List<GameObject> importList = new List<GameObject>();
        [SerializeField] private SaBanLayerKind importKind = SaBanLayerKind.Nature;
        [SerializeField] private string importCustomKey = "khac";
        [SerializeField] private string importCustomLabel = "KHÁC";
        [SerializeField] private ImportPlacement importPlacement = ImportPlacement.TableCenter;
        [SerializeField] private float importRouteDistance = 500f;
        [SerializeField] private bool importKeepTableSize = false;
        [SerializeField] private float importScaleMultiplier = 1f;
        [SerializeField] private float importSpread = 0f;

        // ---- Tab 4
        [SerializeField] private SaBanLayerKind moveKind = SaBanLayerKind.Nature;
        [SerializeField] private string moveCustomKey = "khac";
        [SerializeField] private string moveCustomLabel = "KHÁC";
        [SerializeField] private float routeStart = 100f;
        [SerializeField] private float routeSpacing = 200f;
        [SerializeField] private float routeLateral = 60f;
        [SerializeField] private float routeHeight = 0f;
        [SerializeField] private bool routeAlign = true;
        [SerializeField] private float routeYaw = 0f;
        [SerializeField] private bool routeAlternate = true;
        [SerializeField] private bool routeRandomYaw = false;
        [SerializeField] private Vector2 routeRandomScale = new Vector2(1f, 1f);
        [SerializeField] private int cloneCount = 20;
        [SerializeField] private int gridColumns = 5;
        [SerializeField] private Vector2 gridSpacing = new Vector2(50f, 50f);

        [MenuItem("Tools/UTT/Sa Bàn Builder")]
        public static void Open()
        {
            var w = GetWindow<SaBanBuilderWindow>("Sa Bàn Builder");
            w.minSize = new Vector2(440f, 600f);
        }

        private void OnEnable()
        {
            AutoPickTarget();
        }

        private void OnSelectionChange()
        {
            AutoPickTarget();
            Repaint();
        }

        private void AutoPickTarget()
        {
            GameObject sel = Selection.activeGameObject;
            if (sel != null)
            {
                SaBanRoot r = sel.GetComponentInParent<SaBanRoot>();
                if (r != null) target = r;
            }
            if (target == null) target = Object.FindFirstObjectByType<SaBanRoot>();
        }

        // =====================================================================
        //  GIAO DIỆN
        // =====================================================================

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space(4);
            target = (SaBanRoot)EditorGUILayout.ObjectField("Sa Bàn đang chỉnh", target, typeof(SaBanRoot), true);
            if (target == null)
                EditorGUILayout.HelpBox("Chưa có Sa Bàn trong scene. Ở tab 1 bấm \"Tạo Sa Bàn mới\" (hoặc chọn một Sa Bàn có sẵn).", MessageType.Info);

            tab = GUILayout.Toolbar(tab, TabNames);
            EditorGUILayout.Space(6);

            switch (tab)
            {
                case 0: DrawTabTable(); break;
                case 1: DrawTabConvert(); break;
                case 2: DrawTabImport(); break;
                case 3: DrawTabArrange(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------- TAB 1
        private void DrawTabTable()
        {
            EditorGUILayout.LabelField("BÀN SA BÀN", EditorStyles.boldLabel);
            if (target == null) newName = EditorGUILayout.TextField("Tên Sa Bàn", newName);
            tableSize = EditorGUILayout.Vector2Field("Kích thước mặt bàn (rộng x sâu, mét)", tableSize);
            tableHeight = EditorGUILayout.FloatField("Độ cao mặt bàn (mét)", tableHeight);
            boardThickness = EditorGUILayout.FloatField("Độ dày mặt bàn", boardThickness);
            legSize = EditorGUILayout.FloatField("Cỡ chân bàn", legSize);
            tableColor = EditorGUILayout.ColorField("Màu mặt bàn", tableColor);

            if (target == null)
            {
                if (GUILayout.Button("Tạo Sa Bàn mới", GUILayout.Height(30))) CreateNew();
                return;
            }

            if (GUILayout.Button("Dựng lại bàn theo thông số trên")) RebuildTable();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("TỈ LỆ", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Tỉ lệ hiện tại", "1 : " + Mathf.RoundToInt(1f / Mathf.Max(1e-7f, target.modelScale)).ToString("N0")
                                                           + "   (Model Scale = " + target.modelScale.ToString("0.#######") + ")");
            scaleDenominator = EditorGUILayout.FloatField("Đổi tỉ lệ thành 1 : N", scaleDenominator);
            if (GUILayout.Button("Áp dụng tỉ lệ 1 : N"))
            {
                Undo.RecordObject(target, "Đổi tỉ lệ Sa Bàn");
                target.SetModelScale(1f / Mathf.Max(1f, scaleDenominator));
                MarkDirty();
            }

            paddingPercent = EditorGUILayout.Slider("Chừa lề mép bàn (%)", paddingPercent, 0f, 30f);
            fitRouteOnly = EditorGUILayout.ToggleLeft("Vừa khít theo TUYẾN RAY (bỏ chọn = theo toàn bộ nội dung, kể cả núi/địa hình)", fitRouteOnly);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Vừa khít mặt bàn (tự chọn tỉ lệ)"))
                {
                    if (SaBanEditorUtil.FitToTable(target, fitRouteOnly, paddingPercent, true))
                    {
                        scaleDenominator = Mathf.Round(1f / target.modelScale);
                        MarkDirty();
                    }
                    else EditorUtility.DisplayDialog("Sa Bàn", "Chưa có nội dung (hoặc tuyến ray) để đo. Hãy chuyển scene hoặc thêm model trước.", "OK");
                }
                if (GUILayout.Button("Chỉ căn giữa (giữ tỉ lệ)"))
                {
                    if (SaBanEditorUtil.RecenterContent(target, fitRouteOnly)) MarkDirty();
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("TÀU", EditorStyles.boldLabel);
            SaBanTrainRunner runner = target.trainRunner;
            if (runner == null)
            {
                EditorGUILayout.HelpBox("Chưa có SaBanTrainRunner.", MessageType.Warning);
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                float journey = EditorGUILayout.FloatField("Thời gian chạy hết tuyến ở x1 (giây)", runner.journeySecondsAt1x);
                float start = EditorGUILayout.Slider("Vị trí xuất phát (0..1)", runner.startProgress, 0f, 1f);
                bool loop = EditorGUILayout.Toggle("Chạy vòng lại khi tới cuối", runner.loop);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(runner, "Chỉnh tàu Sa Bàn");
                    runner.journeySecondsAt1x = Mathf.Max(5f, journey);
                    runner.startProgress = start;
                    runner.loop = loop;
                    runner.Rebuild();
                    runner.PlaceAtStart();
                    MarkDirty();
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("BẢNG ĐIỀU KHIỂN NỔI", EditorStyles.boldLabel);
            panelAutoPosition = EditorGUILayout.ToggleLeft("Tự đặt phía trên bàn", panelAutoPosition);
            if (panelAutoPosition) panelHeightAboveTable = EditorGUILayout.FloatField("Cao hơn mặt bàn (mét)", panelHeightAboveTable);
            else panelCustomPosition = EditorGUILayout.Vector3Field("Vị trí (so với gốc Sa Bàn)", panelCustomPosition);
            panelFacePlayer = EditorGUILayout.ToggleLeft("Bảng luôn xoay về phía người chơi", panelFacePlayer);
            EditorGUI.BeginChangeCheck();
            panelSizeMultiplier = EditorGUILayout.Slider("Cỡ bảng", panelSizeMultiplier, 0.1f, 100f);
            if (EditorGUI.EndChangeCheck() && target != null && target.panel != null)
            {
                Undo.RecordObject(target.panel, "Đổi cỡ bảng điều khiển");
                target.panel.localScale = Vector3.one * SaBanPanelFactory.PixelToMeter * panelSizeMultiplier;
                MarkDirty();
            }
            if (GUILayout.Button(target.panel == null ? "Tạo bảng điều khiển" : "Cập nhật bảng (sau khi thêm lớp mới)", GUILayout.Height(28)))
                BuildPanel();
            EditorGUILayout.HelpBox(
                "Bảng là một GameObject con của Sa Bàn tên \"SaBan_ControlPanel\" - bạn có thể chọn nó rồi kéo đi chỗ khác, xoay, đổi cỡ bằng công cụ Move/Rotate/Scale của Unity. Nó vẫn dính theo Sa Bàn.\n" +
                "Ở góc nhìn thứ nhất: ngắm tâm vào nút rồi bấm E.", MessageType.None);
        }

        private void CreateNew()
        {
            Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            pos.y = 0f;
            target = SaBanEditorUtil.CreateRoot(newName, pos);
            SaBanEditorUtil.BuildTable(target, tableSize, tableHeight, boardThickness, legSize, tableColor);
            Selection.activeGameObject = target.gameObject;
            MarkDirty();
        }

        private void RebuildTable()
        {
            SaBanEditorUtil.BuildTable(target, tableSize, tableHeight, boardThickness, legSize, tableColor);
            MarkDirty();
        }

        private void BuildPanel()
        {
            Vector3 pos = panelAutoPosition
                ? new Vector3(0f, target.tableTopHeight + panelHeightAboveTable, 0f)
                : panelCustomPosition;

            Transform existing = target.panel != null ? target.panel : target.transform.Find(SaBanPanelFactory.PanelName);
            bool isNew = existing == null;
            Transform panel = SaBanPanelFactory.Build(target, existing, pos, true);
            if (isNew) Undo.RegisterCreatedObjectUndo(panel.gameObject, "Tạo bảng điều khiển Sa Bàn");
            panel.localScale = Vector3.one * SaBanPanelFactory.PixelToMeter * panelSizeMultiplier;

            target.panel = panel;
            SaBanControlPanel ctl = panel.GetComponent<SaBanControlPanel>();
            if (ctl != null) ctl.faceLocalPlayer = panelFacePlayer;
            Selection.activeGameObject = panel.gameObject;
            MarkDirty();
        }

        // ---------------------------------------------------------------- TAB 2
        private void DrawTabConvert()
        {
            EditorGUILayout.HelpBox(
                "Chọn một scene (ví dụ YenTuHaLongXanh) → Quét → chỉnh danh sách → Chuyển đổi.\n" +
                "Các vật thể có trong scene sẽ được SAO CHÉP vào Sa Bàn (scene gốc không bị sửa). " +
                "Ray, trụ cầu, cột điện, hầm, núi, nhà ga do streamer sinh lúc chạy sẽ được \"bake\" thành vật thể tĩnh cho toàn tuyến.",
                MessageType.Info);

            sourceScene = (SceneAsset)EditorGUILayout.ObjectField("Scene nguồn", sourceScene, typeof(SceneAsset), false);
            using (new EditorGUI.DisabledScope(sourceScene == null))
            {
                if (GUILayout.Button("Quét scene", GUILayout.Height(26))) ScanSource();
            }

            if (items.Count == 0)
            {
                EditorGUILayout.LabelField("(chưa quét)");
                return;
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Đối tượng gốc trong scene", EditorStyles.boldLabel);
            foreach (SourceItem it in items)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    it.include = EditorGUILayout.Toggle(it.include, GUILayout.Width(18));
                    EditorGUILayout.LabelField(it.name, GUILayout.MinWidth(120));
                    if (it.isRoute || it.isTrain)
                        EditorGUILayout.LabelField(it.isTrain ? "→ TÀU" : "→ TUYẾN RAY", GUILayout.Width(110));
                    else
                        it.kind = (SaBanLayerKind)EditorGUILayout.EnumPopup(it.kind, GUILayout.Width(110));
                }
                if (!string.IsNullOrEmpty(it.note))
                    EditorGUILayout.LabelField("      " + it.note, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(6);
            if (_routes.Count > 0)
            {
                var names = new string[_routes.Count];
                for (int i = 0; i < _routes.Count; i++) names[i] = _routes[i] != null ? _routes[i].gameObject.name : "(mất)";
                routePick = Mathf.Clamp(routePick, 0, names.Length - 1);
                routePick = EditorGUILayout.Popup("Tuyến ray dùng cho Sa Bàn", routePick, names);
            }
            else EditorGUILayout.HelpBox("Không thấy SplineContainer (tuyến ray) trong scene nguồn. Tàu và bake sẽ bị bỏ qua.", MessageType.Warning);
            if (!string.IsNullOrEmpty(_routeSummary)) EditorGUILayout.LabelField(_routeSummary, EditorStyles.miniLabel);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Bake streamer → vật thể tĩnh", EditorStyles.boldLabel);
            bake.rails = EditorGUILayout.ToggleLeft("Ray (đốt ray)", bake.rails);
            bake.piers = EditorGUILayout.ToggleLeft("Trụ cầu cạn", bake.piers);
            bake.catenary = EditorGUILayout.ToggleLeft("Cột điện", bake.catenary);
            bake.tunnels = EditorGUILayout.ToggleLeft("Hầm", bake.tunnels);
            bake.stations = EditorGUILayout.ToggleLeft("Nhà ga đầu / cuối", bake.stations);
            bake.scenery = EditorGUILayout.ToggleLeft("Núi / cây", bake.scenery);
            bake.railStride = Mathf.Max(1, EditorGUILayout.IntField("Ray: cứ N đốt đặt 1 (nhẹ hơn khi lớn)", bake.railStride));
            bake.tunnelStride = Mathf.Max(1, EditorGUILayout.IntField("Hầm: cứ N đốt đặt 1", bake.tunnelStride));
            bake.sceneryIntervalMeters = Mathf.Max(5f, EditorGUILayout.FloatField("Núi: cách nhau (mét thật)", bake.sceneryIntervalMeters));
            bake.maxObjectsPerType = Mathf.Max(100, EditorGUILayout.IntField("Giới hạn số vật thể mỗi loại", bake.maxObjectsPerType));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("SCALE RIÊNG TỪNG LỚP (1 = giữ nguyên, tối đa 100)", EditorStyles.boldLabel);
            bake.railScale = EditorGUILayout.Slider("Cỡ ray", bake.railScale, 0.1f, 100f);
            bake.pierScale = EditorGUILayout.Slider("Cỡ trụ cầu", bake.pierScale, 0.1f, 100f);
            bake.catenaryScale = EditorGUILayout.Slider("Cỡ cột điện", bake.catenaryScale, 0.1f, 100f);
            bake.tunnelScale = EditorGUILayout.Slider("Cỡ hầm", bake.tunnelScale, 0.1f, 100f);
            bake.stationScale = EditorGUILayout.Slider("Cỡ nhà ga", bake.stationScale, 0.1f, 100f);
            bake.sceneryScale = EditorGUILayout.Slider("Cỡ núi / cây", bake.sceneryScale, 0.1f, 100f);
            trainScaleMultiplier = EditorGUILayout.Slider("Cỡ tàu", trainScaleMultiplier, 0.1f, 100f);
            terrainScaleMultiplier = EditorGUILayout.Slider("Cỡ địa hình / chunk", terrainScaleMultiplier, 0.1f, 100f);

            fitAfterConvert = EditorGUILayout.ToggleLeft("Tự vừa khít mặt bàn sau khi chuyển", fitAfterConvert);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("CHUYỂN ĐỔI VÀO SA BÀN", GUILayout.Height(32))) Convert();
                if (GUILayout.Button("Xóa nội dung Sa Bàn", GUILayout.Height(32))) ClearContent();
            }
            if (_openedByTool && GUILayout.Button("Đóng scene nguồn")) CloseSource();
            if (!string.IsNullOrEmpty(_lastReport)) EditorGUILayout.HelpBox(_lastReport, MessageType.None);
        }

        private void ScanSource()
        {
            string path = AssetDatabase.GetAssetPath(sourceScene);
            _sourceHandle = SceneManager.GetSceneByPath(path);
            _openedByTool = false;
            if (!_sourceHandle.isLoaded)
            {
                _sourceHandle = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                _openedByTool = true;
            }

            // Tự động tìm và mở toàn bộ các scene con RuntimeZones nếu có
            CloseZoneScenes();
            string zoneFolder = "Assets/ProjectLopHoc/Generated/RuntimeZones";
            if (System.IO.Directory.Exists(zoneFolder))
            {
                string[] zoneFiles = System.IO.Directory.GetFiles(zoneFolder, "Zone_*.unity");
                System.Array.Sort(zoneFiles);
                for (int i = 0; i < zoneFiles.Length; i++)
                {
                    string zPath = zoneFiles[i].Replace('\\', '/');
                    Scene zs = SceneManager.GetSceneByPath(zPath);
                    if (!zs.isLoaded)
                    {
                        zs = EditorSceneManager.OpenScene(zPath, OpenSceneMode.Additive);
                        _openedZoneScenes.Add(zs);
                    }
                }
            }

            items.Clear();
            _routes.Clear();

            var allRoots = new List<GameObject>();
            if (_sourceHandle.IsValid() && _sourceHandle.isLoaded)
                allRoots.AddRange(_sourceHandle.GetRootGameObjects());
            foreach (Scene zs in _openedZoneScenes)
            {
                if (zs.IsValid() && zs.isLoaded)
                    allRoots.AddRange(zs.GetRootGameObjects());
            }

            foreach (GameObject go in allRoots)
            {
                if (go == null) continue;
                // Nếu là cụm RuntimeZoneContent của phân vùng, mở bung các phần tử con ra
                if (go.name.StartsWith("RuntimeZoneContent"))
                {
                    foreach (Transform child in go.transform)
                    {
                        GameObject cgo = child.gameObject;
                        Classify(cgo, out bool inc, out SaBanLayerKind k, out bool isR, out bool isT, out string nt);
                        items.Add(new SourceItem { name = cgo.name, include = inc, kind = k, isRoute = isR, isTrain = isT, note = nt, go = cgo });
                    }
                    continue;
                }

                Classify(go, out bool include, out SaBanLayerKind kind, out bool isRoute, out bool isTrain, out string note);
                items.Add(new SourceItem { name = go.name, include = include, kind = kind, isRoute = isRoute, isTrain = isTrain, note = note, go = go });
            }

            foreach (GameObject go in allRoots)
            {
                if (go == null) continue;
                foreach (SplineContainer c in go.GetComponentsInChildren<SplineContainer>(true))
                    if (!_routes.Contains(c)) _routes.Add(c);
            }

            // Chọn tuyến mặc định: tuyến mà DynamicTrackStreamer hoặc TrainSplineMover đang dùng
            routePick = 0;
            var track = SaBanBaker.FindFirst<DynamicTrackStreamer>(allRoots.ToArray());
            var mover = SaBanBaker.FindFirst<TrainSplineMover>(allRoots.ToArray());
            SplineContainer preferred = track != null ? track.splineContainer : (mover != null ? mover.splineRoute : null);
            if (preferred != null)
            {
                int idx = _routes.IndexOf(preferred);
                if (idx >= 0) routePick = idx;
            }

            _routeSummary = "Tìm thấy: " + _routes.Count + " tuyến ray | " + _openedZoneScenes.Count + " phân vùng môi trường (Zone) | " +
                (track != null ? "DynamicTrackStreamer " : "") +
                (SaBanBaker.FindFirst<TunnelStreamer>(allRoots.ToArray()) != null ? "TunnelStreamer " : "") +
                (SaBanBaker.FindFirst<StationStreamer>(allRoots.ToArray()) != null ? "StationStreamer " : "") +
                (SaBanBaker.FindFirst<SceneryStreamer>(allRoots.ToArray()) != null ? "SceneryStreamer " : "") +
                (mover != null ? "TrainSplineMover" : "");
        }

        private static void Classify(GameObject go, out bool include, out SaBanLayerKind kind, out bool isRoute, out bool isTrain, out string note)
        {
            include = true;
            note = "";
            bool hasRenderer = go.GetComponentInChildren<Renderer>(true) != null || go.GetComponentInChildren<Terrain>(true) != null;
            isRoute = go.GetComponentInChildren<SplineContainer>(true) != null && !hasRenderer;
            isTrain = go.GetComponentInChildren<TrainSplineMover>(true) != null;
            kind = KindByName(go.name);

            if (isTrain) { kind = SaBanLayerKind.Train; note = "Tàu → tạo bản thu nhỏ chạy trên ray"; return; }
            if (isRoute) { kind = SaBanLayerKind.Rail; note = "Tuyến ray (SplineContainer) - dùng để tàu chạy"; return; }

            bool streamer = go.GetComponentInChildren<DynamicTrackStreamer>(true) != null ||
                            go.GetComponentInChildren<TunnelStreamer>(true) != null ||
                            go.GetComponentInChildren<StationStreamer>(true) != null ||
                            go.GetComponentInChildren<SceneryStreamer>(true) != null;
            if (streamer) { include = false; note = "Bộ sinh động (streamer) → sẽ được bake thành vật thể tĩnh, không sao chép trực tiếp"; return; }

            if (go.GetComponentInChildren<Camera>(true) != null || go.GetComponentInChildren<AudioListener>(true) != null ||
                go.GetComponentInChildren<EventSystem>(true) != null || go.GetComponentInChildren<Canvas>(true) != null)
            { include = false; note = "Camera / UI / hệ thống → bỏ qua"; return; }

            if (go.GetComponentInChildren<Light>(true) != null && !hasRenderer) { include = false; note = "Đèn → bỏ qua"; return; }

            string n = go.name.ToLowerInvariant();
            if (n.Contains("spawner") || n.Contains("manager") || n.Contains("crosshair") || n.Contains("uiinfo"))
            { include = false; note = "Đối tượng hệ thống → bỏ qua"; return; }
        }

        private static SaBanLayerKind KindByName(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("chunk") || n.Contains("terrain") || n.Contains("dia hinh") || n.Contains("diahinh") || n.Contains("ground") || n.Contains("slope") || n.Contains("boke") || n.Contains("subgrade") || n.Contains("road")) return SaBanLayerKind.Terrain;
            if (n.Contains("water") || n.Contains("river") || n.Contains("song") || n.Contains("mountain") || n.Contains("hill") || n.Contains("tree") || n.Contains("rock") || n.Contains("nui") || n.Contains("cay") || n.Contains("vegetation")) return SaBanLayerKind.Nature;
            if (n.Contains("rail") || n.Contains("ray") || n.Contains("pole") || n.Contains("catenary") || n.Contains("wire")) return SaBanLayerKind.Rail;
            if (n.Contains("bridge") || n.Contains("cau")) return SaBanLayerKind.Bridge;
            if (n.Contains("tunnel") || n.Contains("ham")) return SaBanLayerKind.Tunnel;
            if (n.Contains("station") || n.Contains("nhaga") || n.Contains("nha ga")) return SaBanLayerKind.Station;
            if (n.Contains("train") || n.Contains("tau")) return SaBanLayerKind.Train;
            return SaBanLayerKind.Custom;
        }

        private void CloseZoneScenes()
        {
            if (_openedZoneScenes != null)
            {
                foreach (Scene zs in _openedZoneScenes)
                {
                    if (zs.IsValid() && zs.isLoaded)
                        EditorSceneManager.CloseScene(zs, true);
                }
                _openedZoneScenes.Clear();
            }
        }

        private void CloseSource()
        {
            if (_sourceHandle.IsValid() && _sourceHandle.isLoaded && _openedByTool)
                EditorSceneManager.CloseScene(_sourceHandle, true);
            CloseZoneScenes();
            _openedByTool = false;
            items.Clear();
            _routes.Clear();
        }

        private void OnDestroy()
        {
            if (_openedByTool && _sourceHandle.IsValid() && _sourceHandle.isLoaded)
                EditorSceneManager.CloseScene(_sourceHandle, true);
            CloseZoneScenes();
        }

        private void Convert()
        {
            foreach (SourceItem it in items)
            {
                if (it.go == null)
                {
                    EditorUtility.DisplayDialog("Sa Bàn", "Dữ liệu quét đã cũ (Unity vừa biên dịch lại). Hãy bấm \"Quét scene\" lần nữa.", "OK");
                    return;
                }
            }
            if (target == null) CreateNew();

            Scene dest = target.gameObject.scene;
            var allRoots = new List<GameObject>();
            if (_sourceHandle.IsValid() && _sourceHandle.isLoaded)
                allRoots.AddRange(_sourceHandle.GetRootGameObjects());
            foreach (Scene zs in _openedZoneScenes)
            {
                if (zs.IsValid() && zs.isLoaded)
                    allRoots.AddRange(zs.GetRootGameObjects());
            }
            GameObject[] roots = allRoots.ToArray();
            SplineContainer srcRoute = (_routes.Count > 0 && routePick < _routes.Count) ? _routes[routePick] : null;

            try
            {
                EditorUtility.DisplayProgressBar("Chuyển Sa Bàn", "Đang sao chép đối tượng...", 0.05f);

                // 1. Tuyến ray: tạo bản chỉ có SplineContainer
                if (srcRoute != null) CloneRoute(srcRoute, dest);

                // 2. Đối tượng thường
                int step = 0;
                foreach (SourceItem it in items)
                {
                    step++;
                    if (!it.include || it.isRoute || it.isTrain) continue;
                    EditorUtility.DisplayProgressBar("Chuyển Sa Bàn", "Sao chép " + it.name, 0.1f + 0.3f * step / items.Count);
                    SaBanLayer layer = SaBanEditorUtil.GetOrCreateLayer(target, it.kind, importCustomKey, importCustomLabel);
                    CopyIntoLayer(it.go, layer.transform, dest, it.kind);
                }

                // 3. Tàu
                foreach (SourceItem it in items)
                    if (it.include && it.isTrain) BuildTrain(it.go, dest);

                // 4. Bake streamer
                BakeReport report = srcRoute != null ? SaBanBaker.Bake(target, roots, srcRoute, bake) : new BakeReport { notes = "Không có tuyến ray nên bỏ qua bake." };
                _lastReport = "ĐÃ CHUYỂN. " + report;

                // 5. Hoàn thiện
                if (target.trainRunner != null)
                {
                    target.trainRunner.route = target.route;
                    target.trainRunner.Rebuild();
                }
                if (fitAfterConvert && SaBanEditorUtil.FitToTable(target, fitRouteOnly, paddingPercent, true))
                    scaleDenominator = Mathf.Round(1f / target.modelScale);
                else SaBanEditorUtil.RecenterContent(target, fitRouteOnly);

                if (target.trainRunner != null) target.trainRunner.PlaceAtStart();
                BuildPanel();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            MarkDirty();
        }

        private void CloneRoute(SplineContainer srcRoute, Scene dest)
        {
            if (target.route != null) Undo.DestroyObjectImmediate(target.route.gameObject);

            GameObject clone = Object.Instantiate(srcRoute.gameObject);
            clone.name = "Route";
            SceneManager.MoveGameObjectToScene(clone, dest);
            Undo.RegisterCreatedObjectUndo(clone, "Sao chép tuyến ray");

            // Giữ lại duy nhất SplineContainer
            SaBanEditorUtil.ClearChildren(clone.transform);
            foreach (Component c in clone.GetComponents<Component>())
            {
                if (c == null || c is Transform || c is SplineContainer) continue;
                Object.DestroyImmediate(c);
            }

            Transform src = srcRoute.transform;
            clone.transform.SetParent(target.content, false);
            clone.transform.localPosition = src.position;
            clone.transform.localRotation = src.rotation;
            clone.transform.localScale = src.lossyScale;

            target.route = clone.GetComponent<SplineContainer>();
        }

        private void CopyIntoLayer(GameObject source, Transform layer, Scene dest, SaBanLayerKind kind)
        {
            GameObject clone = Object.Instantiate(source);
            clone.name = source.name;
            SceneManager.MoveGameObjectToScene(clone, dest);
            Undo.RegisterCreatedObjectUndo(clone, "Sao chép " + source.name);
            SaBanEditorUtil.StripGameplay(clone);

            Transform s = source.transform;
            clone.transform.SetParent(layer, false);
            clone.transform.localPosition = s.position;
            clone.transform.localRotation = s.rotation;

            float scaleMult = 1f;
            if (kind == SaBanLayerKind.Terrain) scaleMult = terrainScaleMultiplier;
            else if (kind == SaBanLayerKind.Rail) scaleMult = bake.railScale;
            else if (kind == SaBanLayerKind.Bridge) scaleMult = bake.pierScale;
            else if (kind == SaBanLayerKind.Tunnel) scaleMult = bake.tunnelScale;
            else if (kind == SaBanLayerKind.Station) scaleMult = bake.stationScale;
            else if (kind == SaBanLayerKind.Nature) scaleMult = bake.sceneryScale;

            clone.transform.localScale = !Mathf.Approximately(scaleMult, 1f) ? s.localScale * scaleMult : s.localScale;
        }

        private void BuildTrain(GameObject source, Scene dest)
        {
            TrainSplineMover mover = source.GetComponentInChildren<TrainSplineMover>(true);
            if (mover == null) return;

            SaBanLayer layer = SaBanEditorUtil.GetOrCreateLayer(target, SaBanLayerKind.Train);
            SaBanEditorUtil.ClearChildren(layer.transform);

            int[] frontPath = SaBanEditorUtil.PathFrom(source.transform, mover.frontCar);
            int[] rearPath = SaBanEditorUtil.PathFrom(source.transform, mover.rearCar);

            GameObject clone = Object.Instantiate(source);
            clone.name = source.name;
            SceneManager.MoveGameObjectToScene(clone, dest);
            Undo.RegisterCreatedObjectUndo(clone, "Sao chép tàu");
            SaBanEditorUtil.StripGameplay(clone);

            Transform s = source.transform;
            clone.transform.SetParent(layer.transform, false);
            clone.transform.localPosition = s.position;
            clone.transform.localRotation = s.rotation;
            clone.transform.localScale = !Mathf.Approximately(trainScaleMultiplier, 1f)
                ? s.localScale * trainScaleMultiplier
                : s.localScale;

            Transform front = SaBanEditorUtil.FindByPath(clone.transform, frontPath) ?? clone.transform;
            Transform rear = SaBanEditorUtil.FindByPath(clone.transform, rearPath);

            SaBanTrainRunner runner = target.trainRunner;
            if (runner == null) runner = target.trainRunner = target.gameObject.AddComponent<SaBanTrainRunner>();
            Undo.RecordObject(runner, "Cấu hình tàu Sa Bàn");
            if (rear != null && rear != front)
            {
                runner.cars = new[] { front, rear };
                runner.carBackOffsets = new[] { 0f, mover.carSpacing * trainScaleMultiplier };
                runner.carHeights = new[] { mover.frontHeightOffset * trainScaleMultiplier, (mover.frontHeightOffset + mover.rearYOffset) * trainScaleMultiplier };
            }
            else
            {
                runner.cars = new[] { front };
                runner.carBackOffsets = new[] { 0f };
                runner.carHeights = new[] { mover.frontHeightOffset * trainScaleMultiplier };
            }
        }

        private void ClearContent()
        {
            if (target == null) return;
            if (!EditorUtility.DisplayDialog("Xóa nội dung Sa Bàn", "Xóa toàn bộ vật thể trong các lớp (bàn và bảng điều khiển giữ nguyên)?", "Xóa", "Hủy")) return;

            foreach (SaBanLayer layer in target.GetLayers()) SaBanEditorUtil.ClearChildren(layer.transform);
            if (target.route != null) Object.DestroyImmediate(target.route.gameObject);
            target.route = null;
            if (target.trainRunner != null)
            {
                target.trainRunner.cars = new Transform[0];
                target.trainRunner.route = null;
            }
            _lastReport = "";
            MarkDirty();
        }

        // ---------------------------------------------------------------- TAB 3
        private void DrawTabImport()
        {
            if (target == null) { EditorGUILayout.HelpBox("Cần có Sa Bàn trước (tab 1).", MessageType.Warning); return; }

            EditorGUILayout.HelpBox(
                "Thêm prefab hoặc model (FBX...) vào Sa Bàn.\n" +
                "Mặc định model được coi là KÍCH THƯỚC THẬT (mét) và tự thu nhỏ theo tỉ lệ Sa Bàn - phù hợp với nhà ga, cầu, cây... có kích thước như ngoài đời. " +
                "Nếu model của bạn vốn đã làm nhỏ sẵn cho sa bàn, tick \"Giữ kích thước theo bàn\".", MessageType.Info);

            EditorGUILayout.LabelField("Danh sách thêm", EditorStyles.boldLabel);
            for (int i = 0; i < importList.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    importList[i] = (GameObject)EditorGUILayout.ObjectField(importList[i], typeof(GameObject), false);
                    if (GUILayout.Button("X", GUILayout.Width(24))) { importList.RemoveAt(i); i--; }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Thêm ô trống")) importList.Add(null);
                if (GUILayout.Button("Lấy từ mục đang chọn trong Project"))
                {
                    foreach (Object o in Selection.objects)
                    {
                        if (o is GameObject g && EditorUtility.IsPersistent(g) && !importList.Contains(g)) importList.Add(g);
                    }
                }
            }

            EditorGUILayout.Space(6);
            importKind = (SaBanLayerKind)EditorGUILayout.EnumPopup("Thêm vào lớp", importKind);
            if (importKind == SaBanLayerKind.Custom)
            {
                importCustomKey = EditorGUILayout.TextField("Mã lớp (không dấu)", importCustomKey);
                importCustomLabel = EditorGUILayout.TextField("Tên nút (hiện trên bảng)", importCustomLabel);
            }
            importPlacement = (ImportPlacement)EditorGUILayout.EnumPopup("Đặt tại", importPlacement);
            if (importPlacement == ImportPlacement.AlongRoute)
                importRouteDistance = EditorGUILayout.FloatField("Khoảng cách dọc ray (mét thật)", importRouteDistance);
            importKeepTableSize = EditorGUILayout.ToggleLeft("Giữ kích thước theo bàn (không bị thu nhỏ theo tỉ lệ)", importKeepTableSize);
            importScaleMultiplier = EditorGUILayout.FloatField("Nhân thêm kích thước", importScaleMultiplier);
            importSpread = EditorGUILayout.FloatField("Giãn cách giữa các model (đơn vị gốc)", importSpread);

            if (GUILayout.Button("THÊM VÀO SA BÀN", GUILayout.Height(30))) ImportAll();
        }

        private void ImportAll()
        {
            SaBanLayer layer = SaBanEditorUtil.GetOrCreateLayer(target, importKind, importCustomKey, importCustomLabel);
            var created = new List<Object>();

            Vector3 baseWorld;
            switch (importPlacement)
            {
                case ImportPlacement.SceneViewPivot:
                    baseWorld = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : target.transform.position;
                    break;
                case ImportPlacement.AlongRoute:
                    baseWorld = target.transform.TransformPoint(0f, target.tableTopHeight, 0f);
                    if (target.route != null)
                    {
                        var s = new SaBanRouteSampler(target.route);
                        if (s.IsValid) s.EvaluateWorld(importRouteDistance, out baseWorld, out _, out _);
                    }
                    break;
                default:
                    baseWorld = target.transform.TransformPoint(0f, target.tableTopHeight, 0f);
                    break;
            }
            Vector3 baseLocal = target.content.InverseTransformPoint(baseWorld);

            int n = 0;
            foreach (GameObject asset in importList)
            {
                if (asset == null) continue;
                GameObject go = PrefabUtility.IsPartOfPrefabAsset(asset)
                    ? (GameObject)PrefabUtility.InstantiatePrefab(asset, layer.transform)
                    : Object.Instantiate(asset, layer.transform);
                Undo.RegisterCreatedObjectUndo(go, "Thêm model vào Sa Bàn");

                go.transform.localPosition = baseLocal + new Vector3(n * importSpread, 0f, 0f);
                Vector3 prefabScale = go.transform.localScale;
                float mult = Mathf.Max(0.0001f, importScaleMultiplier);
                go.transform.localScale = importKeepTableSize
                    ? prefabScale * mult / Mathf.Max(1e-7f, target.modelScale)
                    : prefabScale * mult;
                created.Add(go);
                n++;
            }
            if (created.Count > 0) Selection.objects = created.ToArray();
            MarkDirty();
        }

        // ---------------------------------------------------------------- TAB 4
        private void DrawTabArrange()
        {
            if (target == null) { EditorGUILayout.HelpBox("Cần có Sa Bàn trước (tab 1).", MessageType.Warning); return; }

            EditorGUILayout.HelpBox(
                "Chọn vật thể trong Hierarchy rồi bấm nút bên dưới. Ngoài ra có thể kéo trực tiếp bằng Move/Rotate/Scale của Unity (kéo vật thể là con của Sa Bàn).\n" +
                "Mọi khoảng cách tính bằng MÉT THẬT của scene gốc (tự quy đổi theo tỉ lệ Sa Bàn).", MessageType.Info);
            EditorGUILayout.LabelField("Đang chọn: " + Selection.gameObjects.Length + " vật thể");

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("ĐẶT DỌC RAY", EditorStyles.boldLabel);
            routeStart = EditorGUILayout.FloatField("Bắt đầu tại (mét)", routeStart);
            routeSpacing = EditorGUILayout.FloatField("Khoảng cách giữa các vật (mét)", routeSpacing);
            routeLateral = EditorGUILayout.FloatField("Lệch ngang khỏi ray (mét, + là bên phải)", routeLateral);
            routeHeight = EditorGUILayout.FloatField("Nâng cao (mét)", routeHeight);
            routeAlternate = EditorGUILayout.ToggleLeft("Xen kẽ trái / phải", routeAlternate);
            routeAlign = EditorGUILayout.ToggleLeft("Xoay theo hướng ray", routeAlign);
            routeYaw = EditorGUILayout.FloatField("Xoay thêm quanh trục đứng (độ)", routeYaw);
            routeRandomYaw = EditorGUILayout.ToggleLeft("Xoay ngẫu nhiên", routeRandomYaw);
            routeRandomScale = EditorGUILayout.Vector2Field("Cỡ ngẫu nhiên (min, max)", routeRandomScale);
            if (GUILayout.Button("Đặt các vật đang chọn dọc ray")) PlaceSelectionAlongRoute();

            using (new EditorGUILayout.HorizontalScope())
            {
                cloneCount = Mathf.Max(1, EditorGUILayout.IntField("Số bản nhân", cloneCount));
                if (GUILayout.Button("Nhân bản dọc ray (từ vật đầu tiên đang chọn)")) CloneAlongRoute();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("XẾP LƯỚI", EditorStyles.boldLabel);
            gridColumns = Mathf.Max(1, EditorGUILayout.IntField("Số cột", gridColumns));
            gridSpacing = EditorGUILayout.Vector2Field("Khoảng cách (X, Z) đơn vị gốc", gridSpacing);
            if (GUILayout.Button("Xếp lưới các vật đang chọn (quanh vật đầu tiên)")) ArrangeGrid();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("ĐỔI LỚP", EditorStyles.boldLabel);
            moveKind = (SaBanLayerKind)EditorGUILayout.EnumPopup("Chuyển vào lớp", moveKind);
            if (moveKind == SaBanLayerKind.Custom)
            {
                moveCustomKey = EditorGUILayout.TextField("Mã lớp (không dấu)", moveCustomKey);
                moveCustomLabel = EditorGUILayout.TextField("Tên nút", moveCustomLabel);
            }
            if (GUILayout.Button("Chuyển các vật đang chọn vào lớp này")) MoveSelectionToLayer();
        }

        private SaBanRouteSampler Sampler()
        {
            if (target.route == null)
            {
                EditorUtility.DisplayDialog("Sa Bàn", "Sa Bàn chưa có tuyến ray. Hãy chuyển scene (tab 2) trước.", "OK");
                return null;
            }
            var s = new SaBanRouteSampler(target.route);
            if (!s.IsValid)
            {
                EditorUtility.DisplayDialog("Sa Bàn", "Tuyến ray rỗng hoặc không hợp lệ.", "OK");
                return null;
            }
            return s;
        }

        private void PlaceOnRoute(Transform t, SaBanRouteSampler s, float distance, float lateral, float height, bool align, float yaw)
        {
            s.EvaluateWorld(Mathf.Clamp(distance, 0f, s.Length), out Vector3 pos, out Vector3 tan, out Vector3 up);
            Vector3 right = Vector3.Cross(up, tan).normalized;
            float unit = target.route.transform.lossyScale.x;   // 1 mét thật = unit đơn vị thế giới
            Undo.RecordObject(t, "Đặt dọc ray");
            t.position = pos + right * (lateral * unit) + up * (height * unit);
            if (align) t.rotation = Quaternion.LookRotation(tan, up) * Quaternion.Euler(0f, yaw, 0f);
            else if (!Mathf.Approximately(yaw, 0f)) t.rotation = Quaternion.Euler(0f, yaw, 0f) * t.rotation;
        }

        private void PlaceSelectionAlongRoute()
        {
            SaBanRouteSampler s = Sampler();
            if (s == null) return;
            GameObject[] sel = Selection.gameObjects;
            for (int i = 0; i < sel.Length; i++)
            {
                float side = routeAlternate ? ((i % 2 == 0) ? 1f : -1f) : 1f;
                float yaw = routeYaw + (routeRandomYaw ? Random.Range(0f, 360f) : 0f);
                PlaceOnRoute(sel[i].transform, s, routeStart + i * routeSpacing, routeLateral * side, routeHeight, routeAlign, yaw);
                ApplyRandomScale(sel[i].transform);
            }
            MarkDirty();
        }

        private void CloneAlongRoute()
        {
            SaBanRouteSampler s = Sampler();
            if (s == null) return;
            GameObject template = Selection.activeGameObject;
            if (template == null) { EditorUtility.DisplayDialog("Sa Bàn", "Hãy chọn 1 vật thể mẫu trong Hierarchy.", "OK"); return; }

            var created = new List<Object>();
            Transform parent = template.transform.parent;
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(template);
            for (int i = 0; i < cloneCount; i++)
            {
                GameObject go = prefabAsset != null
                    ? (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, parent)
                    : Object.Instantiate(template, parent);
                Undo.RegisterCreatedObjectUndo(go, "Nhân bản dọc ray");
                go.transform.localScale = template.transform.localScale;

                float side = routeAlternate ? ((i % 2 == 0) ? 1f : -1f) : 1f;
                float yaw = routeYaw + (routeRandomYaw ? Random.Range(0f, 360f) : 0f);
                PlaceOnRoute(go.transform, s, routeStart + i * routeSpacing, routeLateral * side, routeHeight, routeAlign, yaw);
                ApplyRandomScale(go.transform);
                created.Add(go);
            }
            Selection.objects = created.ToArray();
            MarkDirty();
        }

        private void ApplyRandomScale(Transform t)
        {
            float lo = Mathf.Min(routeRandomScale.x, routeRandomScale.y);
            float hi = Mathf.Max(routeRandomScale.x, routeRandomScale.y);
            if (Mathf.Approximately(lo, 1f) && Mathf.Approximately(hi, 1f)) return;
            Undo.RecordObject(t, "Cỡ ngẫu nhiên");
            t.localScale *= Random.Range(lo, hi);
        }

        private void ArrangeGrid()
        {
            GameObject[] sel = Selection.gameObjects;
            if (sel.Length < 2) { EditorUtility.DisplayDialog("Sa Bàn", "Hãy chọn từ 2 vật thể trở lên.", "OK"); return; }
            Transform content = target.content;
            Vector3 origin = content.InverseTransformPoint(sel[0].transform.position);
            for (int i = 0; i < sel.Length; i++)
            {
                int row = i / gridColumns, col = i % gridColumns;
                Vector3 local = origin + new Vector3(col * gridSpacing.x, 0f, row * gridSpacing.y);
                Undo.RecordObject(sel[i].transform, "Xếp lưới");
                sel[i].transform.position = content.TransformPoint(local);
            }
            MarkDirty();
        }

        private void MoveSelectionToLayer()
        {
            SaBanLayer layer = SaBanEditorUtil.GetOrCreateLayer(target, moveKind, moveCustomKey, moveCustomLabel);
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go.GetComponent<SaBanLayer>() != null || go == target.gameObject) continue;
                Undo.SetTransformParent(go.transform, layer.transform, "Đổi lớp Sa Bàn");
            }
            MarkDirty();
        }

        private void MarkDirty()
        {
            if (target != null)
            {
                EditorUtility.SetDirty(target);
                EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            }
            Repaint();
        }
    }
}
