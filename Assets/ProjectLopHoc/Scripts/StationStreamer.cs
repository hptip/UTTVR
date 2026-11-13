using UnityEngine;
using UnityEngine.Splines;

public class StationStreamer : MonoBehaviour
{
    [Header("Tham chiếu Tuyến ray và Tàu")]
    public SplineContainer splineRoute;
    public TrainSplineMover trainMover;

    [Header("Prefab Ga đầu và Ga cuối")]
    public GameObject startStationPrefab; // Prefab Ga Yên Tử
    public GameObject endStationPrefab;   // Prefab Ga Hạ Long Xanh

    [Header("Tầm sinh và xóa (mét)")]
    public float triggerDistance = 450f;

    [Header("Căn chỉnh vị trí Ga đầu tuyến")]
    public float startHeightOffset = 3.2f;    // Nâng cao sàn ga lên ngang bằng cửa tàu
    public float startLateralOffset = 0f;     // Dịch sang trái hoặc phải để khớp tim ray
    public float startForwardOffset = 15f;    // Dịch dọc theo ray để tàu đỗ trúng giữa sảnh
    public Vector3 startRotationOffset = Vector3.zero;

    [Header("Căn chỉnh vị trí Ga cuối tuyến")]
    public float endHeightOffset = 3.2f;
    public float endLateralOffset = 0f;
    public float endForwardOffset = -15f;
    public Vector3 endRotationOffset = Vector3.zero;

    private GameObject _instantiatedStartStation;
    private GameObject _instantiatedEndStation;
    private float _totalSplineLength = 0f;

    private void Start()
    {
        if (trainMover == null)
        {
            trainMover = FindFirstObjectByType<TrainSplineMover>();
        }

        if (splineRoute == null && trainMover != null)
        {
            splineRoute = trainMover.splineRoute;
        }

        if (splineRoute != null)
        {
            _totalSplineLength = splineRoute.CalculateLength();
        }
    }

    private void Update()
    {
        if (trainMover == null || splineRoute == null || _totalSplineLength <= 0f) return;

        XuLyGaDauTuyen();
        XuLyGaCuoiTuyen();
    }

    private void XuLyGaDauTuyen()
    {
        if (startStationPrefab == null) return;

        float currentDist = trainMover.currentDistanceMeters;

        if (currentDist <= triggerDistance)
        {
            if (_instantiatedStartStation == null)
            {
                _instantiatedStartStation = SinhGa(0f, startHeightOffset, startLateralOffset, startForwardOffset, startRotationOffset, startStationPrefab);
            }
        }
        else
        {
            if (_instantiatedStartStation != null)
            {
                Destroy(_instantiatedStartStation);
                _instantiatedStartStation = null;
            }
        }
    }

    private void XuLyGaCuoiTuyen()
    {
        if (endStationPrefab == null) return;

        float currentDist = trainMover.currentDistanceMeters;
        float distanceToEnd = _totalSplineLength - currentDist;

        if (distanceToEnd <= triggerDistance)
        {
            if (_instantiatedEndStation == null)
            {
                _instantiatedEndStation = SinhGa(1f, endHeightOffset, endLateralOffset, endForwardOffset, endRotationOffset, endStationPrefab);
            }
        }
        else
        {
            if (_instantiatedEndStation != null)
            {
                Destroy(_instantiatedEndStation);
                _instantiatedEndStation = null;
            }
        }
    }

    private GameObject SinhGa(float t, float hOffset, float lOffset, float fOffset, Vector3 rOffset, GameObject prefab)
    {
        Vector3 localPos = splineRoute.EvaluatePosition(t);
        Vector3 localTangent = splineRoute.EvaluateTangent(t);
        Vector3 localUp = splineRoute.EvaluateUpVector(t);

        // Đưa về tọa độ thế giới thực tế
        Vector3 worldPos = splineRoute.transform.TransformPoint(localPos);
        Vector3 worldTangent = splineRoute.transform.TransformDirection(localTangent);
        Vector3 worldUp = splineRoute.transform.TransformDirection(localUp);
        Vector3 worldRight = Vector3.Cross(worldUp, worldTangent).normalized;

        Quaternion rot = Quaternion.identity;
        if (worldTangent != Vector3.zero)
        {
            rot = Quaternion.LookRotation(worldTangent, worldUp) * Quaternion.Euler(rOffset);
        }

        Vector3 finalPos = worldPos + (worldUp * hOffset) + (worldRight * lOffset) + (worldTangent * fOffset);
        return Instantiate(prefab, finalPos, rot, transform);
    }

    private void OnDestroy()
    {
        if (_instantiatedStartStation != null) Destroy(_instantiatedStartStation);
        if (_instantiatedEndStation != null) Destroy(_instantiatedEndStation);
    }
}