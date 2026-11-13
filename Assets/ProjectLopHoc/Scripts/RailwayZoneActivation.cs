using UnityEngine;

/// <summary>Activates authored environment chunks over several frames after additive scene integration.</summary>
public sealed class RailwayZoneActivation : MonoBehaviour
{
    public GameObject[] chunks = System.Array.Empty<GameObject>();
    [Min(1)] public int objectsPerFrame = 4;
    [Min(0.1f)] public float millisecondsPerFrame = 0.75f;
    private int next;
    public bool IsReady => next >= chunks.Length;
    private void Update()
    {
        double start = Time.realtimeSinceStartupAsDouble;
        int activated = 0;
        while (next < chunks.Length && activated < objectsPerFrame)
        {
            var chunk = chunks[next++];
            if (chunk != null) chunk.SetActive(true);
            activated++;
            if ((Time.realtimeSinceStartupAsDouble - start) * 1000 >= millisecondsPerFrame) break;
        }
        if (IsReady) enabled = false;
    }
}
