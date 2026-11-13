using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static partial class ShowroomPackageMergeTools
{
    static void CheckNeutralDirtyScene()
    {
        var lines=new System.Collections.Generic.List<string>();
        for(int i=0;i<SceneManager.sceneCount;i++)
        {
            var scene=SceneManager.GetSceneAt(i);if(scene.path==ScenePath||!scene.isDirty)continue;
            string copy=Dir+"/OtherSceneMemoryComparison.unity";
            bool saved=EditorSceneManager.SaveScene(scene,copy,true);
            lines.Add("Scene="+scene.path+"; diagnostic serialization copy="+saved+"; source still dirty="+scene.isDirty);
            if(!saved||!File.Exists(copy))continue;
            bool equal=File.ReadAllBytes(scene.path).SequenceEqual(File.ReadAllBytes(copy));lines.Add("Exact source/copy byte equality="+equal);
            if(equal)
            {
                var clear=typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(Scene)},null);
                if(clear!=null){clear.Invoke(null,new object[]{scene});lines.Add("Cleared only neutral dirty flag after exact equality; original file was not saved or changed.");}
                else lines.Add("No safe clear-dirty API available; source preserved.");
            }
        }
        File.WriteAllLines(Dir+"/OtherSceneDirtyCheck.txt",lines);
        Status();
    }
}
