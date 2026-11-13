#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

// Test-only controller input reader; excluded from Player builds.
public sealed class ShowroomMergeTestTrigger:MonoBehaviour,IXRInputButtonReader
{
    public bool pressed,edge;
    public bool ReadIsPerformed()=>pressed;
    public bool ReadWasPerformedThisFrame()=>edge;
    public bool ReadWasCompletedThisFrame()=>!pressed;
    public float ReadValue()=>pressed?1:0;
    public bool TryReadValue(out float value){value=ReadValue();return true;}
}
#endif
