---
name: resize_pipe
category: utility_optimization
description: Resize a single pipe to a new inner diameter (e.g. upsizing from 12-inch to 18-inch). Modifies the drawing.
requires_write: true
parameters:
  - name: networkName
    type: string
    required: true
    description: Network containing the pipe
  - name: pipeName
    type: string
    required: true
    description: Pipe to resize (e.g. "Pipe - (5)")
  - name: newInnerDiameter_in
    type: number
    required: true
    description: Target inner diameter in inches (e.g. 18 for 18-inch pipe)
---

## Code Template

```csharp
Network net = null;
foreach (ObjectId id in CivilDoc.GetPipeNetworkIds())
{
    var n = Transaction.GetObject(id, OpenMode.ForRead) as Network;
    if (n != null && n.Name.Equals("NETWORK_NAME", StringComparison.OrdinalIgnoreCase))
    { net = n; break; }
}
if (net == null) return new { error = "Network not found" };

Pipe targetPipe = null;
foreach (ObjectId pipeId in net.GetPipeIds())
{
    var pipe = Transaction.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
    if (pipe != null && pipe.Name.Equals("PIPE_NAME", StringComparison.OrdinalIgnoreCase))
    { targetPipe = pipe; break; }
}
if (targetPipe == null) return new { error = "Pipe not found in network" };

string oldSize = targetPipe.PartSizeName;
double oldDia_in = targetPipe.InnerDiameterOrWidth * 12.0;
double newDia_in = 18.0; // NEW_INNER_DIAMETER_IN — replace with the caller's value
double newDia_ft = newDia_in / 12.0;

try
{
    targetPipe.ResizeByInnerDiameterOrWidth(newDia_ft, true); // true = maintain rules/apply nearest catalog size
}
catch (System.Exception ex)
{
    return new { error = $"Failed to resize pipe: {ex.Message}. The requested diameter may not exist in this network's parts list catalog." };
}

return new
{
    pipeName = targetPipe.Name,
    oldPartSize = oldSize,
    newPartSize = targetPipe.PartSizeName,
    oldInnerDia_in = Math.Round(oldDia_in, 2),
    newInnerDia_in = Math.Round(targetPipe.InnerDiameterOrWidth * 12.0, 2)
};
```

## Usage Notes
- **Requires write access** — modifies the drawing. Always confirm with the engineer before running.
- `Part.PartSizeName` is read-only in the managed API — resizing goes through `Pipe.ResizeByInnerDiameterOrWidth(diameterOrWidth, applyRules)`, which snaps to the nearest catalog size in the network's parts list rather than taking a free-text size name directly. Check `newPartSize` in the result to see which catalog size it actually landed on.
- For a resize by exact part family/size objects instead of a raw diameter, use `Part.SwapPartFamilyAndSize(partFamilyId, partSizeId)` — requires resolving those ids from the parts list first (see the family-lookup pattern in `add_structure_and_pipe`).
- After resizing, re-run `check_pipe_cover` and `find_pipe_conflicts` to verify the change didn't break anything.
- For bulk resizing, loop this skill over a list of {pipeName, newInnerDiameter_in} pairs from the O-Pipes sizing output.
