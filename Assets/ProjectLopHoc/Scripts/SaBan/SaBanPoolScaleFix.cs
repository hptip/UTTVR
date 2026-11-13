using UnityEngine;

[DefaultExecutionOrder(10000)]
public class SaBanPoolScaleFix : MonoBehaviour
{
    void LateUpdate()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform) continue;
            string n = t.name;
            if (n == "Dynamic_Track_System_Pool" || n == "Scenery_Dynamic_Pool" || n == "Tunnel_Pool_Instances")
            {
                if (t.localScale != Vector3.one) t.localScale = Vector3.one;
            }
        }
    }
}
