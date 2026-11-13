
using UnityEngine;

public class ElementIdentity : MonoBehaviour
{
    [SerializeField] public string elementID;

    public string ElementID
    {
        get
        {
            return string.IsNullOrWhiteSpace(elementID)
                ? gameObject.name
                : elementID.Trim();
        }
    }
}
