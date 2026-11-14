using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BIM4D.AI.Generative;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Runs only on explicit file requests (Temp/YenTuChatbotSetup/request.txt); uses the currently open Editor.
//   inspect                     -> Inspection.txt: train cars, seats, spawn points
//   setup|<seat path>|<side>    -> robot beside that seat inside its car + chatbot UI in YenTuHaLongXanh
[InitializeOnLoad]
public static class YenTuChatbotSetup
{
    const string Dir = "Temp/YenTuChatbotSetup";
    const string Request = Dir + "/request.txt";
    const string ScenePath = "Assets/ProjectLopHoc/Scenes/YenTuHaLongXanh.unity";
    const string ChatbotRoot = "Assets/ProjectLopHoc/AIChatbot_Phase3_MetaConnect_Final_Unity6/AIChatbot_Phase3_MetaConnect_Final_Unity6/Unity/Assets/AIChatbot_Phase2_Final_Unity6";
    const string ChatbotPrefab = ChatbotRoot + "/Prefabs/AIChatbot_Prefab.prefab";
    const string RobotPrefab = "Assets/ProjectLopHoc/Prefabs/Robot.prefab";
    const string RulesAsset = ChatbotRoot + "/Knowledge/UTT_QuyTac.txt";
    const string KnowledgeAsset = ChatbotRoot + "/Knowledge/YenTu_KienThuc.txt";
    const string RobotName = "Robot Chatbot (Khoang tau)";
    const string BrainName = "Chat Bot (YenTu)";
    const float SideOffset = 0.75f;   // m, from seat centre to robot
    const float YawTowardSeat = 70f;  // deg, turned from the car's forward axis toward the seat

    static YenTuChatbotSetup() { EditorApplication.update += Tick; }

