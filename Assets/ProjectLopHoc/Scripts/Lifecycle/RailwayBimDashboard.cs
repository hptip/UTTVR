using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SFB;

/// <summary>Adapter for the copied ChiTietSaBan dashboard. All selection routes use the same controller.</summary>
public sealed class RailwayBimDashboard:MonoBehaviour
{
    public RailwayBimController controller;
    public RectTransform listContent,fieldsContent;public GameObject assetPanel;
    public Button itemTemplate;public TMP_InputField inputTemplate;public ElementInfoRow rowTemplate;
    public TMP_InputField search;public TMP_Text breadcrumb,title,status,listTitle;
    public Button detailButton,backButton,focusButton,resetButton,hideButton,measureButton,statsButton,exportButton,importButton,applyButton,saveButton,addInspectionButton,addMaintenanceButton;
    public Button[] tabs;public string NextFilePath;
    public Button[] stageButtons=Array.Empty<Button>();public RectTransform lifecycleNavigation;public Button stageProfileButton,stageRecordsButton,stageHistoryButton;
    public GameObject leftNav,leftBody,assetBody;public Button collapseNavButton,collapseAssetButton,toggleUiButton,recenterUiButton;
    public RectTransform commandPanel;
    public Button historyPreviousButton,historyNextButton;int historyOffset;const int HistoryPageSize=50;
    public string ActiveTab{get;private set;}="General";
    public string ActiveStage{get;private set;}="Planning";public string StageView{get;private set;}="Profile";
    public string EditingContext=>ActiveTab+"|"+ActiveStage+"|"+StageView+"|"+DisplayedID;
    public string DisplayedID{get;private set;}
    public RailwayBimWorkbook.Preview PendingImport{get;private set;}
    public Dictionary<string,TMP_InputField> Inputs{get;}=new Dictionary<string,TMP_InputField>();
    readonly List<Button> items=new List<Button>();readonly Dictionary<string,Button> byID=new Dictionary<string,Button>();
    sealed class Row{public ElementInfoRow view;public TMP_InputField input;}
    readonly List<Row> rows=new List<Row>();int usedRows;
    string listedContext;
    public IReadOnlyDictionary<string,Button> NavigationButtons=>byID;
    void Start()
    {
        controller.Changed+=Refresh;controller.Status+=SetStatus;
        detailButton.onClick.AddListener(()=>Run(controller.EnterDetails));backButton.onClick.AddListener(()=>Run(controller.Back));resetButton.onClick.AddListener(()=>Run(()=>{controller.SetOverview();controller.cameraController.ResetView();Refresh();}));
        hideButton.onClick.AddListener(()=>Run(controller.ToggleIsolation));
        saveButton.onClick.AddListener(()=>Run(Save));addInspectionButton.onClick.AddListener(()=>Run(AddInspection));addMaintenanceButton.onClick.AddListener(()=>Run(AddMaintenance));
        exportButton.onClick.AddListener(()=>Run(()=>{string path=FilePath(false);if(!string.IsNullOrEmpty(path))Export(path);}));importButton.onClick.AddListener(()=>Run(()=>{string path=FilePath(true);if(!string.IsNullOrEmpty(path))Preview(path);}));applyButton.onClick.AddListener(()=>Run(Apply));
        string[] names={"General","Technical","Lifecycle"};for(int i=0;i<Math.Min(tabs.Length,3);i++){string tab=names[i];tabs[i].onClick.AddListener(()=>ShowTab(tab));}
        for(int i=0;i<stageButtons.Length;i++){string stage=RailwayBimStages.All[i].key;stageButtons[i].onClick.AddListener(()=>ShowStage(stage));}
        if(stageProfileButton!=null)stageProfileButton.onClick.AddListener(()=>ShowStageView("Profile"));if(stageRecordsButton!=null)stageRecordsButton.onClick.AddListener(()=>ShowStageView("Records"));if(stageHistoryButton!=null)stageHistoryButton.onClick.AddListener(()=>ShowStageView("History"));
        if(historyPreviousButton!=null)historyPreviousButton.onClick.AddListener(()=>{historyOffset=Mathf.Max(0,historyOffset-HistoryPageSize);RefreshInfo();});if(historyNextButton!=null)historyNextButton.onClick.AddListener(()=>{historyOffset+=HistoryPageSize;RefreshInfo();});
        var presentation=GetComponent<RailwayBimVrPresentation>();if(toggleUiButton!=null)toggleUiButton.onClick.AddListener(()=>presentation.SetPanelVisible(false));if(recenterUiButton!=null)recenterUiButton.onClick.AddListener(presentation.PlacePanel);
        if(collapseNavButton!=null)collapseNavButton.onClick.AddListener(()=>presentation.ToggleSidebar(false));if(collapseAssetButton!=null)collapseAssetButton.onClick.AddListener(()=>presentation.ToggleSidebar(true));
        search.onValueChanged.AddListener(_=>RefreshList());Refresh();
    }
    string FilePath(bool open)
    {
        if(!string.IsNullOrEmpty(NextFilePath)){string p=NextFilePath;NextFilePath=null;return p;}
        if(Application.platform==RuntimePlatform.Android)
        {
            string directory=System.IO.Path.Combine(Application.persistentDataPath,"TaiLieu");System.IO.Directory.CreateDirectory(directory);
            string file=System.IO.Path.Combine(directory,open?"NhapTaiSan.xlsx":"TaiSan_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".xlsx");
            if(open&&!System.IO.File.Exists(file))throw new System.IO.FileNotFoundException("Hãy chép tệp NhapTaiSan.xlsx vào: "+directory);
            return file;
        }
        var ext=new[]{new ExtensionFilter("Bảng tính Excel","xlsx")};
        if(open)return StandaloneFileBrowser.OpenFilePanel("Xem trước dữ liệu nhập", "",ext,false).FirstOrDefault();
        return StandaloneFileBrowser.SaveFilePanel("Xuất hồ sơ tài sản","","TaiSanDuongSat.xlsx",ext);
    }
    void Run(Action action){try{action();}catch(Exception e){SetStatus("Lỗi: "+e.Message);}}
    public void SetStatus(string text){if(status!=null)status.text=RailwayBimVietnamese.Text(text);}
    public void Refresh()
    {
        if(!controller.Ready){SetStatus(controller.LastStatus??"Đang chuẩn bị cơ sở dữ liệu…");return;}
        if(DisplayedID!=controller.SelectedID)historyOffset=0;DisplayedID=controller.SelectedID;breadcrumb.text="Khu vực 021"+(controller.SegmentID!=null?" > "+controller.SegmentID:"")+(controller.BlockID!=null?" > "+controller.BlockID:"")+(DisplayedID!=null?" > "+DisplayedID:"");
        backButton.interactable=controller.Level>0;detailButton.interactable=DisplayedID!=null&&controller.Level<2;hideButton.interactable=saveButton.interactable=DisplayedID!=null;
        hideButton.GetComponentInChildren<TMP_Text>().text=controller.IsIsolated?"Hiện lại tài sản khác":"Cô lập tài sản";
        RefreshList();RefreshInfo();
    }
    void RefreshList()
    {
        if(!controller.Ready)return;byID.Clear();string text=search.text.Trim();
        var assets=text.Length>0?controller.Store.Assets.Values.Where(a=>a.AssetID.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0||a.DisplayName.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0||RailwayBimVietnamese.Name(a).IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0).OrderBy(a=>a.AssetID).Take(150).ToList():controller.CurrentItems();
        listTitle.text=text.Length>0?"KẾT QUẢ TÌM KIẾM · tối đa 150":controller.Level==0?"PHÂN ĐOẠN · HAI ĐƯỜNG RAY":controller.Level==1?"DANH SÁCH MÔ-ĐUN":"DANH SÁCH CẤU KIỆN";
        for(int i=0;i<Mathf.Max(items.Count,assets.Count);i++)
        {
            if(i>=items.Count){var b=Instantiate(itemTemplate,listContent);b.gameObject.name="AssetItem_"+i;b.gameObject.SetActive(true);var layout=b.GetComponent<LayoutElement>()??b.gameObject.AddComponent<LayoutElement>();layout.preferredHeight=74;items.Add(b);}
            var button=items[i];button.gameObject.SetActive(i<assets.Count);button.onClick.RemoveAllListeners();if(i>=assets.Count)continue;
            var asset=assets[i];var label=button.GetComponentInChildren<TMP_Text>();label.text="<b>"+RailwayBimVietnamese.Name(asset)+"</b>\n<size=80%>"+asset.AssetID+"</size>\n<color=#8DE0CC>"+RailwayBimVietnamese.Text(controller.Store.Condition(asset.AssetID))+"</color>";label.fontSize=22;label.enableWordWrapping=true;label.alignment=TextAlignmentOptions.MidlineLeft;button.GetComponent<LayoutElement>().preferredHeight=102;
            button.GetComponent<Image>().color=Color.white;button.GetComponent<Image>().raycastTarget=true;var colors=button.colors;colors.normalColor=asset.AssetID==controller.SelectedID?new Color(.07f,.37f,.60f,1):new Color(.08f,.13f,.20f,.95f);colors.selectedColor=colors.normalColor;colors.highlightedColor=new Color(.12f,.31f,.46f,1);colors.pressedColor=new Color(.05f,.24f,.38f,1);button.colors=colors;
            string id=asset.AssetID;bool global=text.Length>0;button.onClick.AddListener(()=>Run(()=>{if(global)controller.Locate(id);else controller.Select(id);}));byID[id]=button;
        }
        Canvas.ForceUpdateCanvases();string context=controller.Level+"|"+controller.SegmentID+"|"+controller.BlockID+"|"+text;var listScroll=listContent.GetComponentInParent<ScrollRect>();
        if(controller.SelectedID!=null&&byID.TryGetValue(controller.SelectedID,out var selected))
        {var rect=(RectTransform)selected.transform;var scroll=listContent.GetComponentInParent<ScrollRect>();if(scroll!=null&&listContent.rect.height>scroll.viewport.rect.height){float t=Mathf.Clamp01((-rect.anchoredPosition.y-scroll.viewport.rect.height*.5f)/(listContent.rect.height-scroll.viewport.rect.height));scroll.verticalNormalizedPosition=1-t;}}
        else if(context!=listedContext&&listScroll!=null)listScroll.verticalNormalizedPosition=1;listedContext=context;
    }
    public void ShowTab(string tab){if(tab=="Inspection"){ActiveTab="Lifecycle";ActiveStage="Operation";StageView="Records";}else if(tab=="Maintenance"){ActiveTab="Lifecycle";ActiveStage="Maintenance";StageView="Records";}else if(tab=="History"){ActiveTab="Lifecycle";StageView="History";}else{ActiveTab=tab;StageView="Profile";}RefreshInfo();}
    public void ShowStage(string stage){RailwayBimStages.Get(stage);ActiveTab="Lifecycle";ActiveStage=stage;StageView="Profile";historyOffset=0;RefreshInfo();}
    public void ShowStageView(string view){StageView=view;historyOffset=0;RefreshInfo();}
    void Update(){for(int i=0;i<stageButtons.Length;i++)if(stageButtons[i]!=null&&stageButtons[i].gameObject.activeInHierarchy)stageButtons[i].transform.localScale=Vector3.Lerp(stageButtons[i].transform.localScale,Vector3.one*(RailwayBimStages.All[i].key==ActiveStage?1.04f:1),1-Mathf.Exp(-Time.unscaledDeltaTime*18));}
    void BeginRows(){usedRows=0;Inputs.Clear();foreach(var r in rows)r.view.gameObject.SetActive(false);}
    void Field(string key,string value,bool editable=false,string unit=null,string displayLabel=null)
    {
        if(usedRows==rows.Count)
        {
            var view=Instantiate(rowTemplate,fieldsContent);view.gameObject.SetActive(true);var element=view.GetComponent<LayoutElement>()??view.gameObject.AddComponent<LayoutElement>();element.preferredHeight=88;var rect=(RectTransform)view.transform;rect.localScale=Vector3.one;var originalLayout=view.GetComponent<HorizontalLayoutGroup>();if(originalLayout!=null)originalLayout.enabled=false;var background=view.GetComponent<Image>();if(background!=null){background.color=new Color(.07f,.12f,.19f,.95f);background.raycastTarget=false;}
            var input=Instantiate(inputTemplate,view.transform);input.gameObject.SetActive(true);var ir=(RectTransform)input.transform;ir.anchorMin=new Vector2(.43f,.06f);ir.anchorMax=new Vector2(.99f,.94f);ir.offsetMin=ir.offsetMax=Vector2.zero;input.textComponent.fontSize=23;input.textComponent.color=new Color(.94f,.97f,1);input.GetComponent<Image>().color=new Color(.11f,.18f,.25f,1);input.lineType=TMP_InputField.LineType.MultiLineNewline;
            var labels=view.GetComponentsInChildren<TMP_Text>(true).Where(t=>!t.transform.IsChildOf(input.transform)&&t.name.Contains("Txt")).ToArray();foreach(var label in labels){bool isName=label.name.Contains("Name");var lr=(RectTransform)label.transform;lr.anchorMin=new Vector2(isName?.02f:.43f,0);lr.anchorMax=new Vector2(isName?.41f:.98f,1);lr.offsetMin=lr.offsetMax=Vector2.zero;label.fontSize=23;label.color=new Color(.90f,.94f,1);label.enableWordWrapping=true;label.alignment=TextAlignmentOptions.MidlineLeft;}
            rows.Add(new Row{view=view,input=input});
        }
        var row=rows[usedRows++];row.view.gameObject.SetActive(true);row.view.Set((displayLabel??RailwayBimVietnamese.Text(key))+(string.IsNullOrEmpty(unit)?"":" ["+unit+"]"),RailwayBimVietnamese.Text(value));row.input.gameObject.SetActive(editable);row.input.SetTextWithoutNotify(value??"");
        foreach(var label in row.view.GetComponentsInChildren<TMP_Text>(true).Where(t=>!t.transform.IsChildOf(row.input.transform)))if(label.name.Contains("Value"))label.gameObject.SetActive(!editable);
        if(editable)Inputs[key]=row.input;
        float width=Mathf.Max(140,fieldsContent.rect.width*.53f-18);float height=row.input.textComponent.GetPreferredValues(value??"",width,float.PositiveInfinity).y+20;row.view.GetComponent<LayoutElement>().preferredHeight=editable?Mathf.Clamp(height,88,210):Mathf.Max(88,height);
    }
    void RefreshInfo()
    {
        string[] tabNames={"General","Technical","Lifecycle"};for(int i=0;i<Math.Min(tabs.Length,3);i++)Tint(tabs[i],ActiveTab==tabNames[i]);
        for(int i=0;i<stageButtons.Length;i++)Tint(stageButtons[i],ActiveStage==RailwayBimStages.All[i].key);
        BeginRows();bool selected=controller.SelectedID!=null,life=ActiveTab=="Lifecycle";
        saveButton.gameObject.SetActive(selected&&(!life||StageView=="Profile"));addInspectionButton.gameObject.SetActive(selected&&life&&ActiveStage=="Operation"&&StageView=="Records");addMaintenanceButton.gameObject.SetActive(selected&&life&&ActiveStage=="Maintenance"&&StageView=="Records");
        if(historyPreviousButton!=null){bool history=selected&&life&&StageView=="History";historyPreviousButton.transform.parent.gameObject.SetActive(history);historyPreviousButton.interactable=historyOffset>0;if(history)historyNextButton.interactable=historyOffset+HistoryPageSize<controller.Store.StageHistoryCount(controller.SelectedID,ActiveStage);}
        if(lifecycleNavigation!=null)lifecycleNavigation.gameObject.SetActive(life);if(stageProfileButton!=null){stageProfileButton.transform.parent.gameObject.SetActive(life);Tint(stageProfileButton,StageView=="Profile");Tint(stageHistoryButton,StageView=="History");stageRecordsButton.gameObject.SetActive(ActiveStage=="Operation"||ActiveStage=="Maintenance");stageRecordsButton.GetComponentInChildren<TMP_Text>().text=ActiveStage=="Operation"?"Kiểm tra":"Bảo trì";Tint(stageRecordsButton,StageView=="Records");}
        var viewport=(RectTransform)fieldsContent.GetComponentInParent<ScrollRect>(true).transform;viewport.anchorMax=new Vector2(.975f,life?.56f:.815f);
        if(!controller.Ready)return;
        if(!selected){title.text="HỒ SƠ TÀI SẢN";Field("Cấp hiện tại",controller.Level==0?"Segment":controller.Level==1?"Block":"Component");Field("Hướng dẫn","Chọn đối tượng trên mô hình hoặc trong danh sách, sau đó chọn Xem chi tiết.");Field("Dữ liệu","Hồ sơ BIM tuyến Yên Tử – Hạ Long · Khu đoạn 021");return;}
        assetPanel.SetActive(true);var id=controller.SelectedID;var asset=controller.Store.Assets[id];title.text=RailwayBimVietnamese.Name(asset)+"\n<size=75%>"+RailwayBimVietnamese.Text(ActiveTab)+"</size>";
        switch(ActiveTab)
        {
            case "General":Field("AssetID",id);Field("DisplayName",asset.DisplayName,true);foreach(var p in typeof(BimAsset).GetProperties().Where(p=>p.Name!="AssetID"&&p.Name!="DisplayName"))Field(p.Name,Convert.ToString(p.GetValue(asset),CultureInfo.InvariantCulture));break;
            case "Technical":foreach(var p in controller.Store.Properties(id))Field(p.Name,p.Value,true,p.Unit);break;
            case "Lifecycle":RenderStage(id);break;
            case "Inspection":
                foreach(var i in controller.Store.Inspections(id)){Field(i.InspectionDate+" — "+i.ConditionGrade,i.Finding+"\n"+i.Inspector+" · "+i.Notes);}
                Field("InspectionDate",DateTime.Today.ToString("yyyy-MM-dd"),true);Field("ConditionGrade",controller.Store.Lifecycle(id).ConditionGrade,true);Field("Finding","",true);Field("Inspector","",true);Field("Notes","",true);break;
            case "Maintenance":
                foreach(var m in controller.Store.Maintenance(id))Field(m.MaintenanceDate+" — "+m.MaintenanceType,m.Description+" · "+m.Cost.ToString("N0")+" VND · "+m.Result);
                Field("MaintenanceDate",DateTime.Today.ToString("yyyy-MM-dd"),true);Field("MaintenanceType","",true);Field("Description","",true);Field("Cost","0",true);Field("Result","",true);break;
            case "History":foreach(var e in controller.Store.History(id))Field(e.EventDate+" — "+RailwayBimVietnamese.Text(e.EventType),e.Description+"\n"+e.PreviousValue+" → "+e.NewValue);break;
        }
    }
    static void Tint(Button button,bool active){if(button==null)return;button.GetComponent<Image>().color=Color.white;var colors=button.colors;colors.normalColor=active?new Color(.06f,.38f,.40f,1):new Color(.07f,.13f,.20f,.98f);colors.selectedColor=colors.normalColor;colors.highlightedColor=new Color(.13f,.43f,.46f,1);button.colors=colors;}
    void RenderStage(string id)
    {
        var stage=RailwayBimStages.Get(ActiveStage);title.text=RailwayBimVietnamese.Name(controller.Store.Assets[id])+"\n<size=75%>Vòng đời · "+stage.name+"</size>";
        if(StageView=="History"){Field("Nhật ký","Trang "+(historyOffset/HistoryPageSize+1)+" · Gồm giai đoạn đang chọn và nhật ký chung chưa phân loại. Dùng nút nhật ký cũ hơn để xem tiếp.");foreach(var e in controller.Store.StageHistory(id,ActiveStage,historyOffset,HistoryPageSize))Field(e.EventDate+" · "+RailwayBimVietnamese.Text(e.EventType),RailwayBimVietnamese.Text(e.Description)+"\n"+e.PreviousValue+" → "+e.NewValue);return;}
        if(StageView=="Records")
        {
            if(ActiveStage=="Operation")
            {foreach(var record in controller.Store.Inspections(id))Field(record.InspectionDate+" · "+record.ConditionGrade,record.Finding+"\n"+record.Inspector+" · "+record.Notes);Field("InspectionDate",DateTime.Today.ToString("yyyy-MM-dd"),true);Field("ConditionGrade",controller.Store.Lifecycle(id).ConditionGrade,true);Field("Finding","",true);Field("Inspector","",true);Field("Notes","",true);}
            else if(ActiveStage=="Maintenance")
            {foreach(var record in controller.Store.Maintenance(id))Field(record.MaintenanceDate+" · "+record.MaintenanceType,record.Description+"\n"+record.Cost.ToString("N0")+" VND · "+record.Result);Field("MaintenanceDate",DateTime.Today.ToString("yyyy-MM-dd"),true);Field("MaintenanceType","",true);Field("Description","",true);Field("Cost","0",true);Field("Result","",true);}
            return;
        }
        var state=controller.Store.Stage(id,ActiveStage);Field("Status",state.Status,true,displayLabel:"Trạng thái hồ sơ");var values=controller.Store.StageFields(id,ActiveStage);
        foreach(var field in stage.fields)Field(field.key,values[field.key],!field.readOnly,field.unit,field.label);
    }
    public void Save()
    {
        string id=controller.SelectedID;controller.Store.Require(id);
        if(ActiveTab=="General")controller.Store.Rename(id,Inputs["DisplayName"].text);
        else if(ActiveTab=="Technical"){var ps=controller.Store.Properties(id);foreach(var p in ps)p.Value=Inputs[p.Name].text;controller.Store.SaveProperties(id,ps);}
        else if(ActiveTab=="Lifecycle"&&StageView=="Profile")controller.Store.SaveStage(id,ActiveStage,RailwayBimStages.Get(ActiveStage).fields.Where(f=>!f.readOnly).ToDictionary(f=>f.key,f=>Inputs[f.key].text),Inputs["Status"].text);
        Refresh();SetStatus("Đã lưu hồ sơ tài sản "+id);
    }
    public void AddInspection(){controller.Store.AddInspection(new BimInspection{AssetID=controller.SelectedID,InspectionDate=Inputs["InspectionDate"].text,ConditionGrade=Inputs["ConditionGrade"].text,Finding=Inputs["Finding"].text,Inspector=Inputs["Inspector"].text,Notes=Inputs["Notes"].text});Refresh();SetStatus("Đã lưu kiểm tra cho đúng mã tài sản");}
    public void AddMaintenance(){controller.Store.AddMaintenance(new BimMaintenance{AssetID=controller.SelectedID,MaintenanceDate=Inputs["MaintenanceDate"].text,MaintenanceType=Inputs["MaintenanceType"].text,Description=Inputs["Description"].text,Cost=double.Parse(Inputs["Cost"].text,CultureInfo.InvariantCulture),Result=Inputs["Result"].text});Refresh();SetStatus("Đã lưu bảo trì cho đúng mã tài sản");}
    public void Export(string path){RailwayBimWorkbook.Write(path,RailwayBimWorkbook.ExportData(controller.Store));SetStatus("Đã xuất bảng tính: "+path);}
    public void Preview(string path){PendingImport=RailwayBimWorkbook.PreviewFile(controller.Store,path);applyButton.interactable=PendingImport.CanApply;SetStatus(PendingImport.CanApply?"Dữ liệu hợp lệ. Chọn Xác nhận nhập để lưu.":"Dữ liệu nhập có lỗi: "+string.Join("; ",PendingImport.errors.Take(3)));}
    public void Apply(){if(PendingImport==null||!PendingImport.CanApply)throw new InvalidOperationException("Cần xem trước và kiểm tra dữ liệu hợp lệ");RailwayBimWorkbook.Apply(controller.Store,PendingImport);PendingImport=null;applyButton.interactable=false;Refresh();SetStatus("Đã nhập và lưu dữ liệu an toàn");}
    void OnDestroy(){if(controller!=null){controller.Changed-=Refresh;controller.Status-=SetStatus;}}
}

