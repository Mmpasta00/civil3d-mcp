---
name: add_boundary_from_polyline
category: surfaces
description: Add an outer or hide boundary to a TIN surface from a closed polyline
requires_write: true
parameters:
  - name: surfaceName
    type: string
    required: true
  - name: polylineHandle
    type: string
    required: true
  - name: boundaryType
    type: string
    required: false
    description: "Outer, Hide, Show, or DataClip (default Outer)"
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME";
string handleStr = "POLYLINE_HANDLE";
var boundaryType = SurfaceBoundaryType.Outer; // Outer | Hide | Show | DataClip

TinSurface surface = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForWrite) as TinSurface;
    if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surface = s; break; }
}
if (surface == null) return new { error = "Surface not found" };

long handleValue = Convert.ToInt64(handleStr, 16);
if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId plineId))
    return new { error = $"No object found with handle {handleStr}" };

var boundaryIds = new ObjectIdCollection { plineId };
surface.BoundariesDefinition.AddBoundaries(boundaryIds, 0.0, boundaryType, false);
surface.Rebuild();

return new { success = true, surfaceName = surface.Name, boundaryType = boundaryType.ToString() };
```

## Usage Notes
- `Outer` trims the surface to the polyline's extent; `Hide` cuts a hole inside it (e.g. a building pad); `Show` re-reveals a previously hidden area; `DataClip` limits which source data is used without changing the visible boundary.
- The polyline must be closed for a valid boundary.
- `useNonDestructiveBreakline: false` lets Civil 3D re-triangulate freely along the boundary; set true to preserve exact vertex elevations.
