using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Fixed world-space baked rail chunks, independent of additive environment zones.</summary>
public sealed class RailwayMicroChunkStreamer : MonoBehaviour
{
    public RailwayMicroChunkData data;
    public TrainSplineMover train;
    public float ahead=650, rearViewDistance=300;
    public int poolCapacity=16, operationsPerFrame=2;
    public float millisecondsPerFrame=0.75f;
    sealed class Slot
    {
        public GameObject root;
        public MeshFilter[] filters;
        public MeshRenderer[] renderers;
        public int index=-1, phase;
        public ResourceRequest request;
    }
    Slot[] slots;
    int warmed;
    public int ActiveChunks {get;private set;}
    public bool EssentialReady {get;private set;}
    public int MissingEssentialFrames {get;private set;}
    public int PeakActiveChunks {get;private set;}
    void Awake(){ slots=new Slot[Mathf.Max(12,poolCapacity)]; }
    void Update()
    {
        if(data==null || train==null || train.totalRouteLength<=0) return;
        double started=Time.realtimeSinceStartupAsDouble;
        int operations=0;
        // Warm at most one empty pooled pair per frame, never instantiate rail prefabs.
        if(warmed<slots.Length)
        {
            var s=new Slot{root=new GameObject("PooledRailPair_"+warmed),filters=new MeshFilter[2],renderers=new MeshRenderer[2]};
            s.root.transform.SetParent(transform,false);s.root.SetActive(false);
            for(int side=0;side<2;side++)
            {
                var go=new GameObject(side==0?"Track_Right":"Track_Left");go.transform.SetParent(s.root.transform,false);
                s.filters[side]=go.AddComponent<MeshFilter>();s.renderers[side]=go.AddComponent<MeshRenderer>();
                s.renderers[side].shadowCastingMode=ShadowCastingMode.Off;s.renderers[side].reflectionProbeUsage=ReflectionProbeUsage.Off;
            }
            slots[warmed++]=s;
        }
        float d=train.currentDistanceMeters;
        float min=Mathf.Max(0,d-Mathf.Max(rearViewDistance,train.carSpacing+80));
        float max=Mathf.Min(data.length,d+Mathf.Max(ahead,train.currentSpeedKmh/3.6f*12));
        int loading=0;for(int i=0;i<warmed;i++)if(slots[i].request!=null)loading++;ActiveChunks=0;
        for(int i=0;i<warmed;i++)
        {
            var s=slots[i];if(s.index<0)continue;
            var chunk=data.chunks[s.index];
            if(s.request!=null)
            {
                if(s.request.isDone && operations<operationsPerFrame)
                {
                    var mesh=s.request.asset as Mesh;
                    if(mesh==null){Debug.LogError("[RailwayMicroChunk] Missing mesh: "+(s.phase==0?chunk.right.meshResource:chunk.left.meshResource),this);enabled=false;return;}
                    s.filters[s.phase].sharedMesh=mesh;s.request=null;s.phase++;operations++;
                    RailwayStreamingBudget.Release(this);
                    if(s.phase==2) s.root.SetActive(true);
                }
            }
            if(s.root.activeSelf)ActiveChunks++;
            if(loading==0 && s.request==null && (chunk.end<min-100 || chunk.start>max+100) && operations<operationsPerFrame && RailwayStreamingBudget.TryAcquire(this))
            {
                s.root.SetActive(false);
                // Release one mesh at a time. No UnloadUnusedAssets/GC.Collect at boundaries.
                int side=s.filters[0].sharedMesh!=null?0:1;
                var mesh=s.filters[side].sharedMesh;s.filters[side].sharedMesh=null;
                if(mesh!=null)Resources.UnloadAsset(mesh);
                if(s.filters[0].sharedMesh==null && s.filters[1].sharedMesh==null){s.index=-1;s.phase=0;}
                RailwayStreamingBudget.Release(this);
                operations++;
            }
        }
        PeakActiveChunks=Mathf.Max(PeakActiveChunks,ActiveChunks);
        // Current segment first, then progressively farther ahead. All source meshes
        // are referenced by Resources keys; registry does not hold the whole route in RAM.
        if(loading==0 && operations<operationsPerFrame)
        {
            int best=-1;float score=float.PositiveInfinity;
            for(int k=0;k<data.chunks.Length;k++)
            {
                var c=data.chunks[k];if(c.end<min || c.start>max)continue;
                bool assigned=false;
                for(int i=0;i<warmed;i++)if(slots[i].index==k){assigned=true;break;}
                if(assigned)continue;
                float cost=c.start<=d && c.end>=d?-1000:Mathf.Abs(c.start-d);
                if(cost<score){score=cost;best=k;}
            }
            Slot candidate=null;
            for(int i=0;i<warmed;i++)if(slots[i].index>=0 && slots[i].phase<2 && slots[i].request==null){candidate=slots[i];break;}
            if(candidate==null && best>=0)
                for(int i=0;i<warmed;i++)if(slots[i].index<0){candidate=slots[i];candidate.index=best;break;}
            if(candidate!=null && (Time.realtimeSinceStartupAsDouble-started)*1000<millisecondsPerFrame && RailwayStreamingBudget.TryAcquire(this))
            {
                var track=candidate.phase==0?data.chunks[candidate.index].right:data.chunks[candidate.index].left;
                var tr=candidate.filters[candidate.phase].transform;tr.SetPositionAndRotation(track.position,track.rotation);tr.localScale=track.scale;
                candidate.renderers[candidate.phase].sharedMaterials=track.materials;
                candidate.request=Resources.LoadAsync<Mesh>(track.meshResource);
            }
        }
        EssentialReady=true;
        // Check all chunks under the entire train; bridge gaps have no rail chunk
        // records because their fixed deck/rail assembly remains in its zone.
        for(int k=0;k<data.chunks.Length;k++)
        {
            var c=data.chunks[k];if(c.end<d-train.carSpacing-35 || c.start>d+30)continue;
            bool ready=false;for(int i=0;i<warmed;i++)if(slots[i].index==k && slots[i].phase==2){ready=true;break;}
            if(!ready){EssentialReady=false;break;}
        }
        if(!EssentialReady && Time.timeSinceLevelLoad>15)MissingEssentialFrames++;
    }
    void OnDisable()=>RailwayStreamingBudget.Release(this);
}
