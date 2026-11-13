#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AutoEnableReadWrite : EditorWindow
{
    private GameObject targetObject;

    [MenuItem("Tools/Railway/Bat Nhanh Read Write Cho Model")]
    public static void ShowWindow()
    {
        GetWindow<AutoEnableReadWrite>("Mở Khóa Read/Write");
    }

    private void OnGUI()
    {
        GUILayout.Label("Tự động bật Read/Write cho toàn bộ Mesh con", EditorStyles.boldLabel);
        targetObject = (GameObject)EditorGUILayout.ObjectField("GameObject Tàu", targetObject, typeof(GameObject), true);

        if (GUILayout.Button("Quét và Bật Read/Write"))
        {
            if (targetObject == null)
            {
                EditorUtility.DisplayDialog("Lỗi", "Vui lòng kéo Train_THSR_700T vào ô mục tiêu.", "OK");
                return;
            }

            XuLyBatReadWrite();
        }
    }

    private void XuLyBatReadWrite()
    {
        MeshFilter[] meshFilters = targetObject.GetComponentsInChildren<MeshFilter>(true);
        HashSet<string> processedPaths = new HashSet<string>();
        int count = 0;

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            string assetPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (string.IsNullOrEmpty(assetPath) || processedPaths.Contains(assetPath)) continue;

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null)
            {
                if (!importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    count++;
                }
                processedPaths.Add(assetPath);
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Thành công", $"Đã tự động bật Read/Write và Apply cho {count} tệp mô hình nguồn.", "OK");
    }
}
#endif
