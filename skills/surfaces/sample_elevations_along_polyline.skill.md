---
name: sample_elevations_along_polyline
category: surfaces
description: Sample a surface's elevation at every vertex/breakpoint along an existing polyline (e.g. a proposed utility trench or trail alignment)
requires_write: false
parameters:
  - name: surfaceName
    type: string
    required: true
  - name: polylineHandle
    type: string
    required: true
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME";
string handleStr = "POLYLINE_HANDLE";

TinSurface surface = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as TinSurface;
    if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surface = s; break; }
}
if (surface == null) return new { error = "Surface not found" };

long handleValue = Convert.ToInt64(handleStr, 16);
if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId curveId))
    return new { error = $"No object found with handle {handleStr}" };

var samples = surface.SampleElevations(curveId);
var results = new List<object>();
double cumulativeDist = 0;
Point3d? last = null;

foreach (Point3d pt in samples)
{
    if (last.HasValue)
        cumulativeDist += last.Value.DistanceTo(pt);
    results.Add(new { x = pt.X, y = pt.Y, surfaceElevation = pt.Z, distanceAlong = Math.Round(cumulativeDist, 2) });
    last = pt;
}

return new { surfaceName = surface.Name, sampleCount = results.Count, samples = results };
```

## Usage Notes
- `SampleElevations(ObjectId)` is a `TinSurface`-specific method (not on the base `Surface` type) — samples at every vertex of the curve plus wherever it crosses a TIN triangle edge, denser than just the polyline's own vertices.
- Good for a quick trench-depth or trail-grade check without building a full profile/profile view.
