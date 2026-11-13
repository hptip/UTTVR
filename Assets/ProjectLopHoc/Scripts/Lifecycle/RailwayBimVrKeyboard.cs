using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>A scene-owned tracked-canvas keyboard; retains target/caret when a key receives XR focus.</summary>
public sealed class RailwayBimVrKeyboard:MonoBehaviour
{
    public RailwayBimVrPresentation presentation;public RailwayBimDashboard dashboard;
    public TMP_InputField Target{get;private set;}public bool IsOpen=>root!=null&&root.activeSelf;
    GameObject root;TMP_Text preview;bool shift;int anchor,focus;readonly HashSet<TMP_InputField> attached=new HashSet<TMP_InputField>();readonly List<(Button button,char key)> letters=new List<(Button,char)>();
    float nextCheck;string context;
    void Update()
    {
        if(!presentation.IsVr)return;
        string current=dashboard.EditingContext;if(context!=current){context=current;Close();}
        if(Time.unscaledTime>nextCheck){nextCheck=Time.unscaledTime+.25f;foreach(var field in dashboard.GetComponentsInChildren<TMP_InputField>(true))Attach(field);Attach(dashboard.search);foreach(var f in dashboard.Inputs.Values)Attach(f);}
        if(Target!=null&&IsOpen){if(!Target.gameObject.activeInHierarchy){Close();return;}if(Target.isFocused){anchor=Target.selectionStringAnchorPosition;focus=Target.selectionStringFocusPosition;}preview.text=Target.text;}
    }
    void Attach(TMP_InputField field){if(field==null||!attached.Add(field))return;field.onSelect.AddListener(_=>{if(presentation.IsVr)Open(field);});}
    public void Open(TMP_InputField field)
    {Target=field;anchor=field.selectionStringAnchorPosition;focus=field.selectionStringFocusPosition;if(root==null)Build();root.SetActive(true);preview.text=field.text;var head=presentation.Origin.Camera.transform;var forward=Vector3.ProjectOnPlane(head.forward,Vector3.up).normalized;root.transform.SetPositionAndRotation(head.position+forward*1.12f-Vector3.up*.44f,Quaternion.LookRotation(forward,Vector3.up));}
    public void Close(){if(root!=null)root.SetActive(false);Target=null;}
    public void Insert(string text)
    {
        if(Target==null)return;int length=Target.text.Length;int a=Mathf.Clamp(Math.Min(anchor,focus),0,length),b=Mathf.Clamp(Math.Max(anchor,focus),0,length);
        Set(Target.text.Substring(0,a)+text+Target.text.Substring(b),a+text.Length);
    }
    void Set(string value,int caret){Target.text=value;anchor=focus=caret;Target.selectionStringAnchorPosition=Target.selectionStringFocusPosition=caret;Target.ForceLabelUpdate();preview.text=value;}
    public void Backspace(){if(Target==null)return;if(anchor!=focus){Insert("");return;}int c=Mathf.Clamp(focus,0,Target.text.Length);if(c>0)Set(Target.text.Remove(c-1,1),c-1);}
    public void Tone(char combining)
    {
        if(Target==null)return;int caret=Mathf.Clamp(focus,0,Target.text.Length);string value=Target.text;
        for(int i=caret-1;i>=0&&!char.IsWhiteSpace(value[i]);i--)
        {string normalized=value[i].ToString().Normalize(NormalizationForm.FormD);if("aAeEiIoOuUyY".IndexOf(normalized[0])<0)continue;var sb=new StringBuilder();foreach(char c in normalized)if(c!='\u0300'&&c!='\u0301'&&c!='\u0303'&&c!='\u0309'&&c!='\u0323')sb.Append(c);if(combining!='\0')sb.Append(combining);string result=sb.ToString().Normalize(NormalizationForm.FormC);Set(value.Substring(0,i)+result+value.Substring(i+1),caret+result.Length-1);break;}
    }
    void Build()
    {
        root=new GameObject("Bàn phím tiếng Việt",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster),typeof(Image));root.transform.SetParent(presentation.transform,false);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=presentation.Origin.Camera;canvas.sortingOrder=40;
        var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(1240,540);rect.localScale=Vector3.one*.00095f;root.GetComponent<Image>().color=new Color(.035f,.065f,.10f,1);
        var header=Key("Nội dung",0,482,1160,44,()=>{});preview=header.GetComponentInChildren<TMP_Text>();header.interactable=false;
        string[] lines={"1234567890-./","qwertyuiop","asdfghjkl","zxcvbnm","ăâđêôơư"};
        for(int row=0;row<lines.Length;row++)for(int i=0;i<lines[row].Length;i++){char c=lines[row][i];var b=Key(c.ToString(),i*82+(12-lines[row].Length)*41,418-row*58,76,52,()=>Insert((shift?char.ToUpperInvariant(c):c).ToString()));letters.Add((b,c));}
        Key("Hoa / thường",0,18,180,54,()=>{shift=!shift;foreach(var item in letters)item.button.GetComponentInChildren<TMP_Text>().text=(shift?char.ToUpperInvariant(item.key):item.key).ToString();});
        Key("Khoảng trắng",188,18,270,54,()=>Insert(" "));Key("Xóa lùi",466,18,160,54,Backspace);Key("Xóa hết",634,18,150,54,()=>{if(Target!=null)Set("",0);});Key("Xong",792,18,170,54,Close);
        string[] tones={"Sắc","Huyền","Hỏi","Ngã","Nặng","Bỏ dấu"};char[] marks={'\u0301','\u0300','\u0309','\u0303','\u0323','\0'};for(int i=0;i<tones.Length;i++){char mark=marks[i];Key(tones[i],i*160,120,152,50,()=>Tone(mark));}
    }
    Button Key(string text,float x,float y,float w,float h,UnityEngine.Events.UnityAction click)
    {
        var button=Instantiate(dashboard.itemTemplate,root.transform);button.gameObject.SetActive(true);button.name="Phím "+text;button.onClick=new Button.ButtonClickedEvent();button.onClick.AddListener(click);
        var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,0);rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x+20,y);rect.sizeDelta=new Vector2(w,h);
        var label=button.GetComponentInChildren<TMP_Text>();label.text=text;label.fontSize=25;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
        button.GetComponent<Image>().raycastTarget=true;return button;
    }
}
