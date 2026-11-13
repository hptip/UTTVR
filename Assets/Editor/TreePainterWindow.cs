
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class TreePainterWindow : EditorWindow
{
    // =========================================================
    // AREA
    // =========================================================

    private GameObject treeArea;

    private int rows = 10;
    private int columns = 10;
    private float cellSize = 2f;

    private int pendingRows = 10;
    private int pendingColumns = 10;
    private float pendingCellSize = 2f;

    // =========================================================
    // TREE
    // =========================================================

    private GameObject treePrefab;

    // =========================================================
    // MODEL COLOR
    // =========================================================

    private Color modelColor = Color.white;
    private bool applyColorToPrefab = true;
    private bool createMaterialCopy = true;

    // =========================================================
    // GRID
    // =========================================================

    private bool showGrid = true;
    private bool showAxis = true;

    // =========================================================
    // TREE SETTINGS
    // =========================================================

    private float minScale = 0.8f;
    private float maxScale = 1.2f;
    private bool randomRotation = true;

    // =========================================================
    // GROUND
    // =========================================================

    private bool followGround = true;
    private float raycastHeight = 50f;

    // =========================================================
    // PAINT
    // =========================================================

    private bool paintMode = false;

    private HashSet<Vector2Int> selectedCells =
        new HashSet<Vector2Int>();

    private int lastPaintRow = -1;
    private int lastPaintColumn = -1;

    // =========================================================
    // MIRROR
    // =========================================================

    private enum MirrorMode
    {
        None,
        Horizontal,
        Vertical,
        Both
    }

    private MirrorMode mirrorMode = MirrorMode.None;

    // =========================================================
    // UI
    // =========================================================

    private Vector2 scrollPosition;

    private bool areaFoldout = true;
    private bool treeFoldout = true;
    private bool colorFoldout = true;
    private bool gridFoldout = true;
    private bool treeSettingsFoldout = true;
    private bool groundFoldout = true;
    private bool paintFoldout = true;
    private bool mirrorFoldout = true;

    // =========================================================
    // WINDOW
    // =========================================================

    [MenuItem("Tools/Tree Painter")]
    public static void ShowWindow()
    {
        TreePainterWindow window =
            GetWindow<TreePainterWindow>("Tree Painter");

        window.minSize = new Vector2(420f, 750f);
    }

    // =========================================================
    // ENABLE
    // =========================================================

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // =========================================================
    // GUI
    // =========================================================

    private void OnGUI()
    {
        EditorGUILayout.Space(5);

        // -----------------------------------------------------
        // HEADER
        // -----------------------------------------------------

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUILayout.Label(
            "TREE PAINTER",
            EditorStyles.boldLabel
        );

        GUILayout.Label(
            paintMode
                ? "PAINT MODE: ON"
                : "PAINT MODE: OFF",
            paintMode
                ? EditorStyles.boldLabel
                : EditorStyles.label
        );

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // -----------------------------------------------------
        // SCROLL
        // -----------------------------------------------------

        scrollPosition = EditorGUILayout.BeginScrollView(
            scrollPosition
        );

        // =====================================================
        // AREA
        // =====================================================

        areaFoldout = EditorGUILayout.Foldout(
            areaFoldout,
            "AREA",
            true
        );

        if (areaFoldout)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            treeArea = (GameObject)EditorGUILayout.ObjectField(
                "Tree Area",
                treeArea,
                typeof(GameObject),
                true
            );

            EditorGUILayout.Space(3);

            pendingRows = EditorGUILayout.IntField(
                "Rows",
                pendingRows
            );

            pendingColumns = EditorGUILayout.IntField(
                "Columns",
                pendingColumns
            );

            pendingCellSize = EditorGUILayout.FloatField(
                "Cell Size",
                pendingCellSize
            );

            pendingRows = Mathf.Max(1, pendingRows);
            pendingColumns = Mathf.Max(1, pendingColumns);
            pendingCellSize = Mathf.Max(0.1f, pendingCellSize);

            EditorGUILayout.Space(5);

            if (GUILayout.Button("APPLY AREA"))
            {
                ApplyAreaSettings();
            }

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // TREE
        // =====================================================

        treeFoldout = EditorGUILayout.Foldout(
            treeFoldout,
            "TREE",
            true
        );

        if (treeFoldout)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            treePrefab = (GameObject)EditorGUILayout.ObjectField(
                "Tree Prefab",
                treePrefab,
                typeof(GameObject),
                false
            );

            if (treePrefab == null)
            {
                EditorGUILayout.HelpBox(
                    "Drag a tree prefab here.",
                    MessageType.Warning
                );
            }

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // MODEL COLOR
        // =====================================================

        colorFoldout = EditorGUILayout.Foldout(
            colorFoldout,
            "MODEL COLOR",
            true
        );

        if (colorFoldout)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            GUILayout.Label(
                "Change color of the selected model.",
                EditorStyles.wordWrappedLabel
            );

            EditorGUILayout.Space(5);

            modelColor = EditorGUILayout.ColorField(
                "Model Color",
                modelColor
            );

            EditorGUILayout.Space(5);

            applyColorToPrefab = EditorGUILayout.Toggle(
                "Apply To Tree Prefab",
                applyColorToPrefab
            );

            createMaterialCopy = EditorGUILayout.Toggle(
                "Create Material Copy",
                createMaterialCopy
            );

            EditorGUILayout.Space(5);

            GUI.enabled = treePrefab != null;

            if (GUILayout.Button(
                "APPLY COLOR",
                GUILayout.Height(30)
            ))
            {
                ApplyColor();
            }

            GUI.enabled = true;

            EditorGUILayout.Space(3);

            if (GUILayout.Button(
                "RESET COLOR",
                GUILayout.Height(25)
            ))
            {
                ResetColor();
            }

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // GRID
        // =====================================================

        gridFoldout = EditorGUILayout.Foldout(
            gridFoldout,
            "GRID",
            true
        );

        if (gridFoldout)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            showGrid = EditorGUILayout.Toggle(
                "Show Grid",
                showGrid
            );

            showAxis = EditorGUILayout.Toggle(
                "Show Axis",
                showAxis
            );

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // TREE SETTINGS
        // =====================================================

        treeSettingsFoldout = EditorGUILayout.Foldout(
            treeSettingsFoldout,
            "TREE SETTINGS",
            true
        );

        if (treeSettingsFoldout)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            minScale = EditorGUILayout.FloatField(
                "Min Scale",
                minScale
            );

            maxScale = EditorGUILayout.FloatField(
                "Max Scale",
                maxScale
            );

            randomRotation = EditorGUILayout.Toggle(
                "Random Rotation",
                randomRotation
            );

            minScale = Mathf.Max(0.01f, minScale);
            maxScale = Mathf.Max(minScale, maxScale);

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // GROUND
        // =====================================================

        groundFoldout = EditorGUILayout.Foldout(
            groundFoldout,
            "GROUND",
            true
        );

        if (groundFoldout)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            followGround = EditorGUILayout.Toggle(
                "Follow Ground",
                followGround
            );

            if (followGround)
            {
                raycastHeight = EditorGUILayout.FloatField(
                    "Raycast Height",
                    raycastHeight
                );

                raycastHeight =
                    Mathf.Max(1f, raycastHeight);
            }

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // PAINT
        // =====================================================

        paintFoldout = EditorGUILayout.Foldout(
            paintFoldout,
            "PAINT",
            true
        );

        if (paintFoldout)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            bool newPaintMode = EditorGUILayout.Toggle(
                "Paint Mode",
                paintMode
            );

            if (newPaintMode != paintMode)
            {
                paintMode = newPaintMode;

                lastPaintRow = -1;
                lastPaintColumn = -1;

                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("SELECT ALL"))
            {
                SelectAllCells();
            }

            if (GUILayout.Button("CLEAR SELECTION"))
            {
                selectedCells.Clear();

                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            GUILayout.Label(
                "Selected Cells: " +
                selectedCells.Count
            );

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // MIRROR
        // =====================================================

        mirrorFoldout = EditorGUILayout.Foldout(
            mirrorFoldout,
            "MIRROR",
            true
        );

        if (mirrorFoldout)
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );

            mirrorMode = (MirrorMode)EditorGUILayout.EnumPopup(
                "Mirror Mode",
                mirrorMode
            );

            EditorGUILayout.Space(3);

            GUILayout.Label(
                "Horizontal = mirror across X axis"
            );

            GUILayout.Label(
                "Vertical = mirror across Z axis"
            );

            GUILayout.Label(
                "Both = mirror both directions"
            );

            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // GENERATE
        // =====================================================

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        GUILayout.Label(
            "GENERATE",
            EditorStyles.boldLabel
        );

        GUI.enabled =
            treeArea != null &&
            treePrefab != null &&
            selectedCells.Count > 0;

        if (GUILayout.Button(
            "GENERATE TREES",
            GUILayout.Height(35)
        ))
        {
            GenerateTrees();
        }

        GUI.enabled = true;

        EditorGUILayout.Space(3);

        if (GUILayout.Button(
            "CLEAR TREES",
            GUILayout.Height(30)
        ))
        {
            ClearGeneratedTrees();
        }

        EditorGUILayout.EndVertical();

        // =====================================================
        // HELP
        // =====================================================

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox
        );

        GUILayout.Label(
            "HOW TO USE",
            EditorStyles.boldLabel
        );

        GUILayout.Label(
            "1. Assign Tree Area."
        );

        GUILayout.Label(
            "2. Assign Tree Prefab."
        );

        GUILayout.Label(
            "3. Choose Model Color."
        );

        GUILayout.Label(
            "4. Click APPLY COLOR."
        );

        GUILayout.Label(
            "5. Set Rows / Columns / Cell Size."
        );

        GUILayout.Label(
            "6. Click APPLY AREA."
        );

        GUILayout.Label(
            "7. Enable Paint Mode."
        );

        GUILayout.Label(
            "8. Click or drag on the grid."
        );

        GUILayout.Label(
            "9. Choose Mirror if needed."
        );

        GUILayout.Label(
            "10. Click GENERATE TREES."
        );

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();
    }

    // =========================================================
    // APPLY AREA
    // =========================================================

    private void ApplyAreaSettings()
    {
        rows = Mathf.Max(1, pendingRows);
        columns = Mathf.Max(1, pendingColumns);
        cellSize = Mathf.Max(0.1f, pendingCellSize);

        selectedCells.Clear();

        lastPaintRow = -1;
        lastPaintColumn = -1;

        SceneView.RepaintAll();
    }

    // =========================================================
    // APPLY COLOR
    // =========================================================

    private void ApplyColor()
    {
        if (treePrefab == null)
        {
            Debug.LogWarning(
                "Tree Painter: Tree Prefab is missing."
            );

            return;
        }

        Renderer[] renderers =
            treePrefab.GetComponentsInChildren<Renderer>(
                true
            );

        if (renderers.Length == 0)
        {
            Debug.LogWarning(
                "Tree Painter: No Renderer found in the model."
            );

            return;
        }

        Undo.RecordObject(
            treePrefab,
            "Change Model Color"
        );

        int changedMaterials = 0;

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material originalMaterial = materials[i];

                if (originalMaterial == null)
                {
                    continue;
                }

                Material materialToUse;

                if (createMaterialCopy)
                {
                    materialToUse =
                        new Material(originalMaterial);

                    materialToUse.name =
                        originalMaterial.name +
                        "_ColorCopy";

                    materials[i] = materialToUse;

                    Undo.RegisterCreatedObjectUndo(
                        materialToUse,
                        "Create Color Material"
                    );
                }
                else
                {
                    materialToUse =
                        originalMaterial;
                }

                if (materialToUse.HasProperty("_BaseColor"))
                {
                    materialToUse.SetColor(
                        "_BaseColor",
                        modelColor
                    );

                    EditorUtility.SetDirty(
                        materialToUse
                    );

                    changedMaterials++;
                }
                else if (materialToUse.HasProperty("_Color"))
                {
                    materialToUse.SetColor(
                        "_Color",
                        modelColor
                    );

                    EditorUtility.SetDirty(
                        materialToUse
                    );

                    changedMaterials++;
                }
            }

            renderer.sharedMaterials = materials;

            EditorUtility.SetDirty(renderer);
        }

        EditorUtility.SetDirty(treePrefab);

        Debug.Log(
            "Tree Painter: Model color changed. " +
            "Materials changed: " +
            changedMaterials
        );

        SceneView.RepaintAll();
        Repaint();
    }

    // =========================================================
    // RESET COLOR
    // =========================================================

    private void ResetColor()
    {
        if (treePrefab == null)
        {
            return;
        }

        Renderer[] renderers =
            treePrefab.GetComponentsInChildren<Renderer>(
                true
            );

        foreach (Renderer renderer in renderers)
        {
            Material[] materials =
                renderer.sharedMaterials;

            foreach (Material material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor(
                        "_BaseColor",
                        Color.white
                    );

                    EditorUtility.SetDirty(
                        material
                    );
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor(
                        "_Color",
                        Color.white
                    );

                    EditorUtility.SetDirty(
                        material
                    );
                }
            }

            EditorUtility.SetDirty(renderer);
        }

        EditorUtility.SetDirty(treePrefab);

        modelColor = Color.white;

        Debug.Log(
            "Tree Painter: Model color reset."
        );

        SceneView.RepaintAll();
        Repaint();
    }

    // =========================================================
    // SELECT ALL
    // =========================================================

    private void SelectAllCells()
    {
        selectedCells.Clear();

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                selectedCells.Add(
                    new Vector2Int(column, row)
                );
            }
        }

        SceneView.RepaintAll();
    }

    // =========================================================
    // SCENE GUI
    // =========================================================

    private void OnSceneGUI(SceneView sceneView)
    {
        if (treeArea == null)
        {
            return;
        }

        DrawGrid();

        if (showAxis)
        {
            DrawMirrorAxis();
        }

        if (!paintMode)
        {
            return;
        }

        HandlePainting(sceneView);
    }

    // =========================================================
    // DRAW GRID
    // =========================================================

    private void DrawGrid()
    {
        Vector3 right =
            treeArea.transform.right;

        Vector3 forward =
            treeArea.transform.forward;

        float totalWidth =
            columns * cellSize;

        float totalDepth =
            rows * cellSize;

        Vector3 center =
            treeArea.transform.position;

        Vector3 start =
            center
            - right * (totalWidth * 0.5f)
            - forward * (totalDepth * 0.5f);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Vector3 cellCenter =
                    start
                    + right * ((column + 0.5f) * cellSize)
                    + forward * ((row + 0.5f) * cellSize);

                Vector3 p1 =
                    cellCenter
                    - right * (cellSize * 0.5f)
                    - forward * (cellSize * 0.5f);

                Vector3 p2 =
                    cellCenter
                    + right * (cellSize * 0.5f)
                    - forward * (cellSize * 0.5f);

                Vector3 p3 =
                    cellCenter
                    + right * (cellSize * 0.5f)
                    + forward * (cellSize * 0.5f);

                Vector3 p4 =
                    cellCenter
                    - right * (cellSize * 0.5f)
                    + forward * (cellSize * 0.5f);

                Vector2Int cell =
                    new Vector2Int(column, row);

                bool selected =
                    selectedCells.Contains(cell);

                if (selected)
                {
                    Handles.color =
                        new Color(
                            0.2f,
                            1f,
                            0.2f,
                            0.25f
                        );

                    Handles.DrawSolidRectangleWithOutline(
                        new Vector3[]
                        {
                            p1,
                            p2,
                            p3,
                            p4
                        },
                        new Color(
                            0.2f,
                            1f,
                            0.2f,
                            0.25f
                        ),
                        Color.green
                    );
                }
                else if (showGrid)
                {
                    Handles.color =
                        new Color(
                            1f,
                            1f,
                            1f,
                            0.25f
                        );

                    Handles.DrawLine(p1, p2);
                    Handles.DrawLine(p2, p3);
                    Handles.DrawLine(p3, p4);
                    Handles.DrawLine(p4, p1);
                }
            }
        }

        if (showGrid)
        {
            Handles.color =
                new Color(
                    1f,
                    1f,
                    1f,
                    0.5f
                );

            for (
                int column = 0;
                column <= columns;
                column++
            )
            {
                Vector3 p1 =
                    start
                    + right * (column * cellSize);

                Vector3 p2 =
                    p1
                    + forward * totalDepth;

                Handles.DrawLine(p1, p2);
            }

            for (
                int row = 0;
                row <= rows;
                row++
            )
            {
                Vector3 p1 =
                    start
                    + forward * (row * cellSize);

                Vector3 p2 =
                    p1
                    + right * totalWidth;

                Handles.DrawLine(p1, p2);
            }
        }
    }

    // =========================================================
    // AXIS
    // =========================================================

    private void DrawMirrorAxis()
    {
        Vector3 right =
            treeArea.transform.right;

        Vector3 forward =
            treeArea.transform.forward;

        float totalWidth =
            columns * cellSize;

        float totalDepth =
            rows * cellSize;

        Vector3 center =
            treeArea.transform.position;

        Vector3 leftEdge =
            center
            - right * (totalWidth * 0.5f);

        Vector3 rightEdge =
            center
            + right * (totalWidth * 0.5f);

        Vector3 backEdge =
            center
            - forward * (totalDepth * 0.5f);

        Vector3 frontEdge =
            center
            + forward * (totalDepth * 0.5f);

        Handles.color = Color.red;

        Handles.DrawLine(
            leftEdge,
            rightEdge
        );

        Handles.color = Color.blue;

        Handles.DrawLine(
            backEdge,
            frontEdge
        );
    }

    // =========================================================
    // PAINTING
    // =========================================================

    private void HandlePainting(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type != EventType.MouseDown &&
            e.type != EventType.MouseDrag)
        {
            return;
        }

        if (e.button != 0)
        {
            return;
        }

        Ray ray =
            HandleUtility.GUIPointToWorldRay(
                e.mousePosition
            );

        Plane plane =
            new Plane(
                treeArea.transform.up,
                treeArea.transform.position
            );

        float distance;

        if (!plane.Raycast(ray, out distance))
        {
            return;
        }

        Vector3 worldPoint =
            ray.GetPoint(distance);

        Vector2Int cell;

        if (!WorldToCell(
            worldPoint,
            out cell
        ))
        {
            return;
        }

        if (cell.x == lastPaintColumn &&
            cell.y == lastPaintRow)
        {
            return;
        }

        lastPaintColumn = cell.x;
        lastPaintRow = cell.y;

        SetMirroredCells(cell);

        e.Use();

        SceneView.RepaintAll();

        Repaint();
    }

    // =========================================================
    // WORLD TO CELL
    // =========================================================

    private bool WorldToCell(
        Vector3 worldPoint,
        out Vector2Int cell
    )
    {
        Vector3 local =
            treeArea.transform.InverseTransformPoint(
                worldPoint
            );

        float totalWidth =
            columns * cellSize;

        float totalDepth =
            rows * cellSize;

        float x =
            local.x + totalWidth * 0.5f;

        float z =
            local.z + totalDepth * 0.5f;

        int column =
            Mathf.FloorToInt(
                x / cellSize
            );

        int row =
            Mathf.FloorToInt(
                z / cellSize
            );

        if (column < 0 ||
            column >= columns ||
            row < 0 ||
            row >= rows)
        {
            cell = Vector2Int.zero;

            return false;
        }

        cell =
            new Vector2Int(
                column,
                row
            );

        return true;
    }

    // =========================================================
    // MIRROR CELLS
    // =========================================================

    private void SetMirroredCells(
        Vector2Int cell
    )
    {
        int column = cell.x;
        int row = cell.y;

        AddCell(column, row);

        if (mirrorMode == MirrorMode.Horizontal ||
            mirrorMode == MirrorMode.Both)
        {
            int mirroredRow =
                rows - 1 - row;

            AddCell(
                column,
                mirroredRow
            );
        }

        if (mirrorMode == MirrorMode.Vertical ||
            mirrorMode == MirrorMode.Both)
        {
            int mirroredColumn =
                columns - 1 - column;

            AddCell(
                mirroredColumn,
                row
            );
        }

        if (mirrorMode == MirrorMode.Both)
        {
            int mirroredColumn =
                columns - 1 - column;

            int mirroredRow =
                rows - 1 - row;

            AddCell(
                mirroredColumn,
                mirroredRow
            );
        }
    }

    // =========================================================
    // ADD CELL
    // =========================================================

    private void AddCell(
        int column,
        int row
    )
    {
        if (column < 0 ||
            column >= columns ||
            row < 0 ||
            row >= rows)
        {
            return;
        }

        selectedCells.Add(
            new Vector2Int(
                column,
                row
            )
        );
    }

    // =========================================================
    // CELL CENTER
    // =========================================================

    private Vector3 GetCellCenter(
        int column,
        int row
    )
    {
        Vector3 right =
            treeArea.transform.right;

        Vector3 forward =
            treeArea.transform.forward;

        float totalWidth =
            columns * cellSize;

        float totalDepth =
            rows * cellSize;

        Vector3 center =
            treeArea.transform.position;

        Vector3 start =
            center
            - right * (totalWidth * 0.5f)
            - forward * (totalDepth * 0.5f);

        return
            start
            + right * ((column + 0.5f) * cellSize)
            + forward * ((row + 0.5f) * cellSize);
    }

    // =========================================================
    // GENERATE TREES
    // =========================================================

    private void GenerateTrees()
    {
        if (treeArea == null)
        {
            Debug.LogWarning(
                "Tree Painter: Tree Area is missing."
            );

            return;
        }

        if (treePrefab == null)
        {
            Debug.LogWarning(
                "Tree Painter: Tree Prefab is missing."
            );

            return;
        }

        if (selectedCells.Count == 0)
        {
            Debug.LogWarning(
                "Tree Painter: No cells selected."
            );

            return;
        }

        ClearGeneratedTrees();

        GameObject generatedParent =
            new GameObject("GeneratedTrees");

        generatedParent.transform.SetParent(
            treeArea.transform
        );

        generatedParent.transform.localPosition =
            Vector3.zero;

        generatedParent.transform.localRotation =
            Quaternion.identity;

        generatedParent.transform.localScale =
            Vector3.one;

        foreach (Vector2Int cell in selectedCells)
        {
            Vector3 position =
                GetCellCenter(
                    cell.x,
                    cell.y
                );

            // -------------------------------------------------
            // GROUND RAYCAST
            // -------------------------------------------------

            if (followGround)
            {
                Vector3 rayStart =
                    position
                    + treeArea.transform.up
                    * raycastHeight;

                Ray ray =
                    new Ray(
                        rayStart,
                        -treeArea.transform.up
                    );

                RaycastHit hit;

                if (Physics.Raycast(
                    ray,
                    out hit,
                    raycastHeight * 2f
                ))
                {
                    position =
                        hit.point;
                }
            }

            // -------------------------------------------------
            // CREATE PREFAB
            // -------------------------------------------------

            GameObject newTree;

            if (PrefabUtility.IsPartOfPrefabAsset(
                treePrefab
            ))
            {
                newTree =
                    PrefabUtility.InstantiatePrefab(
                        treePrefab
                    ) as GameObject;
            }
            else
            {
                newTree =
                    Object.Instantiate(
                        treePrefab
                    );
            }

            if (newTree == null)
            {
                continue;
            }

            Undo.RegisterCreatedObjectUndo(
                newTree,
                "Generate Tree"
            );

            newTree.transform.SetParent(
                generatedParent.transform
            );

            // -------------------------------------------------
            // POSITION
            // -------------------------------------------------

            newTree.transform.position =
                position;

            // -------------------------------------------------
            // ROTATION
            // -------------------------------------------------

            Quaternion rotation =
                treeArea.transform.rotation;

            if (randomRotation)
            {
                rotation *=
                    Quaternion.Euler(
                        0f,
                        Random.Range(
                            0f,
                            360f
                        ),
                        0f
                    );
            }

            newTree.transform.rotation =
                rotation;

            // -------------------------------------------------
            // SCALE
            // -------------------------------------------------

            float scale =
                Random.Range(
                    minScale,
                    maxScale
                );

            newTree.transform.localScale =
                Vector3.one * scale;
        }

        EditorUtility.SetDirty(
            generatedParent
        );

        Selection.activeGameObject =
            generatedParent;

        Debug.Log(
            "Tree Painter: Generated " +
            selectedCells.Count +
            " trees."
        );
    }

    // =========================================================
    // CLEAR TREES
    // =========================================================

    private void ClearGeneratedTrees()
    {
        if (treeArea == null)
        {
            return;
        }

        Transform generated =
            treeArea.transform.Find(
                "GeneratedTrees"
            );

        if (generated == null)
        {
            return;
        }

        Undo.DestroyObjectImmediate(
            generated.gameObject
        );

        Debug.Log(
            "Tree Painter: Cleared generated trees."
        );
    }
}

