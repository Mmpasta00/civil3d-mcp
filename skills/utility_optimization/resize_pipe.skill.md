---
name: resize_pipe
category: utility_optimization
description: Resize a single pipe by swapping its part size (e.g. upsizing from 12-inch to 18-inch). Modifies the drawing.
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
  - name: newPartSizeName
    type: string
    required: true
    description: Target part size from the parts list (e.g. "18 inch HDPE", "RCP 24 inch")
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

try
{
    targetPipe.PartSizeName = "NEW_PART_SIZE_NAME";
}
catch (System.Exception ex)
{
    return new { error = $"Failed to change part size: {ex.Message}. Make sure '{("NEW_PART_SIZE_NAME")}' exists in this network's parts list." };
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
- The `newPartSizeName` must exist in the network's parts list. Use `list_pipe_networks` to see the parts list name; the engineer's CAD manager can list available sizes.
- After resizing, re-run `check_pipe_cover` and `find_pipe_conflicts` to verify the change didn't break anything.
- For bulk resizing, loop this skill over a list of {pipeName, newPartSize} pairs from the O-Pipes sizing output.
