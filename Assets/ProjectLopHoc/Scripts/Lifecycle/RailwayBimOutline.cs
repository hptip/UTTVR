using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>URP X-Ray selection adapter: stencil silhouette + expanded shell, without replacing original materials.</summary>
public sealed class RailwayBimOutline:MonoBehaviour
{
    public Material maskMaterial,outlineMaterial;
    readonly List<GameObject> slots=new List<GameObject>();
    public int HighlightedRenderers{get;private set;}
    public void Show(Renderer[] sources)
    {
        Clear();if(sources==null)return;int slot=0;
        foreach(var source in sources)
        {
            var mesh=source.GetComponent<MeshFilter>()?.sharedMesh;if(mesh==null)continue;
            for(int pass=0;pass<2;pass++)
            {
                if(slot==slots.Count){var g=new GameObject("PooledOutline_"+slot);g.transform.SetParent(transform,false);g.AddComponent<MeshFilter>();var r=g.AddComponent<MeshRenderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;slots.Add(g);}
                var go=slots[slot++];go.GetComponent<MeshFilter>().sharedMesh=mesh;var mr=go.GetComponent<MeshRenderer>();var materials=new Material[mesh.subMeshCount];for(int i=0;i<materials.Length;i++)materials[i]=pass==0?maskMaterial:outlineMaterial;mr.sharedMaterials=materials;
                go.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);go.transform.localScale=source.transform.lossyScale;go.SetActive(true);
            }
            HighlightedRenderers++;
        }
    }
    public void Clear(){foreach(var g in slots)g.SetActive(false);HighlightedRenderers=0;}
}
