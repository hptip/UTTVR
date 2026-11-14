using UnityEngine;

// This component belongs only to Showroom. It never replaces colliders or object identities.
[DefaultExecutionOrder(-500), DisallowMultipleComponent]
public sealed class ShowroomGeometryController : MonoBehaviour
{
    public ShowroomGeometryProfile profile;
    public MeshFilter[] targets;
    public ShowroomGeometryMode mode = ShowroomGeometryMode.DeviceDefault;
    Mesh[] originals;
    static ShowroomGeometryController active;
    public ShowroomGeometryMode EffectiveMode => mode != ShowroomGeometryMode.DeviceDefault ? mode : Application.platform == RuntimePlatform.Android ? profile.androidDefault : ShowroomGeometryMode.Original;
    void Awake()
    {
        if (gameObject.scene.name != "Showroom" || profile == null) { enabled = false; return; }
        active = this; originals = new Mesh[targets.Length];
        for (int i = 0; i < targets.Length; i++) if (targets[i] != null) originals[i] = targets[i].sharedMesh;
        Apply();
    }
    public void SetMode(ShowroomGeometryMode value) { mode = value; Apply(); }
    void Apply()
    {
        if (originals == null || profile == null) return;
        for (int i = 0; i < targets.Length; i++) if (targets[i] != null) targets[i].sharedMesh = profile.Resolve(originals[i], EffectiveMode);
    }
    public static Mesh Resolve(Mesh original, Component owner) => active != null && active.isActiveAndEnabled && owner.gameObject.scene == active.gameObject.scene ? active.profile.Resolve(original, active.EffectiveMode) : original;
    void OnEnable() { if (originals != null) { active = this; Apply(); } }
    void OnDisable()
    {
        if (originals != null) for (int i = 0; i < targets.Length; i++) if (targets[i] != null) targets[i].sharedMesh = originals[i];
        if (active == this) active = null;
    }
}
