#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public class TrackSplineGenerator : EditorWindow
{
    private TextAsset csvFile;
    private GameObject splineTargetObject;

    [Header("Cấu hình làm mượt")]
    private int smoothWindowSize = 5;
    private float maxSlopePerMille = 25f; // Giới hạn dốc tối đa 25 phần nghìn cho đường sắt cao tốc

    [MenuItem("Tools/Railway/Tao Spline Tu CSV")]
    public static void ShowWindow()
    {
        GetWindow<TrackSplineGenerator>("Dựng Spline Tuyến Ray");
    }

    private void OnGUI()
    {
        GUILayout.Label("Nạp Dữ Liệu Tọa Độ Tuyến", EditorStyles.boldLabel);
        csvFile = (TextAsset)EditorGUILayout.ObjectField("File CSV Tọa Độ", csvFile, typeof(TextAsset), false);
        splineTargetObject = (GameObject)EditorGUILayout.ObjectField("GameObject Chứa Spline", splineTargetObject, typeof(GameObject), true);

        smoothWindowSize = EditorGUILayout.IntSlider("Độ rộng làm mượt góc cua", smoothWindowSize, 1, 9);
        maxSlopePerMille = EditorGUILayout.Slider("Độ dốc tối đa (phần nghìn)", maxSlopePerMille, 15f, 35f);

        if (GUILayout.Button("Dựng Đường Ray Spline Chuẩn Hóa"))
        {
            if (csvFile == null || splineTargetObject == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Vui lòng chọn đủ file CSV và GameObject mục tiêu.", "OK");
                return;
            }

            XuLyVaTaoSpline();
        }
    }

    private void XuLyVaTaoSpline()
    {
        List<Vector3> rawPoints = DocDuLieuCsv();
        if (rawPoints.Count == 0) return;

        List<Vector3> smoothedPoints = LamMuotToaDo(rawPoints);

        SplineContainer container = splineTargetObject.GetComponent<SplineContainer>();
        if (container == null) container = splineTargetObject.AddComponent<SplineContainer>();

        Spline spline = container.Spline;
        spline.Clear();

        for (int i = 0; i < smoothedPoints.Count; i++)
        {
            Vector3 pt = smoothedPoints[i];
            BezierKnot knot = new BezierKnot(new float3(pt.x, pt.y, pt.z));
            spline.Add(knot, TangentMode.AutoSmooth);
        }

        EditorUtility.SetDirty(container);
        Debug.Log($"Đã tạo dải Spline mượt với {smoothedPoints.Count} điểm nút.");
    }

    private List<Vector3> DocDuLieuCsv()
    {
        List<Vector3> points = new List<Vector3>();
        string[] lines = csvFile.text.Split('\n');

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');
            if (values.Length < 3) continue;

            if (float.TryParse(values[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(values[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(values[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
            {
                points.Add(new Vector3(x, y, z));
            }
        }
        return points;
    }

    private List<Vector3> LamMuotToaDo(List<Vector3> points)
    {
        int count = points.Count;
        List<Vector3> horizontalSmoothed = new List<Vector3>(count);

        // 1. Làm mượt mặt bằng X và Z bằng cửa sổ trượt để nới rộng bán kính cua
        int radius = smoothWindowSize / 2;
        for (int i = 0; i < count; i++)
        {
            int start = Mathf.Max(0, i - radius);
            int end = Mathf.Min(count - 1, i + radius);

            float sumX = 0f;
            float sumZ = 0f;
            int windowCount = 0;

            for (int j = start; j <= end; j++)
            {
                sumX += points[j].x;
                sumZ += points[j].z;
                windowCount++;
            }

            horizontalSmoothed.Add(new Vector3(sumX / windowCount, points[i].y, sumZ / windowCount));
        }

        // 2. Khống chế độ dốc đứng Y để triệt tiêu các điểm nhảy dốc cục bộ
        List<Vector3> finalPoints = new List<Vector3>(count);
        finalPoints.Add(horizontalSmoothed[0]);

        for (int i = 1; i < count; i++)
        {
            Vector3 prev = finalPoints[i - 1];
            Vector3 curr = horizontalSmoothed[i];

            float dist2D = Vector2.Distance(new Vector2(prev.x, prev.z), new Vector2(curr.x, curr.z));
            float maxDeltaY = dist2D * (maxSlopePerMille / 1000f);

            float clampedY = Mathf.Clamp(curr.y, prev.y - maxDeltaY, prev.y + maxDeltaY);
            finalPoints.Add(new Vector3(curr.x, clampedY, curr.z));
        }

        return finalPoints;
    }
}
#endif
