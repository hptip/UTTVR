using UnityEngine;
using UnityEngine.Splines;

public class SceneryStreamer : MonoBehaviour
{
    [Header("Liên kết hệ thống")]
    public SplineContainer splineRoute;
    public TrainSplineMover trainMover;

    [Header("Bộ mô hình núi 3D")]
    public GameObject[] mountainPrefabs;

    [Header("Thông số cuốn chiếu")]
    public int totalMountains = 20;
    public float spawnAheadDistance = 1000f;
    public float despawnBehindDistance = 200f;
    public float sideOffset = 180f;

    [Header("Tùy chỉnh góc xoay chung")]
    public float verticalOffset = -6.0f;
    public Vector3 baseRotation = Vector3.zero;
    public bool randomRotationY = true;

    [Header("Góc xoay riêng cho GreenHill")]
    public Vector3 greenHillRotation = new Vector3(0f, 90f, 0f); // Tùy chỉnh góc xoay riêng trên Inspector
    public bool greenHillUseRandomY = false;                      // Tắt nếu muốn GreenHill luôn cố định góc này

    [Header("Tùy chỉnh kích thước núi")]
    public Vector3 scaleMultiplier = new Vector3(3.5f, 2.5f, 3.5f);

    private GameObject[] pool;
    private float[] mountainDistances;
    private float totalSplineLength;

    void Start()
    {
        if (splineRoute == null || trainMover == null || mountainPrefabs == null || mountainPrefabs.Length == 0)
        {
            Debug.LogError("Thiếu tham số cấu hình trong SceneryStreamer.");
            return;
        }

        totalSplineLength = splineRoute.CalculateLength();
        pool = new GameObject[totalMountains];
        mountainDistances = new float[totalMountains];

        GameObject root = new GameObject("Scenery_Dynamic_Pool");
        root.transform.SetParent(transform);

        float currentTrainDist = trainMover.currentDistanceMeters;

        for (int i = 0; i < totalMountains; i++)
        {
            int prefabIndex = i % mountainPrefabs.Length;
            pool[i] = Instantiate(mountainPrefabs[prefabIndex], root.transform);

            float initialDist = currentTrainDist + 80f + (i * (spawnAheadDistance / totalMountains));
            mountainDistances[i] = initialDist;

            float sideSign = (i % 2 == 0) ? 1f : -1f;
            float randomOffset = sideSign * (sideOffset + Random.Range(-20f, 30f));
            float randomScale = Random.Range(0.85f, 1.4f);

            DatViTriNui(i, initialDist, randomOffset, randomScale);
        }
    }

    void Update()
    {
        if (trainMover == null || totalSplineLength <= 0f) return;

        float trainDist = trainMover.currentDistanceMeters;

        for (int i = 0; i < totalMountains; i++)
        {
            if (mountainDistances[i] < trainDist - despawnBehindDistance)
            {
                float newDist = trainDist + spawnAheadDistance + Random.Range(0f, 120f);

                if (newDist <= totalSplineLength)
                {
                    mountainDistances[i] = newDist;

                    float sideSign = (Random.value > 0.5f) ? 1f : -1f;
                    float randomOffset = sideSign * (sideOffset + Random.Range(-20f, 30f));
                    float randomScale = Random.Range(0.85f, 1.4f);

                    DatViTriNui(i, newDist, randomOffset, randomScale);
                }
                else
                {
                    pool[i].SetActive(false);
                }
            }
        }
    }

    void DatViTriNui(int index, float distOnRoute, float sideDistance, float scaleFactor)
    {
        float t = Mathf.Clamp01(distOnRoute / totalSplineLength);

        Vector3 pos = splineRoute.EvaluatePosition(t);
        Vector3 tangent = splineRoute.EvaluateTangent(t);
        Vector3 up = splineRoute.EvaluateUpVector(t);

        Vector3 right = Vector3.Cross(up, tangent).normalized;
        Vector3 finalPos = pos + (right * sideDistance);
        finalPos.y += verticalOffset;

        pool[index].transform.position = finalPos;

        // Phân biệt GreenHill với các loại núi khác qua tên đối tượng
        bool isGreenHill = pool[index].name.Contains("GreenHill");

        if (isGreenHill)
        {
            float rotY = greenHillUseRandomY ? Random.Range(0f, 360f) : greenHillRotation.y;
            pool[index].transform.rotation = Quaternion.Euler(greenHillRotation.x, rotY, greenHillRotation.z);
        }
        else
        {
            float rotY = randomRotationY ? Random.Range(0f, 360f) : baseRotation.y;
            pool[index].transform.rotation = Quaternion.Euler(baseRotation.x, rotY, baseRotation.z);
        }

        pool[index].transform.localScale = new Vector3(
            scaleFactor * scaleMultiplier.x,
            scaleFactor * scaleMultiplier.y,
            scaleFactor * scaleMultiplier.z
        );

        if (!pool[index].activeSelf)
        {
            pool[index].SetActive(true);
        }
    }
}