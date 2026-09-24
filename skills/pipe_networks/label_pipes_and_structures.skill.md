---
name: label_pipes_and_structures
category: pipe_networks
description: Add plan labels to every pipe and structure in a network using the network's assigned label styles
requires_write: true
parameters:
  - name: networkName
    type: string
    required: true
---

## Code Template

```csharp
string networkName = "NETWORK_NAME";

Network net = null;
foreach (ObjectId id in CivilDoc.GetPipeNetworkIds())
{
    var n = Transaction.GetObject(id, OpenMode.ForWrite) as Network;
    if (n != null && n.Name.Equals(networkName, StringComparison.OrdinalIgnoreCase)) { net = n; break; }
}
if (net == null) return new { error = "Network not found" };

int pipesLabeled = 0, structuresLabeled = 0;

foreach (ObjectId pipeId in net.GetPipeIds())
{
    var pipe = Transaction.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
    if (pipe == null) continue;
    try
    {
        // ratio 0.5 = label placed at the pipe's midpoint
        Autodesk.Civil.DatabaseServices.PipeLabel.Create(pipeId, 0.5, net.PipePlanLabelStyleId);
        pipesLabeled++;
    }
    catch { /* label may already exist or style may be invalid — skip */ }
}

foreach (ObjectId structId in net.GetStructureIds())
{
    var structure = Transaction.GetObject(structId, OpenMode.ForWrite) as Structure;
    if (structure == null) continue;
    try
    {
        Autodesk.Civil.DatabaseServices.StructureLabel.Create(structId, net.StructurePlanLabelStyleId, structure.Location);
        structuresLabeled++;
    }
    catch { /* skip */ }
}

return new { networkName = net.Name, pipesLabeled, structuresLabeled };
```

## Usage Notes
- Uses the NETWORK's plan label style (`Network.PipePlanLabelStyleId`/`StructurePlanLabelStyleId`), not a per-part style — set those on the network first (or via `create_pipe_network`'s parts list assignment) if a firm standard style should apply.
- `PipeLabel.Create(pipeId, ratio, labelStyleId)` places the label at a point along the pipe (`ratio` 0.0–1.0, 0.5 = midpoint); `StructureLabel.Create(structureId, labelStyleId, labelLocation)` needs an explicit location — this uses the structure's own insertion point.
- Re-running does not delete or replace existing labels — check `GetLabelIds()` first if you want to avoid duplicates.
