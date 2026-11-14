using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static partial class ShowroomGeometryV3
{
    public static string PreviewMesh(int entryIndex)
    {
        var scene = Scene(); var profile = AssetDatabase.LoadAssetAtPath<ShowroomGeometryProfile>(ProfilePath); var e = profile.entries[entryIndex];
        MeshRenderer source = null;
        foreach (var r in Components<MeshRenderer>(scene)) if (r.GetComponent<MeshFilter>()?.sharedMesh == e.original) { source = r; break; }
        if (source == null) throw new Exception("Source renderer not found");
        string folder = Output + "/Images/" + entryIndex.ToString("D3") + "_" + e.original.name; Directory.CreateDirectory(folder);
        var visual = new GameObject("__TemporaryGeometryPreview", typeof(MeshFilter), typeof(MeshRenderer));
        var cameraObject = new GameObject("__TemporaryGeometryPreviewCamera", typeof(Camera));
        var lightObject = new GameObject("__TemporaryGeometryPreviewLight", typeof(Light));
        visual.hideFlags = cameraObject.hideFlags = lightObject.hideFlags = HideFlags.HideAndDontSave;
        visual.layer = 31; var filter = visual.GetComponent<MeshFilter>(); var renderer = visual.GetComponent<MeshRenderer>(); renderer.sharedMaterials = source.sharedMaterials;
        var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false; camera.cullingMask = 1 << 31; camera.fieldOfView = 40; camera.aspect = 960f / 540;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .18f, .22f);
        var bounds = e.original.bounds; float distance = Mathf.Max(.1f, bounds.extents.magnitude * 3.2f);
        camera.nearClipPlane = Mathf.Max(.0001f, distance / 10000); camera.farClipPlane = distance * 10;
        camera.transform.position = bounds.center + new Vector3(1, .5f, 1).normalized * distance; camera.transform.LookAt(bounds.center);
        var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.cullingMask = 1 << 31; light.transform.rotation = Quaternion.Euler(40, -30, 0);
        var rt = new RenderTexture(960, 540, 24); var previous = RenderTexture.active; Texture2D image = null;
        try
        {
            rt.Create(); image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            foreach (var mode in new[] { ShowroomGeometryMode.Original, ShowroomGeometryMode.Quality50, ShowroomGeometryMode.Quality25, ShowroomGeometryMode.Quality10 })
            {
                filter.sharedMesh = profile.Resolve(e.original, mode);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply(); File.WriteAllBytes(folder + "/" + mode + ".png", image.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = previous; rt.Release(); Object.DestroyImmediate(rt); if (image != null) Object.DestroyImmediate(image);
            Object.DestroyImmediate(visual); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(lightObject);
        }
        return folder;
    }
}
