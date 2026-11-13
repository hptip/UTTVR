using UnityEngine;

/// <summary>One shared integration lane for rail resources and environment scenes.</summary>
public static class RailwayStreamingBudget
{
    static Object owner;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()=>owner=null;
    public static bool TryAcquire(Object requester)
    {
        if(owner!=null && owner!=requester)return false;
        owner=requester;return true;
    }
    public static void Release(Object requester){if(owner==requester)owner=null;}
}
