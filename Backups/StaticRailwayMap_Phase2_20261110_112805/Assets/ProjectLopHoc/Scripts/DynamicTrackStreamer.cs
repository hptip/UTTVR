using UnityEngine;
using UnityEngine.Splines;

public class DynamicTrackStreamer : MonoBehaviour
{
    [Header("Cấu hình tuyến ray")]
    public SplineContainer splineContainer;
    public GameObject railPrefab;
    public TrainSplineMover trainMover;
    public GameObject pierPrefab; // Prefab Pier Column

    [Header("Thông số phân đoạn ray")]
    public float segmentLength = 3.0f;
    public int poolSize = 150;
    public float backwardOffset = 60.0f;

    [Header("Cấu hình trụ cầu cạn")]
    public int pierInterval = 12; // Cứ mỗi 12 đốt ray (36m) đặt 1 trụ đỡ
    public float pierVerticalOffset = 0.0f;

    [Header("Cấu hình Cột điện Catenary độc lập")]
    public GameObject catenaryPrefab;           // Kéo Prefab cột điện vào đây
    public int catenaryInterval = 15;           // Bước nhảy: Số đốt ray giữa 2 cột (Ví dụ: 15 đốt * 3m = 45m)
    public float catenaryLateralOffset = -2.6f; // Dịch sang mép lan can cầu cạn
    public float catenaryHeightOffset = 0.35f;  // Cao độ đặt chân cột khớp mặt dầm
    public Vector3 catenaryRotationOffset = Vector3.zero; // Bù góc xoay nếu tay xà bị lệch

    private GameObject[] railPool;
    private GameObject[] pierPool;
    private GameObject[] catenaryPool;

    private int pierPoolSize;
    private int catenaryPoolSize;
    private float totalSplineLength;
    private int lastBaseIndex = -999;

    void Start()
    {
        if (splineContainer == null || railPrefab == null)
        {
            Debug.LogError("Vui lòng gắn SplineContainer và RailPrefab vào script.");
            return;
        }

        totalSplineLength = splineContainer.CalculateLength();
        KhoiTaoHoBoiDoiTuong();
    }

    void KhoiTaoHoBoiDoiTuong()
    {
        GameObject poolRoot = new GameObject("Dynamic_Track_System_Pool");
        poolRoot.transform.SetParent(transform);

        // 1. Hồ bơi đối tượng cho các phân đoạn ray
        railPool = new GameObject[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            GameObject rail = Instantiate(railPrefab, poolRoot.transform);
            rail.SetActive(false);
            railPool[i] = rail;
        }

        // 2. Hồ bơi đối tượng cho trụ cầu cạn
        if (pierPrefab != null && pierInterval > 0)
        {
            pierPoolSize = Mathf.CeilToInt((float)poolSize / pierInterval) + 4;
            pierPool = new GameObject[pierPoolSize];

            for (int i = 0; i < pierPoolSize; i++)
            {
                GameObject pier = Instantiate(pierPrefab, poolRoot.transform);
                pier.SetActive(false);
                pierPool[i] = pier;
            }
        }

        // 3. Hồ bơi đối tượng riêng cho cột điện tiếp xúc
        if (catenaryPrefab != null && catenaryInterval > 0)
        {
            catenaryPoolSize = Mathf.CeilToInt((float)poolSize / catenaryInterval) + 4;
            catenaryPool = new GameObject[catenaryPoolSize];

            for (int i = 0; i < catenaryPoolSize; i++)
            {
                GameObject cat = Instantiate(catenaryPrefab, poolRoot.transform);
                cat.SetActive(false);
                catenaryPool[i] = cat;
            }
        }

        CapNhatViTriRay(0f);
    }

    void Update()
    {
        if (trainMover == null || splineContainer == null) return;

        CapNhatViTriRay(trainMover.currentDistanceMeters);
    }

    void CapNhatViTriRay(float trainDist)
    {
        float startDist = Mathf.Max(0f, trainDist - backwardOffset);
        int currentBaseIndex = Mathf.FloorToInt(startDist / segmentLength);

        if (currentBaseIndex == lastBaseIndex) return;
        lastBaseIndex = currentBaseIndex;

        int activePierCount = 0;
        int activeCatenaryCount = 0;

        for (int i = 0; i < poolSize; i++)
        {
            int segmentIndex = currentBaseIndex + i;
            float dist = segmentIndex * segmentLength;

            if (dist > totalSplineLength)
            {
                railPool[i].SetActive(false);
                continue;
            }

            float t = dist / totalSplineLength;
            Vector3 pos = splineContainer.EvaluatePosition(t);
            Vector3 tangent = splineContainer.EvaluateTangent(t);
            Vector3 up = splineContainer.EvaluateUpVector(t);

            if (up == Vector3.zero) up = Vector3.up;

            // Đặt vị trí phân đoạn ray
            railPool[i].transform.position = pos;
            if (tangent != Vector3.zero)
            {
                railPool[i].transform.rotation = Quaternion.LookRotation(tangent, up);
            }

            if (!railPool[i].activeSelf)
            {
                railPool[i].SetActive(true);
            }

            // Kích hoạt vị trí trụ cầu theo chu kỳ riêng
            if (pierPool != null && pierInterval > 0 && segmentIndex % pierInterval == 0)
            {
                if (activePierCount < pierPoolSize)
                {
                    GameObject pier = pierPool[activePierCount];
                    Vector3 pierPos = pos;
                    pierPos.y += pierVerticalOffset;

                    pier.transform.position = pierPos;
                    if (tangent != Vector3.zero)
                    {
                        pier.transform.rotation = Quaternion.LookRotation(tangent, up);
                    }

                    if (!pier.activeSelf) pier.SetActive(true);
                    activePierCount++;
                }
            }

            // Kích hoạt vị trí cột điện theo chu kỳ riêng hoàn toàn
            if (catenaryPool != null && catenaryInterval > 0 && segmentIndex % catenaryInterval == 0)
            {
                if (activeCatenaryCount < catenaryPoolSize)
                {
                    GameObject cat = catenaryPool[activeCatenaryCount];

                    Vector3 rightVec = Vector3.Cross(up.normalized, tangent.normalized).normalized;
                    Vector3 catPos = pos + (rightVec * catenaryLateralOffset) + (up.normalized * catenaryHeightOffset);

                    cat.transform.position = catPos;

                    if (tangent != Vector3.zero)
                    {
                        Quaternion baseRot = Quaternion.LookRotation(tangent, up);
                        cat.transform.rotation = baseRot * Quaternion.Euler(catenaryRotationOffset);
                    }

                    if (!cat.activeSelf) cat.SetActive(true);
                    activeCatenaryCount++;
                }
            }
        }

        // Tắt các trụ cầu dư thừa ngoài tầm nhìn
        if (pierPool != null)
        {
            for (int i = activePierCount; i < pierPoolSize; i++)
            {
                if (pierPool[i].activeSelf) pierPool[i].SetActive(false);
            }
        }

        // Tắt các cột điện dư thừa ngoài tầm nhìn
        if (catenaryPool != null)
        {
            for (int i = activeCatenaryCount; i < catenaryPoolSize; i++)
            {
                if (catenaryPool[i].activeSelf) catenaryPool[i].SetActive(false);
            }
        }
    }
}