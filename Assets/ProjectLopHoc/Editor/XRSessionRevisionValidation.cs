using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class XRSessionRevisionRun
{
    static double lastStatus,deadline;
    static XRSessionRevisionRun()=>EditorApplication.update+=Tick;
    static void Tick()
    {
        if(!File.Exists("Temp/XRSessionRevision.request")){deadline=0;return;}
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(EditorApplication.timeSinceStartup>lastStatus){lastStatus=EditorApplication.timeSinceStartup+1;File.WriteAllText("Temp/XRSessionRevision_Progress.txt",$"playing={EditorApplication.isPlaying}; paused={EditorApplication.isPaused}; active={SceneManager.GetActiveScene().name}; probe={Object.FindFirstObjectByType<XRSessionRevisionValidation>()!=null}");}
        if(EditorApplication.isPaused)EditorApplication.isPaused=false;
        if(deadline==0)deadline=EditorApplication.timeSinceStartup+120;
        if(EditorApplication.timeSinceStartup>deadline){File.WriteAllText("Temp/XRSessionRevision_Test.txt","FAIL: Editor wall-clock timeout");File.Delete("Temp/XRSessionRevision.request");EditorApplication.isPlaying=false;deadline=0;return;}
        if(!EditorApplication.isPlayingOrWillChangePlaymode){var game=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");if(game!=null)EditorWindow.GetWindow(game).Focus();EditorApplication.isPlaying=true;return;}
        if(EditorApplication.isPlaying&&Object.FindFirstObjectByType<XRSessionRevisionValidation>()==null)
        {var go=new GameObject("Editor XR session validation");Object.DontDestroyOnLoad(go);go.AddComponent<XRSessionRevisionValidation>();}
    }
}
