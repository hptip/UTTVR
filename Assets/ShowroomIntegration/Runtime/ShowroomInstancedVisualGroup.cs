using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using UTT.SaBan;

/// <summary>
/// Runtime-only submission optimization. Original objects, meshes, transforms, colliders and
/// Editor visuals remain intact. Disabling this component restores original renderers.
/// Owns exactly one existing independently switchable display layer.
/// </summary>
public sealed class ShowroomInstancedVisualGroup : MonoBehaviour
{
    public ShowroomInstancedVisualData data;
    public MeshRenderer[] sources;
    [System.NonSerialized] public bool suppressRendering;
    bool[] saved;
    Matrix4x4[][] world;
    Matrix4x4 previous;
    Bounds[] batchBounds;
    bool submitted;
    bool visible=true;
    SaBanLayer layer;
    Renderer[] remainder;
    bool[] remainderStates;
    System.Func<bool> visibilityReader;
    System.Action<bool> visibilityWriter;
    void OnEnable()
    {
        // Layer visibility must not rebuild arrays or re-enable 49k renderers on every click.
        if(submitted)return;
        if(!Application.isPlaying||data==null||!SystemInfo.supportsInstancing)return;
        if(saved==null||saved.Length!=sources.Length)saved=new bool[sources.Length];
        for(int i=0;i<sources.Length;i++)if(sources[i]!=null){saved[i]=sources[i].enabled;sources[i].enabled=false;}
        if(world==null)
        {
            world=new Matrix4x4[data.batches.Length][];
            batchBounds=new Bounds[data.batches.Length];
            for(int i=0;i<world.Length;i++)world[i]=new Matrix4x4[data.batches[i].localMatrices.Length];
        }
        RefreshMatrices();submitted=true;
        InstallVisualOnlyVisibility();
    }
    void InstallVisualOnlyVisibility()
    {
        layer=GetComponent<SaBanLayer>();if(layer==null)return;
        // Never bypass physics, interaction, animation or active script lifetime semantics.
        if(GetComponentsInChildren<Collider>(true).Length!=0||GetComponentsInChildren<Rigidbody>(true).Length!=0||GetComponentsInChildren<Light>(true).Length!=0||GetComponentsInChildren<Animator>(true).Length!=0)return;
        foreach(var b in GetComponentsInChildren<MonoBehaviour>(true))if(b!=null&&b!=this&&b!=layer&&b.isActiveAndEnabled)return;
        var owned=new HashSet<Renderer>(sources);var others=new List<Renderer>();
        foreach(var r in GetComponentsInChildren<Renderer>(true))if(!owned.Contains(r))others.Add(r);
        remainder=others.ToArray();remainderStates=new bool[remainder.Length];
        for(int i=0;i<remainder.Length;i++)remainderStates[i]=remainder[i].enabled;
        visibilityReader=()=>gameObject.activeSelf&&visible;visibilityWriter=SetVisualVisible;
        layer.runtimeVisibilityReader=visibilityReader;layer.runtimeVisibilityWriter=visibilityWriter;
    }
    void SetVisualVisible(bool value)
    {
        if(value&&!gameObject.activeSelf)gameObject.SetActive(true);
        visible=value;
        for(int i=0;i<remainder.Length;i++)if(remainder[i]!=null)remainder[i].enabled=value&&remainderStates[i];
    }
    void RefreshMatrices()
    {
        previous=transform.localToWorldMatrix;
        int sourceIndex=0;
        for(int b=0;b<world.Length;b++)
        {
            bool first=true;
            for(int i=0;i<world[b].Length;i++)
            {
            // Read current source transforms once, so manual Editor placement changes are respected.
            var source=sources[sourceIndex];
            world[b][i]=source!=null&&saved[sourceIndex]&&source.gameObject.activeInHierarchy?source.transform.localToWorldMatrix:Matrix4x4.zero;
            if(source!=null&&saved[sourceIndex]&&source.gameObject.activeInHierarchy)
            {
                if(first){batchBounds[b]=source.bounds;first=false;}else batchBounds[b].Encapsulate(source.bounds);
            }
            sourceIndex++;
            }
        }
    }
    void LateUpdate()
    {
        if(!submitted||suppressRendering||!visible)return;
        if(previous!=transform.localToWorldMatrix)RefreshMatrices();
        for(int i=0;i<world.Length;i++)
        {
            var b=data.batches[i];
            var mesh=ShowroomGeometryController.Resolve(b.mesh,this);
            // Each variant is a compact physical mesh with exactly one LOD. Never rely on
            // implicit Mesh LOD selection in a custom instancing path. No original is also drawn.
            var parameters=new RenderParams(b.material)
            {
                worldBounds=batchBounds[i], layer=b.layer, shadowCastingMode=b.shadows,
                receiveShadows=b.receiveShadows, lightProbeUsage=LightProbeUsage.BlendProbes,
                forceMeshLod=0
            };
            Graphics.RenderMeshInstanced(parameters,mesh,0,world[i],world[i].Length);
        }
    }
    void OnDisable()
    {
        if(Application.isPlaying&&!gameObject.activeInHierarchy)return;
        Restore();
    }
    void OnDestroy()=>Restore();
    void Restore()
    {
        if(!submitted)return;
        bool hidden=!visible;
        if(layer!=null&&layer.runtimeVisibilityReader==visibilityReader){layer.runtimeVisibilityReader=null;layer.runtimeVisibilityWriter=null;}
        if(remainder!=null)for(int i=0;i<remainder.Length;i++)if(remainder[i]!=null)remainder[i].enabled=remainderStates[i];
        for(int i=0;i<sources.Length;i++)if(sources[i]!=null)sources[i].enabled=saved[i];
        submitted=false;
        visible=true;
        if(hidden&&Application.isPlaying&&gameObject.activeInHierarchy)gameObject.SetActive(false);
    }
}
