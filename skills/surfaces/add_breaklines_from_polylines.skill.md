---
name: add_breaklines_from_polylines
category: surfaces
description: Add breaklines to a TIN surface from one or more 3D polylines (ridge lines, ditches, curb returns)
requires_write: true
parameters:
  - name: surfaceName
    type: string
    required: true
  - name: polylineHandles
    type: string
    required: true
    description: Comma-separated drawing handles of the source 3D polylines
  - name: description
    type: string
    required: false
    description: Label for this breakline set (e.g. "Curb & gutter")
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME";
string handlesCsv = "HANDLE1,HANDLE2";
string description = "Breaklines";

TinSurface surface = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForWrite) as TinSurface;
    if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surface = s; break; }
}
if (surface == null) return new { error = "Surface not found" };

var breaklineIds = new ObjectIdCollection();
foreach (var h in handlesCsv.Split(',').Select(s => s.Trim()))
{
    long handleValue = Convert.ToInt64(h, 16);
    if (Database.TryGetObjectId(new Handle(handleValue), out ObjectId id))
        breaklineIds.Add(id);
}
if (breaklineIds.Count == 0) return new { error = "No valid polyline handles resolved" };

surface.BreaklinesDefinition.AddStandardBreaklines(breaklineIds, 0.0, 0.0, 0.0, 0.0);
surface.Rebuild();

return new { success = true, surfaceName = surface.Name, breaklinesAdded = breaklineIds.Count, description };
```

## Usage Notes
- `AddStandardBreaklines` parameters after the entity list are mid-ordinate distance, maximum chord-to-arc distance, weeding distance, and weeding angle — 0.0 disables simplification (use exact vertices).
- Source polylines should have real Z elevations (3D polylines or 2D polylines with elevation) — flat polylines add a level breakline at the entity's single elevation.
- Get handles from `select_by_layer` filtered to the survey/breakline layer.
