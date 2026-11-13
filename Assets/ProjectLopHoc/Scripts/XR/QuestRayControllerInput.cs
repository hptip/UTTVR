using UnityEngine;
using UnityEngine.XR;

using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using InputController = UnityEngine.InputSystem.XR.XRController;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
#endif

/// <summary>Bridges Quest trigger state into XRI ray selection and tracked-device UI press.</summary>
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor))]
public sealed class QuestRayControllerInput : MonoBehaviour, IXRInputButtonReader
{
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    private UnityEngine.XR.InputDevice _device;
    private bool _pressed;
    private bool _previous;
    private bool _hasInputSource;

    private void Awake()
    {
        var ray = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        var selectReader = new XRInputButtonReader("Quest Trigger Select");
        selectReader.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
        selectReader.SetObjectReference(this);
        ray.selectInput = selectReader;

        var uiReader = new XRInputButtonReader("Quest Trigger UI Press");
        uiReader.inputSourceMode = XRInputButtonReader.InputSourceMode.ObjectReference;
        uiReader.SetObjectReference(this);
        ray.uiPressInput = uiReader;
    }

    private void Update()
    {
        _previous = _pressed;
        _hasInputSource = false;

        // A simulator can still be enabled while the first local player is
        // spawning. Use the same priority as the existing pose driver: tracked
        // real controllers first, simulator only when no physical hand is tracked.
        if (!_device.isValid) _device = InputDevices.GetDeviceAtXRNode(controllerNode);
        bool trackingKnown = _device.isValid && _device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState, out _);
        bool tracked = _device.isValid &&
            ((_device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState, out InputTrackingState state) && state != InputTrackingState.None) ||
             (_device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool isTracked) && isTracked) ||
             (!trackingKnown && _device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position) && position.sqrMagnitude > .000001f));
        if (tracked) { ReadNative(); return; }

#if ENABLE_INPUT_SYSTEM
        string hand = controllerNode == XRNode.LeftHand ? "LeftHand" : "RightHand";
        foreach (var input in InputSystem.devices)
        {
            if (!(input is InputController controller) || controller is XRSimulatedController || !controller.added) continue;
            bool matches = false; foreach (var usage in controller.usages) if (usage == hand) { matches = true; break; }
            if (!matches) continue;
            int inputTracking = controller.TryGetChildControl<IntegerControl>("trackingState")?.ReadValue() ?? 0;
            if ((inputTracking & 3) == 0 && controller.TryGetChildControl<ButtonControl>("isTracked")?.isPressed != true) continue;
            _pressed = controller.TryGetChildControl<ButtonControl>("triggerButton")?.isPressed == true ||
                       (controller.TryGetChildControl<AxisControl>("trigger")?.ReadValue() ?? 0) >= .55f;
            _hasInputSource = true;
            return;
        }
        // XR Device Simulator publishes its controllers through the Input System,
        // not through UnityEngine.XR.InputDevice. Read the simulator action for
        // the hand currently being manipulated: the simulated device can retain
        // a stale trigger button after that hand stops being manipulated.
        if (Application.isEditor)
        {
#pragma warning disable CS0618
            var simulator = Object.FindAnyObjectByType<XRDeviceSimulator>();
#pragma warning restore CS0618
            if (simulator != null && simulator.isActiveAndEnabled && simulator.gameObject.activeInHierarchy)
            {
                bool isManipulated = controllerNode == XRNode.LeftHand
                    ? simulator.manipulatingLeftController
                    : simulator.manipulatingRightController;
                var triggerAction = simulator.triggerAction != null ? simulator.triggerAction.action : null;
                _pressed = isManipulated && triggerAction != null && triggerAction.IsPressed();
                _hasInputSource = true;
                return;
            }
        }
#endif

        ReadNative();
    }

    private void ReadNative()
    {
        _pressed = _device.isValid &&
                   ((_device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool button) && button) ||
                    (_device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trigger) && trigger >= 0.55f));
        _hasInputSource = _device.isValid;
    }

    public bool ReadIsPerformed() => _pressed;
    public bool ReadWasPerformedThisFrame() => _pressed && !_previous;
    public bool ReadWasCompletedThisFrame() => !_pressed && _previous;
    public float ReadValue() => _pressed ? 1f : 0f;
    public bool TryReadValue(out float value)
    {
        value = ReadValue();
        return _hasInputSource;
    }
}
