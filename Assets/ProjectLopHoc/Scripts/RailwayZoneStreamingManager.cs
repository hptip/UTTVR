using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads fixed railway/environment zone scenes around TrainSplineMover.currentDistanceMeters.
/// Zones are authored offline and are never randomly generated at runtime.
/// </summary>
public sealed class RailwayZoneStreamingManager : MonoBehaviour
{
    [Serializable]
    public sealed class ZoneRecord
    {
        public string zoneId;
        public float startChainage;
        public float endChainage;
        [ScenePath] public string scenePath;
    }

    /// <summary>One indivisible fixed facility. Every listed zone is retained while the train traverses it.</summary>
    [Serializable]
    public sealed class FixedInfrastructureRecord
    {
        public string id;
        public string kind;
        public float startChainage;
        public float endChainage;
        public string zoneId;
        public string mountainAssetReference;
        public string tunnelAssetReference;
        public string[] zoneIds = Array.Empty<string>();
    }

    [AttributeUsage(AttributeTargets.Field)]
    private sealed class ScenePathAttribute : PropertyAttribute { }

    [Header("Route")]
    public TrainSplineMover trainMover;
    public float routeLengthMeters = 28811.06f;
    public float zoneLengthMeters = 500f;

    [Header("Streaming distances")]
    [Min(0f)] public float loadAheadMeters = 650f;
    [Min(0f)] public float keepBehindMeters = 150f;
    [Min(0f)] public float loadSafetyMarginSeconds = 12f;
    [Min(0f)] public float maximumAdaptiveLoadAheadMeters = 2000f;
    [Min(0.05f)] public float updateIntervalSeconds = 0.25f;
    [Min(0f)] public float boundaryHysteresisMeters = 25f;
    private float _trainRearExtentMeters;
    [Header("Main-thread loading budget")]
    public bool useLowBackgroundLoadingPriority = true;
    private UnityEngine.ThreadPriority _previousLoadingPriority;
    private bool _changedLoadingPriority;

    private void OnEnable()
    {
        if (!useLowBackgroundLoadingPriority) return;
        _previousLoadingPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.Low;
        _changedLoadingPriority = true;
    }

    private void OnDisable()
    {
        RailwayStreamingBudget.Release(this);
        if (_changedLoadingPriority)
            Application.backgroundLoadingPriority = _previousLoadingPriority;
        _changedLoadingPriority = false;
    }

    [Header("Fixed data")]
    public List<ZoneRecord> zones = new List<ZoneRecord>();
    public List<FixedInfrastructureRecord> fixedInfrastructure = new List<FixedInfrastructureRecord>();

    [Header("Diagnostics")]
    public bool logZoneTransitions;

    private readonly Dictionary<string, AsyncOperation> _loads = new Dictionary<string, AsyncOperation>(StringComparer.Ordinal);
    private readonly Dictionary<string, AsyncOperation> _unloads = new Dictionary<string, AsyncOperation>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _loadStartedAt = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _loadReachedActivationAt = new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly HashSet<string> _failedLoads = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _required = new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> _completedLoads = new List<string>();
    private readonly List<string> _completedUnloads = new List<string>();
    private readonly Dictionary<string, RailwayZoneActivation> _activationGates = new Dictionary<string, RailwayZoneActivation>();
    private float _nextUpdate;
    private float _lastDistance;
    private float _estimatedLoadDurationSeconds = 1.5f;
    private int _travelDirection = 1;

    private void Start()
    {
        if (trainMover == null)
            Debug.LogError("RailwayZoneStreamingManager: TrainSplineMover reference is missing.", this);
        _lastDistance = trainMover != null ? trainMover.currentDistanceMeters : 0f;
        MeasureTrainEnvelope();
        ReconcileZones(true);
    }

    private void MeasureTrainEnvelope()
    {
        if (trainMover == null || trainMover.frontCar == null) return;
        // currentDistanceMeters is the frontCar pivot; rearCar is carSpacing
        // behind it. Include geometry beyond that pivot instead of treating it
        // as the train's nose or tail.
        float rearOverhang = 0f;
        if (trainMover.rearCar != null)
        {
            Transform rear = trainMover.rearCar;
            foreach (Renderer renderer in rear.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Bounds b = renderer.bounds;
                float projectedRadius = Mathf.Abs(rear.forward.x) * b.extents.x +
                    Mathf.Abs(rear.forward.y) * b.extents.y + Mathf.Abs(rear.forward.z) * b.extents.z;
                rearOverhang = Mathf.Max(rearOverhang,
                    -Vector3.Dot(b.center - rear.position, rear.forward) + projectedRadius);
            }
        }
        _trainRearExtentMeters = Mathf.Max(0f, trainMover.carSpacing) + rearOverhang;
    }

