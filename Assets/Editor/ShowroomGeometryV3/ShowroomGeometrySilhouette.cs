using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static partial class ShowroomGeometryV3
{
    // Aggregate bounds alone cannot detect a small bracket collapsing into a pyramid.
    // Three-view rendered silhouette coverage provides an additional conservative gate.
    public static string ValidateSilhouettes()
    {
        var scene=Scene();var uses=Uses(scene);var profile=LoadProfile();
        profile.entries=profile.entries.Where(e=>e.original!=null&&EditorUtility.IsPersistent(e.original)).ToArray();
        var weights=uses.Where(u=>u.active).GroupBy(u=>u.mesh).ToDictionary(g=>g.Key,g=>g.Sum(u=>u.Triangles));
        var visual=new GameObject("__SilhouetteGate",typeof(MeshFilter),typeof(MeshRenderer));var cameraObject=new GameObject("__SilhouetteGateCamera",typeof(Camera));
        visual.hideFlags=cameraObject.hideFlags=HideFlags.HideAndDontSave;visual.layer=31;
        var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.SetColor("_BaseColor",Color.white);
        var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<31;camera.fieldOfView=40;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowHDR=false;
        var rt=new RenderTexture(192,192,24);rt.Create();var image=new Texture2D(192,192,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        var views=new[]{new Vector3(1,.5f,1),new Vector3(-1,.4f,-1),new Vector3(.2f,1,.3f)};
        var log=new List<string>{"Mesh,Mode,OldTriangles,AcceptedTriangles,MinimumSilhouetteIoU,WeightedInstances"};int changed=0;
        try
        {
            foreach(var entry in profile.entries.Where(e=>weights.TryGetValue(e.original,out var weight)&&weight>=40000))
            {
                var source=uses.First(u=>u.mesh==entry.original).renderer;visual.GetComponent<MeshRenderer>().sharedMaterials=Enumerable.Repeat(material,source.sharedMaterials.Length).ToArray();
                var bounds=entry.original.bounds;float distance=Mathf.Max(.01f,bounds.extents.magnitude*3.2f);camera.nearClipPlane=Mathf.Max(.00001f,distance/10000);camera.farClipPlane=distance*10;
                bool[] Mask(Mesh mesh,Vector3 view)
                {
                    visual.GetComponent<MeshFilter>().sharedMesh=mesh;camera.transform.position=bounds.center+view.normalized*distance;camera.transform.LookAt(bounds.center);
                    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,192,192),0,0);image.Apply();return image.GetPixels32().Select(c=>c.r>127).ToArray();
                }
                var originals=views.Select(v=>Mask(entry.original,v)).ToArray();var scores=new Dictionary<Mesh,float>{{entry.original,1}};
                float Score(Mesh mesh)
                {
                    if(scores.TryGetValue(mesh,out var known))return known;float score=1;
                    for(int view=0;view<views.Length;view++){var candidate=Mask(mesh,views[view]);int union=0,intersection=0;for(int i=0;i<candidate.Length;i++){if(candidate[i]||originals[view][i])union++;if(candidate[i]&&originals[view][i])intersection++;}score=Mathf.Min(score,union==0?1:intersection/(float)union);}
                    scores[mesh]=score;return score;
                }
                var meshes=new[]{entry.quality50,entry.quality25,entry.quality10,entry.original}.Where(m=>m!=null).Distinct().OrderBy(TrianglesForSort).ToArray();
                for(int quality=0;quality<3;quality++)
                {
                    Mesh before=quality==0?entry.quality50:quality==1?entry.quality25:entry.quality10;
                    var accepted=meshes.First(m=>Triangles(m)>=Triangles(before)&&Score(m)>=.95f);
                    if(accepted!=before)changed++;
                    if(quality==0){entry.quality50=accepted;entry.triangles50=Triangles(accepted);entry.boundsError50=BoundsError(entry.original,accepted);}
                    if(quality==1){entry.quality25=accepted;entry.triangles25=Triangles(accepted);entry.boundsError25=BoundsError(entry.original,accepted);}
                    if(quality==2){entry.quality10=accepted;entry.triangles10=Triangles(accepted);entry.boundsError10=BoundsError(entry.original,accepted);}
                    log.Add(string.Join(",",new object[]{entry.original.name,quality,Triangles(before),Triangles(accepted),Score(accepted),weights[entry.original]/Math.Max(1,entry.originalTriangles)}.Select(Csv)));
                }
                entry.generator+="; high-contribution mesh passed three-view silhouette IoU >= 95% (fallback to a higher-detail level where necessary)";
            }
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);WriteOptimizationTable(profile,uses);File.WriteAllLines(Output+"/SilhouetteValidation.csv",log);
            return "Higher-detail safety fallbacks="+changed+"; validated "+(log.Count-1)+" mesh/quality combinations";
        }
        finally{RenderTexture.active=previous;Object.DestroyImmediate(image);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(visual);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(material);}
    }
    static long TrianglesForSort(Mesh mesh)=>Triangles(mesh);
}
