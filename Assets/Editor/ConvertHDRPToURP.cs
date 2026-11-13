using UnityEngine;
using UnityEditor;

public class ConvertHDRPToURP : EditorWindow
{
    [MenuItem("Tools/Khôi phục Texture và chuyển sang URP Lit")]
    static void ConvertMaterials()
    {
        Object[] selectedObjects = Selection.objects;
        int count = 0;

        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null)
        {
            Debug.LogError("Không tìm thấy Shader Universal Render Pipeline/Lit!");
            return;
        }

        foreach (Object obj in selectedObjects)
        {
            if (obj is Material mat)
            {
                Undo.RecordObject(mat, "Restore and Convert Material to URP");

                // Trích xuất trực tiếp ảnh từ cấu trúc dữ liệu ngầm của file .mat
                SerializedObject so = new SerializedObject(mat);
                SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");

                Texture baseColorTex = null;
                Texture normalMapTex = null;

                if (texEnvs != null && texEnvs.isArray)
                {
                    for (int i = 0; i < texEnvs.arraySize; i++)
                    {
                        SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
                        SerializedProperty first = entry.FindPropertyRelative("first");
                        SerializedProperty second = entry.FindPropertyRelative("second");

                        string propName = first != null ? first.stringValue : "";
                        Texture tex = second != null ? second.FindPropertyRelative("m_Texture").objectReferenceValue as Texture : null;

                        if (tex != null)
                        {
                            if (propName == "_BaseColorMap" || propName == "_MainTex" || propName == "_BaseMap")
                            {
                                baseColorTex = tex;
                            }
                            else if (propName == "_NormalMap" || propName == "_BumpMap")
                            {
                                normalMapTex = tex;
                            }
                        }
                    }
                }

                // Chuyển shader sang URP Lit
                mat.shader = urpShader;

                // Gán lại đúng ô ảnh bề mặt
                if (baseColorTex != null)
                {
                    mat.SetTexture("_BaseMap", baseColorTex);
                }
                if (normalMapTex != null)
                {
                    mat.SetTexture("_BumpMap", normalMapTex);
                    mat.EnableKeyword("_NORMALMAP");
                }

                // Tự động bật viền trong suốt cho lá cây
                if (mat.name.ToLower().Contains("leaves") || mat.name.ToLower().Contains("leaf"))
                {
                    mat.SetFloat("_AlphaClip", 1);
                    mat.EnableKeyword("_ALPHATEST_ON");
                }

                mat.SetFloat("_Smoothness", 0.3f);
                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Đã khôi phục texture thành công cho {count} vật liệu!");
    }
}