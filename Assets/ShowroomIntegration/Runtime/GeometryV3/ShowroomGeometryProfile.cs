using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShowroomGeometryMode { DeviceDefault, Original, Quality50, Quality25, Quality10 }

[CreateAssetMenu(menuName = "UTT/Showroom Geometry Profile")]
public sealed class ShowroomGeometryProfile : ScriptableObject
{
    [Serializable] public sealed class Entry
    {
        public Mesh original, quality50, quality25, quality10;
        public string sourceGuid, sourceLocalId, category, generator;
        public long originalTriangles, triangles50, triangles25, triangles10;
        public float boundsError50, boundsError25, boundsError10;
        public Mesh Select(ShowroomGeometryMode mode) => mode == ShowroomGeometryMode.Quality50 ? quality50 : mode == ShowroomGeometryMode.Quality25 ? quality25 : mode == ShowroomGeometryMode.Quality10 ? quality10 : original;
    }
    public Entry[] entries = Array.Empty<Entry>();
    public ShowroomGeometryMode androidDefault = ShowroomGeometryMode.Quality10;
    // Trains are viewed up close; aggressive decimation tears their surfaces. Never go below trainMinimumDetail,
    // and keep train meshes under trainKeepOriginalBelow triangles untouched (they cost little).
    public ShowroomGeometryMode trainMinimumDetail = ShowroomGeometryMode.Quality50;
    public long trainKeepOriginalBelow = 500000;
    [NonSerialized] Dictionary<Mesh, Entry> lookup;
    public Mesh Resolve(Mesh mesh, ShowroomGeometryMode mode)
    {
        if (mesh == null || mode == ShowroomGeometryMode.Original) return mesh;
        if (lookup == null || lookup.Count != entries.Length) { lookup = new Dictionary<Mesh, Entry>(); foreach (var e in entries) if (e.original != null) lookup[e.original] = e; }
        if (!lookup.TryGetValue(mesh, out var entry)) return mesh;
        if (entry.category == "train")
        {
            if (entry.originalTriangles < trainKeepOriginalBelow) return mesh;
            if (mode > trainMinimumDetail) mode = trainMinimumDetail;
        }
        return entry.Select(mode) ?? mesh;
    }
}
