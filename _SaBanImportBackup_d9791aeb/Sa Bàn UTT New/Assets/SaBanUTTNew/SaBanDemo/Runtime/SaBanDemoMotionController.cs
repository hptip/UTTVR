using System.Collections;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Standalone tabletop train controller. It never moves its parent transform.</summary>
[DisallowMultipleComponent]
public sealed class SaBanDemoMotionController : MonoBehaviour
{
    [Header("Tuyến và toa")]
    public SplineContainer route;
    public Transform frontCar;
    public Transform rearCar;
    public float carSpacing = 0.52f;
    public float frontHeightOffset = 0.008f;
    public float rearHeightOffset = 0f;
    public float startDistance = 0.58f;

    [Header("Chuyển động sa bàn")]
    public bool playOnStart = true;
    public bool requirePresence = true;
    public float speedUnitsPerSecond = 0.12f;
    public float verticalSmoothTime = 0.6f;
    public bool keepCarsLevel = true;
    public bool loopAtEnd = true;
    public float endPauseSeconds = 0.8f;

    public float CurrentDistance => _distance;
    public float TotalLength => _totalLength;

    private const int ArcSamples = 2048;
    private float[] _sampleDistances;
    private float _totalLength;
    private float _distance;
    private float _frontY;
    private float _rearY;
    private float _frontYVelocity;
    private float _rearYVelocity;
    private bool _heightInitialized;
    private bool _waitingAtEnd;
    private bool _presenceActive;

    private void Awake()
    {
        _presenceActive = !requirePresence;
        BuildArcLengthTable();
    }

    private void Start()
    {
        if (_totalLength <= 0f) return;
        _distance = Mathf.Clamp(Mathf.Max(startDistance, carSpacing), 0f, _totalLength);
        PlaceCars(true);
        if (playOnStart) enabled = true;
    }

    private void Update()
    {
        if (!playOnStart || (requirePresence && !_presenceActive) || _waitingAtEnd || _totalLength <= 0f) return;

        _distance += Mathf.Max(0f, speedUnitsPerSecond) * Time.deltaTime;
        if (_distance >= _totalLength)
        {
            _distance = _totalLength;
            PlaceCars(false);
            if (loopAtEnd) StartCoroutine(WaitThenRestart());
        }
        else
        {
            PlaceCars(false);
        }
    }

    private IEnumerator WaitThenRestart()
    {
        _waitingAtEnd = true;
        if (frontCar != null) frontCar.gameObject.SetActive(false);
        if (rearCar != null) rearCar.gameObject.SetActive(false);
        yield return new WaitForSeconds(Mathf.Max(0f, endPauseSeconds));
        while (requirePresence && !_presenceActive) yield return null;

        _distance = Mathf.Clamp(Mathf.Max(startDistance, carSpacing), 0f, _totalLength);
        _heightInitialized = false;
        if (frontCar != null) frontCar.gameObject.SetActive(true);
        if (rearCar != null) rearCar.gameObject.SetActive(true);
        PlaceCars(true);
        _waitingAtEnd = false;
    }

    public void SetPresenceActive(bool active) => _presenceActive = active;

    public Vector3 GetPositionAtDistance(float distance)
    {
        float t = DistanceToParameter(distance);
        return route != null ? route.EvaluatePosition(t) : Vector3.zero;
    }

    public Vector3 GetTangentAtDistance(float distance)
    {
        float t = DistanceToParameter(distance);
        return route != null ? route.EvaluateTangent(t) : Vector3.forward;
    }

    private float DistanceToParameter(float distance)
    {
        if (route == null || _sampleDistances == null || _totalLength <= 0f) return 0f;
        float d = Mathf.Clamp(distance, 0f, _totalLength);
        int low = 0;
        int high = _sampleDistances.Length - 1;
        while (high - low > 1)
        {
            int mid = (low + high) >> 1;
            if (_sampleDistances[mid] < d) low = mid;
            else high = mid;
        }

        float segment = _sampleDistances[high] - _sampleDistances[low];
        float fraction = segment > 0.000001f ? (d - _sampleDistances[low]) / segment : 0f;
        return Mathf.Lerp(low / (float)ArcSamples, high / (float)ArcSamples, fraction);
    }

    private void BuildArcLengthTable()
    {
        if (route == null) return;
        _sampleDistances = new float[ArcSamples + 1];
        Vector3 previous = route.EvaluatePosition(0f);
        for (int i = 1; i <= ArcSamples; i++)
        {
            Vector3 current = route.EvaluatePosition(i / (float)ArcSamples);
            _sampleDistances[i] = _sampleDistances[i - 1] + Vector3.Distance(previous, current);
            previous = current;
        }
        _totalLength = _sampleDistances[ArcSamples];
    }

    private void PlaceCars(bool snapHeight)
    {
        PlaceCar(frontCar, _distance, frontHeightOffset, true, snapHeight);
        PlaceCar(rearCar, Mathf.Max(0f, _distance - carSpacing), rearHeightOffset, false, snapHeight);
    }

    private void PlaceCar(Transform car, float distance, float heightOffset, bool isFront, bool snapHeight)
    {
        if (car == null || route == null || _totalLength <= 0f) return;

        Vector3 position = GetPositionAtDistance(distance);
        position.y += heightOffset;
        if (snapHeight || !_heightInitialized)
        {
            if (isFront) _frontY = position.y;
            else _rearY = position.y;
        }
        else
        {
            float smoothTime = Mathf.Max(0.01f, verticalSmoothTime);
            if (isFront)
                _frontY = Mathf.SmoothDamp(_frontY, position.y, ref _frontYVelocity, smoothTime);
            else
                _rearY = Mathf.SmoothDamp(_rearY, position.y, ref _rearYVelocity, smoothTime);
        }

        position.y = isFront ? _frontY : _rearY;
        car.position = position;
        Vector3 tangent = GetTangentAtDistance(distance);
        if (keepCarsLevel) tangent.y = 0f;
        if (tangent.sqrMagnitude > 0.000001f)
            car.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
        _heightInitialized = true;
    }
}