    private void Update()
    {
        PollOperations();
        if (trainMover == null || zones.Count == 0 || Time.unscaledTime < _nextUpdate) return;
        _nextUpdate = Time.unscaledTime + Mathf.Max(0.05f, updateIntervalSeconds);

        float distance = Mathf.Clamp(trainMover.currentDistanceMeters, 0f, Mathf.Max(routeLengthMeters, trainMover.totalRouteLength));
        float delta = distance - _lastDistance;
        if (Mathf.Abs(delta) > 0.05f) _travelDirection = delta >= 0f ? 1 : -1;
        _lastDistance = distance;
        ReconcileZones(false);
    }

    private void ReconcileZones(bool force)
    {
        float distance = trainMover != null ? Mathf.Clamp(trainMover.currentDistanceMeters, 0f, routeLengthMeters) : 0f;
        float effectiveLoadAhead = GetEffectiveLoadAheadMeters();
        float retainedBehind = keepBehindMeters + _trainRearExtentMeters;
        float min = Mathf.Max(0f, distance - (_travelDirection >= 0 ? retainedBehind : effectiveLoadAhead));
        float max = Mathf.Min(routeLengthMeters, distance + (_travelDirection >= 0 ? effectiveLoadAhead : keepBehindMeters));
        _required.Clear();

        foreach (var zone in zones)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.zoneId) || string.IsNullOrWhiteSpace(zone.scenePath)) continue;
            Scene existing = SceneManager.GetSceneByPath(zone.scenePath);
            float hysteresis = existing.isLoaded || _loads.ContainsKey(zone.zoneId) ? boundaryHysteresisMeters : 0f;
            if (zone.endChainage >= min - hysteresis && zone.startChainage <= max + hysteresis)
                _required.Add(zone.zoneId);
            // Adaptive lookahead can shrink after a fast load. Do not discard
            // an already prefetched zone still ahead of the train, then load
            // it again moments later. Release it after traversal (or reversal).
            if (hysteresis > 0f &&
                (_travelDirection >= 0
                    ? zone.startChainage > distance && zone.endChainage >= min - hysteresis
                    : zone.endChainage < distance && zone.startChainage <= max + hysteresis))
                _required.Add(zone.zoneId);
        }

        // Keep all parts of a bridge or tunnel/mountain group together near and throughout traversal.
        foreach (var facility in fixedInfrastructure)
        {
            if (facility == null || string.IsNullOrWhiteSpace(facility.id)) continue;
            float groupMin = Mathf.Max(0f, facility.startChainage - (_travelDirection >= 0 ? effectiveLoadAhead : retainedBehind));
            float groupMax = Mathf.Min(routeLengthMeters, facility.endChainage + (_travelDirection >= 0 ? retainedBehind : effectiveLoadAhead));
            if (distance < groupMin || distance > groupMax) continue;

            if (facility.zoneIds != null)
                foreach (string id in facility.zoneIds)
                    if (!string.IsNullOrWhiteSpace(id)) _required.Add(id);
            if (!string.IsNullOrWhiteSpace(facility.zoneId)) _required.Add(facility.zoneId);
        }

        bool pendingRequiredLoad = false;
        foreach (var zone in zones)
        {
            if (zone == null || !_required.Contains(zone.zoneId) || _failedLoads.Contains(zone.zoneId)) continue;
            if (!SceneManager.GetSceneByPath(zone.scenePath).isLoaded && !_unloads.ContainsKey(zone.zoneId)) { pendingRequiredLoad = true; break; }
        }
        foreach (var zone in zones)
        {
            if (zone == null || string.IsNullOrWhiteSpace(zone.zoneId) || string.IsNullOrWhiteSpace(zone.scenePath)) continue;
            bool required = _required.Contains(zone.zoneId);
            Scene scene = SceneManager.GetSceneByPath(zone.scenePath);
            bool loaded = scene.IsValid() && scene.isLoaded;

            if (required && !loaded && !_loads.ContainsKey(zone.zoneId) && !_unloads.ContainsKey(zone.zoneId) && !_failedLoads.Contains(zone.zoneId))
            {
                // Serialize integration work; do not start several additive
                // integrations/unloads together at startup or a zone boundary.
                if (_loads.Count > 0 || _unloads.Count > 0) continue;
                if (!RailwayStreamingBudget.TryAcquire(this)) continue;
                AsyncOperation operation = SceneManager.LoadSceneAsync(zone.scenePath, LoadSceneMode.Additive);
                if (operation == null)
                {
                    _failedLoads.Add(zone.zoneId);
                    RailwayStreamingBudget.Release(this);
                    Debug.LogError($"RailwayZoneStreamingManager: Could not load {zone.scenePath}. Is it included in the active Build Profile?", this);
                }
                else
                {
                    _loads.Add(zone.zoneId, operation);
                    _loadStartedAt[zone.zoneId] = Time.realtimeSinceStartup;
                    _loadReachedActivationAt.Remove(zone.zoneId);
                    if (logZoneTransitions) Debug.Log($"Loading {zone.zoneId} ({zone.startChainage:F0}-{zone.endChainage:F0} m)", this);
                }
            }
            else if (!required && loaded && !_unloads.ContainsKey(zone.zoneId) && !_loads.ContainsKey(zone.zoneId))
            {
                if (pendingRequiredLoad) continue;
                if (_loads.Count > 0 || _unloads.Count > 0) continue;
                if (!RailwayStreamingBudget.TryAcquire(this)) continue;
                AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                if (operation != null)
                {
                    _unloads.Add(zone.zoneId, operation);
                    if (logZoneTransitions) Debug.Log($"Unloading {zone.zoneId}", this);
                }
                else RailwayStreamingBudget.Release(this);
            }
        }
    }

    private float GetEffectiveLoadAheadMeters()
    {
        if (trainMover == null)
            return loadAheadMeters;

        float speedMetersPerSecond = Mathf.Max(0f, trainMover.currentSpeedKmh / 3.6f);
        float requiredDistance = speedMetersPerSecond *
            (_estimatedLoadDurationSeconds + Mathf.Max(0f, loadSafetyMarginSeconds));
        return Mathf.Clamp(Mathf.Max(loadAheadMeters, requiredDistance), loadAheadMeters,
            Mathf.Max(loadAheadMeters, maximumAdaptiveLoadAheadMeters));
    }

    private void PollOperations()
    {
        _completedLoads.Clear();
        foreach (var pair in _loads)
        {
            if (pair.Value != null && !pair.Value.isDone)
            {
                if (pair.Value.progress >= 0.9f && !_loadReachedActivationAt.ContainsKey(pair.Key))
                    _loadReachedActivationAt[pair.Key] = Time.realtimeSinceStartup;
                continue;
            }
            if (!_activationGates.TryGetValue(pair.Key, out var gate))
            {
                foreach (var zone in zones)
                {
                    if (zone == null || zone.zoneId != pair.Key) continue;
                    Scene scene = SceneManager.GetSceneByPath(zone.scenePath);
                    if (scene.IsValid() && scene.isLoaded)
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            gate = root.GetComponent<RailwayZoneActivation>();
                            if (gate != null) break;
                        }
                    break;
                }
                _activationGates[pair.Key] = gate;
            }
            // Do not overlap a heavy resource integration with the authored
            // zone's staggered activation. The next resource waits its turn.
            if (gate != null && !gate.IsReady) continue;
            _completedLoads.Add(pair.Key);
        }
        foreach (string id in _completedLoads)
        {
            float completedAt = Time.realtimeSinceStartup;
            if (_loadStartedAt.TryGetValue(id, out float startedAt))
            {
                float elapsed = Mathf.Max(0f, completedAt - startedAt);
                _estimatedLoadDurationSeconds = Mathf.Max(elapsed, _estimatedLoadDurationSeconds * 0.9f);
                if (_loadReachedActivationAt.TryGetValue(id, out float reachedActivationAt))
                {
                    float preparation = Mathf.Max(0f, reachedActivationAt - startedAt);
                    float activation = Mathf.Max(0f, completedAt - reachedActivationAt);
                    if (logZoneTransitions)
                        Debug.Log($"[RailwayStreamingTiming] {id}: load-to-activation={preparation:F3}s, activation={activation:F3}s, effectiveAhead={GetEffectiveLoadAheadMeters():F0}m", this);
                }
                else if (logZoneTransitions)
                {
                    Debug.Log($"[RailwayStreamingTiming] {id}: totalAsyncLoad={elapsed:F3}s (activation boundary not sampled), effectiveAhead={GetEffectiveLoadAheadMeters():F0}m", this);
                }
            }
            _loads.Remove(id);
            _loadStartedAt.Remove(id);
            _loadReachedActivationAt.Remove(id);
            _activationGates.Remove(id);
        }

        _completedUnloads.Clear();
        foreach (var pair in _unloads)
            if (pair.Value == null || pair.Value.isDone) _completedUnloads.Add(pair.Key);
        foreach (string id in _completedUnloads) _unloads.Remove(id);
        if (_loads.Count == 0 && _unloads.Count == 0) RailwayStreamingBudget.Release(this);
    }

    /// <summary>Allows an explicit retry after a scene asset or Build Profile is corrected.</summary>
    public void RetryFailedZoneLoads()
    {
        _failedLoads.Clear();
        ReconcileZones(true);
    }

    public int LoadedZoneCount
    {
        get
        {
            int count = 0;
            foreach (var zone in zones)
            {
                if (zone == null) continue;
                Scene scene = SceneManager.GetSceneByPath(zone.scenePath);
                if (scene.IsValid() && scene.isLoaded) count++;
            }
            return count;
        }
    }
}
