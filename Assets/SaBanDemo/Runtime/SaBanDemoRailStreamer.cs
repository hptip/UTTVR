using UnityEngine;
using UnityEngine.Splines;

/// <summary>Creates only the sa bàn rail segments around its standalone train.</summary>
[DisallowMultipleComponent]
public sealed class SaBanDemoRailStreamer : MonoBehaviour
{
    public SaBanDemoMotionController train;
    public SplineContainer route;
    public GameObject railPrefab;
    public float railVisualScale = 60f;
    public float segmentSpacing = 0.0124f;
    public int poolSize = 180;
    public float behindDistance = 0.15f;
    public float railHeightOffset = 0.012f;

    private GameObject[] _pool;
    private float _lastBaseDistance = -1f;

    private void Start()
    {
        // Recover serialized links when this prefab is copied between projects.
        if (train == null) train = GetComponentInChildren<SaBanDemoMotionController>(true);
        if (route == null && train != null) route = train.route;

        if (train == null || route == null || railPrefab == null)
        {
            Debug.LogError($"Sa bàn: thiếu liên kết (tàu={train != null}, spline={route != null}, prefab ray={railPrefab != null}).", this);
            enabled = false;
            return;
        }

        _pool = new GameObject[Mathf.Max(1, poolSize)];
        GameObject poolRoot = new GameObject("SaBan_Rail_Segment_Pool");
        poolRoot.transform.SetParent(transform, false);
        poolRoot.transform.localScale = Vector3.one * Mathf.Max(0.1f, railVisualScale);
        for (int i = 0; i < _pool.Length; i++)
        {
            _pool[i] = Instantiate(railPrefab, poolRoot.transform);
            _pool[i].name = "SaBan_Rail_Segment_" + i.ToString("000");
            _pool[i].SetActive(false);
        }
        UpdateRails(true);
    }

    private void LateUpdate() => UpdateRails(false);

    private void UpdateRails(bool force)
    {
        if (_pool == null || train == null || train.TotalLength <= 0f) return;
        float spacing = Mathf.Max(0.001f, segmentSpacing);
        float firstDistance = Mathf.Max(0f, train.CurrentDistance - behindDistance);
        float baseDistance = Mathf.Floor(firstDistance / spacing) * spacing;
        if (!force && Mathf.Approximately(baseDistance, _lastBaseDistance)) return;
        _lastBaseDistance = baseDistance;

        for (int i = 0; i < _pool.Length; i++)
        {
            float distance = baseDistance + i * spacing;
            GameObject segment = _pool[i];
            if (distance > train.TotalLength)
            {
                segment.SetActive(false);
                continue;
            }

            Vector3 position = train.GetPositionAtDistance(distance);
            Vector3 up = route.EvaluateUpVector(distance / train.TotalLength);
            position += (up.sqrMagnitude > 0.000001f ? up.normalized : Vector3.up) * railHeightOffset;
            segment.transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(train.GetTangentAtDistance(distance), up.sqrMagnitude > 0.000001f ? up : Vector3.up));
            if (!segment.activeSelf) segment.SetActive(true);
        }
    }
}
