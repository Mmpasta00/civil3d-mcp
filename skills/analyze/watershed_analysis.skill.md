---
name: watershed_analysis
category: analyze
description: Extract watershed (drainage catchment) boundaries from a surface's slope-direction analysis
requires_write: true
parameters:
  - name: surfaceName
    type: string
    required: true
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME";

TinSurface surface = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForWrite) as TinSurface;
    if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surface = s; break; }
}
if (surface == null) return new { error = "Surface not found" };

var watershedIds = surface.ExtractWatershed(SurfaceExtractionSettingsType.Plan);

return new
{
    success = true,
    surfaceName = surface.Name,
    watershedBoundariesCreated = watershedIds.Count,
    note = "Boundaries are drawn as polylines in the current layer/space. Cross-reference against rational_peak_flow by measuring each boundary's enclosed area."
};
```

## Usage Notes
- `ExtractWatershed` relies on the surface's Analysis object already being configured for a "Watersheds" analysis type — if this returns zero boundaries, first set `surface.Analysis` to a watershed analysis (via the Surface Properties > Analysis tab, run once interactively) so the underlying computation exists to extract.
- Once extracted as polylines, get each catchment's area with a `Curve.Area` read (see `parcel_area_table` for the same pattern) and feed it into `rational_peak_flow`.
