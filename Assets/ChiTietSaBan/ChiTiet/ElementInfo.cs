using TMPro;
using UnityEngine;

/// <summary>One dynamically-created property row in ElementInfoPanel.</summary>
[DisallowMultipleComponent]
public sealed class ElementInfoRow : MonoBehaviour
{
    [SerializeField] private TMP_Text fieldNameText;
    [SerializeField] private TMP_Text fieldValueText;

    private void Awake()
    {
        ResolveTextReferences();
    }

    private void ResolveTextReferences()
    {
        if (fieldNameText == null)
        {
            Transform t = FindDeep(transform, "FieldNameText");
            if (t != null) fieldNameText = t.GetComponent<TMP_Text>();
        }

        if (fieldValueText == null)
        {
            Transform t = FindDeep(transform, "FieldValueText");
            if (t != null) fieldValueText = t.GetComponent<TMP_Text>();
        }

        // Fallback: use the first two TMP labels in the prefab.
        if (fieldNameText == null || fieldValueText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length >= 2)
            {
                if (fieldNameText == null) fieldNameText = texts[0];
                if (fieldValueText == null) fieldValueText = texts[1];
            }
        }
    }

    public void Set(string fieldName, string fieldValue)
    {
        ResolveTextReferences();

        if (fieldNameText == null || fieldValueText == null)
        {
            Debug.LogError(
                "ElementInfoRow: Không tìm thấy hai TMP_Text. Hãy gán FieldNameText và FieldValueText trong prefab.",
                this);
            return;
        }

        fieldNameText.text = fieldName ?? string.Empty;
        fieldValueText.text = fieldValue ?? string.Empty;
    }

    private static Transform FindDeep(Transform parent, string objectName)
    {
        if (parent == null) return null;
        if (parent.name == objectName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), objectName);
            if (found != null) return found;
        }

        return null;
    }
}
