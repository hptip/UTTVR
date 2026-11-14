using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ProjectLopHoc.Features.Lecture;
using ProjectLopHoc.UI;
using Object=UnityEngine.Object;

public static partial class StationClassroomFinish
{
    public const string Dir="Temp/StationClassroomFinish";
    public const string Classroom="Assets/ProjectLopHoc/Scenes/Classroom.unity";
    const string Folder="Assets/ProjectLopHoc/Generated/StationAppearance";
    static readonly string[] ZonePaths={"Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_010.unity","Assets/ProjectLopHoc/Generated/RuntimeZones/Zone_026.unity"};
    static readonly string[] Roles={"Walls","Roof","Columns","Frames","Base","Lights","Glass"};
    static readonly string[] Colors={"#E6DFC9","#536B7C","#F3F2EE","#48545D","#A9A69C","#F3F2EE","#48545D"};
    static string HashFile(string p){using(var sha=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","");}
    static void Guard()
    {
        if(!Application.dataPath.Replace('\\','/').Equals("F:/GITHUB/UTTVR/Assets",StringComparison.OrdinalIgnoreCase))throw new Exception("Wrong project");
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Exit Play before authoring");
        Directory.CreateDirectory(Dir);
    }
    public static void Protect()
    {
        Guard();var paths=Directory.GetFiles("Assets/ProjectLopHoc/Scenes","*.unity").Where(p=>p.Replace('\\','/')!=Classroom)
            .Concat(Directory.GetFiles("Assets/ProjectLopHoc/Generated/RuntimeZones","*.unity").Where(p=>!ZonePaths.Contains(p.Replace('\\','/'))))
            .Concat(new[]{"Assets/ProjectLopHoc/Prefabs/Train Ride.prefab","Assets/ProjectLopHoc/Generated/QuestRailway/Station_Start_TrainRide_Combined.asset","Assets/ProjectLopHoc/Generated/QuestRailway/Station_End_TrainRide_Combined.asset"})
            .Concat(Directory.GetFiles("Assets/ProjectLopHoc/Art/3DModels/OK/train-ride/mat","*.mat"));
        File.WriteAllLines(Dir+"/Protected.tsv",paths.Distinct().Select(p=>p+"\t"+HashFile(p)));
    }
    public static string WireClassroom()
    {
        Guard();var s=SceneManager.GetSceneByPath(Classroom);if(!s.isLoaded)s=EditorSceneManager.OpenScene(Classroom,OpenSceneMode.Additive);
        if(s.isDirty)throw new Exception("Classroom has unsaved changes; preserving them");
        var nodes=s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
        var board=nodes.Select(t=>t.GetComponent<LectureBoardController>()).Single(b=>b!=null);
        var manager=nodes.Select(t=>t.GetComponent<LectureActionManager>()).Single(b=>b!=null);
        var student=nodes.Select(t=>t.GetComponent<StudentSlideReviewUI>()).Single(b=>b!=null);
        var teacher=nodes.Select(t=>t.GetComponent<TeacherLecturePanelController>()).Single(b=>b!=null);
        Undo.RecordObjects(new Object[]{board,manager,student,teacher},"Connect common lecture state");
        board.classroomConfig=student.classroomConfig;manager.boardController=board;student.board=board;
        student.previousLessonButton=LessonButton(student.transform,manager,false);
        student.nextLessonButton=LessonButton(student.transform,manager,true);
        teacher.previousLessonButton=LessonButton(teacher.transform,manager,false);
        teacher.nextLessonButton=LessonButton(teacher.transform,manager,true);
        var so=new SerializedObject(teacher);
        SetRef(so,"panel",teacher.gameObject);SetRef(so,"board",board);SetRef(so,"actionManager",manager);
        SetRef(so,"slidePreview",teacher.transform.Find("SlideDisplay").GetComponent<Image>());
        SetRef(so,"lectureName",teacher.transform.Find("SlideName").GetComponent<TMP_Text>());
        SetRef(so,"slideCounter",teacher.transform.Find("SlideCounter").GetComponent<TMP_Text>());
        SetRef(so,"lectureDropdown",teacher.transform.Find("LectureDropdown").GetComponent<TMP_Dropdown>());
        so.ApplyModifiedProperties();
        foreach(var panel in new[]{student.transform,teacher.transform})
        {
            var title=panel.Find("SlideName").GetComponent<TMP_Text>();Undo.RecordObject(title.rectTransform,"Fit lecture title");
            title.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,880);title.enableAutoSizing=true;title.fontSizeMin=18;title.fontSizeMax=28;
            EditorUtility.SetDirty(title);EditorUtility.SetDirty(title.rectTransform);
        }
        foreach(var o in new Object[]{board,manager,student,teacher}){EditorUtility.SetDirty(o);if(PrefabUtility.IsPartOfPrefabInstance(o))PrefabUtility.RecordPrefabInstancePropertyModifications(o);}
        Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
        string result="Saved Classroom; common controller/config; four lesson buttons; existing slide buttons and Canvas retained. Teacher panel keeps its original trailing space in name.";
        File.WriteAllText(Dir+"/ClassroomWiring.txt",result);return result;
    }
    static void SetRef(SerializedObject so,string field,Object value){var p=so.FindProperty(field);if(p==null)throw new Exception("Missing field "+field);p.objectReferenceValue=value;}
    static Button LessonButton(Transform parent,LectureActionManager manager,bool next)
    {
        string name=next?"NextLessonButton":"PreviousLessonButton";var child=parent.Find(name);
        if(child!=null)throw new Exception("Lesson button already exists: "+parent.name+"/"+name);
        var template=parent.Find("SlideNext").GetComponent<Button>();var go=Object.Instantiate(template.gameObject,parent,false);go.name=name;Undo.RegisterCreatedObjectUndo(go,"Add lesson navigation button");
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(next?585:225,-180);rect.sizeDelta=new Vector2(320,64);rect.localScale=Vector3.one;
        var text=go.GetComponentInChildren<TMP_Text>();text.text=next?"Bài giảng sau":"Bài giảng trước";text.fontSize=24;text.enableAutoSizing=true;text.fontSizeMin=18;text.fontSizeMax=24;text.raycastTarget=false;
        var button=go.GetComponent<Button>();button.onClick=new Button.ButtonClickedEvent();button.interactable=next;
        if(next)UnityEventTools.AddPersistentListener(button.onClick,manager.NextLecture);else UnityEventTools.AddPersistentListener(button.onClick,manager.PreviousLecture);
        return button;
    }

    struct Face { public Vector3 a,b,c;public int role; }
    static Vector3Int Cell(Vector3 p)=>new Vector3Int(Mathf.FloorToInt(p.x/.05f),Mathf.FloorToInt(p.y/.05f),Mathf.FloorToInt(p.z/.05f));
    static bool Near(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<.0001f;
    static bool Match(Face f,Vector3 a,Vector3 b,Vector3 c)=>
        Near(a,f.a)&&((Near(b,f.b)&&Near(c,f.c))||(Near(b,f.c)&&Near(c,f.b)))||
        Near(a,f.b)&&((Near(b,f.a)&&Near(c,f.c))||(Near(b,f.c)&&Near(c,f.a)))||
        Near(a,f.c)&&((Near(b,f.a)&&Near(c,f.b))||(Near(b,f.b)&&Near(c,f.a)));
    static int Role(string name)
    {
        name=name.ToLowerInvariant();
        if(name.Contains("roof")||name.Contains("celing")||name.Contains("ceiling"))return 1;
        if(name.Contains("pillar")||name.Contains("post")||name.Contains("beam")||name.Contains("guirder")||name.Contains("brace")||name.Contains("shaft"))return 2;
        if(name.Contains("door")||name.Contains("window")||name.Contains("chair")||name.Contains("trash"))return 3;
        if(name.Contains("platform")||name.Contains("plane")||name.Contains("track")||name.Contains("rock"))return 4;
        return 0;
    }
    static Dictionary<Vector3Int,List<Face>> SourceFaces()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ProjectLopHoc/Prefabs/Train Ride.prefab");var dict=new Dictionary<Vector3Int,List<Face>>();
        foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            var r=mf.GetComponent<MeshRenderer>();if(r==null||!r.enabled||mf.sharedMesh==null)continue;
            // A prefab asset is not activeInHierarchy in an open scene; inspect its authored activeSelf chain.
            bool active=true;for(var t=mf.transform;t!=null;t=t.parent)if(!t.gameObject.activeSelf){active=false;break;}
            if(!active)continue;
            var matrix=prefab.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;if(matrix.determinant<=0)continue;
            var mesh=mf.sharedMesh;var vertices=mesh.vertices;var mats=r.sharedMaterials;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                if(sub>=mats.Length||mats[sub]==null||!mats[sub].name.Contains("Trim"))continue;
                var tris=mesh.GetTriangles(sub);
                for(int t=0;t<tris.Length;t+=3)
                {
                    var f=new Face{a=matrix.MultiplyPoint3x4(vertices[tris[t]]),b=matrix.MultiplyPoint3x4(vertices[tris[t+1]]),c=matrix.MultiplyPoint3x4(vertices[tris[t+2]]),role=Role(r.name)};
                    var key=Cell((f.a+f.b+f.c)/3);if(!dict.TryGetValue(key,out var list))dict[key]=list=new List<Face>();list.Add(f);
                }
            }
        }
        return dict;
    }
    public static string ColorStations()
    {
        Guard();Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var sourceFaces=SourceFaces();var output=new List<string>();var materials=new Dictionary<int,Material>();
        foreach(string path in ZonePaths)
        {
            var scene=SceneManager.GetSceneByPath(path);if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            if(scene.isDirty)throw new Exception("Zone has unsaved changes: "+path);
            var station=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name.StartsWith("Station_"));
            var renderer=station.GetComponentsInChildren<MeshRenderer>(true).Single();var filter=renderer.GetComponent<MeshFilter>();var original=filter.sharedMesh;
            if(AssetDatabase.GetAssetPath(original).StartsWith(Folder))throw new Exception("Station already has appearance variant; refusing duplicate");
            var vertices=original.vertices;var groups=Enumerable.Range(0,7).Select(_=>new List<int>()).ToArray();int matched=0,missing=0,ambiguous=0,opaque=0;
            for(int sub=0;sub<original.subMeshCount;sub++)
            {
                var indices=original.GetTriangles(sub);var mat=renderer.sharedMaterials[sub];
                for(int i=0;i<indices.Length;i+=3)
                {
                    int role=mat.name.Contains("Light")?5:mat.name.Contains("Seethrough")?6:0;
                    if(role==0)
                    {
                        opaque++;var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];var center=Cell((a+b+c)/3);int found=-1;
                        for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                            if(sourceFaces.TryGetValue(center+new Vector3Int(x,y,z),out var faces))foreach(var f in faces)if(Match(f,a,b,c)){if(found>=0&&found!=f.role)found=-2;else if(found!=-2)found=f.role;}
                        if(found>=0){role=found;matched++;}else if(found==-2)ambiguous++;else missing++;
                    }
                    groups[role].Add(indices[i]);groups[role].Add(indices[i+1]);groups[role].Add(indices[i+2]);
                }
            }
            bool partitionSafe=(missing+ambiguous)<=Math.Max(1,opaque/1000);
            if(!partitionSafe)throw new Exception("Cannot prove safe architectural material partition: "+station.name+" matched="+matched+" missing="+missing+" ambiguous="+ambiguous+"; original untouched");
            var mesh=Object.Instantiate(original);mesh.name=station.name+"_ExistingGeometry_Colored";mesh.subMeshCount=groups.Count(g=>g.Count>0);var assigned=new List<Material>();int slot=0;
            for(int role=0;role<groups.Length;role++)
            {
                if(groups[role].Count==0)continue;
                mesh.SetTriangles(groups[role],slot++,false);
                if(!materials.TryGetValue(role,out var mat))
                {
                    var originalMat=renderer.sharedMaterials[role==5?1:role==6?2:0];mat=Object.Instantiate(originalMat);mat.name="YenTuStation_"+Roles[role];
                    ColorUtility.TryParseHtmlString(Colors[role],out var color);color.a=originalMat.color.a;
                    if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);if(mat.HasProperty("_Color"))mat.SetColor("_Color",color);
                    string materialPath=Folder+"/"+Roles[role]+".mat";if(File.Exists(materialPath))throw new Exception("Existing private material: "+materialPath);
                    AssetDatabase.CreateAsset(mat,materialPath);materials[role]=mat;
                }
                assigned.Add(mat);
            }
            mesh.bounds=original.bounds;
            if(!mesh.vertices.SequenceEqual(original.vertices)||!mesh.normals.SequenceEqual(original.normals)||!mesh.uv.SequenceEqual(original.uv))throw new Exception("Geometry attribute preservation failed");
            string Triangles(Mesh m)=>string.Join(";",Enumerable.Range(0,m.subMeshCount).SelectMany(s=>{var t=m.GetTriangles(s);return Enumerable.Range(0,t.Length/3).Select(i=>t[i*3]+","+t[i*3+1]+","+t[i*3+2]);}).OrderBy(t=>t,StringComparer.Ordinal));
            if(Triangles(mesh)!=Triangles(original))throw new Exception("Triangle topology changed");
            string meshPath=Folder+"/"+station.name+"_ColorSlots.asset";AssetDatabase.CreateAsset(mesh,meshPath);
            Undo.RecordObjects(new Object[]{filter,renderer},"Color existing station architecture");filter.sharedMesh=mesh;renderer.sharedMaterials=assigned.ToArray();EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);
            foreach(var asset in assigned)AssetDatabase.SaveAssetIfDirty(asset);AssetDatabase.SaveAssetIfDirty(mesh);Undo.FlushUndoRecordObjects();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            output.Add(station.name+" | "+path+" | vertices="+mesh.vertexCount+" (identical), normals/UV/triangle multiset identical | matched="+matched+" missing="+missing+" ambiguous="+ambiguous+" | renderer count=1 unchanged | slots="+assigned.Count+" | "+string.Join(",",groups.Select((g,n)=>Roles[n]+"="+g.Count/3)));
        }
        File.WriteAllLines(Dir+"/Stations.txt",output);return string.Join("\n",output);
    }
    public static string VerifyProtected()
    {
        var rows=File.ReadAllLines(Dir+"/Protected.tsv");var changed=rows.Where(r=>{var p=r.Split('\t');return HashFile(p[0])!=p[1];}).ToArray();
        string text="Protected files checked="+rows.Length+"; changed="+changed.Length+"\n"+string.Join("\n",changed);File.WriteAllText(Dir+"/ProtectedResult.txt",text);return text;
    }
}
