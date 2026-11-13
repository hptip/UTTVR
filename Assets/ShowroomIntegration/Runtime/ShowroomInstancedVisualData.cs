using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Exact existing local matrices and original meshes; no generated route or scenery geometry.</summary>
public sealed class ShowroomInstancedVisualData : ScriptableObject
{
    [Serializable] public sealed class Batch
    {
        public Mesh mesh;
        public Material material;
        public Matrix4x4[] localMatrices;
        public ShadowCastingMode shadows;
        public bool receiveShadows;
        public int layer;
    }
    public Batch[] batches;
    public int originalRendererCount;
}
