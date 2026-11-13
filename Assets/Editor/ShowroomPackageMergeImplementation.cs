using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Splines;
using UTT.SaBan;
using Object=UnityEngine.Object;

public static partial class ShowroomPackageMergeTools
{
    static string Key(Object value)
    {var id=GlobalObjectId.GetGlobalObjectIdSlow(value);return id.identifierType+"-"+id.targetObjectId+"-"+id.targetPrefabId;}
    sealed class Original
    {public Transform t,parent;public Vector3 position,scale;public Quaternion rotation;public bool active;public int layer;public string tag;public Component[] components;public string[] data;}
    static void MergeIncoming()
    {
        var main=SceneManager.GetSceneByPath(ScenePath);if(!main.IsValid()||!main.isLoaded)main=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        if(main.GetRootGameObjects().Any(g=>g.GetComponent<ShowroomPackageIntegration>()!=null)){File.WriteAllText(Dir+"/Merge.txt","Already merged; refusing to create duplicates.");return;}
        var incoming=EditorSceneManager.OpenScene(IncomingPath,OpenSceneMode.Additive);
        var originals=main.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>
        {var cs=t.GetComponents<Component>().Where(c=>c!=null&&!(c is Transform)).ToArray();return new Original{t=t,parent=t.parent,position=t.localPosition,rotation=t.localRotation,scale=t.localScale,active=t.gameObject.activeSelf,layer=t.gameObject.layer,tag=t.tag,components=cs,data=cs.Select(EditorJsonUtility.ToJson).ToArray()};}).ToArray();
        var current=originals.SelectMany(o=>new Object[]{o.t.gameObject,o.t}.Concat(o.components)).ToDictionary(Key,o=>o);
        var sourceTransforms=incoming.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var map=new Dictionary<Object,Object>();
        foreach(var t in sourceTransforms)
        {
            if(current.TryGetValue(Key(t.gameObject),out var same))
            {
                map[t.gameObject]=same;map[t]=((GameObject)same).transform;
                foreach(var c in t.GetComponents<Component>().Where(c=>c!=null&&!(c is Transform)))if(current.TryGetValue(Key(c),out var component)&&component.GetType()==c.GetType())map[c]=component;
            }
        }
        if(map.Keys.OfType<GameObject>().Count()!=originals.Length){EditorSceneManager.CloseScene(incoming,true);throw new Exception("Shared object identity mismatch; source/current IDs need review before merge");}
        int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Merge new Showroom package features");
        var added=new List<Component>();var moved=new List<GameObject>();var log=new List<string>();
        try
        {
            // Common object parenting stays unchanged. Capture the new rail display's
            // relative frame before detaching the existing module from its staging copy.
            var ray=incoming.GetRootGameObjects().FirstOrDefault(g=>g.name=="Ray");
            Transform railAnchor=ray!=null?ray.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>map.ContainsKey(t.gameObject)&&t.parent==ray.transform):null;
            Matrix4x4 rayRelative=railAnchor!=null?railAnchor.worldToLocalMatrix*ray.transform.localToWorldMatrix:Matrix4x4.identity;
            Transform destinationRail=railAnchor!=null?(Transform)map[railAnchor]:null;
            foreach(var t in sourceTransforms.Where(t=>map.ContainsKey(t.gameObject)&&t.parent!=null&&!map.ContainsKey(t.parent.gameObject)))t.SetParent(null,true);
            var newBranches=sourceTransforms.Where(t=>!map.ContainsKey(t.gameObject)&&(t.parent==null||map.ContainsKey(t.parent.gameObject))).ToArray();
            foreach(var t in newBranches)
            {
                if(t.name=="Directional Light (1)"&&t.parent==null){log.Add("KEPT_CURRENT_LIGHTING: omitted extra global directional light");continue;}
                var parent=t.parent;var lp=t.localPosition;var lr=t.localRotation;var ls=t.localScale;
                if(parent!=null)t.SetParent(null,true);
                SceneManager.MoveGameObjectToScene(t.gameObject,main);Undo.RegisterCreatedObjectUndo(t.gameObject,"Add Showroom feature "+t.name);
                if(parent!=null){t.SetParent((Transform)map[parent],false);t.localPosition=lp;t.localRotation=lr;t.localScale=ls;}
                if(t.gameObject==ray&&destinationRail!=null){t.SetParent(destinationRail,false);t.localPosition=rayRelative.GetColumn(3);t.localRotation=rayRelative.rotation;t.localScale=rayRelative.lossyScale;}
                moved.Add(t.gameObject);log.Add("ADDED_BRANCH: "+Hierarchy(t));
            }
            foreach(var pair in map.Where(p=>p.Key is GameObject).ToArray())
            {
                var source=(GameObject)pair.Key;var target=(GameObject)pair.Value;
                foreach(var component in source.GetComponents<Component>().Where(c=>c!=null&&!(c is Transform)&&!map.ContainsKey(c)))
                {
                    if(component is BaseInputModule){log.Add("KEPT_CURRENT_INPUT_MODULE: "+component.GetType().Name);continue;}
                    var copy=Undo.AddComponent(target,component.GetType());EditorUtility.CopySerialized(component,copy);map[component]=copy;added.Add(copy);log.Add("ADDED_COMPONENT: "+Hierarchy(target.transform)+" / "+component.GetType().FullName);
                }
            }
            // Only new content is retargeted. Existing Inspector values/events stay intact.
            var owners=moved.SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Concat(added).Where(c=>c is MonoBehaviour||c is Canvas||c is Joint||c is SkinnedMeshRenderer).Distinct().ToArray();
            foreach(var owner in owners)
            {
                if(owner==null)continue;var so=new SerializedObject(owner);var p=so.GetIterator();bool changed=false;
                while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null&&map.TryGetValue(p.objectReferenceValue,out var target)){p.objectReferenceValue=target;changed=true;}
                if(changed)so.ApplyModifiedPropertiesWithoutUndo();
            }
            var root=moved.SelectMany(g=>g.GetComponentsInChildren<SaBanRoot>(true)).Single();
            ConfigureMiniature(root,log);
            // Labels follow their exhibit's existing placement rather than moving old trains.
            var modelNames=new[]{"RiyadhMetro_D","glink_tram_skecthfab","sydney_metro_jotrain","McFace"};
            foreach(var stand in moved.SelectMany(g=>g.GetComponentsInChildren<MuseumLabelStand>(true)))
            {
                string model=stand.titleText.Contains("Riyadh")?modelNames[0]:stand.titleText.Contains("link")?modelNames[1]:stand.titleText.Contains("Sydney")?modelNames[2]:modelNames[3];
                var source=incoming.GetRootGameObjects().First(g=>g.name==model);var target=(GameObject)map[source];stand.transform.position+=target.transform.position-source.transform.position;log.Add("LABEL_ALIGNED: "+stand.titleText.Trim()+" -> "+model);
            }
            var chat=moved.SelectMany(g=>g.GetComponentsInChildren<AIChatbotUIController>(true)).Single();
            var marker=new GameObject("Showroom Package Integration");SceneManager.MoveGameObjectToScene(marker,main);Undo.RegisterCreatedObjectUndo(marker,"Add Showroom-owned integration");
            var integration=Undo.AddComponent<ShowroomPackageIntegration>(marker);integration.saBan=root;integration.chatbot=chat;integration.panelColliders=root.panel.GetComponentsInChildren<SaBanPanelButton>(true).SelectMany(b=>b.GetComponents<Collider>()).ToArray();
            var robot=main.GetRootGameObjects().Single(g=>g.name=="Robot");var interact=Undo.AddComponent<ShowroomRobotChatInteractable>(robot);interact.chatbot=chat;interact.objectName="Trò chuyện với trợ lý";interact.targetSceneName="";
            if(root.panel.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>()==null)Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>(root.panel.gameObject);
            // Verify every old object, component and serialized property before saving.
            foreach(var before in originals)
            {
                var t=before.t;if(t==null||t.parent!=before.parent||t.localPosition!=before.position||t.localRotation!=before.rotation||t.localScale!=before.scale||t.gameObject.activeSelf!=before.active||t.gameObject.layer!=before.layer||t.tag!=before.tag)throw new Exception("Existing object changed: "+(t!=null?Hierarchy(t):"destroyed"));
                for(int i=0;i<before.components.Length;i++)if(before.components[i]==null||EditorJsonUtility.ToJson(before.components[i])!=before.data[i])throw new Exception("Existing component changed: "+Hierarchy(t)+" / "+before.components[i]?.GetType().Name);
            }
            var dangling=new List<string>();
            foreach(var owner in owners.Where(c=>c!=null))
            {
                var so=new SerializedObject(owner);var p=so.GetIterator();while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue!=null)
                {var go=p.objectReferenceValue as GameObject??(p.objectReferenceValue as Component)?.gameObject;if(go!=null&&go.scene==incoming)dangling.Add(Hierarchy(owner.transform)+" / "+p.propertyPath+" -> "+Hierarchy(go.transform));}
            }
            if(dangling.Count>0){File.WriteAllLines(Dir+"/DanglingReferences.txt",dangling);throw new Exception("Staging-scene references remain; refusing to save");}
            EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);Undo.CollapseUndoOperations(group);
            log.Add("PASS: original "+originals.Length+" objects and all existing component serialized values unchanged");
            log.Add("PASS: no new-content references to staging scene; one existing EventSystem/input module retained");
            log.Add("Saved="+main.path);File.WriteAllLines(Dir+"/Merge.txt",log);
            SceneManager.SetActiveScene(main);
        }
        catch{Undo.RevertAllDownToGroup(group);throw;}
        finally{if(incoming.IsValid()&&incoming.isLoaded)EditorSceneManager.CloseScene(incoming,true);}
    }
    static void ConfigureMiniature(SaBanRoot root,List<string> log)
    {
        int disabled=0;foreach(var mb in root.content.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(mb==null)continue;var type=mb.GetType();string name=type.Name;
            if(name=="RailwayZoneStreamingManager"||name=="RailwayMicroChunkStreamer"){mb.gameObject.SetActive(false);disabled++;}
            else if(name=="TrainDriverHUD"){mb.gameObject.SetActive(false);disabled++;}
            else if(name=="TrainSpeedButtonFeedback"||name=="RailwayFarSceneryVisibility"||name=="BIMInteractable"){mb.enabled=false;disabled++;}
        }
        int colliders=0;foreach(var c in root.content.GetComponentsInChildren<Collider>(true)){if(c.enabled){c.enabled=false;colliders++;}}
        // Keep this new display clear of the current rear train exhibits. Fit only
        // the copied model; source spline knots/meshes/prefabs are never changed.
        root.transform.SetPositionAndRotation(new Vector3(10,0,19),Quaternion.identity);
        Bounds LocalBounds()
        {
            bool initialized=false;var bounds=new Bounds();
            foreach(var r in root.model.GetComponentsInChildren<Renderer>(true))
            {
                if(!r.enabled||r is LineRenderer||r is TrailRenderer||r.GetComponent<MeshFilter>()?.sharedMesh==null)continue;
                var b=r.GetComponent<MeshFilter>().sharedMesh.bounds;var m=root.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
                for(int i=0;i<8;i++){var e=b.extents;var p=m.MultiplyPoint3x4(b.center+new Vector3((i&1)==0?-e.x:e.x,(i&2)==0?-e.y:e.y,(i&4)==0?-e.z:e.z));if(!initialized){bounds=new Bounds(p,Vector3.zero);initialized=true;}else bounds.Encapsulate(p);}
            }
            return bounds;
        }
        var before=LocalBounds();float factor=Mathf.Min(1,root.tableSize.x*.94f/Mathf.Max(before.size.x,.001f),root.tableSize.y*.94f/Mathf.Max(before.size.z,.001f));root.SetModelScale(root.modelScale*factor);
        var after=LocalBounds();root.model.localPosition+=new Vector3(-after.center.x,root.tableTopHeight+.02f-after.min.y,-after.center.z);
        root.panel.localPosition=new Vector3(0,1.45f,root.tableSize.y*.5f+.7f);root.panel.localScale=Vector3.one*.0015f;
        log.Add("MINIATURE: only copied legacy runtime systems disabled="+disabled+"; copied model colliders disabled="+colliders+"; modelScale="+root.modelScale+"; fitted within table "+root.tableSize+"; new display positioned in front free area");
    }
}
