using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="UTT/Railway BIM catalog")]
public sealed class RailwayBimCatalog:ScriptableObject
{
    [Serializable] public sealed class Part
    {
        public string key,name,type,sourcePath;
        public Mesh mesh;public Material[] materials;public Bounds bounds;
    }
    [Serializable] public sealed class Block
    {
        public string id,segmentID,track;public float start,end;public Vector3 position;public Quaternion rotation;
        public Bounds bounds;public Mesh overview;public Material[] materials;
    }
    [Serializable] public sealed class Segment
    {public string id,track;public float start,end;public Bounds bounds;public Mesh overview;public Material[] materials;}
    public Part[] parts;public Block[] blocks;public Segment[] segments;
    public string zoneID="Z021";public string seedVersion="1";
    [Tooltip("Rigid model registration to the overview rail-head datum; does not deform component geometry.")]
    public float detailedVerticalOffset;
    public Dictionary<string,Block> BlockIndex(){var result=new Dictionary<string,Block>();foreach(var b in blocks)result.Add(b.id,b);return result;}
}
