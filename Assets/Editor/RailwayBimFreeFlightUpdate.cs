using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

public static partial class RailwayBimRefinement
{
    static void ApplyFreeFlightStyle()
    {
        var scene=Main();
        var outline=Object.FindFirstObjectByType<RailwayBimOutline>();
        var material=outline.outlineMaterial;
        if(AssetDatabase.GetAssetPath(material)!="Assets/ProjectLopHoc/Generated/RailwayLifecycleBim/OutlineShell.mat")throw new Exception("Unexpected highlight material");
        Undo.RecordObject(material,"Improve BIM selection contrast");
        material.SetColor("_BaseColor",new Color(1,.08f,.45f,1));material.SetFloat("_Width",5);
        EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Dir+"/FreeFlightApplied.txt","PC noclip flight; VR Origin noclip flight (right stick Y altitude); hot-pink outline RGB(1,.08,.45), width 5px. Source materials and tracked HMD local pose untouched.");
        File.WriteAllText("Temp/BimRefinementTest.request","flight");
    }
}

public static partial class RailwayBimRefinementTests
{
    static bool FlightTick(RailwayBimController c)
    {
        if(EditorApplication.timeSinceStartup<wait)return false;
        switch(flow)
        {
            case 0:
                SetupInteraction(c);vr.SetVrMode(false);vr.SetPanelVisible(false);
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                c.cameraController.transform.position=new Vector3(5000,5000,5000);
                c.cameraController.SetFreeMode(false);c.cameraController.SetFreeMode(true);
                var camera=c.cameraController;var initial=camera.transform.position;
                camera.Orbit(new Vector2(80,1200));
                typeof(RailwayBimCamera).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(camera,null);
                Check(Vector3.Distance(initial,camera.transform.position)<.01f,"Free-look rotates in place instead of orbiting the asset");
                Check(camera.current.pitch<0,"Free camera can look upward beyond old pitch restriction");
                positionBefore=camera.transform.position;
                var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="FlightTestObstacle";obstacle.transform.position=positionBefore+Vector3.up*.5f;obstacle.transform.localScale=new Vector3(100,.1f,100);Physics.SyncTransforms();
                Check(Physics.Raycast(positionBefore,Vector3.up,2),"A real collider blocks the ascent path in the test");
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.E));return WaitFor(.5,1);
            case 1:
                InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                Check(c.cameraController.transform.position.y>positionBefore.y+.8f,"Actual PC E input flies through the collider");
                Object.Destroy(GameObject.Find("FlightTestObstacle"));vr.SetVrMode(true);return WaitFor(.7,2);
            case 2:
                vr.SetPanelVisible(false);var head=vr.Origin.Camera.transform;
                vr.Origin.transform.position+=new Vector3(5000,5000,5000)-head.position;
                var start=head.position;var local=head.localPosition;
                var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="VRFlightTestWall";wall.transform.SetPositionAndRotation(start+forward*.3f,Quaternion.LookRotation(forward));wall.transform.localScale=new Vector3(100,100,.02f);Physics.SyncTransforms();
                for(int i=0;i<12;i++)vr.StepLocomotion(Vector2.up,Vector2.zero,.05f);
                Check(Vector3.Dot(head.position-start,forward)>.8f,"VR Origin travels through collider outside prior zone bounds");
                float y=head.position.y;
                for(int i=0;i<12;i++)vr.StepLocomotion(Vector2.zero,Vector2.up,.05f);
                Check(head.position.y>y+.8f,"Right joystick Y raises VR Origin in free flight");
                Check(head.localPosition==local,"VR flight preserves tracked HMD local pose");
                var color=c.outline.outlineMaterial.GetColor("_BaseColor");
                Check(color.r>.99f&&color.g<.1f&&color.b>.4f&&c.outline.outlineMaterial.GetFloat("_Width")==5,"High-contrast pink highlight and 5px outline applied");
                c.SetOverview();c.Select(c.catalog.segments[0].id);Check(c.outline.HighlightedRenderers>0,"Selection still produces outline renderers");
                vr.SetPanelVisible(true);vr.SetVrMode(false);
                EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).maximized=oldMaximized;
                return true;
        }
        return true;
    }
}
