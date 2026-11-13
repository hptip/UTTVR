#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class RailwayMeshCombiner : EditorWindow
{
    private GameObject target;
    private bool disableOriginals = true;

    [MenuItem("Tools/Railway/Combine Railway Meshes")]
    static void OpenWindow()
    {
        GetWindow<RailwayMeshCombiner>("Railway Combiner");
    }

    void OnGUI()
    {
        GUILayout.Label("Railway Mesh Combiner",
            EditorStyles.boldLabel);

        target = (GameObject)EditorGUILayout.ObjectField(
            "Railway Parent",
            target,
            typeof(GameObject),
            true
        );

        disableOriginals = EditorGUILayout.Toggle(
            "Disable Original Meshes",
            disableOriginals
        );

        EditorGUILayout.HelpBox(
            "Combine all child MeshFilters into one Mesh. " +
            "Original objects are preserved.",
            MessageType.Info
        );

        GUI.enabled = target != null;

        if (GUILayout.Button("COMBINE MESHES", GUILayout.Height(35)))
        {
            Combine();
        }

        GUI.enabled = true;
    }

    void Combine()
    {
        MeshFilter[] filters =
            target.GetComponentsInChildren<MeshFilter>(false);

        Transform outputParent = target.transform;

        GameObject output = new GameObject("Railway_Combined");

        output.transform.SetParent(outputParent, false);

        MeshFilter outputFilter =
            output.AddComponent<MeshFilter>();

        MeshRenderer outputRenderer =
            output.AddComponent<MeshRenderer>();

        // Group geometry by material
        Dictionary<Material, List<CombineInstance>> groups =
            new Dictionary<Material, List<CombineInstance>>();

        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null ||
                filter.transform == output.transform)
                continue;

            Mesh mesh = filter.sharedMesh;

            MeshRenderer renderer =
                filter.GetComponent<MeshRenderer>();

            if (renderer == null) continue;

            Material[] materials = renderer.sharedMaterials;

            Matrix4x4 matrix =
                output.transform.worldToLocalMatrix *
                filter.transform.localToWorldMatrix;

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                Material mat = i < materials.Length
                    ? materials[i] : null;

                if (!groups.ContainsKey(mat))
                    groups[mat] = new List<CombineInstance>();

                groups[mat].Add(new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = i,
                    transform = matrix
                });
            }
        }

        List<Mesh> temporaryMeshes = new List<Mesh>();
        List<Material> finalMaterials = new List<Material>();

        foreach (var group in groups)
        {
            Mesh part = new Mesh();
            part.indexFormat =
                UnityEngine.Rendering.IndexFormat.UInt32;

            part.CombineMeshes(
                group.Value.ToArray(),
                true,
                true
            );

            temporaryMeshes.Add(part);
            finalMaterials.Add(group.Key);
        }

        List<CombineInstance> finalCombine =
            new List<CombineInstance>();

        foreach (Mesh part in temporaryMeshes)
        {
            finalCombine.Add(new CombineInstance
            {
                mesh = part,
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            });
        }

        Mesh finalMesh = new Mesh();
        finalMesh.name = "Railway_Combined_Mesh";
        finalMesh.indexFormat =
            UnityEngine.Rendering.IndexFormat.UInt32;

        finalMesh.CombineMeshes(
            finalCombine.ToArray(),
            false,
            true
        );

        outputFilter.sharedMesh = finalMesh;
        outputRenderer.sharedMaterials = finalMaterials.ToArray();

        // Save combined mesh as an asset
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Combined Railway Mesh",
            "Railway_Combined_Mesh",
            "asset",
            "Choose a location to save the combined mesh."
        );

        if (string.IsNullOrEmpty(path))
        {
            DestroyImmediate(output);
            DestroyImmediate(finalMesh);

            foreach (Mesh m in temporaryMeshes)
                DestroyImmediate(m);

            return;
        }

        AssetDatabase.CreateAsset(finalMesh, path);
        AssetDatabase.SaveAssets();

        // Clean temporary meshes
        foreach (Mesh m in temporaryMeshes)
            DestroyImmediate(m);

        // Disable original renderers after successful combine
        if (disableOriginals)
        {
            foreach (MeshFilter filter in filters)
            {
                if (filter.transform == output.transform)
                    continue;

                MeshRenderer renderer =
                    filter.GetComponent<MeshRenderer>();

                if (renderer != null)
                    renderer.enabled = false;
            }
        }

        Selection.activeGameObject = output;

        Debug.Log(
            "Railway meshes combined successfully! " +
            "Original meshes are preserved."
        );
    }
}
#endif
