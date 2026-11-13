using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using InputXRController = UnityEngine.InputSystem.XR.XRController;
using SimulatedXRController = UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRSimulatedController;
#endif

/// <summary>Applies tracked XR node poses without requiring an InputAction asset binding.</summary>
public sealed class XRNodePoseDriver : MonoBehaviour
{
    [SerializeField] private XRNode node = XRNode.Head;
    [SerializeField] private bool usePointerPose;
    [SerializeField] public bool trackPosition = true;
    private UnityEngine.XR.InputDevice _device;

    private void OnEnable() => Application.onBeforeRender += ApplyPose;
    private void OnDisable() => Application.onBeforeRender -= ApplyPose;
    private void Update() => ApplyPose();

    private void ApplyPose()
    {
        string handUsage = node == XRNode.LeftHand ? "LeftHand" :
                           node == XRNode.RightHand ? "RightHand" : string.Empty;

 #if ENABLE_INPUT_SYSTEM
        if (!string.IsNullOrEmpty(handUsage))
        {
            // Prefer a genuinely tracked Quest controller. A simulator device
            // can remain registered after its GameObject is inactive, retaining
            // a stale pose; do not let that pose override the XR runtime.
            if (TryApplyInputSystemPose(handUsage, simulatedOnly: false)) return;
        }
 #endif

        if (!_device.isValid || _device.characteristics == InputDeviceCharacteristics.None)
            _device = InputDevices.GetDeviceAtXRNode(node);

        if (_device.isValid && TryApplyLegacyPose()) return;

#if ENABLE_INPUT_SYSTEM
        if (!string.IsNullOrEmpty(handUsage) && Application.isEditor && IsSimulatorActive() &&
            TryApplyInputSystemPose(handUsage, simulatedOnly: true)) return;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool IsSimulatorActive()
    {
        if (!_simulatorLookupComplete)
        {
            _simulatorLookupComplete = true;
            foreach (var behaviour in Resources.FindObjectsOfTypeAll<Behaviour>())
            {
                string typeName = behaviour != null ? behaviour.GetType().Name : string.Empty;
                if (typeName == "XRDeviceSimulator" || typeName == "XRInteractionSimulator")
                {
                    _simulatorBehaviour = behaviour;
                    break;
                }
            }
        }

        return _simulatorBehaviour != null && _simulatorBehaviour.isActiveAndEnabled &&
               _simulatorBehaviour.gameObject.activeInHierarchy;
    }
#endif

    private bool TryApplyLegacyPose()
    {
        bool trackingKnown = _device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState,
            out InputTrackingState trackingState);
        bool positionTracked = trackingKnown && (trackingState & InputTrackingState.Position) != 0;
        bool rotationTracked = trackingKnown && (trackingState & InputTrackingState.Rotation) != 0;

        if (trackPosition && _device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position) &&
            (positionTracked || (!trackingKnown && position.sqrMagnitude > 0.000001f)))
        {
            transform.localPosition = position;
            positionTracked = true;
        }

        if (_device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion rotation) &&
            (rotationTracked || (!trackingKnown && Quaternion.Angle(rotation, Quaternion.identity) > 0.001f)))
        {
            transform.localRotation = rotation;
            rotationTracked = true;
        }

        return positionTracked || rotationTracked;
    }

#if ENABLE_INPUT_SYSTEM
    private bool TryApplyInputSystemPose(string handUsage, bool simulatedOnly)
    {
        // First pass intentionally excludes XR Device Simulator when asking for
        // a physical controller, and vice versa on the second pass.
        for (int pass = 0; pass < 2; pass++)
        {
            bool wantSimulated = pass == 1;
            if (wantSimulated != simulatedOnly) continue;

            foreach (var inputDevice in InputSystem.devices)
            {
                if (!(inputDevice is InputXRController controller)) continue;
                if ((controller is SimulatedXRController) != wantSimulated) continue;

                bool matchesHand = false;
                for (int i = 0; i < controller.usages.Count; i++)
                {
                    if (controller.usages[i].ToString() == handUsage)
                    {
                        matchesHand = true;
                        break;
                    }
                }
                if (!matchesHand) continue;

                var trackingControl = controller.TryGetChildControl<IntegerControl>("trackingState");
                var trackedControl = controller.TryGetChildControl<ButtonControl>("isTracked");
                int tracking = trackingControl != null ? trackingControl.ReadValue() : 0;
                bool tracked = (tracking & (int)(InputTrackingState.Position | InputTrackingState.Rotation)) != 0 ||
                               (trackedControl != null && trackedControl.isPressed);

                string positionName = usePointerPose ? "pointerPosition" : "devicePosition";
                string rotationName = usePointerPose ? "pointerRotation" : "deviceRotation";
                var positionControl = controller.TryGetChildControl<Vector3Control>(positionName);
                var rotationControl = controller.TryGetChildControl<QuaternionControl>(rotationName);
                if (positionControl == null && usePointerPose)
                    positionControl = controller.TryGetChildControl<Vector3Control>("devicePosition");
                if (rotationControl == null && usePointerPose)
                    rotationControl = controller.TryGetChildControl<QuaternionControl>("deviceRotation");

                Vector3 position = positionControl != null ? positionControl.ReadValue() : Vector3.zero;
                Quaternion rotation = rotationControl != null ? rotationControl.ReadValue() : Quaternion.identity;
                if (simulatedOnly && position.sqrMagnitude <= 0.000001f &&
                    Quaternion.Angle(rotation, Quaternion.identity) <= 0.001f)
                    continue;
                if (!tracked && position.sqrMagnitude <= 0.000001f &&
                    Quaternion.Angle(rotation, Quaternion.identity) <= 0.001f)
                    continue;

                if (trackPosition && positionControl != null) transform.localPosition = position;
                if (rotationControl != null) transform.localRotation = rotation;
                return (trackPosition && positionControl != null) || rotationControl != null;
            }
        }

        return false;
    }
#endif

#if ENABLE_INPUT_SYSTEM
    private static Behaviour _simulatorBehaviour;
    private static bool _simulatorLookupComplete;
#endif
}
