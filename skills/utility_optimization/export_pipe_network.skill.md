---
name: export_pipe_network
category: utility_optimization
description: Export every pipe in a network with size, slope, inverts, and connected structures — ready to feed into the O-Pipes Python sizing pipeline
requires_write: false
parameters:
  - name: networkName
    type: string
    required: true
    description: Name of the pipe network to export (use list_pipe_networks first)
---

## Code Template

```csharp
Network targetNetwork = null;
foreach (ObjectId netId in CivilDoc.GetPipeNetworkIds())
{
    var n = Transaction.GetObject(netId, OpenMode.ForRead) as Network;
    if (n != null && n.Name.Equals("NETWORK_NAME", StringComparison.OrdinalIgnoreCase))
    { targetNetwork = n; break; }
}
if (targetNetwork == null) return new { error = "Network not found" };

var pipes = new List<object>();
foreach (ObjectId pipeId in targetNetwork.GetPipeIds())
{
    var pipe = Transaction.GetObject(pipeId, OpenMode.ForRead) as Pipe;
    if (pipe == null) continue;

    string fromStructure = "";
    string toStructure = "";
    try
    {
        if (!pipe.StartStructureId.IsNull)
        {
            var s = Transaction.GetObject(pipe.StartStructureId, OpenMode.ForRead) as Structure;
            fromStructure = s?.Name ?? "";
        }
        if (!pipe.EndStructureId.IsNull)
        {
            var s = Transaction.GetObject(pipe.EndStructureId, OpenMode.ForRead) as Structure;
            toStructure = s?.Name ?? "";
        }
    }
    catch { /* ignore */ }

    pipes.Add(new
    {
        name = pipe.Name,
        handle = pipe.Handle.ToString(),
        fromStructure,
        toStructure,
        length_ft = pipe.Length2DToInsideEdge,
        innerDiameter_in = pipe.InnerDiameterOrWidth * 12.0,
        slope_pct = pipe.Slope * 100.0,
        usInvert_ft = pipe.StartPoint.Z,
        dsInvert_ft = pipe.EndPoint.Z,
        partType = pipe.PartType.ToString(),
        partSizeName = pipe.PartSizeName
    });
}

return new { networkName = targetNetwork.Name, pipeCount = pipes.Count, pipes };
```

## Usage Notes
- This is the canonical export format for Mohammad's O-Pipes Python pipeline.
- Slopes are returned as percent (multiply Civil 3D's decimal value by 100).
- Diameters are converted from feet to inches.
- Pair with `check_pipe_cover` to add cover data per pipe.
