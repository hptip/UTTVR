#if UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// Never attached to the saved scene; excluded from Player builds.
public sealed class ShowroomMergeTestBrain:MonoBehaviour
{
    public static int calls;
    public Task<string> AskAsync(string text,CancellationToken token)
    {calls++;return Task.FromResult("LOCAL_PIPELINE_TEST: "+text);}
}
#endif
