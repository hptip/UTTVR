using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.InputSystem;

public class TrainSplineMover : MonoBehaviour
{
    [Header("Liên kết tuyến ray")]
    public SplineContainer splineRoute;

    [Header("Các toa tàu")]
    public Transform frontCar;
    public Transform rearCar;

    [Header("Khoảng cách và bù trừ hình học")]
    public float carSpacing = 24.09f;
    public float frontHeightOffset = 0.35f;
    public float rearYOffset = -0.298f;
    public float startDistance = 25.0f;

    [Header("Thông số vận hành")]
    public float currentSpeedKmh = 0f;
    public float targetSpeedKmh = 0f;
    public float maxSpeedKmh = 200f;
    public float accelerationRate = 4.5f;
    public float brakeRate = 8.0f;

    [Header("Điều khiển VR")]
    [SerializeField, Min(0f)] private float controllerInputSmoothing = 4f;
    [SerializeField, Range(1, 5)] private int speedLevel = 1;
    private Vector2 _requestedControllerInput;
    private Vector2 _smoothedControllerInput;
    private bool _vrControlInputActive;

    public int SpeedLevel => speedLevel;
    public float SpeedLimitForLevel => maxSpeedKmh * speedLevel / 5f;
    public bool IsStopped => currentSpeedKmh <= 0.1f && targetSpeedKmh <= 0.1f;

    [Header("Trạng thái hành trình")]
    public float currentDistanceMeters = 0f;
    public float totalRouteLength = 0f;

    [Header("Trạng thái Điều khiển")]
    public bool isDriverSeated = false; // Chỉ nhận phím ga và phanh khi có người ngồi ghế

    void Start()
    {
        if (splineRoute != null)
        {
            speedLevel = Mathf.Clamp(speedLevel, 1, 5);
            targetSpeedKmh = Mathf.Clamp(targetSpeedKmh, 0f, SpeedLimitForLevel);
            currentSpeedKmh = Mathf.Max(0f, currentSpeedKmh);
            totalRouteLength = splineRoute.CalculateLength();
            currentDistanceMeters = Mathf.Max(startDistance, carSpacing);
            CapNhatViTriToaXe();
        }
    }

    void Update()
    {
        if (splineRoute == null || totalRouteLength <= 0f) return;

        XuLyNhapLenhLaiTau();
        CapNhatVanToc();
        CapNhatViTriToaXe();
    }

    void XuLyNhapLenhLaiTau()
    {
        if (!isDriverSeated)
        {
            _vrControlInputActive = false;
            _requestedControllerInput = Vector2.zero;
            _smoothedControllerInput = Vector2.MoveTowards(
                _smoothedControllerInput, Vector2.zero, controllerInputSmoothing * Time.deltaTime);
            return;
        }

        if (_vrControlInputActive)
        {
            _smoothedControllerInput = Vector2.MoveTowards(
                _smoothedControllerInput, _requestedControllerInput, controllerInputSmoothing * Time.deltaTime);

            float throttle = _smoothedControllerInput.y;
            if (throttle > 0f)
                targetSpeedKmh = Mathf.Min(targetSpeedKmh + throttle * 25f * Time.deltaTime, SpeedLimitForLevel);
            else if (throttle < 0f)
                targetSpeedKmh = Mathf.Max(targetSpeedKmh + throttle * 40f * Time.deltaTime, 0f);

            targetSpeedKmh = Mathf.Clamp(targetSpeedKmh, 0f, SpeedLimitForLevel);
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null) return;
        if (Keyboard.current.wKey.isPressed)
            targetSpeedKmh = Mathf.Min(targetSpeedKmh + 25f * Time.deltaTime, SpeedLimitForLevel);
        if (Keyboard.current.sKey.isPressed)
            targetSpeedKmh = Mathf.Max(targetSpeedKmh - 40f * Time.deltaTime, 0f);
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            targetSpeedKmh = 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.W))
            targetSpeedKmh = Mathf.Min(targetSpeedKmh + 25f * Time.deltaTime, SpeedLimitForLevel);
        if (Input.GetKey(KeyCode.S))
            targetSpeedKmh = Mathf.Max(targetSpeedKmh - 40f * Time.deltaTime, 0f);
        if (Input.GetKeyDown(KeyCode.Space))
            targetSpeedKmh = 0f;
#endif
        targetSpeedKmh = Mathf.Clamp(targetSpeedKmh, 0f, SpeedLimitForLevel);
    }

    /// <summary>Receives the seated driver's left-stick input from the local player authority.</summary>
    public void SetVRControlInput(Vector2 axis)
    {
        if (!isDriverSeated)
        {
            _requestedControllerInput = Vector2.zero;
            _vrControlInputActive = false;
            return;
        }

        _vrControlInputActive = true;
        float forwardBrakeAxis = Mathf.Clamp(axis.y, -1f, 1f);
        _requestedControllerInput = Mathf.Abs(forwardBrakeAxis) < 0.15f
            ? Vector2.zero
            : new Vector2(0f, forwardBrakeAxis);
    }

    public void AdjustSpeedLevel(int delta)
    {
        if (!isDriverSeated || delta == 0) return;

        int previousLevel = speedLevel;
        speedLevel = Mathf.Clamp(speedLevel + delta, 1, 5);
        if (speedLevel == previousLevel) return;

        // The buttons only select a speed cap. The train moves only when the
        // driver applies forward throttle; lowering the cap brakes to the new limit.
        if (delta < 0)
            targetSpeedKmh = Mathf.Min(targetSpeedKmh, SpeedLimitForLevel);

        targetSpeedKmh = Mathf.Clamp(targetSpeedKmh, 0f, SpeedLimitForLevel);
    }

    void CapNhatVanToc()
    {
        // Khi người lái rời ghế, targetSpeedKmh được giữ nguyên để tàu tiếp tục tự hành theo vận tốc đã đặt
        float rate = (targetSpeedKmh > currentSpeedKmh) ? accelerationRate : brakeRate;
        currentSpeedKmh = Mathf.Max(0f,
            Mathf.MoveTowards(currentSpeedKmh, targetSpeedKmh, rate * Time.deltaTime));

        float speedMps = currentSpeedKmh / 3.6f;
        currentDistanceMeters += speedMps * Time.deltaTime;
        currentDistanceMeters = Mathf.Clamp(currentDistanceMeters, carSpacing, totalRouteLength);
    }

    void CapNhatViTriToaXe()
    {
        DatViTriToa(frontCar, currentDistanceMeters, frontHeightOffset);

        float rearDistance = currentDistanceMeters - carSpacing;
        DatViTriToa(rearCar, rearDistance, frontHeightOffset + rearYOffset);

        if (frontCar != null)
        {
            transform.position = frontCar.position;
        }
    }

    void DatViTriToa(Transform carTransform, float distanceAlongRoute, float totalHeight)
    {
        if (carTransform == null) return;

        float normalizedT = Mathf.Clamp01(distanceAlongRoute / totalRouteLength);

        Vector3 splinePos = splineRoute.EvaluatePosition(normalizedT);
        Vector3 forwardTangent = splineRoute.EvaluateTangent(normalizedT);
        Vector3 upVector = splineRoute.EvaluateUpVector(normalizedT);

        carTransform.position = splinePos + (upVector.normalized * totalHeight);

        if (forwardTangent != Vector3.zero)
            carTransform.rotation = Quaternion.LookRotation(forwardTangent, upVector);
    }
}
