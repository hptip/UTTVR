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
    private Camera _xrCamera;

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
        // Keep ownership in the original scene. Parenting to the persistent XR
        // camera would migrate classroom UI into DontDestroyOnLoad.
        _xrCamera = xrCamera;
        canvasTransform.SetPositionAndRotation(xrCamera.transform.position + xrCamera.transform.forward * distanceFromCamera, xrCamera.transform.rotation);
        canvasTransform.localScale = Vector3.one * worldScale;

        if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        GraphicRaycaster standardRaycaster = GetComponent<GraphicRaycaster>();
        if (standardRaycaster != null)
            standardRaycaster.enabled = false;

        _configured = true;
    }
    private void LateUpdate()
    {
        if (!_configured) return;
        if (_xrCamera == null) { _configured = false; return; }
        transform.SetPositionAndRotation(_xrCamera.transform.position + _xrCamera.transform.forward * distanceFromCamera, _xrCamera.transform.rotation);
    }
}
