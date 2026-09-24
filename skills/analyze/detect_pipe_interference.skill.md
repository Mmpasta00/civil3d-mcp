---
name: detect_pipe_interference
category: analyze
description: True 3D clash detection between pipes and structures across ALL networks (not just same-type gravity crossings) — flags any two parts closer than a minimum clearance
requires_write: false
parameters:
  - name: minClearance_ft
    type: number
    required: true
    description: Minimum required clearance between outer surfaces, in feet
---

## Code Template

```csharp
double minClearance = 1.0; // Replace

var parts = new List<(string network, string name, Point3d start, Point3d end, double radius)>();

foreach (ObjectId netId in CivilDoc.GetPipeNetworkIds())
{
    var net = Transaction.GetObject(netId, OpenMode.ForRead) as Network;
    if (net == null) continue;

    foreach (ObjectId pipeId in net.GetPipeIds())
    {
        var pipe = Transaction.GetObject(pipeId, OpenMode.ForRead) as Pipe;
        if (pipe == null) continue;
        parts.Add((net.Name, pipe.Name, pipe.StartPoint, pipe.EndPoint, pipe.OuterDiameterOrWidth / 2.0));
    }
    foreach (ObjectId structId in net.GetStructureIds())
    {
        var s = Transaction.GetObject(structId, OpenMode.ForRead) as Structure;
        if (s == null) continue;
        parts.Add((net.Name, s.Name, s.Location, s.Location, s.DiameterOrWidth / 2.0));
    }
}

double SegmentDistance(Point3d p1, Point3d p2, Point3d q1, Point3d q2)
{
    // Closest distance between two 3D line segments (degenerate segments = points work fine too)
    var d1 = p2 - p1; var d2 = q2 - q1; var r = p1 - q1;
    double a = d1.DotProduct(d1), e = d2.DotProduct(d2), f = d2.DotProduct(r);
    double s, t;
    if (a <= 1e-9 && e <= 1e-9) { s = 0; t = 0; }
    else if (a <= 1e-9) { s = 0; t = Math.Clamp(f / e, 0, 1); }
    else
    {
        double c = d1.DotProduct(r);
        if (e <= 1e-9) { t = 0; s = Math.Clamp(-c / a, 0, 1); }
        else
        {
            double b = d1.DotProduct(d2);
            double denom = a * e - b * b;
            s = Math.Abs(denom) > 1e-9 ? Math.Clamp((b * f - c * e) / denom, 0, 1) : 0;
            t = (b * s + f) / e;
            t = Math.Clamp(t, 0, 1);
            s = Math.Clamp((b * t - c) / a, 0, 1);
        }
    }
    var closestP = p1 + s * d1;
    var closestQ = q1 + t * d2;
    return closestP.DistanceTo(closestQ);
}

var conflicts = new List<object>();
for (int i = 0; i < parts.Count; i++)
{
    for (int j = i + 1; j < parts.Count; j++)
    {
        if (parts[i].network == parts[j].network && parts[i].name == parts[j].name) continue;
        double centerDist = SegmentDistance(parts[i].start, parts[i].end, parts[j].start, parts[j].end);
        double clearance = centerDist - parts[i].radius - parts[j].radius;
        if (clearance < minClearance)
        {
            conflicts.Add(new
            {
                partA = $"{parts[i].network}.{parts[i].name}",
                partB = $"{parts[j].network}.{parts[j].name}",
                clearance_ft = Math.Round(clearance, 2)
            });
        }
    }
}

return new { partCount = parts.Count, minRequired_ft = minClearance, conflictCount = conflicts.Count, conflicts };
```

## Usage Notes
- Uses true 3D centerline-to-centerline distance minus combined radii — more general than `find_pipe_conflicts` (which only checks pairs within the SAME set of gravity networks using a 2D bounding-box approximation).
- O(n²) — fine for typical site-scale part counts (a few hundred); for city-scale GIS-sized networks, spatial-index the parts first.
- Structures are modeled as point-radius (their footprint diameter), not their true 3D shape — adequate for a coarse clash screen, not a final constructability check.
