using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace UTT.SaBan.EditorTools
{
    /// <summary>Các hàm dùng chung của công cụ Sa Bàn (tạo khung, lớp, vật liệu, đo kích thước).</summary>
    public static class SaBanEditorUtil
    {
        public const string GeneratedFolder = "Assets/UTTSaBan/Generated";

        // ============================ Tạo khung Sa Bàn ============================

        public static SaBanRoot CreateRoot(string name, Vector3 position)
        {
            var go = new GameObject(string.IsNullOrWhiteSpace(name) ? "SaBan" : name);
            Undo.RegisterCreatedObjectUndo(go, "Tạo Sa Bàn");
            go.transform.position = position;
            SaBanRoot root = go.AddComponent<SaBanRoot>();

            var model = new GameObject("Model").transform;
            model.SetParent(go.transform, false);
            var content = new GameObject("Content").transform;
            content.SetParent(model, false);

            root.model = model;
            root.content = content;
            root.ApplyScale();

            root.trainRunner = go.AddComponent<SaBanTrainRunner>();

            // Tạo sẵn các lớp chuẩn (lớp rỗng sẽ không hiện nút trên bảng)
            foreach (SaBanLayerKind kind in new[]
            {
                SaBanLayerKind.Train, SaBanLayerKind.Terrain, SaBanLayerKind.Nature, SaBanLayerKind.Rail,
                SaBanLayerKind.Bridge, SaBanLayerKind.Tunnel, SaBanLayerKind.Station
            })
            {
                GetOrCreateLayer(root, kind);
            }
            return root;
        }

        public static SaBanLayer GetOrCreateLayer(SaBanRoot root, SaBanLayerKind kind, string customKey = null, string customLabel = null)
        {
            SaBanLayer.GetPreset(kind, out string key, out string label, out int order);
            if (kind == SaBanLayerKind.Custom)
            {
                if (!string.IsNullOrWhiteSpace(customKey)) key = Slug(customKey);
                if (!string.IsNullOrWhiteSpace(customLabel)) label = customLabel.Trim().ToUpperInvariant();
            }

            SaBanLayer existing = root.FindLayer(key);
            if (existing != null) return existing;

            var go = new GameObject("Layer_" + key);
            Undo.RegisterCreatedObjectUndo(go, "Tạo lớp Sa Bàn");
            go.transform.SetParent(root.content, false);
            SaBanLayer layer = go.AddComponent<SaBanLayer>();
            layer.key = key;
            layer.label = label;
            layer.kind = kind;
            layer.panelOrder = order;
            return layer;
        }

        public static string Slug(string s)
        {
            var sb = new System.Text.StringBuilder();
            string lower = (s ?? "").Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            foreach (char c in lower)
            {
                var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (c == 'đ') { sb.Append('d'); continue; }
                if (char.IsLetterOrDigit(c) && c < 128) sb.Append(c);
                else if (c == ' ' || c == '-' || c == '_') sb.Append('_');
            }
            string result = sb.ToString().Trim('_');
            return string.IsNullOrEmpty(result) ? "custom" : result;
        }

        // ============================ Bàn ============================

        public static void BuildTable(SaBanRoot root, Vector2 size, float height, float thickness, float legSize, Color color)
        {
            Transform old = root.transform.Find("Table");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            Material wood = GetOrCreateMaterial("SaBan_Table", color);

            var table = new GameObject("Table").transform;
            Undo.RegisterCreatedObjectUndo(table.gameObject, "Tạo bàn Sa Bàn");
            table.SetParent(root.transform, false);
            table.SetAsFirstSibling();

            float boardY = height - thickness * 0.5f;
            MakeBox("Board", table, new Vector3(0f, boardY, 0f), new Vector3(size.x, thickness, size.y), wood);

            float legH = Mathf.Max(0.05f, height - thickness);
            float inset = Mathf.Max(legSize, 0.06f);
            float lx = size.x * 0.5f - inset, lz = size.y * 0.5f - inset;
            int idx = 0;
            foreach (int sx in new[] { -1, 1 })
            {
                foreach (int sz in new[] { -1, 1 })
                {
                    MakeBox("Leg_" + (++idx), table, new Vector3(sx * lx, legH * 0.5f, sz * lz), new Vector3(legSize, legH, legSize), wood);
                }
            }

            root.tableSize = size;
            root.tableTopHeight = height;
            if (root.model != null) root.model.localPosition = new Vector3(0f, height, 0f);
        }

        private static void MakeBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static Material GetOrCreateMaterial(string name, Color color)
        {
            EnsureFolder(GeneratedFolder);
            string path = GeneratedFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null)
            {
                m.color = color;
                EditorUtility.SetDirty(m);
                return m;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            m.color = color;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ============================ Đo kích thước nội dung ============================

        /// <summary>Hộp bao quanh nội dung, tính theo toạ độ của Content (= đơn vị gốc của scene nguồn).</summary>
        public static bool ComputeContentBounds(SaBanRoot root, bool routeOnly, out Bounds bounds)
        {
            bounds = new Bounds();
            bool has = false;
            Transform content = root.content;
            if (content == null) return false;

            if (root.route != null)
            {
                var sampler = new SaBanRouteSampler(root.route);
                if (sampler.IsValid)
                {
                    const int n = 300;
                    for (int i = 0; i <= n; i++)
                    {
                        sampler.EvaluateWorld(sampler.Length * i / n, out Vector3 p, out _, out _);
                        Encapsulate(ref bounds, ref has, content.InverseTransformPoint(p));
                    }
                }
            }
            if (routeOnly) return has;

            foreach (Renderer r in content.GetComponentsInChildren<Renderer>(false))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                Bounds wb = r.bounds;
                foreach (Vector3 corner in Corners(wb))
                    Encapsulate(ref bounds, ref has, content.InverseTransformPoint(corner));
            }
            foreach (Terrain t in content.GetComponentsInChildren<Terrain>(false))
            {
                if (t.terrainData == null) continue;
                Vector3 pos = t.transform.position;
                Vector3 size = t.terrainData.size;
                foreach (Vector3 corner in Corners(new Bounds(pos + size * 0.5f, size)))
                    Encapsulate(ref bounds, ref has, content.InverseTransformPoint(corner));
            }
            return has;
        }

        private static void Encapsulate(ref Bounds b, ref bool has, Vector3 p)
        {
            if (!has) { b = new Bounds(p, Vector3.zero); has = true; }
            else b.Encapsulate(p);
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            Vector3 c = b.center, e = b.extents;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        yield return c + new Vector3(e.x * x, e.y * y, e.z * z);
        }

        /// <summary>
        /// Tính tỉ lệ để vừa mặt bàn, đặt tâm nội dung vào giữa bàn và đáy nội dung chạm mặt bàn.
        /// </summary>
        public static bool FitToTable(SaBanRoot root, bool routeOnly, float paddingPercent, bool applyScale)
        {
            if (!ComputeContentBounds(root, routeOnly, out Bounds b)) return false;

            float usableX = root.tableSize.x * (1f - 2f * paddingPercent / 100f);
            float usableZ = root.tableSize.y * (1f - 2f * paddingPercent / 100f);
            float sx = b.size.x > 0.0001f ? usableX / b.size.x : float.MaxValue;
            float sz = b.size.z > 0.0001f ? usableZ / b.size.z : float.MaxValue;
            float scale = Mathf.Min(sx, sz);
            if (scale <= 0f || float.IsInfinity(scale) || scale == float.MaxValue) return false;

            Undo.RecordObject(root, "Vừa khít mặt bàn");
            Undo.RecordObject(root.content, "Vừa khít mặt bàn");
            if (applyScale) root.SetModelScale(scale);
            root.content.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            EditorUtility.SetDirty(root);
            return true;
        }

        /// <summary>Chỉ căn lại vị trí (tâm vào giữa bàn, đáy chạm mặt bàn), giữ nguyên tỉ lệ.</summary>
        public static bool RecenterContent(SaBanRoot root, bool routeOnly)
        {
            if (!ComputeContentBounds(root, routeOnly, out Bounds b)) return false;
            Undo.RecordObject(root.content, "Căn giữa nội dung");
            root.content.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            return true;
        }

        // ============================ Làm sạch bản sao ============================

        private static readonly HashSet<string> RemoveByName = new HashSet<string>
        {
            "NetworkObject", "NetworkTransform", "NetworkRigidbody", "NetworkRigidbody3D", "NetworkCharacterController",
            "TrainSplineMover", "TrainSeatInteractable", "TrainPassengerCarrier", "TrainInteractable",
            "FirstPersonController", "PlayerSpawner", "Camera", "AudioListener", "AudioSource",
            "EventSystem", "StandaloneInputModule", "InputSystemUIInputModule", "Rigidbody"
        };

        /// <summary>
        /// Xoá khỏi bản sao các thành phần gameplay/mạng/camera (không được phép nằm trong mô hình thu nhỏ).
        /// Mô hình chỉ giữ lại hình dạng, vật liệu, collider.
        /// </summary>
        public static void StripGameplay(GameObject clone)
        {
            for (int pass = 0; pass < 3; pass++)
            {
                var all = new List<Component>(clone.GetComponentsInChildren<Component>(true));
                foreach (Component c in all)
                {
                    if (c == null || c is Transform) continue;
                    string typeName = c.GetType().Name;
                    bool kill = RemoveByName.Contains(typeName) || c is InteractableObject || InheritsFromName(c.GetType(), "NetworkBehaviour");
                    if (!kill) continue;
                    if (typeName == "NetworkObject" && pass < 2) continue;   // gỡ NetworkObject sau cùng
                    Object.DestroyImmediate(c);
                }
            }
        }

        private static bool InheritsFromName(System.Type t, string baseName)
        {
            for (System.Type x = t; x != null; x = x.BaseType)
                if (x.Name == baseName) return true;
            return false;
        }

        // ============================ Đường dẫn tới một Transform con ============================

        public static int[] PathFrom(Transform root, Transform target)
        {
            if (target == null || root == null) return null;
            var path = new List<int>();
            Transform t = target;
            while (t != null && t != root)
            {
                path.Add(t.GetSiblingIndex());
                t = t.parent;
            }
            if (t != root) return null;
            path.Reverse();
            return path.ToArray();
        }

        public static Transform FindByPath(Transform root, int[] path)
        {
            if (root == null || path == null) return null;
            Transform t = root;
            foreach (int i in path)
            {
                if (i < 0 || i >= t.childCount) return null;
                t = t.GetChild(i);
            }
            return t;
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}
