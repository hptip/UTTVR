# Showroom Geometry Audit V3

Unity: 6000.3.0f1

These are active geometry submission estimates for one color pass with all active objects visible, not measured triangles/frame. Instanced sources are counted once, not again per batch. Actual stereo, shadows, extra cameras, frustum/occlusion and shader passes change frame counters. Extra material slots repeat the last submesh as Unity does. Unique asset counts are separate.

|Group|Unique meshes|Renderers|Active instances|Vertices × active instances|Triangles × active submissions|Materials|Submeshes (unique)|
|---|---:|---:|---:|---:|---:|---:|---:|
|rail|135|51486|51405|11501512|6670362|21|621|
|terrain|181|181|98|85260|157320|2|181|
|nature|10|112|105|4945400|2064271|5|10|
|bridge|3|3|3|25906|14324|4|4|
|tunnel|3|204|204|25568|14552|3|3|
|station|126|261|261|290037|239118|3|160|
|train|88|91|91|2577137|4214237|69|179|
|khac|317|436|418|2716901|1486936|51|335|

## Top 20 weighted mesh/category bottlenecks

|Group|Mesh|Active instances|Triangles per mesh|Weighted triangles|
|---|---|---:|---:|---:|
|train|obj9|1|1852424|1852424|
|train|obj18|1|1852424|1852424|
|rail|Mesh_60|2667|423|1128141|
|nature|Mountain_19_Depth_00|47|19660|924020|
|rail|Mesh_56|2667|344|917448|
|rail|Mesh_54|2667|344|917448|
|nature|Mountain_6_Depth_00|29|19660|570140|
|nature|Mountain_11_Depth_00|29|19659|570111|
|rail|Mesh_51|2667|204|544068|
|rail|Mesh_57|2667|183|488061|
|rail|Object_4_mesh0|2667|164|437388|
|rail|Mesh_59|2667|124|330708|
|rail|Mesh_61|2667|120|320040|
|khac|Mesh_56|9|35424|318816|
|khac|Mesh_59|9|30948|278532|
|rail|Mesh_62|2667|96|256032|
|rail|Mesh_53|2667|76|202692|
|rail|Mesh_55|2667|71|189357|
|khac|Mesh_55|9|19919|179271|
|rail|Cylinder|178|764|135992|

LODGroup count: 0
Mesh assets with embedded Mesh LOD: 0
Custom instancing originally calls DrawMeshInstanced at full detail, 390 batches / 49,074 sources. GPU instancing does not reduce per-instance triangles.
Total active weighted geometry triangles (one pass): 14861120

No native Terrain or SkinnedMeshRenderer was found in this saved Showroom (reconfirm on subsequent audits).
