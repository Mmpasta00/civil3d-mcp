---
name: create_corridor_surface
category: corridors
description: Add a finished-grade TIN surface built from a corridor's top links
requires_write: true
parameters:
  - name: corridorName
    type: string
    required: true
  - name: corridorSurfaceName
    type: string
    required: true
  - name: dataCodes
    type: string
    required: false
    description: Comma-separated corridor link/point codes to add as breaklines/points (e.g. "Top", "Top,Datum")
---

## Code Template

```csharp
string corridorName = "CORRIDOR_NAME";
string corridorSurfaceName = "FG - CORRIDOR_NAME";
string dataCodesCsv = "Top";

Corridor corridor = null;
foreach (ObjectId id in CivilDoc.CorridorCollection)
{
    var c = Transaction.GetObject(id, OpenMode.ForWrite) as Corridor;
    if (c != null && c.Name.Equals(corridorName, StringComparison.OrdinalIgnoreCase)) { corridor = c; break; }
}
if (corridor == null) return new { error = "Corridor not found" };

var corridorSurface = corridor.CorridorSurfaces.Add(corridorSurfaceName);

var codes = dataCodesCsv.Split(',').Select(s => s.Trim()).ToArray();
foreach (var code in codes)
{
    corridorSurface.AddLinkCode(code, false);
}

if (corridor.IsOutOfDate) corridor.Rebuild();

// Promote the corridor surface into a standalone TIN surface for volume/report use
var tinSurfaceId = TinSurface.CreateFromCorridorSurface(corridorSurfaceName, corridorSurface);
var tinSurface = Transaction.GetObject(tinSurfaceId, OpenMode.ForRead) as TinSurface;

return new
{
    success = true,
    corridorSurfaceName,
    standaloneTinSurface = tinSurface.Name,
    dataCodes = codes
};
```

## Usage Notes
- `CorridorSurface.AddLinkCode(codeName, addAsBreakLine)` adds a corridor link code as surface data — "Top" is the standard finished-pavement/finished-grade link code for most assemblies. Use `AddFeatureLineCode` instead for point-based (feature line) data.
- Corridor surfaces update automatically with the corridor; the standalone TIN surface created here is a one-time snapshot — re-run to refresh it after design changes.
- Feed the resulting surface into `surface_volume` against existing ground for earthwork.
