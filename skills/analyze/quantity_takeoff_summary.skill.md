---
name: quantity_takeoff_summary
category: analyze
description: Roll up drawing-wide quantities for a plan-check submittal — pipe lengths by size, structure counts, alignment lengths, surface areas
requires_write: false
parameters: []
---

## Code Template

```csharp
var pipesBySize = new Dictionary<string, (int count, double totalLength_ft)>();
int structureCount = 0;

foreach (ObjectId netId in CivilDoc.GetPipeNetworkIds())
{
    var net = Transaction.GetObject(netId, OpenMode.ForRead) as Network;
    if (net == null) continue;
    structureCount += net.GetStructureIds().Count;

    foreach (ObjectId pipeId in net.GetPipeIds())
    {
        var pipe = Transaction.GetObject(pipeId, OpenMode.ForRead) as Pipe;
        if (pipe == null) continue;
        string key = $"{pipe.PartType} - {pipe.PartSizeName}";
        if (!pipesBySize.ContainsKey(key)) pipesBySize[key] = (0, 0);
        var current = pipesBySize[key];
        pipesBySize[key] = (current.count + 1, current.totalLength_ft + pipe.Length2DToInsideEdge);
    }
}

double totalAlignmentLength = 0;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null) totalAlignmentLength += a.Length;
}

var surfaceAreas = new List<object>();
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as TinSurface;
    if (s == null) continue;
    var props = s.GetTerrainProperties();
    surfaceAreas.Add(new { name = s.Name, surfaceArea_acres = Math.Round(props.SurfaceArea2D / 43560.0, 2) });
}

return new
{
    pipesBySize = pipesBySize.Select(kv => new { partSize = kv.Key, count = kv.Value.count, totalLength_ft = Math.Round(kv.Value.totalLength_ft, 1) }),
    totalStructures = structureCount,
    totalAlignmentLength_ft = Math.Round(totalAlignmentLength, 1),
    surfaces = surfaceAreas
};
```

## Usage Notes
- `TinSurfaceProperties.SurfaceArea2D` gives plan-view (footprint) area; use `SurfaceArea3D` from the same properties object for true surface area on sloped terrain if your plan-check needs that instead.
- This is a rollup, not a cost estimate — pair with the O-Pipes pipeline or a firm unit-cost sheet for a bid quantity takeoff.
