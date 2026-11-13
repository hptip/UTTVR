using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using Object=UnityEngine.Object;

public static partial class RailwayBimRefinement
{
    static void ApplyTopBar()
    {
        var scene=Main();var ui=Object.FindFirstObjectByType<RailwayBimDashboard>();
        var root=ui.commandPanel.parent;
        var footer=(RectTransform)ui.status.transform.parent;
        var header=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="BimHeader");
        int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Compact BIM top controls");
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject,"Compact BIM top controls");
        try
        {
            if(header!=null)
            {
                // Keep the live navigation/status bindings and the hide-UI control;
                // discard the old header's branding, background and empty holders.
                Undo.SetTransformParent(ui.breadcrumb.transform,footer,"Keep BIM context label");
                if(ui.toggleUiButton!=null)Undo.SetTransformParent(ui.toggleUiButton.transform,footer,"Keep hide-UI control");
                foreach(var owner in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c!=null&&!(c is Transform)&&!c.transform.IsChildOf(header)))
                {
                    var so=new SerializedObject(owner);var p=so.GetIterator();
                    while(p.Next(true))if(p.propertyType==SerializedPropertyType.ObjectReference)
                    {
                        var target=p.objectReferenceValue as GameObject??(p.objectReferenceValue as Component)?.gameObject;
                        if(target!=null&&(target.transform==header||target.transform.IsChildOf(header)))throw new Exception("Header still referenced by "+owner.GetType().Name+"."+p.propertyPath);
                    }
                }
                Undo.DestroyObjectImmediate(header.gameObject);
            }
            Rect(footer,new Vector2(.012f,.88f),new Vector2(.988f,.988f));
            Rect(ui.commandPanel,new Vector2(.248f,.705f),new Vector2(.625f,.87f));
            Rect(ui.breadcrumb.transform,new Vector2(.016f,.65f),new Vector2(.76f,.97f));
            ui.breadcrumb.fontSize=18;ui.breadcrumb.enableAutoSizing=true;ui.breadcrumb.fontSizeMin=12;ui.breadcrumb.fontSizeMax=18;ui.breadcrumb.textWrappingMode=TextWrappingModes.NoWrap;
            Rect(ui.status.transform,new Vector2(.016f,.06f),new Vector2(.76f,.63f));ui.status.fontSize=18;ui.status.enableAutoSizing=true;ui.status.fontSizeMin=13;ui.status.fontSizeMax=18;ui.status.raycastTarget=false;
            if(ui.toggleUiButton!=null)Rect(ui.toggleUiButton.transform,new Vector2(.79f,.14f),new Vector2(.984f,.86f));
            footer.SetAsLastSibling();ui.commandPanel.SetAsLastSibling();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Undo.CollapseUndoOperations(group);
            File.WriteAllText(Dir+"/TopBarApplied.txt","Removed BimHeader after serialized-reference checks; kept breadcrumb and hide-UI control inside StatusFooter at former header bounds. BimCommands moved to upper center. In VR, status and commands share the raised command canvas, retaining three World Space panels.");
            File.WriteAllText("Temp/BimRefinementTest.request","topbar");
        }
        catch{Undo.RevertAllDownToGroup(group);throw;}
    }
}

public static partial class RailwayBimRefinementTests
{
    static bool TopBarTick(RailwayBimController c)
    {
        if(EditorApplication.timeSinceStartup<wait)return false;
        switch(flow)
        {
            case 0:
                SetupInteraction(c);vr.SetVrMode(false);vr.SetPanelVisible(true);
                Check(!c.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Any(t=>t.name=="BimHeader"),"BimHeader removed from saved scene");
                var footer=(RectTransform)ui.status.transform.parent;
                Check(footer.name=="StatusFooter"&&footer.anchorMin.y>=.88f,"StatusFooter replaces header at top of desktop UI");
                Check(ui.breadcrumb.transform.parent==footer&&ui.toggleUiButton.transform.parent==footer,"Context and hide-UI references preserved in compact footer");
                Check(ui.commandPanel.anchorMin.y>.7f&&ui.commandPanel.anchorMax.y<footer.anchorMin.y,"Commands above model area without overlap with top status");
                ui.SetStatus("Kiểm tra thanh trạng thái phía trên");Check(ui.status.text.Contains("thanh trạng thái"),"Status updates still reach relocated text");
                c.Select(segment);ui.detailButton.onClick.Invoke();Check(c.Level==1,"Relocated detail button still works");ui.backButton.onClick.Invoke();Check(c.Level==0,"Relocated back button still works");
                vr.SetVrMode(true);return WaitFor(.7,1);
            case 1:
                var pane=vr.WorldPanels.Single(p=>p.name=="BIM VR · Thao tác");var head=vr.Origin.Camera.transform;
                Check(vr.WorldPanels.Count==3,"VR keeps three panels without duplicate canvas");
                Check(pane.transform.position.y>head.position.y+.4f,"Command canvas is above head-level model view");
                Check(ui.status.GetComponentInParent<Canvas>(true)==pane&&ui.status.gameObject.activeInHierarchy,"Relocated status is visible in VR, not left under disabled desktop canvas");
                Check(ui.commandPanel.anchorMax.y<((RectTransform)ui.status.transform.parent).anchorMin.y,"VR status and command rows have separate vertical areas");
                vr.SetPanelVisible(false);vr.SetPanelVisible(true);vr.SetVrMode(false);
                Check(ui.status.transform.parent.name=="StatusFooter"&&ui.status.transform.parent.parent==ui.commandPanel.parent,"Desktop hierarchy restored after VR toggle");
                Check(((RectTransform)ui.status.transform.parent).anchorMin.y>=.88f,"Top status position survives VR toggle");
                return true;
        }
        return true;
    }
}
