using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
    // The chat canvas follows the head (XRWorldSpaceCanvas) and YenTu moves the whole rig on a train,
    // so the keyboard keeps its pose relative to the chat canvas instead of staying where it was first built.
    // It is re-posed when the canvas is (XRRigFollowPanel.Positioned, just before render) so both move in the same frame.
    Transform keyboardAnchor;Vector3 keyboardLocalPosition;Quaternion keyboardLocalRotation;
    XRRigFollowPanel keyboardFollow;
    readonly HashSet<int> priorKeyboardRoots=new HashSet<int>();
    XROrigin origin;
    bool xrConfigured;
    bool controlsHiddenForChat,controlsWereVisible;
    float nextCheck;
    // Quest shortcuts: A opens/closes the chat, right grip sends (left grip push-to-talk lives in PointerVoiceTrigger).
    InputAction toggleChat,sendChat;
    // The chat panel reads small at 1.5 m in the headset; 1.4x the default 0.001 world scale.
    const float ChatWorldScale=0.0014f;
    Button keyboardButton;
    void Start()
    {
        if(chatbot!=null){chatbot.onOpened.AddListener(OnChatOpened);chatbot.onClosed.AddListener(OnChatClosed);}
        Configure();
        toggleChat=new InputAction("Chatbot A",InputActionType.Button,"<XRController>{RightHand}/primaryButton");toggleChat.Enable();
        sendChat=new InputAction("Chatbot send grip",InputActionType.Button,"<XRController>{RightHand}/gripPressed");sendChat.AddBinding("<XRController>{RightHand}/gripButton");sendChat.Enable();
    }
    void Update()
    {
        if(Time.unscaledTime>=nextCheck){nextCheck=Time.unscaledTime+.5f;Configure();}
        if(chatbot==null)return;
        if(toggleChat!=null&&toggleChat.WasPressedThisFrame())chatbot.Toggle();
        if(sendChat!=null&&sendChat.WasPressedThisFrame()&&chatbot.IsOpen)chatbot.Send();
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
            if(canvas!=null)
            {
                var worldCanvas=canvas.GetComponent<XRWorldSpaceCanvas>();
                if(worldCanvas==null)worldCanvas=canvas.gameObject.AddComponent<XRWorldSpaceCanvas>();
                worldCanvas.WorldScale=ChatWorldScale;
            }
            xrConfigured=true;
        }
        if(xr&&chatbot!=null&&chatbot.IsOpen&&keyboard==null)OnChatOpened();
    }
    public void OpenChat(){if(chatbot!=null)chatbot.Open();}
    void OnChatOpened()
    {
        if(!controlsHiddenForChat&&saBan!=null&&saBan.panel!=null){controlsWereVisible=saBan.panel.gameObject.activeSelf;saBan.panel.gameObject.SetActive(false);controlsHiddenForChat=true;}
        if(origin==null||origin.Camera==null||chatbot==null||chatbot.inputField==null)return;
        if(keyboard==null)
        {
            var canvas=chatbot.inputField.GetComponentInParent<Canvas>();
            foreach(var root in LoadedRoots())if(root.name=="Quest 3D Keyboard")priorKeyboardRoots.Add(root.GetInstanceID());
            keyboard=chatbot.gameObject.AddComponent<QuestLoginKeyboard>();
            keyboardAnchor=canvas!=null?canvas.transform:chatbot.transform;
            keyboard.Initialize(chatbot.inputField,null,keyboardAnchor);
            chatbot.inputField.onSelect.AddListener(CaptureKeyboardRoot);
            CreateKeyboardButton();
        }
    }
    // The keyboard stays hidden when the chat opens; a "Phím" button next to the mic toggles it.
    // It is a copy of the Clear button so it inherits the row's look and layout size.
    void CreateKeyboardButton()
    {
        if(keyboardButton!=null||chatbot.clearButton==null||chatbot.voiceButton==null)return;
        keyboardButton=Instantiate(chatbot.clearButton,chatbot.clearButton.transform.parent);
        keyboardButton.name="KeyboardButton";
        keyboardButton.onClick=new Button.ButtonClickedEvent();
        keyboardButton.onClick.AddListener(ToggleKeyboard);
        keyboardButton.transform.SetSiblingIndex(chatbot.voiceButton.transform.GetSiblingIndex()+1);
        var label=keyboardButton.GetComponentInChildren<TMP_Text>(true);
        if(label!=null)label.text="Phím";
    }
    void ToggleKeyboard()
    {
        if(keyboardRoot!=null&&keyboardRoot.activeSelf){keyboardRoot.SetActive(false);return;}
        // The keyboard only opens from the input field's onSelect, which ActivateInputField does not raise and
        // EventSystem selection does not reliably reach under the XR input module; raise it directly.
        var field=chatbot!=null?chatbot.inputField:null;
        if(field==null||!chatbot.IsOpen||!field.isActiveAndEnabled)return;
        if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(field.gameObject);
        field.onSelect.Invoke(field.text);
        field.ActivateInputField();
    }
    // The keyboard is created in the active scene, which in YenTu can be a streamed zone rather than this scene.
    static IEnumerable<GameObject> LoadedRoots()
    {
        for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);if(scene.isLoaded)foreach(var root in scene.GetRootGameObjects())yield return root;}
    }
    void CaptureKeyboardRoot(string _)
    {
        if(keyboardRoot!=null)return;
        keyboardRoot=LoadedRoots().FirstOrDefault(g=>g.name=="Quest 3D Keyboard"&&!priorKeyboardRoots.Contains(g.GetInstanceID()));
        if(keyboardRoot!=null&&keyboardAnchor!=null)
        {
            PlaceKeyboardUnderPanel();
            keyboardLocalPosition=keyboardAnchor.InverseTransformPoint(keyboardRoot.transform.position);
            keyboardLocalRotation=Quaternion.Inverse(keyboardAnchor.rotation)*keyboardRoot.transform.rotation;
        }
    }
    // QuestLoginKeyboard centres the keyboard on the canvas origin, which is the chat panel's right edge and,
    // with the larger panel, overlaps it; sit it just below the panel's bottom edge, slightly toward the viewer.
    void PlaceKeyboardUnderPanel()
    {
        var panelRect=chatbot.panel!=null?chatbot.panel.transform as RectTransform:null;
        var keyboardRect=keyboardRoot.transform as RectTransform;
        if(panelRect==null||keyboardRect==null)return;
        var corners=new Vector3[4];panelRect.GetWorldCorners(corners);
        Vector3 bottomCenter=(corners[0]+corners[3])*.5f;
        Vector3 up=keyboardAnchor.up,forward=keyboardAnchor.forward;
        float halfHeight=keyboardRect.rect.height*keyboardRect.lossyScale.y*.5f;
        keyboardRoot.transform.SetPositionAndRotation(bottomCenter-up*(halfHeight+.02f)-forward*.03f,keyboardAnchor.rotation);
    }
    void LateUpdate()
    {
        if(keyboardFollow==null&&keyboardAnchor!=null&&(keyboardFollow=keyboardAnchor.GetComponent<XRRigFollowPanel>())!=null)keyboardFollow.Positioned+=PoseKeyboard;
        if(keyboardFollow==null)PoseKeyboard(keyboardAnchor);
    }
    void PoseKeyboard(Transform anchor)
    {
        if(keyboardRoot==null||anchor==null||!keyboardRoot.activeSelf)return;
        keyboardRoot.transform.SetPositionAndRotation(anchor.TransformPoint(keyboardLocalPosition),anchor.rotation*keyboardLocalRotation);
    }
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
        if(keyboardFollow!=null)keyboardFollow.Positioned-=PoseKeyboard;
        if(keyboardRoot!=null)Destroy(keyboardRoot);
        toggleChat?.Dispose();sendChat?.Dispose();
    }
}
