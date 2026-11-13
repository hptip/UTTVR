using UnityEngine;

public class SaBanTrainVisual : MonoBehaviour
{
    public TrainSplineMover mover;
    public Color bodyColor = new Color(.95f,.55f,.08f);
    Transform frontVisual, rearVisual;
    float rootScale;
    void Start()
    {
        if(!mover)mover=GetComponent<TrainSplineMover>();
        rootScale=Mathf.Max(transform.lossyScale.x,.000001f);
        frontVisual=CreateCar("SaBan_Train_Car_Front",.34f,.11f,.14f);
        rearVisual=CreateCar("SaBan_Train_Car_Rear",.34f,.11f,.14f);
        frontVisual.localPosition=new Vector3(0,.08f/rootScale,0);
        rearVisual.localPosition=new Vector3(0,.08f/rootScale,-.42f/rootScale);
    }
    Transform CreateCar(string n,float length,float height,float width)
    {
        GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=n; g.transform.SetParent(transform,false);
        g.transform.localPosition=new Vector3(0,.08f/rootScale,0); g.transform.localRotation=Quaternion.identity;
        g.transform.localScale=new Vector3(width/rootScale,height/rootScale,length/rootScale);
        var renderer=g.GetComponent<Renderer>(); Shader sh=Shader.Find("Universal Render Pipeline/Lit"); if(!sh)sh=Shader.Find("Standard"); renderer.material=new Material(sh){color=bodyColor};
        var info=g.AddComponent<SaBanInfoTarget>(); info.title="ĐOÀN TÀU"; info.details="Tàu Yên Tử – Hạ Long Xanh.";
        return g.transform;
    }
    void LateUpdate()
    {
        if(!mover||mover.frontCar==null||frontVisual==null)return;
        transform.position=mover.frontCar.position; transform.rotation=mover.frontCar.rotation;
    }
}

