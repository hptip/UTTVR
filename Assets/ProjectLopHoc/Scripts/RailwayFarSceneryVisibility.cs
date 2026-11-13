using UnityEngine;

/// <summary>Conservative whole-batch distance culling beyond the shader fade.</summary>
[RequireComponent(typeof(MeshRenderer))]
public sealed class RailwayFarSceneryVisibility : MonoBehaviour
{
    [SerializeField] private float fadeEnd=380f;
    private MeshRenderer batch;private Camera view;private float next;
    private void Awake()=>batch=GetComponent<MeshRenderer>();
    private void Update()
    {
        if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
        if(view==null||!view.isActiveAndEnabled)view=Camera.main;
        if(view==null||batch==null)return;
        float distance=batch.bounds.SqrDistance(view.transform.position);
        if(batch.enabled&&distance>(fadeEnd+30)*(fadeEnd+30))batch.enabled=false;
        else if(!batch.enabled&&distance<fadeEnd*fadeEnd)batch.enabled=true;
    }
}
