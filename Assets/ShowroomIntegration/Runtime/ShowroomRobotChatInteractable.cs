using UnityEngine;

/// <summary>Uses the existing PC/XR interaction contract without replacing bot follow logic.</summary>
public sealed class ShowroomRobotChatInteractable:InteractableObject
{
    public AIChatbotUIController chatbot;
    void Awake(){objectName="Trò chuyện với trợ lý";targetSceneName="";highlightRenderer=null;}
    public override void OnInteract(){if(chatbot!=null)chatbot.Open();}
}
