---
name: pipe_slope_and_cover_report
category: pipe_networks
description: Full per-pipe report of slope, size, invert elevations, and rim-to-invert depth for an entire network
requires_write: false
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
    var n = Transaction.GetObject(id, OpenMode.ForRead) as Network;
    if (n != null && n.Name.Equals(networkName, StringComparison.OrdinalIgnoreCase)) { net = n; break; }
}
if (net == null) return new { error = "Network not found" };

var rows = new List<object>();
foreach (ObjectId pipeId in net.GetPipeIds())
{
    var pipe = Transaction.GetObject(pipeId, OpenMode.ForRead) as Pipe;
    if (pipe == null) continue;

    string usStructName = "", dsStructName = "";
    double usRim = double.NaN, dsRim = double.NaN;
    if (!pipe.StartStructureId.IsNull)
    {
        var s = Transaction.GetObject(pipe.StartStructureId, OpenMode.ForRead) as Structure;
        usStructName = s?.Name ?? ""; usRim = s?.RimElevation ?? double.NaN;
    }
    if (!pipe.EndStructureId.IsNull)
    {
        var s = Transaction.GetObject(pipe.EndStructureId, OpenMode.ForRead) as Structure;
        dsStructName = s?.Name ?? ""; dsRim = s?.RimElevation ?? double.NaN;
    }

    rows.Add(new
    {
        pipeName = pipe.Name,
        partSize = pipe.PartSizeName,
        innerDiameter_in = Math.Round(pipe.InnerDiameterOrWidth * 12.0, 2),
        slope_pct = Math.Round(pipe.Slope * 100.0, 3),
        length_ft = Math.Round(pipe.Length2DToInsideEdge, 1),
        usStructure = usStructName,
        usInvert_ft = Math.Round(pipe.StartPoint.Z, 2),
        usCover_ft = double.IsNaN(usRim) ? (double?)null : Math.Round(usRim - pipe.StartPoint.Z - pipe.OuterDiameterOrWidth, 2),
        dsStructure = dsStructName,
        dsInvert_ft = Math.Round(pipe.EndPoint.Z, 2),
        dsCover_ft = double.IsNaN(dsRim) ? (double?)null : Math.Round(dsRim - pipe.EndPoint.Z - pipe.OuterDiameterOrWidth, 2)
    });
}

return new { networkName = net.Name, pipeCount = rows.Count, pipes = rows };
```

## Usage Notes
- Combines what `export_pipe_network` and `check_pipe_cover` each do separately into one per-pipe table — use this for a quick full-network QA pass.
- Cover is computed from each pipe's own connected structure rim, not a design surface — for surface-based cover use `check_pipe_cover`.
