using UnityEngine;

/// <summary>Starts/resumes the tabletop train while a player is near the display.</summary>
[DisallowMultipleComponent]
public sealed class SaBanPresenceGate : MonoBehaviour
{
    public SaBanDemoMotionController train;
    [Min(0.1f)] public float activationRadius = 2f;
    [Min(0.05f)] public float pollInterval = 0.2f;
    public Transform explicitTarget;
    public bool autoFindMainCamera = true;
    public bool findPlayerTag = true;

    private Bounds _displayBounds;
    private float _nextPollTime;
    private bool _hasBounds;
    private bool _presenceState;
    private bool _warnedNoTarget;

    private void Awake()
    {
        if (train == null) train = GetComponentInChildren<SaBanDemoMotionController>(true);
        CacheDisplayBounds();
    }

    private void Start()
    {
        if (train == null)
        {
            Debug.LogError("Sa bàn: chưa gắn SaBanDemoMotionController.", this);
            enabled = false;
            return;
        }

        train.requirePresence = true;
        SetPresence(false);
        CheckPresence();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextPollTime) return;
        _nextPollTime = Time.unscaledTime + Mathf.Max(0.05f, pollInterval);
        CheckPresence();
    }

    private void CacheDisplayBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (!_hasBounds)
            {
                _displayBounds = renderer.bounds;
                _hasBounds = true;
            }
            else _displayBounds.Encapsulate(renderer.bounds);
        }
        if (!_hasBounds)
            _displayBounds = new Bounds(transform.position, Vector3.zero);
    }

    private void CheckPresence()
    {
        bool nearby = false;
        bool foundTarget = false;

        if (explicitTarget != null)
        {
            foundTarget = true;
            nearby = IsNear(explicitTarget.position);
        }

        if (!nearby && autoFindMainCamera)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                foundTarget = true;
                nearby = IsNear(mainCamera.transform.position);
            }

            // Some scenes use an XR or spectator camera that is not tagged MainCamera.
            // Fall back to any enabled camera so the prefab still responds after being dragged
            // into scenes with a different camera rig.
            if (!nearby)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                for (int i = 0; i < cameras.Length; i++)
                {
                    Camera camera = cameras[i];
                    if (camera == null || !camera.isActiveAndEnabled) continue;
                    foundTarget = true;
                    if (IsNear(camera.transform.position))
                    {
                        nearby = true;
                        break;
                    }
                }
            }
        }

        if (!nearby && findPlayerTag)
        {
            try
            {
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] == null) continue;
                    foundTarget = true;
                    if (IsNear(players[i].transform.position))
                    {
                        nearby = true;
                        break;
                    }
                }
            }
            catch (UnityException)
            {
                // The Player tag may be removed from a project's Tag Manager.
            }
        }

        if (!foundTarget && !_warnedNoTarget)
        {
            Debug.LogWarning("Sa bàn đang chờ người chơi. Hãy dùng Camera có tag MainCamera, tag Player, " +
                             "hoặc gán Explicit Target trong SaBanPresenceGate.", this);
            _warnedNoTarget = true;
        }
        if (foundTarget) _warnedNoTarget = false;
        SetPresence(nearby);
    }

    private bool IsNear(Vector3 targetPosition)
    {
        Vector3 closest = _displayBounds.ClosestPoint(targetPosition);
        return (targetPosition - closest).sqrMagnitude <= activationRadius * activationRadius;
    }

    private void SetPresence(bool present)
    {
        if (_presenceState == present && train != null) return;
        _presenceState = present;
        if (train != null) train.SetPresenceActive(present);
    }
}
