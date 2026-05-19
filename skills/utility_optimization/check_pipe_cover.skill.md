---
name: check_pipe_cover
category: utility_optimization
description: Find pipes with cover (depth from surface to top of pipe) below a minimum threshold — flags shallow pipes for resizing or regrading
requires_write: false
parameters:
  - name: networkName
    type: string
    required: true
    description: Name of the pipe network to check
  - name: surfaceName
    type: string
    required: true
    description: Name of the reference surface (typically existing or finished grade)
  - name: minCover_ft
    type: number
    required: true
    description: Minimum required cover in feet (typical: 2.5 for storm, 3.0 for sanitary)
---

## Code Template

```csharp
// Find network
Network net = null;
foreach (ObjectId id in CivilDoc.GetPipeNetworkIds())
{
    var n = Transaction.GetObject(id, OpenMode.ForRead) as Network;
    if (n != null && n.Name.Equals("NETWORK_NAME", StringComparison.OrdinalIgnoreCase))
    { net = n; break; }
}
if (net == null) return new { error = "Network not found" };

// Find surface
TinSurface surf = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as TinSurface;
    if (s != null && s.Name.Equals("SURFACE_NAME", StringComparison.OrdinalIgnoreCase))
    { surf = s; break; }
}
if (surf == null) return new { error = "Surface not found" };

double minCover = MIN_COVER_FT;
var violations = new List<object>();
var allPipes = new List<object>();

foreach (ObjectId pipeId in net.GetPipeIds())
{
    var pipe = Transaction.GetObject(pipeId, OpenMode.ForRead) as Pipe;
    if (pipe == null) continue;

    // Surface elevation above the pipe endpoints
    double usSurfElev = 0, dsSurfElev = 0;
    try { usSurfElev = surf.FindElevationAtXY(pipe.StartPoint.X, pipe.StartPoint.Y); } catch { }
    try { dsSurfElev = surf.FindElevationAtXY(pipe.EndPoint.X, pipe.EndPoint.Y); } catch { }

    // Cover = surface elev − (invert + outer diameter)
    double outerDia_ft = pipe.OuterDiameterOrWidth;
    double usCover = usSurfElev - (pipe.StartPoint.Z + outerDia_ft);
    double dsCover = dsSurfElev - (pipe.EndPoint.Z + outerDia_ft);
    double minPipeCover = Math.Min(usCover, dsCover);

    var info = new
    {
        name = pipe.Name,
        usCover_ft = Math.Round(usCover, 2),
        dsCover_ft = Math.Round(dsCover, 2),
        minCover_ft = Math.Round(minPipeCover, 2),
        passes = minPipeCover >= minCover
    };
    allPipes.Add(info);
    if (!info.passes) violations.Add(info);
}

return new
{
    networkName = net.Name,
    surfaceName = surf.Name,
    minRequired_ft = minCover,
    totalPipes = allPipes.Count,
    violationCount = violations.Count,
    violations,
    allPipes
};
```

## Usage Notes
- Cover is measured from surface to **top** of pipe (outer diameter), not invert.
- US/DS = upstream/downstream endpoints.
- A pipe `passes` if BOTH endpoints meet minimum cover.
- Typical thresholds: 2.5 ft storm sewer, 3.0 ft sanitary sewer, 3.5 ft water main, 4.0 ft frost regions.
- This is the same check Mohammad's O-Pipes dashboard runs — useful for spot-checks inside Civil 3D.
