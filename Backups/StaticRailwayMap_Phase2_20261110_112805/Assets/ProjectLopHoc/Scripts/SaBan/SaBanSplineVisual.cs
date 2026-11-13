using UnityEngine;
using UnityEngine.Splines;

public class SaBanSplineVisual : MonoBehaviour
{
    public SplineContainer route;
    [Range(50,2000)] public int samples = 600;
    public float spacing = .055f;
    public float width = .025f;
    void Start()
    {
        if (!route) return;
        var pointsA=new Vector3[samples+1]; var pointsB=new Vector3[samples+1];
        for(int i=0;i<=samples;i++)
        {
            float t=(float)i/samples;
            Vector3 p=route.EvaluatePosition(t);
            Vector3 tangent=route.EvaluateTangent(t); tangent.Normalize();
            Vector3 up=route.EvaluateUpVector(t); up.Normalize();
            Vector3 right=Vector3.Cross(up,tangent).normalized;
            pointsA[i]=p-right*(spacing*.5f)+up*.012f;
            pointsB[i]=p+right*(spacing*.5f)+up*.012f;
        }
        CreateRail("SaBan_Rail_Left",pointsA);
        CreateRail("SaBan_Rail_Right",pointsB);
    }
    void CreateRail(string n,Vector3[] positions)
    {
        GameObject g=new GameObject(n); g.transform.SetParent(transform,true);
        var lr=g.AddComponent<LineRenderer>(); lr.useWorldSpace=true; lr.positionCount=positions.Length; lr.SetPositions(positions);
        lr.startWidth=width; lr.endWidth=width; lr.numCapVertices=2; lr.numCornerVertices=2;
        Shader sh=Shader.Find("Universal Render Pipeline/Unlit"); if(!sh)sh=Shader.Find("Sprites/Default");
        lr.material=new Material(sh); lr.startColor=new Color(.34f,.38f,.42f); lr.endColor=new Color(.22f,.25f,.29f);
        lr.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows=false;
    }
}
