---
name: find_pipe_conflicts
category: utility_optimization
description: Find vertical conflicts between pipes (utility crossings where clearance is below a threshold) across one or multiple networks
requires_write: false
parameters:
  - name: minClearance_ft
    type: number
    required: true
    description: Minimum vertical clearance required between pipe outer walls in feet (typical 1.0 ft)
  - name: networkNames
    type: string
    required: false
    description: Comma-separated network names to check; empty = check all gravity networks in the drawing
---

## Code Template

```csharp
double minClearance = MIN_CLEARANCE_FT;
string networkFilter = "NETWORK_NAMES"; // empty string = all

var targetNetworkNames = string.IsNullOrWhiteSpace(networkFilter)
    ? null
    : networkFilter.Split(',').Select(s => s.Trim()).ToList();

// Gather all pipes from target networks
var pipes = new List<(string netName, Pipe pipe, double outerDia)>();
foreach (ObjectId netId in CivilDoc.GetPipeNetworkIds())
{
    var net = Transaction.GetObject(netId, OpenMode.ForRead) as Network;
    if (net == null) continue;
    if (targetNetworkNames != null && !targetNetworkNames.Any(t => t.Equals(net.Name, StringComparison.OrdinalIgnoreCase))) continue;

    foreach (ObjectId pipeId in net.GetPipeIds())
    {
        var pipe = Transaction.GetObject(pipeId, OpenMode.ForRead) as Pipe;
        if (pipe == null) continue;
        pipes.Add((net.Name, pipe, pipe.OuterDiameterOrWidth));
    }
}

// Brute-force pairwise check (acceptable for <500 pipes; project-scale drawings rarely exceed this)
var conflicts = new List<object>();
for (int i = 0; i < pipes.Count; i++)
{
    for (int j = i + 1; j < pipes.Count; j++)
    {
        var a = pipes[i].pipe;
        var b = pipes[j].pipe;
        if (a.NetworkName == b.NetworkName && a.Name == b.Name) continue;

        // Simple 2D intersection check using bounding-box overlap as a fast filter
        var aMin = new Point3d(Math.Min(a.StartPoint.X, a.EndPoint.X), Math.Min(a.StartPoint.Y, a.EndPoint.Y), 0);
        var aMax = new Point3d(Math.Max(a.StartPoint.X, a.EndPoint.X), Math.Max(a.StartPoint.Y, a.EndPoint.Y), 0);
        var bMin = new Point3d(Math.Min(b.StartPoint.X, b.EndPoint.X), Math.Min(b.StartPoint.Y, b.EndPoint.Y), 0);
        var bMax = new Point3d(Math.Max(b.StartPoint.X, b.EndPoint.X), Math.Max(b.StartPoint.Y, b.EndPoint.Y), 0);
        if (aMax.X < bMin.X || bMax.X < aMin.X) continue;
        if (aMax.Y < bMin.Y || bMax.Y < aMin.Y) continue;

        // Approximate crossing point as midpoint of bbox overlap; compute Z on each pipe
        double xMid = (Math.Max(aMin.X, bMin.X) + Math.Min(aMax.X, bMax.X)) / 2.0;
        double yMid = (Math.Max(aMin.Y, bMin.Y) + Math.Min(aMax.Y, bMax.Y)) / 2.0;

        double aZ = InterpZ(a, xMid, yMid);
        double bZ = InterpZ(b, xMid, yMid);

        double aTop = aZ + pipes[i].outerDia / 2.0;
        double aBot = aZ - pipes[i].outerDia / 2.0;
        double bTop = bZ + pipes[j].outerDia / 2.0;
        double bBot = bZ - pipes[j].outerDia / 2.0;

        double clearance = (aZ > bZ) ? (aBot - bTop) : (bBot - aTop);

        if (clearance < minClearance)
        {
            conflicts.Add(new
            {
                pipeA = $"{pipes[i].netName}.{a.Name}",
                pipeB = $"{pipes[j].netName}.{b.Name}",
                aZ_ft = Math.Round(aZ, 2),
                bZ_ft = Math.Round(bZ, 2),
                clearance_ft = Math.Round(clearance, 2),
                crossingX = Math.Round(xMid, 1),
                crossingY = Math.Round(yMid, 1)
            });
        }
    }
}

return new { pipeCount = pipes.Count, conflictCount = conflicts.Count, minRequired_ft = minClearance, conflicts };

double InterpZ(Pipe p, double x, double y)
{
    double totalLen = Math.Sqrt(Math.Pow(p.EndPoint.X - p.StartPoint.X, 2) + Math.Pow(p.EndPoint.Y - p.StartPoint.Y, 2));
    if (totalLen < 1e-9) return p.StartPoint.Z;
    double distFromStart = Math.Sqrt(Math.Pow(x - p.StartPoint.X, 2) + Math.Pow(y - p.StartPoint.Y, 2));
    double t = Math.Clamp(distFromStart / totalLen, 0, 1);
    return p.StartPoint.Z + t * (p.EndPoint.Z - p.StartPoint.Z);
}
```

## Usage Notes
- Brute-force O(n²) pairwise check — fine for typical site drawings (<500 pipes total).
- Crossing point approximated as bounding-box midpoint; for precise crossing geometry use a true line-line intersection.
- Vertical clearance is wall-to-wall (top of lower pipe to bottom of upper pipe).
- Filter by `networkNames` to compare e.g. only storm vs. water main crossings.
