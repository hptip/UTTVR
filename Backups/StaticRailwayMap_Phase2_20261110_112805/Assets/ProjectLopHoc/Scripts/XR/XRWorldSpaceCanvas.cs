using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using UnityEngine.UI;

/// <summary>Moves an existing screen overlay panel into reachable world space when an XR Origin is present.</summary>
[RequireComponent(typeof(Canvas))]
public sealed class XRWorldSpaceCanvas : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float distanceFromCamera = 1.5f;
    [SerializeField, Min(0.0001f)] private float worldScale = 0.001f;

    private Canvas _canvas;
    private bool _configured;

    private void Awake() => _canvas = GetComponent<Canvas>();

    private void Start() => ConfigureForXR();

    private void Update()
    {
        if (!_configured)
            ConfigureForXR();
    }

    private void ConfigureForXR()
    {
        if (_canvas == null)
            _canvas = GetComponent<Canvas>();

        XROrigin origin = FindFirstObjectByType<XROrigin>();
        Camera xrCamera = origin != null ? origin.Camera : null;
        if (_canvas == null || xrCamera == null)
            return;

        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = xrCamera;

        Transform canvasTransform = transform;
        canvasTransform.SetParent(xrCamera.transform, false);
        canvasTransform.localPosition = new Vector3(0f, 0f, distanceFromCamera);
        canvasTransform.localRotation = Quaternion.identity;
        canvasTransform.localScale = Vector3.one * worldScale;

        if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        GraphicRaycaster standardRaycaster = GetComponent<GraphicRaycaster>();
        if (standardRaycaster != null)
            standardRaycaster.enabled = false;

        _configured = true;
    }
}
