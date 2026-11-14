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
    [NonSerialized] Dictionary<Mesh, Entry> lookup;
    public Mesh Resolve(Mesh mesh, ShowroomGeometryMode mode)
    {
        if (mesh == null || mode == ShowroomGeometryMode.Original) return mesh;
        if (lookup == null || lookup.Count != entries.Length) { lookup = new Dictionary<Mesh, Entry>(); foreach (var e in entries) if (e.original != null) lookup[e.original] = e; }
        return lookup.TryGetValue(mesh, out var entry) ? entry.Select(mode) ?? mesh : mesh;
    }
}
