using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace UTT.SaBan.EditorTools
{
    [System.Serializable]
    public sealed class BakeOptions
    {
        public bool rails = true;
        public bool piers = true;
        public bool catenary = true;
        public bool tunnels = true;
        public bool stations = true;
        public bool scenery = true;

        [Tooltip("Cứ N đốt ray mới đặt 1 đốt (1 = đủ hết, 3 = thưa hơn 3 lần, nhẹ hơn nhiều).")]
        public int railStride = 3;
        public int tunnelStride = 1;
        public float sceneryIntervalMeters = 80f;
        public int maxObjectsPerType = 20000;

        [Header("Scale riêng từng loại (1 = giữ nguyên)")]
        public float railScale = 1f;
        public float pierScale = 1f;
        public float catenaryScale = 1f;
        public float tunnelScale = 1f;
        public float stationScale = 1f;
        public float sceneryScale = 1f;
    }

    public sealed class BakeReport
    {
        public int rails, piers, catenary, tunnels, stations, mountains;
        public string notes = "";
        public override string ToString()
        {
            return "Ray: " + rails + " | Trụ cầu: " + piers + " | Cột điện: " + catenary + " | Đốt hầm: " + tunnels +
                   " | Nhà ga: " + stations + " | Núi/cây: " + mountains + (string.IsNullOrEmpty(notes) ? "" : "\n" + notes);
        }
    }

    /// <summary>
    /// Trong scene gốc (như YenTuHaLongXanh), ray / trụ / cột điện / hầm / núi / nhà ga KHÔNG nằm sẵn trong scene mà do các
    /// "streamer" sinh ra lúc chạy, quanh vị trí đoàn tàu. Sa Bàn cần thấy toàn tuyến cùng lúc, nên công cụ này "bake":
    /// chạy lại đúng công thức đặt vị trí của từng streamer cho TOÀN BỘ tuyến, rồi tạo ra vật thể tĩnh trong các lớp của Sa Bàn.
    /// </summary>
    public static class SaBanBaker
    {
        /// <summary>Đo tuyến theo đúng cách các streamer gốc làm (t = khoảng cách / tổng chiều dài).</summary>
        private sealed class StreamerRoute
        {
            private readonly SplineContainer _c;
            private readonly Spline _s;
            public readonly float Length;

            public StreamerRoute(SplineContainer c)
            {
                _c = c;
                _s = c.Splines[0];
                Length = c.CalculateLength();
            }

            public void At(float dist, out Vector3 pos, out Vector3 tan, out Vector3 up)
            {
                float t = Length > 0f ? Mathf.Clamp01(dist / Length) : 0f;
                Transform tr = _c.transform;
                pos = tr.TransformPoint((Vector3)SplineUtility.EvaluatePosition(_s, t));
                tan = tr.TransformDirection((Vector3)SplineUtility.EvaluateTangent(_s, t));
                up = tr.TransformDirection((Vector3)SplineUtility.EvaluateUpVector(_s, t));
                if (tan.sqrMagnitude < 1e-10f) tan = tr.forward;
                if (up.sqrMagnitude < 1e-10f) up = Vector3.up;
                tan.Normalize();
                up.Normalize();
            }
        }

        public static T FindFirst<T>(GameObject[] roots) where T : Component
        {
            foreach (GameObject r in roots)
            {
                if (r == null) continue;
                T c = r.GetComponentInChildren<T>(true);
                if (c != null) return c;
            }
            return null;
        }

        public static BakeReport Bake(SaBanRoot root, GameObject[] sourceRoots, SplineContainer sourceRoute, BakeOptions o)
        {
            var report = new BakeReport();
            if (sourceRoute == null || sourceRoute.Splines == null || sourceRoute.Splines.Count == 0)
            {
                report.notes = "Không có tuyến ray hợp lệ để bake.";
                return report;
            }

            var route = new StreamerRoute(sourceRoute);
            var track = FindFirst<DynamicTrackStreamer>(sourceRoots);
            var tunnel = FindFirst<TunnelStreamer>(sourceRoots);
            var station = FindFirst<StationStreamer>(sourceRoots);
            var scenery = FindFirst<SceneryStreamer>(sourceRoots);

            try
            {
                if (o.rails) report.rails = BakeRails(root, track, route, o);
                if (o.piers) report.piers = BakePiers(root, track, route, o);
                if (o.catenary) report.catenary = BakeCatenary(root, track, route, o);

                if (o.tunnels)
                {
                    if (tunnel != null) report.tunnels = BakeTunnels(root, tunnel, route, o);
                    else report.notes += "Không thấy TunnelStreamer. ";
                }
                if (o.stations)
                {
                    if (station != null) report.stations = BakeStations(root, station, route, o);
                    else report.notes += "Không thấy StationStreamer. ";
                }
                if (o.scenery)
                {
                    if (scenery != null) report.mountains = BakeScenery(root, scenery, route, o);
                    else report.notes += "Không thấy SceneryStreamer. ";
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return report;
        }

        // ============================ Từng loại ============================

        private static int BakeRails(SaBanRoot root, DynamicTrackStreamer s, StreamerRoute r, BakeOptions o)
        {
            GameObject railPrefab = (s != null && s.railPrefab != null)
                ? s.railPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/EBS_Rail_2000_Prefab.prefab");
            if (railPrefab == null) return 0;

            Transform layer = SaBanEditorUtil.GetOrCreateLayer(root, SaBanLayerKind.Rail).transform;
            int stride = Mathf.Max(1, o.railStride);
            float segLen = (s != null && s.segmentLength > 0.1f) ? s.segmentLength : 3.0f;
            int segments = Mathf.FloorToInt(r.Length / segLen);
            int count = 0;
            for (int i = 0; i <= segments && count < o.maxObjectsPerType; i += stride)
            {
                float dist = i * segLen;
                if (dist > r.Length) break;
                r.At(dist, out Vector3 pos, out Vector3 tan, out Vector3 up);
                GameObject go = Spawn(railPrefab, layer);
                if (go == null) break;
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.LookRotation(tan, up);
                if (!Mathf.Approximately(o.railScale, 1f))
                    go.transform.localScale *= o.railScale;
                count++;
                if (count % 300 == 0) EditorUtility.DisplayProgressBar("Bake ray", count + " đốt ray...", (float)i / Mathf.Max(1, segments));
            }
            return count;
        }

        private static int BakePiers(SaBanRoot root, DynamicTrackStreamer s, StreamerRoute r, BakeOptions o)
        {
            GameObject pierPrefab = (s != null && s.pierPrefab != null)
                ? s.pierPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/Pier Column.prefab");
            if (pierPrefab == null) return 0;

            int interval = (s != null && s.pierInterval > 0) ? s.pierInterval : 12;
            float segLen = (s != null && s.segmentLength > 0.1f) ? s.segmentLength : 3.0f;
            float vertOffset = (s != null) ? s.pierVerticalOffset : -0.1f;
            Transform layer = SaBanEditorUtil.GetOrCreateLayer(root, SaBanLayerKind.Bridge).transform;
            int segments = Mathf.FloorToInt(r.Length / segLen);
            int count = 0;
            for (int i = 0; i <= segments && count < o.maxObjectsPerType; i += interval)
            {
                float dist = i * segLen;
                if (dist > r.Length) break;
                r.At(dist, out Vector3 pos, out Vector3 tan, out Vector3 up);
                pos.y += vertOffset;
                GameObject go = Spawn(pierPrefab, layer);
                if (go == null) break;
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.LookRotation(tan, up);
                if (!Mathf.Approximately(o.pierScale, 1f))
                    go.transform.localScale *= o.pierScale;
                count++;
            }
            return count;
        }

        private static int BakeCatenary(SaBanRoot root, DynamicTrackStreamer s, StreamerRoute r, BakeOptions o)
        {
            GameObject catenaryPrefab = (s != null && s.catenaryPrefab != null)
                ? s.catenaryPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/pole.prefab");
            if (catenaryPrefab == null) return 0;

            int interval = (s != null && s.catenaryInterval > 0) ? s.catenaryInterval : 15;
            float segLen = (s != null && s.segmentLength > 0.1f) ? s.segmentLength : 3.0f;
            float lateralOffset = (s != null) ? s.catenaryLateralOffset : -2.6f;
            float heightOffset = (s != null) ? s.catenaryHeightOffset : 0.35f;
            Vector3 rotOffset = (s != null) ? s.catenaryRotationOffset : Vector3.zero;

            Transform layer = SaBanEditorUtil.GetOrCreateLayer(root, SaBanLayerKind.Rail).transform;
            int segments = Mathf.FloorToInt(r.Length / segLen);
            int count = 0;
            for (int i = 0; i <= segments && count < o.maxObjectsPerType; i += interval)
            {
                float dist = i * segLen;
                if (dist > r.Length) break;
                r.At(dist, out Vector3 pos, out Vector3 tan, out Vector3 up);
                Vector3 right = Vector3.Cross(up.normalized, tan.normalized).normalized;
                Vector3 p = pos + right * lateralOffset + up.normalized * heightOffset;
                GameObject go = Spawn(catenaryPrefab, layer);
                if (go == null) break;
                go.transform.localPosition = p;
                go.transform.localRotation = Quaternion.LookRotation(tan, up) * Quaternion.Euler(rotOffset);
                if (!Mathf.Approximately(o.catenaryScale, 1f))
                    go.transform.localScale *= o.catenaryScale;
                count++;
            }
            return count;
        }

        private static int BakeTunnels(SaBanRoot root, TunnelStreamer s, StreamerRoute r, BakeOptions o)
        {
            if (s.tunnelSegmentPrefab == null) return 0;
            Transform layer = SaBanEditorUtil.GetOrCreateLayer(root, SaBanLayerKind.Tunnel).transform;
            float tunnelLength = s.segmentsPerTunnel * s.tunnelSegmentLength;
            float cycle = tunnelLength + s.openAirDistance;
            int stride = Mathf.Max(1, o.tunnelStride);
            int segments = Mathf.FloorToInt(r.Length / Mathf.Max(0.1f, s.tunnelSegmentLength));
            int count = 0;
            for (int i = 0; i <= segments && count < o.maxObjectsPerType; i += stride)
            {
                float dist = i * s.tunnelSegmentLength;
                if (dist < 0f || dist >= r.Length) continue;
                if (Mathf.Repeat(dist, cycle) >= tunnelLength) continue;   // đoạn ngoài trời

                r.At(dist, out Vector3 pos, out Vector3 tan, out Vector3 up);
                Quaternion look = Quaternion.LookRotation(tan, up);
                GameObject go = Spawn(s.tunnelSegmentPrefab, layer);
                if (go == null) break;
                go.transform.localScale = s.tunnelScale;
                if (!Mathf.Approximately(o.tunnelScale, 1f))
                    go.transform.localScale = Vector3.Scale(go.transform.localScale, Vector3.one * o.tunnelScale);
                go.transform.localRotation = look * Quaternion.Euler(s.rotationOffset);
                go.transform.localPosition = pos + look * s.positionOffset;
                count++;
                if (count % 300 == 0) EditorUtility.DisplayProgressBar("Bake hầm", count + " đốt hầm...", (float)i / Mathf.Max(1, segments));
            }
            return count;
        }

        private static int BakeStations(SaBanRoot root, StationStreamer s, StreamerRoute r, BakeOptions o)
        {
            Transform layer = SaBanEditorUtil.GetOrCreateLayer(root, SaBanLayerKind.Station).transform;
            int count = 0;
            count += PlaceStation(s.startStationPrefab, layer, r, 0f, s.startHeightOffset, s.startLateralOffset, s.startForwardOffset, s.startRotationOffset, o.stationScale);
            count += PlaceStation(s.endStationPrefab, layer, r, r.Length, s.endHeightOffset, s.endLateralOffset, s.endForwardOffset, s.endRotationOffset, o.stationScale);
            return count;
        }

        private static int PlaceStation(GameObject prefab, Transform layer, StreamerRoute r, float dist, float h, float lat, float fwd, Vector3 rotOffset, float scale)
        {
            if (prefab == null) return 0;
            r.At(dist, out Vector3 pos, out Vector3 tan, out Vector3 up);
            Vector3 right = Vector3.Cross(up, tan).normalized;
            GameObject go = Spawn(prefab, layer);
            if (go == null) return 0;
            go.transform.localPosition = pos + up * h + right * lat + tan * fwd;
            go.transform.localRotation = Quaternion.LookRotation(tan, up) * Quaternion.Euler(rotOffset);
            if (!Mathf.Approximately(scale, 1f))
                go.transform.localScale *= scale;
            return 1;
        }

        private static int BakeScenery(SaBanRoot root, SceneryStreamer s, StreamerRoute r, BakeOptions o)
        {
            if (s.mountainPrefabs == null || s.mountainPrefabs.Length == 0) return 0;
            Transform layer = SaBanEditorUtil.GetOrCreateLayer(root, SaBanLayerKind.Nature).transform;
            var rnd = new System.Random(12345);   // cố định để chạy lại ra cùng kết quả
            float Range(float a, float b) => (float)(rnd.NextDouble() * (b - a) + a);

            float interval = Mathf.Max(5f, o.sceneryIntervalMeters);
            int count = 0;
            for (int k = 0; count < o.maxObjectsPerType; k++)
            {
                float dist = 80f + k * interval;
                if (dist > r.Length) break;

                GameObject prefab = s.mountainPrefabs[k % s.mountainPrefabs.Length];
                if (prefab == null) continue;

                r.At(dist, out Vector3 pos, out Vector3 tan, out Vector3 up);
                Vector3 right = Vector3.Cross(up, tan).normalized;
                float side = (k % 2 == 0) ? 1f : -1f;
                float offset = side * (s.sideOffset + Range(-20f, 30f));
                float scaleFactor = Range(0.85f, 1.4f);

                Vector3 p = pos + right * offset;
                p.y += s.verticalOffset;

                Quaternion rot;
                if (prefab.name.Contains("GreenHill"))
                {
                    float rotY = s.greenHillUseRandomY ? Range(0f, 360f) : s.greenHillRotation.y;
                    rot = Quaternion.Euler(s.greenHillRotation.x, rotY, s.greenHillRotation.z);
                }
                else
                {
                    float rotY = s.randomRotationY ? Range(0f, 360f) : s.baseRotation.y;
                    rot = Quaternion.Euler(s.baseRotation.x, rotY, s.baseRotation.z);
                }

                GameObject go = Spawn(prefab, layer);
                if (go == null) continue;
                go.transform.localPosition = p;
                go.transform.localRotation = rot;
                go.transform.localScale = new Vector3(
                    scaleFactor * s.scaleMultiplier.x, scaleFactor * s.scaleMultiplier.y, scaleFactor * s.scaleMultiplier.z)
                    * o.sceneryScale;
                count++;
            }
            return count;
        }

        // ============================ Tạo bản sao ============================

        /// <summary>Tạo một bản của prefab dưới layer (giữ liên kết prefab nếu là asset).</summary>
        private static GameObject Spawn(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;
            GameObject go;
            if (PrefabUtility.IsPartOfPrefabAsset(prefab))
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            else
            {
                go = Object.Instantiate(prefab, parent);
                SaBanEditorUtil.StripGameplay(go);
            }
            return go;
        }
    }
}