    static void Tick()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        try
        {
            string[] parts = command.Split('|');
            OpenYenTu();
            if (parts[0] == "inspect") Inspect();
            else if (parts[0] == "setup") Setup(parts.Length > 1 ? parts[1] : "", parts.Length > 2 ? parts[2] : "right");
            File.WriteAllText(Dir + "/Stage.txt", "DONE " + parts[0]);
        }
        catch (Exception e) { File.WriteAllText(Dir + "/Error.txt", e.ToString()); File.WriteAllText(Dir + "/Stage.txt", "FAILED: " + e.Message); Debug.LogException(e); }
    }

    static void OpenYenTu()
    {
        var active = SceneManager.GetActiveScene();
        if (active.path == ScenePath) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Unsaved scene: " + SceneManager.GetSceneAt(i).path);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";

    static TrainSplineMover Mover() => UnityEngine.Object.FindFirstObjectByType<TrainSplineMover>(FindObjectsInactive.Include) ?? throw new Exception("TrainSplineMover not found");

    static void Inspect()
    {
        var mover = Mover();
        var rows = new List<string> { "Mover=" + PathOf(mover.transform) };
        foreach (var car in new[] { mover.frontCar, mover.rearCar })
        {
            if (car == null) continue;
            var renderers = car.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers.Length > 0 ? renderers.Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }) : new Bounds(car.position, Vector3.zero);
            rows.Add($"Car={PathOf(car)} pos={V(car.position)} fwd={V(car.forward)} right={V(car.right)} boundsCenterLocal={V(car.InverseTransformPoint(bounds.center))} size={V(bounds.size)}");
            foreach (var t in car.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("seat") || n.Contains("ghe") || n.Contains("chair") || n.Contains("bench") || n.Contains("spawn") || n.Contains("interior") || n.Contains("cabin"))
                    rows.Add($"  Node={PathOf(t)} local={V(car.InverseTransformPoint(t.position))}");
            }
        }
        var seat = UnityEngine.Object.FindFirstObjectByType<TrainSeatInteractable>(FindObjectsInactive.Include);
        if (seat != null) rows.Add($"DriverSeat={PathOf(seat.transform)} point={(seat.driverSeatPoint != null ? PathOf(seat.driverSeatPoint) + " " + V(seat.driverSeatPoint.position) : "-")}");
        var spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>(FindObjectsInactive.Include);
        if (spawner != null)
        {
            if (spawner.studentSpawnPoint != null) rows.Add($"StudentSpawn={PathOf(spawner.studentSpawnPoint)} {V(spawner.studentSpawnPoint.position)}");
            if (spawner.lecturerSpawnPoint != null) rows.Add($"LecturerSpawn={PathOf(spawner.lecturerSpawnPoint)} {V(spawner.lecturerSpawnPoint.position)}");
        }
        rows.Add("ExistingRobot=" + (GameObject.Find(RobotName) != null));
        rows.Add("ExistingChatUI=" + (UnityEngine.Object.FindFirstObjectByType<AIChatbotUIController>(FindObjectsInactive.Include) != null));
        File.WriteAllLines(Dir + "/Inspection.txt", rows);
    }

    static void Setup(string seatPath, string side)
    {
        var mover = Mover();
        var seat = GameObject.Find(seatPath)?.transform ?? throw new Exception("Seat not found: " + seatPath);
        Transform car = new[] { mover.frontCar, mover.rearCar }.FirstOrDefault(c => c != null && seat.IsChildOf(c)) ?? mover.frontCar;

        // 1. Chat UI (one per scene).
        var ui = UnityEngine.Object.FindFirstObjectByType<AIChatbotUIController>(FindObjectsInactive.Include);
        if (ui == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChatbotPrefab), SceneManager.GetActiveScene());
            ui = go.GetComponentInChildren<AIChatbotUIController>(true);
        }

        // 2. Brain + speech-to-text, wired into the integration.
        var brain = UnityEngine.Object.FindFirstObjectByType<XkiroBrain>(FindObjectsInactive.Include);
        if (brain == null)
        {
            var go = new GameObject(BrainName);
            SceneManager.MoveGameObjectToScene(go, SceneManager.GetActiveScene());
            brain = go.AddComponent<XkiroBrain>();
            go.AddComponent<GroqSpeechToText>();
        }
        var so = new SerializedObject(brain);
        so.FindProperty("model").stringValue = "mistralai/mistral-medium-3.5";
        so.FindProperty("systemPrompt").stringValue = "Bạn là trợ lý ảo trên tàu tuyến Yên Tử – Hạ Long. Chỉ trả lời dựa trên TÀI LIỆU được cung cấp và tuân thủ QUY TẮC TRẢ LỜI.";
        so.FindProperty("assistantInstructions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(RulesAsset);
        so.FindProperty("modelContext").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(KnowledgeAsset);
        so.ApplyModifiedPropertiesWithoutUndo();
        var integration = ui.GetComponentInChildren<AIChatbotIntegration>(true) ?? UnityEngine.Object.FindFirstObjectByType<AIChatbotIntegration>(FindObjectsInactive.Include);
        if (integration != null) { integration.brain = brain; EditorUtility.SetDirty(integration); }

        // 3. XR glue: world-space canvas following the head + Quest keyboard (same component as Showroom).
        var glue = UnityEngine.Object.FindFirstObjectByType<ShowroomPackageIntegration>(FindObjectsInactive.Include);
        if (glue == null) glue = ui.gameObject.AddComponent<ShowroomPackageIntegration>();
        glue.chatbot = ui; EditorUtility.SetDirty(glue);

        // 4. Robot inside the car, beside the seat, turned toward it.
        var robot = GameObject.Find(RobotName);
        if (robot == null)
        {
            robot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefab), SceneManager.GetActiveScene());
            robot.name = RobotName;
        }
        robot.transform.SetParent(car, true);
        float sign = side == "left" ? -1f : 1f;
        Vector3 seatLocal = car.InverseTransformPoint(seat.position);
        Vector3 local = seatLocal + Vector3.right * (SideOffset * sign);
        // Stand on the cabin floor: the player spawn is placed on it.
        var spawner = UnityEngine.Object.FindFirstObjectByType<PlayerSpawner>(FindObjectsInactive.Include);
        if (spawner != null && spawner.studentSpawnPoint != null) local.y = car.InverseTransformPoint(spawner.studentSpawnPoint.position).y;
        robot.transform.localPosition = local;

        var follow = robot.GetComponent<FollowCompanionBot>();
        // Facing the car's forward axis, then turned YawTowardSeat degrees toward the seat side.
        // The FBX's visual front is offset by modelRotationOffset (FollowCompanionBot applies it the same way).
        Quaternion modelOffset = Quaternion.Euler(follow != null ? follow.modelRotationOffset : Vector3.zero);
        robot.transform.localRotation = Quaternion.Euler(0f, -sign * YawTowardSeat, 0f) * modelOffset;

        if (follow != null) { follow.enabled = false; EditorUtility.SetDirty(follow); } // must ride with the car, not chase the player

        if (robot.GetComponentInChildren<Collider>() == null)
        {
            var box = robot.AddComponent<BoxCollider>();
            var rends = robot.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                box.center = robot.transform.InverseTransformPoint(b.center);
                box.size = Vector3.Scale(b.size, new Vector3(1f / robot.transform.lossyScale.x, 1f / robot.transform.lossyScale.y, 1f / robot.transform.lossyScale.z));
            }
        }

        var pc = robot.GetComponent<RobotInteraction>() ?? robot.AddComponent<RobotInteraction>();
        pc.chatbot = ui;
        var talk = robot.GetComponent<ShowroomRobotChatInteractable>() ?? robot.AddComponent<ShowroomRobotChatInteractable>();
        talk.chatbot = ui;

        // Quest: point the controller ray at the robot and pull the trigger.
        var xr = robot.GetComponent<XRSimpleInteractable>() ?? robot.AddComponent<XRSimpleInteractable>();
        for (int i = xr.selectEntered.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(xr.selectEntered, i);
        UnityEventTools.AddVoidPersistentListener(xr.selectEntered, ui.Open);
        EditorUtility.SetDirty(robot);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        File.WriteAllLines(Dir + "/Setup.txt", new[]
        {
            "Car=" + PathOf(car), "Seat=" + PathOf(seat) + " local=" + V(seatLocal),
            "Robot=" + PathOf(robot.transform) + " local=" + V(robot.transform.localPosition) + " yaw=" + robot.transform.localEulerAngles.y.ToString("0.0"),
            "ChatUI=" + PathOf(ui.transform), "Brain=" + PathOf(brain.transform), "Integration=" + (integration != null),
            "Knowledge=" + (AssetDatabase.LoadAssetAtPath<TextAsset>(KnowledgeAsset) != null)
        });
    }
}
