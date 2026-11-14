using System;
using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Keeps a world-space panel in front of the viewer without shaking. The pose is held relative to the
/// XR Origin (the rig), so the panel rides along exactly when the rig is carried, e.g. on a moving train,
/// and is applied in Application.onBeforeRender, after trains and the rig have moved for the frame.
/// The panel only recentres, smoothly, once the head turns or walks away from it.
/// </summary>
public sealed class XRRigFollowPanel : MonoBehaviour
{
    public Camera viewCamera;
    [Min(0.3f)] public float distance = 1.5f;
    [Range(10f, 90f)] public float recenterAngle = 40f;
    [Min(0.1f)] public float recenterDistance = 0.6f;
    [Min(0.5f)] public float recenterSpeed = 5f;

    /// <summary>Raised after the panel pose is written each frame, so attached UI (e.g. a keyboard) can follow in the same frame.</summary>
    public event Action<Transform> Positioned;

    private Transform _rig;
    private Vector3 _localPosition;
    private Quaternion _localRotation = Quaternion.identity;
    private bool _placed;
    private bool _recentering;
    private bool _contentVisible;

    /// <summary>Places the panel straight in front of the viewer on the next render.</summary>
    public void Snap() => _placed = false;

    private void OnEnable()
    {
        _placed = false;
        Application.onBeforeRender += Follow;
    }

    private void OnDisable() => Application.onBeforeRender -= Follow;

    private void Follow()
    {
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) return;
        if (_rig == null) _rig = ResolveRig();

        // Panels whose content is toggled under an always-active canvas (the chatbot) snap when they reappear.
        bool visible = HasActiveContent();
        if (visible && !_contentVisible) _placed = false;
        _contentVisible = visible;

        Transform head = viewCamera.transform;
        Vector3 headLocal = ToRigPoint(head.position);
        Vector3 forwardLocal = ToRigDirection(head.forward);
        Vector3 targetPosition = headLocal + forwardLocal * distance;
        Quaternion targetRotation = Quaternion.LookRotation(forwardLocal, Vector3.up);

        if (!_placed)
        {
            _localPosition = targetPosition;
            _localRotation = targetRotation;
            _placed = true;
            _recentering = false;
        }
        else
        {
            Vector3 toPanel = _localPosition - headLocal;
            if (!_recentering && (Vector3.Angle(forwardLocal, toPanel) > recenterAngle || Mathf.Abs(toPanel.magnitude - distance) > recenterDistance))
                _recentering = true;
            if (_recentering)
            {
                float t = 1f - Mathf.Exp(-recenterSpeed * Time.unscaledDeltaTime);
                _localPosition = Vector3.Lerp(_localPosition, targetPosition, t);
                _localRotation = Quaternion.Slerp(_localRotation, targetRotation, t);
                if ((_localPosition - targetPosition).sqrMagnitude < 0.0004f) _recentering = false;
            }
        }

        if (_rig != null) transform.SetPositionAndRotation(_rig.TransformPoint(_localPosition), _rig.rotation * _localRotation);
        else transform.SetPositionAndRotation(_localPosition, _localRotation);
        Positioned?.Invoke(transform);
    }

    private Transform ResolveRig()
    {
        XROrigin origin = viewCamera.GetComponentInParent<XROrigin>();
        if (origin == null) origin = FindFirstObjectByType<XROrigin>();
        if (origin != null && origin.Camera == viewCamera) return origin.transform;
        return viewCamera.transform.parent;
    }

    private bool HasActiveContent()
    {
        for (int i = 0; i < transform.childCount; i++)
            if (transform.GetChild(i).gameObject.activeSelf) return true;
        return false;
    }

    private Vector3 ToRigPoint(Vector3 world) => _rig != null ? _rig.InverseTransformPoint(world) : world;
    private Vector3 ToRigDirection(Vector3 world) => _rig != null ? _rig.InverseTransformDirection(world) : world;
}
