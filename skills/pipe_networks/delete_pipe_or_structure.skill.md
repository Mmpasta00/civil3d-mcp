---
name: delete_pipe_or_structure
category: pipe_networks
description: Delete a single pipe or structure from a network by name
requires_write: true
parameters:
  - name: networkName
    type: string
    required: true
  - name: partName
    type: string
    required: true
  - name: partKind
    type: string
    required: true
    description: "pipe or structure"
---

## Code Template

```csharp
string networkName = "NETWORK_NAME";
string partName = "PART_NAME";
string partKind = "pipe"; // pipe | structure

Network net = null;
foreach (ObjectId id in CivilDoc.GetPipeNetworkIds())
{
    var n = Transaction.GetObject(id, OpenMode.ForWrite) as Network;
    if (n != null && n.Name.Equals(networkName, StringComparison.OrdinalIgnoreCase)) { net = n; break; }
}
if (net == null) return new { error = "Network not found" };

if (partKind.Equals("pipe", StringComparison.OrdinalIgnoreCase))
{
    foreach (ObjectId pipeId in net.GetPipeIds())
    {
        var pipe = Transaction.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
        if (pipe != null && pipe.Name.Equals(partName, StringComparison.OrdinalIgnoreCase))
        {
            pipe.Erase();
            return new { success = true, deleted = "pipe", name = partName };
        }
    }
    return new { error = "Pipe not found in that network" };
}
else if (partKind.Equals("structure", StringComparison.OrdinalIgnoreCase))
{
    foreach (ObjectId structId in net.GetStructureIds())
    {
        var structure = Transaction.GetObject(structId, OpenMode.ForWrite) as Structure;
        if (structure != null && structure.Name.Equals(partName, StringComparison.OrdinalIgnoreCase))
        {
            structure.Erase();
            return new { success = true, deleted = "structure", name = partName };
        }
    }
    return new { error = "Structure not found in that network" };
}

return new { error = "partKind must be 'pipe' or 'structure'" };
```

## Usage Notes
- Deleting a structure that has connected pipes leaves those pipes dangling — delete connected pipes first, or expect Civil 3D to auto-disconnect them.
- `requires_write: true` — always confirm with the engineer before deleting network parts.
