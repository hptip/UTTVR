using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class YenTuSaBanBuilder
{
    const string Source = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string Dest = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh_SaBan.unity";
    [MenuItem("Tools/Sa Ban/Build Yen Tu - Ha Long Xanh")]
    public static void Build()
    {
        if (System.IO.File.Exists(Dest) && !EditorUtility.DisplayDialog("Tạo lại scene sa bàn", "Scene này đang là bản dựng dở do mình vừa tạo. Tạo lại từ scene gốc để bổ sung đầy đủ hệ thống mô hình?", "Tạo lại", "Hủy")) return;
        Scene src=EditorSceneManager.OpenScene(Source,OpenSceneMode.Single);
        EditorSceneManager.SaveScene(src,Dest);
        Scene scene=EditorSceneManager.OpenScene(Dest,OpenSceneMode.Single);
        GameObject routeObj=FindByComponent<SplineContainer>(scene);
        GameObject trainObj=FindByComponent<TrainSplineMover>(scene);
        GameObject station=FindByName(scene,"Warehouse");
        if(!routeObj||!trainObj) { Debug.LogError("Không tìm thấy SplineContainer hoặc TrainSplineMover trong scene gốc."); return; }
        Debug.Log("YTHL roots: "+string.Join(", ",System.Array.ConvertAll(scene.GetRootGameObjects(),g=>g.name)));
        var objects=new List<GameObject>();
        if(station)objects.Add(station); else Debug.Log("Không có root Warehouse trong scene; lấy các manager streamer để sinh mô hình lúc chạy.");
        foreach(GameObject root in scene.GetRootGameObjects())
            if(root.name=="Scenery_Manager"||root.name=="Tunnel_Manager"||root.name=="Station_Manager")
                if(!objects.Contains(root)) objects.Add(root);
        if(routeObj&&!objects.Contains(routeObj))objects.Add(routeObj);
        if(trainObj&&!objects.Contains(trainObj))objects.Add(trainObj);
        // Use the existing Yên Tử - Hạ Long terrain as the landform base of this new miniature.
        // It is authored in the same project coordinate system as the route.
        GameObject terrainPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SaBanUTTNew/ProjectLopHoc/Prefabs/SaBan_YenTu_HaLong_Miniature.prefab");
        GameObject terrainObj=terrainPrefab?(GameObject)PrefabUtility.InstantiatePrefab(terrainPrefab,scene):null;
        Vector3 terrainSourcePosition=terrainObj?terrainObj.transform.position:Vector3.zero;
        if(terrainObj)
        {
            terrainObj.name="SaBan_YenTu_HaLong_Terrain";
            Terrain terrain=terrainObj.GetComponent<Terrain>();
            TerrainCollider terrainCollider=terrainObj.GetComponent<TerrainCollider>();
            if(terrain&&terrain.terrainData)
            {
                string terrainDataPath="Assets/ProjectLopHoc/Prefabs/SaBan_YTHL_MiniatureTerrainData.asset";
                string sourceDataPath=AssetDatabase.GetAssetPath(terrain.terrainData);
                if(AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath))AssetDatabase.DeleteAsset(terrainDataPath);
                if(AssetDatabase.CopyAsset(sourceDataPath,terrainDataPath))
                {
                    TerrainData data=AssetDatabase.LoadAssetAtPath<TerrainData>(terrainDataPath);
                    terrain.terrainData=data;
                    if(terrainCollider)terrainCollider.terrainData=data;
                    Texture2D satellite=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/SaBanUTTNew/ProjectLopHoc/Art/Terrain_Satellite_Texture.png");
                    string layerPath="Assets/ProjectLopHoc/Prefabs/SaBan_YTHL_SatelliteTerrainLayer.terrainlayer";
                    if(AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath))AssetDatabase.DeleteAsset(layerPath);
                    if(data&&satellite)
                    {
                        TerrainLayer layer=new TerrainLayer();layer.diffuseTexture=satellite;layer.tileSize=new Vector2(data.size.x,data.size.z);layer.metallic=0;layer.smoothness=0;
                        AssetDatabase.CreateAsset(layer,layerPath);
                        data.terrainLayers=new[]{layer};
                        int resolution=data.alphamapResolution;float[,,] weights=new float[resolution,resolution,1];
                        for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)weights[y,x,0]=1f;
                        data.SetAlphamaps(0,0,weights);EditorUtility.SetDirty(data);
                    }
                    else Debug.LogError("Không nạp được texture vệ tinh hoặc TerrainData bản sao cho sa bàn.");
                }
                else Debug.LogError("Không thể tạo bản sao TerrainData riêng cho scene sa bàn.");
                terrain.drawTreesAndFoliage=true;terrain.heightmapPixelError=1f;terrain.basemapDistance=100000f;
            }
            if(terrainCollider)terrainCollider.enabled=false;
            objects.Add(terrainObj);
        }
        else Debug.LogError("Không tìm thấy prefab địa hình sa bàn Yên Tử - Hạ Long Xanh.");
        Bounds b=CalculateBounds(objects,routeObj.GetComponent<SplineContainer>());
        if(terrainObj)objects.Remove(terrainObj);
        float largest=Mathf.Max(b.size.x,b.size.z);
        float scale=Mathf.Clamp(7.2f/Mathf.Max(largest,1f),0.00001f,0.05f);
        GameObject model=new GameObject("SaBan_YTHL_Model"); model.AddComponent<SaBanPoolScaleFix>();
        model.transform.position=b.center;
        foreach(GameObject g in objects)g.transform.SetParent(model.transform,true);
        model.transform.localScale=Vector3.one*scale;
        if(terrainObj)
        {
            Terrain terrain=terrainObj.GetComponent<Terrain>();
            if(terrain&&terrain.terrainData)
            {
                terrainObj.transform.position=b.center+(terrainSourcePosition-b.center)*scale;
                terrainObj.transform.rotation=Quaternion.identity;
                terrainObj.transform.localScale=Vector3.one;
                terrain.terrainData.size=terrain.terrainData.size*scale;
                EditorUtility.SetDirty(terrain.terrainData);
            }
        }
        var mover=trainObj.GetComponent<TrainSplineMover>();
        mover.isDriverSeated=false; mover.targetSpeedKmh=0; mover.currentSpeedKmh=0; trainObj.AddComponent<SaBanTrainVisual>().mover=mover; mover.frontHeightOffset*=scale; mover.rearYOffset*=scale; mover.carSpacing*=scale; mover.startDistance*=scale;
        var streamers=scene.GetRootGameObjects();
        foreach(GameObject root in streamers)foreach(var s in root.GetComponentsInChildren<DynamicTrackStreamer>(true)){s.enabled=false;}
        foreach(GameObject root in streamers)foreach(var s in root.GetComponentsInChildren<TunnelStreamer>(true)){s.enabled=false;}
        foreach(GameObject root in scene.GetRootGameObjects()) { var sc=root.GetComponentInChildren<SceneryStreamer>(true); if(sc){sc.sideOffset*=scale;sc.verticalOffset*=scale;sc.spawnAheadDistance*=scale;sc.despawnBehindDistance*=scale;} var st=root.GetComponentInChildren<StationStreamer>(true); if(st){st.triggerDistance*=scale;st.startHeightOffset*=scale;st.startLateralOffset*=scale;st.startForwardOffset*=scale;st.endHeightOffset*=scale;st.endLateralOffset*=scale;st.endForwardOffset*=scale;} var tn=root.GetComponentInChildren<TunnelStreamer>(true); if(tn){tn.tunnelSegmentLength*=scale;tn.openAirDistance*=scale;tn.forwardViewDistance*=scale;tn.backwardViewDistance*=scale;tn.positionOffset*=scale;} }
        SceneryStreamer scenery=FindByComponent<SceneryStreamer>(scene)?.GetComponent<SceneryStreamer>();
        if(scenery)
        {
            scenery.enabled=false;
            if(scenery.mountainPrefabs!=null&&scenery.mountainPrefabs.Length>0)
            {
                int count=Mathf.Clamp(scenery.totalMountains,8,40);SplineContainer route=routeObj.GetComponent<SplineContainer>();
                for(int i=0;i<count;i++)
                {
                    GameObject prefab=scenery.mountainPrefabs[i%scenery.mountainPrefabs.Length];if(!prefab)continue;
                    float t=(i+.5f)/count;Vector3 p=route.EvaluatePosition(t);Vector3 tangent=route.EvaluateTangent(t);tangent.Normalize();Vector3 up=route.EvaluateUpVector(t);up.Normalize();Vector3 right=Vector3.Cross(up,tangent).normalized;
                    GameObject hill=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);hill.name="SaBan_DiaHinh_Nui_"+(i+1).ToString("00");hill.transform.SetParent(model.transform,true);hill.transform.position=p+right*((i%2==0?1f:-1f)*scenery.sideOffset)+up*scenery.verticalOffset;
                    bool green=hill.name.Contains("GreenHill");float rotY=green?scenery.greenHillRotation.y:(i*47)%360;hill.transform.rotation=Quaternion.Euler(scenery.baseRotation.x,rotY,scenery.baseRotation.z);hill.transform.localScale=scenery.scaleMultiplier*(.9f+.1f*Mathf.Sin(i*1.7f));
                }
            }
        }
        foreach(var otherCamera in Object.FindObjectsOfType<Camera>(true)) if(otherCamera!=Camera.main && otherCamera.name!="Main Camera")otherCamera.gameObject.SetActive(false);
        DisableNamed(scene,"Manager"); DisableNamed(scene,"Multiplayer_Spawner"); DisableNamed(scene,"Crosshair"); DisableNamed(scene,"UIInfo"); DisableNamed(scene,"UIInfoPopup"); DisableNamed(scene,"BIM_ScrollView");
        Physics.SyncTransforms();
        Bounds mb=new Bounds(b.center,b.size*scale);
        float boardTop=mb.min.y-0.04f;
        float tableX=Mathf.Max(8.4f,mb.size.x+1.2f), tableZ=Mathf.Max(6.4f,mb.size.z+5.0f);
        MakeCube("SaBan_Board",new Vector3(mb.center.x,boardTop-.12f,mb.center.z+.25f),new Vector3(tableX,.24f,tableZ),new Color(.22f,.11f,.065f));
        MakeMapSurface(mb,mb.max.y+0.004f);
        MakeCube("Leg_FL",new Vector3(mb.center.x-tableX*.44f,boardTop-.72f,mb.center.z-tableZ*.40f),new Vector3(.28f,1.2f,.28f),new Color(.16f,.08f,.045f));
        MakeCube("Leg_FR",new Vector3(mb.center.x+tableX*.44f,boardTop-.72f,mb.center.z-tableZ*.40f),new Vector3(.28f,1.2f,.28f),new Color(.16f,.08f,.045f));
        MakeCube("Leg_BL",new Vector3(mb.center.x-tableX*.44f,boardTop-.72f,mb.center.z+tableZ*.40f),new Vector3(.28f,1.2f,.28f),new Color(.16f,.08f,.045f));
        MakeCube("Leg_BR",new Vector3(mb.center.x+tableX*.44f,boardTop-.72f,mb.center.z+tableZ*.40f),new Vector3(.28f,1.2f,.28f),new Color(.16f,.08f,.045f));
        // Give the moving train colliders so it can be selected by clicking.
        if(!trainObj.GetComponentInChildren<Collider>()) { Bounds tb=CalculateBounds(new List<GameObject>{trainObj},null); BoxCollider c=trainObj.AddComponent<BoxCollider>(); c.center=trainObj.transform.InverseTransformPoint(tb.center); Vector3 localSize=trainObj.transform.InverseTransformVector(tb.size); c.size=new Vector3(Mathf.Abs(localSize.x),Mathf.Abs(localSize.y),Mathf.Abs(localSize.z)); }
        SaBanInfoTarget target=station?station.GetComponent<SaBanInfoTarget>():null;
        if(station){if(!target)target=station.AddComponent<SaBanInfoTarget>();target.title="NHÀ GA YÊN TỬ – HẠ LONG XANH";target.details="Khu vực nhà ga thuộc tuyến Yên Tử – Hạ Long Xanh.\nBấm tàu để xem trạng thái vận hành.";}
        model.AddComponent<SaBanSplineVisual>().route=routeObj.GetComponent<SplineContainer>();
        AddStationMarker(model.transform,routeObj.GetComponent<SplineContainer>(),scale,0f,"NHÀ GA YÊN TỬ","Ga đầu tuyến Yên Tử.\nBấm tàu để xem trạng thái vận hành.");
        AddStationMarker(model.transform,routeObj.GetComponent<SplineContainer>(),scale,1f,"NHÀ GA HẠ LONG XANH","Ga cuối tuyến Hạ Long Xanh.\nBấm tàu để xem trạng thái vận hành.");
        Camera cam=Camera.main;if(!cam)cam=Object.FindObjectOfType<Camera>();
        Vector3 focus=mb.center;float dist=Mathf.Max(tableX,tableZ)*.9f;
        cam.transform.position=focus+new Vector3(-dist*.68f,dist*.55f,-dist*.68f);cam.transform.rotation=Quaternion.LookRotation(focus-cam.transform.position);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.82f,.86f,.89f);cam.nearClipPlane=.01f;cam.farClipPlane=Mathf.Max(cam.farClipPlane,5000);
        MakeControlPanel(mb,boardTop,trainObj.GetComponent<TrainSplineMover>(),cam,model.transform,focus,dist);
        AddToBuildSettings(Dest);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Đã tạo scene sa bàn: "+Dest+" | scale="+scale+" | bounds="+b.size);
        EditorUtility.DisplayDialog("Đã tạo sa bàn", "Scene mới đã tạo tại:\n"+Dest+"\nScene YenTuHaLongXanh gốc không bị sửa.","OK");
    }
    static GameObject FindByName(Scene s,string n){foreach(var g in s.GetRootGameObjects())if(g.name==n)return g;return null;}
    static GameObject FindByComponent<T>(Scene s) where T:Component{foreach(var g in s.GetRootGameObjects()){var c=g.GetComponentInChildren<T>(true);if(c)return c.gameObject;}return null;}
    static void DisableNamed(Scene s,string n){foreach(var g in s.GetRootGameObjects())foreach(Transform t in g.GetComponentsInChildren<Transform>(true))if(t.name==n)t.gameObject.SetActive(false);}
    static Bounds CalculateBounds(List<GameObject> roots,SplineContainer spline){Bounds b=new Bounds();bool has=false;foreach(var root in roots){if(!root)continue;foreach(var r in root.GetComponentsInChildren<Renderer>(true)){if(!has){b=r.bounds;has=true;}else b.Encapsulate(r.bounds);}Terrain terrain=root.GetComponentInChildren<Terrain>(true);if(terrain&&terrain.terrainData){Vector3 size=Vector3.Scale(terrain.terrainData.size,terrain.transform.lossyScale);Bounds tb=new Bounds(terrain.transform.position+size*.5f,size);if(!has){b=tb;has=true;}else b.Encapsulate(tb);}}if(spline&&spline.Spline!=null){for(int i=0;i<=100;i++){Vector3 p=spline.transform.TransformPoint(spline.EvaluatePosition(i/100f));if(!has){b=new Bounds(p,Vector3.one*.1f);has=true;}else b.Encapsulate(p);}}return has?b:new Bounds(Vector3.zero,new Vector3(8,2,4));}
    static void AddStationMarker(Transform parent,SplineContainer route,float scale,float t,string title,string details){Vector3 p=route.EvaluatePosition(t);Vector3 tangent=route.EvaluateTangent(t); tangent.Normalize();GameObject g=new GameObject("SaBan_StationMarker_"+title);g.transform.SetParent(parent,true);g.transform.position=p+tangent*(.12f*scale);BoxCollider c=g.AddComponent<BoxCollider>();c.size=new Vector3(.5f/scale,.45f/scale,.8f/scale);SaBanInfoTarget info=g.AddComponent<SaBanInfoTarget>();info.title=title;info.details=details;}
    static void MakeMapSurface(Bounds model,float top){GameObject q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name="SaBan_MapSurface";q.transform.position=new Vector3(model.center.x,top+.002f,model.center.z);q.transform.rotation=Quaternion.Euler(-90f,0,0);q.transform.localScale=new Vector3(model.size.x,model.size.z,1);var collider=q.GetComponent<Collider>();if(collider)Object.DestroyImmediate(collider);Material map=AssetDatabase.LoadAssetAtPath<Material>("Assets/SaBanUTTNew/ProjectLopHoc/Prefabs/SaBan_Display_Demo_Surface.mat");if(map)q.GetComponent<Renderer>().sharedMaterial=map;}
    static void MakeCube(string n,Vector3 pos,Vector3 size,Color color){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.position=pos;g.transform.localScale=size;Shader sh=Shader.Find("Universal Render Pipeline/Lit");if(!sh)sh=Shader.Find("Standard");var m=new Material(sh){color=color};g.GetComponent<Renderer>().sharedMaterial=m;}
    static void MakeControlPanel(Bounds model,float top,TrainSplineMover train,Camera cam,Transform root,Vector3 focus,float dist)
    {
        GameObject go=new GameObject("SaBan_ControlPanel",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));Canvas canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;RectTransform cr=go.GetComponent<RectTransform>();cr.sizeDelta=new Vector2(440,250);cr.localScale=Vector3.one*.01f;cr.position=new Vector3(model.center.x,top+.025f,model.min.z-1.0f);cr.rotation=Quaternion.Euler(-90f,0,0);canvas.sortingOrder=10;
        Image bg=go.AddComponent<Image>();bg.color=new Color(.055f,.09f,.14f,.97f);
        YenTuSaBanControls controls=go.AddComponent<YenTuSaBanControls>();controls.train=train;controls.viewCamera=cam;controls.modelRoot=root;controls.focusPoint=focus;controls.orbitDistance=dist;controls.orbitHeight=dist*.72f;
        Text header=MakeText(go.transform,"Header","SA BÀN YÊN TỬ – HẠ LONG XANH",new Vector2(0,108),new Vector2(420,30),20,Color.white,TextAnchor.MiddleCenter);
        controls.infoTitle=MakeText(go.transform,"InfoTitle","Bấm vào nhà ga hoặc tàu",new Vector2(0,78),new Vector2(420,24),14,new Color(.55f,.9f,1),TextAnchor.MiddleCenter);
        controls.infoDetails=MakeText(go.transform,"InfoDetails","Chọn đối tượng trên sa bàn để xem thông tin.",new Vector2(0,54),new Vector2(420,24),12,Color.white,TextAnchor.MiddleCenter);
        string[] labels={"360°","ZOOM +","ZOOM −","TOÀN CẢNH","GÓC TRÊN","RESET CAM","CHẠY TÀU","RESET HÀNH TRÌNH","1x","2x","4x","CAMERA THEO TÀU"};
        string[] names={"BtnCamera360","BtnZoomIn","BtnZoomOut","BtnFrameAll","BtnTopView","BtnResetCamera","BtnPlayPause","BtnResetJourney","BtnSpeed1x","BtnSpeed2x","BtnSpeed4x","BtnFollowTrain"};
        int[] xs={-156,-52,52,156};
        for(int i=0;i<labels.Length;i++){int row=i/4,col=i%4;GameObject button=new GameObject(names[i],typeof(RectTransform),typeof(Image),typeof(Button));button.transform.SetParent(go.transform,false);RectTransform r=button.GetComponent<RectTransform>();r.sizeDelta=new Vector2(96,38);r.anchoredPosition=new Vector2(xs[col],18-row*45);Image im=button.GetComponent<Image>();im.color=(i==6||i==7)?new Color(.08f,.42f,.62f):new Color(.16f,.23f,.32f);button.GetComponent<Button>().targetGraphic=im;MakeText(button.transform,"Label",labels[i],Vector2.zero,new Vector2(92,34),11,Color.white,TextAnchor.MiddleCenter);}
        controls.trainStatus=MakeText(go.transform,"TrainStatus","ĐANG DỪNG",new Vector2(0,-91),new Vector2(420,22),13,new Color(.55f,1,.68f),TextAnchor.MiddleCenter);
        controls.playPauseLabel=go.transform.Find("BtnPlayPause/Label").GetComponent<Text>();controls.followLabel=go.transform.Find("BtnFollowTrain/Label").GetComponent<Text>();
        GameObject close=new GameObject("BtnInfoClose",typeof(RectTransform),typeof(Image),typeof(Button));close.transform.SetParent(go.transform,false);RectTransform rc=close.GetComponent<RectTransform>();rc.sizeDelta=new Vector2(26,22);rc.anchoredPosition=new Vector2(203,111);close.GetComponent<Image>().color=new Color(.65f,.18f,.16f);MakeText(close.transform,"Label","×",Vector2.zero,new Vector2(26,22),16,Color.white,TextAnchor.MiddleCenter);
        if(!Object.FindObjectOfType<EventSystem>()){new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));}
    }
    static Text MakeText(Transform parent,string name,string value,Vector2 pos,Vector2 size,int font,Color color,TextAnchor align){GameObject g=new GameObject(name,typeof(RectTransform),typeof(Text));g.transform.SetParent(parent,false);RectTransform r=g.GetComponent<RectTransform>();r.anchoredPosition=pos;r.sizeDelta=size;Text t=g.GetComponent<Text>();t.text=value;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=font;t.color=color;t.alignment=align;t.raycastTarget=false;return t;}
    static void AddToBuildSettings(string path){var list=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);foreach(var s in list)if(s.path==path)return;list.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=list.ToArray();}
}



















