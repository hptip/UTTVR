using UnityEngine;
using UnityEngine.Splines;

public class TunnelStreamer : MonoBehaviour
{
    [Header("Liên kết hệ thống")]
    public SplineContainer splineRoute;
    public TrainSplineMover trainMover;
    public GameObject tunnelSegmentPrefab;

    [Header("Kích thước và Tỉ lệ đốt hầm")]
    public float tunnelSegmentLength = 3.8f; // Chiều dài 1 đốt sau khi phóng to
    public Vector3 tunnelScale = new Vector3(1.45f, 1.55f, 1.45f); // Phóng to để trùm kín toa tàu

    [Header("Cấu hình chu kỳ hầm")]
    public int segmentsPerTunnel = 35;       // Số đốt tạo thành một đường hầm dài (35 * 3.8m ≈ 133m)
    public float openAirDistance = 450.0f;   // Khoảng cách chạy ngoài trời trước khi gặp hầm tiếp theo

    [Header("Phạm vi hiển thị quanh tàu")]
    public float forwardViewDistance = 220.0f; // Cự ly sinh hầm phía trước mũi tàu
    public float backwardViewDistance = 50.0f; // Cự ly giữ hầm sau đuôi tàu

    [Header("Căn chỉnh hướng và vị trí lòng hầm")]
    public Vector3 rotationOffset = new Vector3(0f, 90f, 0f);
    public Vector3 positionOffset = new Vector3(0f, 0.4f, 0f); // Nâng nhẹ vòm hầm tránh cạ nóc tàu

    [Header("Bộ nhớ đệm")]
    public int poolSize = 50;

    private GameObject[] tunnelPool;
    private float totalSplineLength;
    private float cycleLength;

    void Start()
    {
        if (splineRoute == null || trainMover == null || tunnelSegmentPrefab == null) return;

        totalSplineLength = splineRoute.CalculateLength();
        float tunnelLength = segmentsPerTunnel * tunnelSegmentLength;
        cycleLength = tunnelLength + openAirDistance;

        tunnelPool = new GameObject[poolSize];
        GameObject root = new GameObject("Tunnel_Pool_Instances");
        root.transform.SetParent(transform);

        for (int i = 0; i < poolSize; i++)
        {
            tunnelPool[i] = Instantiate(tunnelSegmentPrefab, root.transform);
            tunnelPool[i].transform.localScale = tunnelScale;
            tunnelPool[i].SetActive(false);
        }
    }

    void Update()
    {
        if (trainMover == null) return;

        float trainDist = trainMover.currentDistanceMeters;
        float tunnelLength = segmentsPerTunnel * tunnelSegmentLength;

        // Khóa mốc tọa độ cố định trên dải Spline bằng chỉ số nguyên index
        int startIndex = Mathf.FloorToInt((trainDist - backwardViewDistance) / tunnelSegmentLength);
        int endIndex = Mathf.CeilToInt((trainDist + forwardViewDistance) / tunnelSegmentLength);

        int activeCount = 0;

        for (int index = startIndex; index <= endIndex; index++)
        {
            // Tọa độ này đứng yên tuyệt đối trên đường ray
            float dist = index * tunnelSegmentLength;
            if (dist < 0f || dist >= totalSplineLength) continue;

            float posInCycle = Mathf.Repeat(dist, cycleLength);

            if (posInCycle < tunnelLength)
            {
                if (activeCount < poolSize)
                {
                    DatViTriDotHam(tunnelPool[activeCount], dist);
                    activeCount++;
                }
            }
        }

        // Ẩn các đốt hầm nằm ngoài vùng quan sát
        for (int i = activeCount; i < poolSize; i++)
        {
            if (tunnelPool[i].activeSelf) tunnelPool[i].SetActive(false);
        }
    }

    void DatViTriDotHam(GameObject segment, float dist)
    {
        float t = Mathf.Clamp01(dist / totalSplineLength);

        Vector3 pos = splineRoute.EvaluatePosition(t);
        Vector3 tangent = splineRoute.EvaluateTangent(t);
        Vector3 up = splineRoute.EvaluateUpVector(t);

        Quaternion lookRot = Quaternion.LookRotation(tangent, up);
        segment.transform.rotation = lookRot * Quaternion.Euler(rotationOffset);
        segment.transform.position = pos + (lookRot * positionOffset);

        if (!segment.activeSelf) segment.SetActive(true);
    }
}