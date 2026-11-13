using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>Applies the reduced standing camera height only while YenTuHaLongXanh is loaded.</summary>
public sealed class YenTuHaLongCameraHeight : MonoBehaviour
{
    [SerializeField, Range(0.5f, 1f)] private float standingHeightMultiplier = 0.53333336f;

    private XROrigin _xrOrigin;
    private float _originalCameraYOffset;
    private float _originalCameraOffsetLocalY;
    private float _originalEffectiveCameraHeight;
    private bool _hasOriginalHeight;
    private FirstPersonController _playerController;
    private float _originalEyeHeight;
    private bool _hasOriginalEyeHeight;
    private bool _isSeated;
    private float _nextLookupTime;

    private void OnEnable() => ApplyHeight();

    private void Start() => ApplyHeight();

    private void Update()
    {
        bool needsLookup = _xrOrigin == null || _playerController == null;
        if (needsLookup && Time.unscaledTime >= _nextLookupTime)
            ApplyHeight();
    }

    private void LateUpdate()
    {
        // XROrigin can re-apply its offset late in the frame. Keep the rendered camera
        // height synchronized even when the Editor simulator has no active XR subsystem.
        ApplyXROriginHeight();
    }

    private void OnDestroy() => RestoreOriginalHeight();

    public void SetSeated(bool seated)
    {
        _isSeated = seated;
        ApplyHeight();
    }

    private void ApplyHeight()
    {
        GameObject originObject = GameObject.Find("XR Origin (VR)");
        XROrigin origin = originObject != null ? originObject.GetComponent<XROrigin>() : null;
        _nextLookupTime = Time.unscaledTime + 0.25f;

        if (origin != null && _xrOrigin != origin)
        {
            if (_xrOrigin != null && _hasOriginalHeight)
                _xrOrigin.CameraYOffset = _originalCameraYOffset;
            _xrOrigin = origin;
            _originalCameraYOffset = origin.CameraYOffset;
            Transform offset = origin.CameraFloorOffsetObject != null
                ? origin.CameraFloorOffsetObject.transform
                : null;
            _originalCameraOffsetLocalY = offset != null ? offset.localPosition.y : origin.CameraYOffset;
            _originalEffectiveCameraHeight = origin.Camera != null
                ? origin.CameraInOriginSpaceHeight
                : _originalCameraOffsetLocalY;
            _hasOriginalHeight = true;
        }

        ApplyXROriginHeight();

        // When Play Mode starts directly in this scene, there may be no persistent XR Origin.
        // The local player then drives the standalone Main Camera using FirstPersonController.eyeHeight.
        FirstPersonController localPlayer = null;
        FirstPersonController[] players = FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None);
        foreach (FirstPersonController player in players)
        {
            if (player != null && player.HasStateAuthority)
            {
                localPlayer = player;
                break;
            }
        }

        if (localPlayer != null && _playerController != localPlayer)
        {
            if (_playerController != null && _hasOriginalEyeHeight)
                _playerController.eyeHeight = _originalEyeHeight;
            _playerController = localPlayer;
            _originalEyeHeight = localPlayer.eyeHeight;
            _hasOriginalEyeHeight = true;
        }

        if (_playerController != null && _hasOriginalEyeHeight)
        {
            _playerController.eyeHeight = _isSeated
                ? _originalEyeHeight
                : _originalEyeHeight * standingHeightMultiplier;
        }
    }

    private void ApplyXROriginHeight()
    {
        if (_xrOrigin == null || !_hasOriginalHeight) return;

        float desiredYOffset = _isSeated
            ? _originalCameraYOffset
            : _originalCameraYOffset * standingHeightMultiplier;
        _xrOrigin.CameraYOffset = desiredYOffset;

        GameObject floorOffsetObject = _xrOrigin.CameraFloorOffsetObject;
        if (floorOffsetObject == null) return;

        Transform offset = floorOffsetObject.transform;
        Vector3 localPosition = offset.localPosition;
        if (_xrOrigin.CurrentTrackingOriginMode == UnityEngine.XR.TrackingOriginModeFlags.Floor)
        {
            float desiredCameraHeight = _isSeated
                ? _originalEffectiveCameraHeight
                : _originalEffectiveCameraHeight * standingHeightMultiplier;
            float cameraLocalHeight = _xrOrigin.Camera != null ? _xrOrigin.Camera.transform.localPosition.y : 0f;
            localPosition.y = desiredCameraHeight - cameraLocalHeight;
        }
        else
        {
            // Device mode uses CameraYOffset. Unknown mode in XR Simulator does not move
            // CameraFloorOffsetObject from the property setter, so synchronize it directly.
            localPosition.y = _isSeated ? _originalCameraOffsetLocalY : desiredYOffset;
        }

        offset.localPosition = localPosition;
    }

    private void RestoreOriginalHeight()
    {
        if (_hasOriginalHeight && _xrOrigin != null)
        {
            _xrOrigin.CameraYOffset = _originalCameraYOffset;
            if (_xrOrigin.CameraFloorOffsetObject != null)
            {
                Transform offset = _xrOrigin.CameraFloorOffsetObject.transform;
                Vector3 localPosition = offset.localPosition;
                localPosition.y = _originalCameraOffsetLocalY;
                offset.localPosition = localPosition;
            }
        }
        if (_hasOriginalEyeHeight && _playerController != null)
            _playerController.eyeHeight = _originalEyeHeight;

        _xrOrigin = null;
        _hasOriginalHeight = false;
        _playerController = null;
        _hasOriginalEyeHeight = false;
    }
}
