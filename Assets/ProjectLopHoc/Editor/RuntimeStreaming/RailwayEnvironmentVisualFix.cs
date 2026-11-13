using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class RailwayEnvironmentVisualFix
{
    static RailwayEnvironmentVisualFix() => EditorApplication.update += Tick;
    static void Tick()
    {
        const string request = "Temp/RailwayEnvironmentVisualFix.request";
        if (!File.Exists(request) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string mode;
        try { mode=File.ReadAllText(request).Trim(); if(mode.Length==0) return; File.Delete(request); }
        catch(IOException) { return; }
        if(mode=="fix") { ApplyFix(); return; }
        if(mode=="finalmatte") { FinishMatte(); return; }
        if(mode=="nearmountains") { FinishMatte(true); return; }
        if(mode=="layers")
        {
            const string folder="Assets/ProjectLopHoc/Generated/EnvironmentVisualFix";
            foreach(var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                var data=Object.Instantiate(terrain.terrainData);
                var layers=data.terrainLayers;
                for(int i=0;i<layers.Length;i++)
                {
                    var layer=Object.Instantiate(layers[i]);
                    layer.smoothness=0;layer.metallic=0;layer.specular=Color.black;
                    layer.maskMapRemapMin=new Vector4(0,0,0,0);
                    layer.maskMapRemapMax=new Vector4(0,1,1,0);
                    AssetDatabase.CreateAsset(layer,folder+"/GroundLayer_"+i+".terrainlayer");layers[i]=layer;
                }
                data.terrainLayers=layers;
                AssetDatabase.CreateAsset(data,folder+"/MatteTerrainData.asset");terrain.terrainData=data;
                var collider=terrain.GetComponent<TerrainCollider>();if(collider!=null) collider.terrainData=data;
                terrain.drawHeightmap=true;
            }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();return;
        }
        if(mode=="view")
        {
            var route=Object.FindFirstObjectByType<UnityEngine.Splines.SplineContainer>();
            float t=0.015f;
            Vector3 p=route.EvaluatePosition(t);
            Vector3 f=route.EvaluateTangent(t);f.Normalize();
            SceneView.lastActiveSceneView.orthographic=false;
            SceneView.lastActiveSceneView.LookAtDirect(p+Vector3.up*3+f*35,Quaternion.LookRotation(f,Vector3.up),35);
            SceneView.lastActiveSceneView.Repaint();return;
        }
        var report = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        foreach(var root in scene.GetRootGameObjects())
        {
            report.AppendLine($"ROOT {root.name} active={root.activeSelf}");
            foreach(var terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                var m=terrain.materialTemplate;
                report.AppendLine($"TERRAIN {terrain.name} material={(m!=null?AssetDatabase.GetAssetPath(m):"NULL")} shader={(m!=null?m.shader.name:"NULL")} size={terrain.terrainData.size} position={terrain.transform.position}");
                foreach(var layer in terrain.terrainData.terrainLayers) report.AppendLine($"LAYER {AssetDatabase.GetAssetPath(layer)} smoothness={layer.smoothness} metallic={layer.metallic}");
            }
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                if(r.name.ToLowerInvariant().Contains("mountain") || r.name.ToLowerInvariant().Contains("backdrop") || r.name.ToLowerInvariant().Contains("ridge") || r.name.ToLowerInvariant().Contains("water") || r.bounds.size.x>1000)
                {
                    report.AppendLine($"RENDERER {r.name} active={r.gameObject.activeInHierarchy} bounds={r.bounds} parent={r.transform.parent?.name}");
                    foreach(var m in r.sharedMaterials) report.AppendLine($" MATERIAL {(m!=null?AssetDatabase.GetAssetPath(m):"NULL")} shader={(m!=null?m.shader.name:"NULL")}");
                }
        }
        File.WriteAllText("Temp/RailwayEnvironmentVisualAudit.txt",report.ToString());
        var zone=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_000.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        foreach(var root in zone.GetRootGameObjects())
        foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            if(r.transform.parent!=null && (r.transform.parent.name=="Track_Right" || r.transform.parent.name=="TerrainTransitions"))
                foreach(var m in r.sharedMaterials) if(m!=null) report.AppendLine($"ZONE {r.transform.parent.name}: {m.name} {AssetDatabase.GetAssetPath(m)} shader={m.shader.name} surface={(m.HasProperty("_Surface")?m.GetFloat("_Surface"):-1)} smooth={(m.HasProperty("_Smoothness")?m.GetFloat("_Smoothness"):-1)} metallic={(m.HasProperty("_Metallic")?m.GetFloat("_Metallic"):-1)}");
        UnityEditor.SceneManagement.EditorSceneManager.CloseScene(zone,true);
        File.WriteAllText("Temp/RailwayEnvironmentVisualAudit.txt",report.ToString());
    }

    static void FinishMatte(bool mountainsOnly=false)
    {
        const string folder="Assets/ProjectLopHoc/Generated/EnvironmentVisualFix";
        var scene=SceneManager.GetActiveScene();
        if(scene.name!="YenTuHaLongXanh") throw new System.InvalidOperationException("Wrong scene");
        var shader=Shader.Find("UTT/Terrain Matte No Reflections");
        if(shader==null || UnityEditor.ShaderUtil.ShaderHasError(shader)) throw new System.InvalidOperationException("Matte terrain shader missing or failed compilation.");
        int terrains=0,water=0,moved=0;
        foreach(var terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            if(terrain.name=="YenTu_Region_Terrain_EditableCopy")
            {terrain.materialTemplate.shader=shader;EditorUtility.SetDirty(terrain.materialTemplate);var tc=terrain.GetComponent<TerrainCollider>();if(tc!=null)tc.terrainData=terrain.terrainData;terrains++;}
        string waterPath=folder+"/Water_Opaque.mat";
        var wm=AssetDatabase.LoadAssetAtPath<Material>(waterPath);
        if(wm==null){wm=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(wm,waterPath);}
        wm.SetColor("_BaseColor",new Color(0.12f,0.28f,0.25f,1));wm.SetFloat("_Surface",0);wm.SetFloat("_ZWrite",1);wm.SetFloat("_Cull",0);wm.renderQueue=2000;EditorUtility.SetDirty(wm);
        for(int z=-1;z<58 && !mountainsOnly;z++)
        {
            var s=z<0?scene:UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_{z:D3}.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            foreach(var root in s.GetRootGameObjects()) foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats=r.sharedMaterials;bool changed=false;
                for(int k=0;k<mats.Length;k++)
                {
                    string n=(r.name+" "+(mats[k]!=null?mats[k].name:"")).ToLowerInvariant();
                    if(n.Contains("water")||n.Contains("ocean")||n.Contains("river_crossing")) {mats[k]=wm;changed=true;}
                }
                if(changed){r.sharedMaterials=mats;r.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;water++;}
            }
            if(z>=0){UnityEditor.SceneManagement.EditorSceneManager.SaveScene(s);UnityEditor.SceneManagement.EditorSceneManager.CloseScene(s,true);}
        }
        var backdrop=GameObject.Find("RailwayMountainBackdrop");var route=Object.FindFirstObjectByType<UnityEngine.Splines.SplineContainer>();
        if(backdrop!=null && route!=null)
        foreach(Transform mountain in backdrop.transform)
        {
            // Guard against applying the 100m move twice.
            if(mountain.name.EndsWith("_Near550m")) continue;
            Bounds bounds=new Bounds();bool first=true;
            foreach(var r in mountain.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            Vector3 closest=Vector3.zero;float best=float.PositiveInfinity;
            for(int i=0;i<=1500;i++){Vector3 p=route.EvaluatePosition(i/1500f);p.y=bounds.center.y;float d=(p-bounds.center).sqrMagnitude;if(d<best){best=d;closest=p;}}
            Vector3 delta=(closest-bounds.center).normalized*100;
            Vector3 targetCenter=bounds.center+delta;
            bool blocked=false;
            for(int i=0;i<=1500;i++){Vector3 p=route.EvaluatePosition(i/1500f);Vector3 local=Quaternion.Inverse(mountain.rotation)*(p-targetCenter);if(Mathf.Abs(local.x)<415 && Mathf.Abs(local.z)<765){blocked=true;break;}}
            if(blocked) continue;
            mountain.position+=delta;mountain.name+="_Near550m";moved++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.AppendAllText("Temp/RailwayEnvironmentFinalMatte.txt",$"\nTerrain custom reflection-free shader assigned={terrains}; opaque unlit water renderers={water}; mountain instances moved 100m closer={moved}; mountainsOnly={mountainsOnly}; changes saved. Source shader/package files unchanged.");
    }

    static Material Matte(Material source, string folder, bool terrain=false)
    {
        if(source==null) return null;
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
        string path=folder+"/"+(terrain?"Terrain":guid+"_"+localId)+"_Matte.mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) { m=new Material(source); AssetDatabase.CreateAsset(m,path); }
        foreach(var property in new[]{"_Metallic","_Smoothness","_Glossiness","_EnvironmentReflections","_SpecularHighlights","_Surface","_AlphaClip"})
            if(m.HasProperty(property)) m.SetFloat(property,0);
        if(m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite",1);
        if(m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend",1);
        if(m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend",0);
        if(!terrain && m.HasProperty("_Cull")) m.SetFloat("_Cull",0);
        foreach(var property in new[]{"_BaseColor","_Color"}) if(m.HasProperty(property)){var c=m.GetColor(property);c.a=1;m.SetColor(property,c);}
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        m.SetOverrideTag("RenderType","Opaque");m.renderQueue=2000;
        EditorUtility.SetDirty(m);return m;
    }

    static void ApplyFix()
    {
        const string folder="Assets/ProjectLopHoc/Generated/EnvironmentVisualFix";
        Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        var scene=SceneManager.GetActiveScene();
        if(scene.name!="YenTuHaLongXanh") throw new System.InvalidOperationException("Open YenTuHaLongXanh first.");
        var report=new StringBuilder();
        foreach(var root in scene.GetRootGameObjects()) foreach(var terrain in root.GetComponentsInChildren<Terrain>(true))
        {
            terrain.materialTemplate=Matte(terrain.materialTemplate,folder,true);
            terrain.drawInstanced=true;
            report.AppendLine("Assigned opaque matte Terrain material: "+terrain.name);
        }
        for(int z=0;z<58;z++)
        {
            var zone=UnityEditor.SceneManagement.EditorSceneManager.OpenScene($"Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_{z:D3}.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            foreach(var root in zone.GetRootGameObjects()) foreach(var r in root.GetComponentsInChildren<Renderer>(true))
            {
                bool ground=false;
                for(var p=r.transform.parent;p!=null;p=p.parent)
                    if(p.name=="TerrainTransitions" || p.name=="Track_Right" || p.name=="Track_Left") ground=true;
                if(!ground) continue;
                var materials=r.sharedMaterials;
                for(int i=0;i<materials.Length;i++) materials[i]=Matte(materials[i],folder);
                r.sharedMaterials=materials;
                r.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(zone);
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(zone,true);
        }
        var route=Object.FindFirstObjectByType<UnityEngine.Splines.SplineContainer>();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/MountainRidge.prefab");
        var backdrop=GameObject.Find("RailwayMountainBackdrop");
        if(backdrop==null && route!=null && prefab!=null)
        {
            backdrop=new GameObject("RailwayMountainBackdrop");
            Vector3 last=new Vector3(float.PositiveInfinity,0,0);
            int count=0;
            for(int i=0;i<=1500;i++)
            {
                float t=i/1500f;Vector3 point=route.EvaluatePosition(t);
                if(Vector3.Distance(last,point)<1050 && i<1500) continue;
                last=point;Vector3 tangent=route.EvaluateTangent(t);tangent.y=0;
                if(tangent.sqrMagnitude<0.01f) continue;
                var rotation=Quaternion.LookRotation(tangent.normalized,Vector3.up);
                Vector3 right=rotation*Vector3.right;
                for(int side=-1;side<=1;side+=2)
                {
                    Vector3 candidate=point+right*(side*650);
                    bool intersectsRoute=false;
                    for(int j=0;j<=1500;j+=2)
                    {
                        Vector3 delta=(Vector3)route.EvaluatePosition(j/1500f)-candidate;
                        if(Mathf.Abs(Vector3.Dot(delta,right))<475 && Mathf.Abs(Vector3.Dot(delta,tangent.normalized))<825)
                        {intersectsRoute=true;break;}
                    }
                    if(intersectsRoute) continue;
                    var mountain=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    mountain.name=$"CorridorRidge_{count:D3}_{side}";
                    mountain.transform.SetParent(backdrop.transform);
                    mountain.transform.rotation=Quaternion.identity;
                    Bounds bounds=new Bounds();bool first=true;
                    foreach(var r in mountain.GetComponentsInChildren<Renderer>()) {if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
                    if(first){Object.DestroyImmediate(mountain);continue;}
                    mountain.transform.localScale=Vector3.Scale(mountain.transform.localScale,new Vector3(650/Mathf.Max(bounds.size.x,1),180/Mathf.Max(bounds.size.y,1),1350/Mathf.Max(bounds.size.z,1)));
                    mountain.transform.rotation=rotation;
                    Vector3 target=point+right*(side*650);
                    mountain.transform.position=target;
                    first=true;
                    foreach(var r in mountain.GetComponentsInChildren<Renderer>()) {if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds); var mats=r.sharedMaterials;for(int k=0;k<mats.Length;k++)mats[k]=Matte(mats[k],folder);r.sharedMaterials=mats;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.Off;}
                    mountain.transform.position+=target-bounds.center+Vector3.up*(bounds.size.y/2-25);
                    foreach(var collider in mountain.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
                    mountain.isStatic=true;
                }
                count++;
            }
            report.AppendLine($"Fixed model ridge pairs={count}; centers about 650m from route, width 650m, length 1350m, height180m, foothills about325m from route. No runtime random generation.");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/RailwayEnvironmentVisualFix.txt",report.ToString());
    }
}
