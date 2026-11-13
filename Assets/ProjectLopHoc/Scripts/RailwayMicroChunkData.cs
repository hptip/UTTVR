using System;
using UnityEngine;

[CreateAssetMenu(menuName="UTT/Railway Micro Chunk Data")]
public sealed class RailwayMicroChunkData : ScriptableObject
{
    [Serializable] public sealed class Track
    {
        public string meshResource;
        public Vector3 position, scale=Vector3.one;
        public Quaternion rotation=Quaternion.identity;
        public Material[] materials;
    }
    [Serializable] public sealed class Chunk
    {
        public float start, end;
        public Track right, left;
    }
    public Chunk[] chunks=Array.Empty<Chunk>();
    public float length, originalStart, originalEnd;
}
