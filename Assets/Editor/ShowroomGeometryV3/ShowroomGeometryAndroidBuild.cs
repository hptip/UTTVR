using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

// Build-copy only: runtime mesh swaps must not be captured into a static combined mesh.
// Desktop and all other scenes retain their original static flags.
public sealed class ShowroomGeometryAndroidBuild : IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || report.summary.platform != BuildTarget.Android || scene.path != ShowroomGeometryV3.ScenePath) return;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var controller in root.GetComponentsInChildren<ShowroomGeometryController>(true))
                foreach (var filter in controller.targets)
                    if (filter != null)
                    {
                        var flags = GameObjectUtility.GetStaticEditorFlags(filter.gameObject);
                        if ((flags & StaticEditorFlags.BatchingStatic) != 0)
                            GameObjectUtility.SetStaticEditorFlags(filter.gameObject, flags & ~StaticEditorFlags.BatchingStatic);
                    }
    }
}
