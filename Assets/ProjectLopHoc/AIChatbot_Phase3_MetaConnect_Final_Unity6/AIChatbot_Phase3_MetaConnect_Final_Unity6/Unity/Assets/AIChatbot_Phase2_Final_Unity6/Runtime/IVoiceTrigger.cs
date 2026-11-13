using System;

/// <summary>Input-agnostic push-to-talk trigger contract. XR can implement the same events later.</summary>
public interface IVoiceTrigger
{
    event Action Pressed;
    event Action Released;
    event Action Cancelled;
}
