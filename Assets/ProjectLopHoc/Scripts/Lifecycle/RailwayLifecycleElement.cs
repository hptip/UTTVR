using UnityEngine;

/// <summary>Scene-local identity; source mesh ID is kept separately for future database mapping.</summary>
[DisallowMultipleComponent]
public sealed class RailwayLifecycleElement : MonoBehaviour
{
    public string modelID="QLVDTS-Z021-R-M001";
    public string sourceMeshID;
    public string componentName;
    public ElementIdentity identity;
    public string ID=>identity!=null?identity.ElementID:modelID+"/"+sourceMeshID;
}
