using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;
using UTT.SaBan;
using ProjectLopHoc.Features.Authentication.Views;
using System.Collections.Generic;
using System.Linq;

/// <summary>Showroom-owned compatibility glue; never edits a shared rig or input asset.</summary>
[DefaultExecutionOrder(200)]
public sealed class ShowroomPackageIntegration:MonoBehaviour
{
    public SaBanRoot saBan;
    public AIChatbotUIController chatbot;
    public Collider[] panelColliders;
    QuestLoginKeyboard keyboard;
    GameObject keyboardRoot;
    readonly HashSet<int> priorKeyboardRoots=new HashSet<int>();
    XROrigin origin;
    bool xrConfigured;
    bool controlsHiddenForChat,controlsWereVisible;
    float nextCheck;
    // Quest shortcuts: B opens/closes the chat, right grip sends (left grip push-to-talk lives in PointerVoiceTrigger).
    InputAction toggleChat,sendChat;
    void Start()
    {
        if(chatbot!=null){chatbot.onOpened.AddListener(OnChatOpened);chatbot.onClosed.AddListener(OnChatClosed);}
        Configure();
        toggleChat=new InputAction("Chatbot B",InputActionType.Button,"<XRController>{RightHand}/secondaryButton");toggleChat.Enable();
        sendChat=new InputAction("Chatbot send grip",InputActionType.Button,"<XRController>{RightHand}/gripPressed");sendChat.AddBinding("<XRController>{RightHand}/gripButton");sendChat.Enable();
    }
    void Update()
    {
        if(Time.unscaledTime>=nextCheck){nextCheck=Time.unscaledTime+.5f;Configure();}
        if(chatbot==null)return;
        // In YenTu the driver uses B for the exterior train view; leave B to it while the local player is driving.
        if(toggleChat!=null&&toggleChat.WasPressedThisFrame()&&!LocalPlayerDriving())chatbot.Toggle();
        if(sendChat!=null&&sendChat.WasPressedThisFrame()&&chatbot.IsOpen)chatbot.Send();
    }
    static bool LocalPlayerDriving()
    {
        foreach(var player in FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
            if(player!=null&&player.isDriving&&player.Object!=null&&player.Object.IsValid&&player.HasStateAuthority)return true;
        return false;
    }
    void Configure()
    {
        if(origin==null)origin=FindFirstObjectByType<XROrigin>();
        bool xr=origin!=null&&origin.Camera!=null;
        if(panelColliders!=null)foreach(var collider in panelColliders)if(collider!=null)collider.enabled=!xr;
        if(saBan!=null&&saBan.panel!=null)
        {
            var canvas=saBan.panel.GetComponent<Canvas>();
            if(canvas!=null)
            {
                canvas.worldCamera=xr?origin.Camera:Camera.main;
                if(xr&&canvas.GetComponent<TrackedDeviceGraphicRaycaster>()==null)canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                var standard=canvas.GetComponent<GraphicRaycaster>();if(standard!=null)standard.enabled=!xr;
            }
        }
        if(xr&&!xrConfigured&&chatbot!=null)
        {
            var canvas=chatbot.GetComponentInChildren<Canvas>(true);
            if(canvas!=null&&canvas.GetComponent<XRWorldSpaceCanvas>()==null)canvas.gameObject.AddComponent<XRWorldSpaceCanvas>();
            xrConfigured=true;
        }
        if(xr&&chatbot!=null&&chatbot.IsOpen&&keyboard==null)OnChatOpened();
    }
    public void OpenChat(){if(chatbot!=null)chatbot.Open();}
    void OnChatOpened()
    {
        if(!controlsHiddenForChat&&saBan!=null&&saBan.panel!=null){controlsWereVisible=saBan.panel.gameObject.activeSelf;saBan.panel.gameObject.SetActive(false);controlsHiddenForChat=true;}
        if(origin==null||origin.Camera==null||keyboard!=null||chatbot==null||chatbot.inputField==null)return;
        var canvas=chatbot.inputField.GetComponentInParent<Canvas>();
        foreach(var root in gameObject.scene.GetRootGameObjects())if(root.name=="Quest 3D Keyboard")priorKeyboardRoots.Add(root.GetInstanceID());
        keyboard=chatbot.gameObject.AddComponent<QuestLoginKeyboard>();
        keyboard.Initialize(chatbot.inputField,null,canvas!=null?canvas.transform:chatbot.transform);
        chatbot.inputField.onSelect.AddListener(CaptureKeyboardRoot);
    }
    void CaptureKeyboardRoot(string _){if(keyboardRoot==null)keyboardRoot=gameObject.scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Quest 3D Keyboard"&&!priorKeyboardRoots.Contains(g.GetInstanceID()));}
    void OnChatClosed()
    {
        if(keyboardRoot!=null)keyboardRoot.SetActive(false);
        if(controlsHiddenForChat&&saBan!=null&&saBan.panel!=null)saBan.panel.gameObject.SetActive(controlsWereVisible);
        controlsHiddenForChat=false;
    }
    void OnDestroy()
    {
        if(chatbot!=null){chatbot.onOpened.RemoveListener(OnChatOpened);chatbot.onClosed.RemoveListener(OnChatClosed);}
        if(chatbot!=null&&chatbot.inputField!=null)chatbot.inputField.onSelect.RemoveListener(CaptureKeyboardRoot);
        if(keyboardRoot!=null)Destroy(keyboardRoot);
        toggleChat?.Dispose();sendChat?.Dispose();
    }
}
