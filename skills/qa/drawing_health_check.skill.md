---
name: drawing_health_check
category: qa
description: Run a multi-point QA scan on the active drawing — counts surfaces, networks, alignments, broken xrefs, missing styles
requires_write: false
parameters: []
---

## Code Template

```csharp
var report = new Dictionary<string, object>();

// Surfaces
int surfCount = CivilDoc.GetSurfaceIds().Count;

// Pipe networks
int netCount = CivilDoc.GetPipeNetworkIds().Count;
int totalPipes = 0;
int totalStructures = 0;
foreach (ObjectId id in CivilDoc.GetPipeNetworkIds())
{
    var n = Transaction.GetObject(id, OpenMode.ForRead) as Network;
    if (n == null) continue;
    totalPipes += n.GetPipeIds().Count;
    totalStructures += n.GetStructureIds().Count;
}

// Alignments
int alignCount = CivilDoc.GetAlignmentIds().Count;

// Xrefs
var xrefs = new List<object>();
var xrefGraph = Database.GetHostDwgXrefGraph(true);
for (int i = 0; i < xrefGraph.NumNodes; i++)
{
    var node = xrefGraph.GetXrefNode(i) as Autodesk.AutoCAD.DatabaseServices.XrefGraphNode;
    if (node == null || node.Name == Database.Filename) continue;
    xrefs.Add(new
    {
        name = node.Name,
        path = node.Database?.Filename ?? "(unloaded)",
        status = node.XrefStatus.ToString(),
        isResolved = node.XrefStatus == XrefStatus.Resolved
    });
}

// Layer count
int layerCount = 0;
var layerTable = Transaction.GetObject(Database.LayerTableId, OpenMode.ForRead) as LayerTable;
foreach (ObjectId id in layerTable!) layerCount++;

return new
{
    drawingFile = Database.Filename,
    summary = new
    {
        surfaces = surfCount,
        pipeNetworks = netCount,
        totalPipes,
        totalStructures,
        alignments = alignCount,
        layers = layerCount,
        xrefs = xrefs.Count,
        unresolvedXrefs = xrefs.Count(x => x.GetType().GetProperty("isResolved")!.GetValue(x)?.Equals(false) ?? false)
    },
    xrefs
};
```

## Usage Notes
- Read-only health scan — safe to run anytime.
- Flags broken xrefs (other DWG files referenced but not loadable).
- For deeper checks (layer-standard compliance, missing styles), build a dedicated skill per firm-specific rule set.
- Run this at the start of any conversation as context for the AI before diving into specific tasks.
