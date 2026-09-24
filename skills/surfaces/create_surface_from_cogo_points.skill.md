---
name: create_surface_from_cogo_points
category: surfaces
description: Create a new TIN surface built from a COGO point group (typical topo-to-surface workflow)
requires_write: true
parameters:
  - name: surfaceName
    type: string
    required: true
  - name: pointGroupName
    type: string
    required: true
    description: Existing point group name; use create_point_group_by_description first if needed
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME_HERE";
string pointGroupName = "POINT_GROUP_NAME";

if (!CivilDoc.PointGroups.Contains(pointGroupName))
    return new { error = $"Point group '{pointGroupName}' not found" };

var pointGroupId = CivilDoc.PointGroups[pointGroupName];

var surfaceId = TinSurface.Create(Database, surfaceName);
var surface = Transaction.GetObject(surfaceId, OpenMode.ForWrite) as TinSurface;

surface.PointGroupsDefinition.AddPointGroup(pointGroupId);
surface.Rebuild();

var props = surface.GetGeneralProperties();

return new
{
    success = true,
    name = surface.Name,
    pointGroupName,
    pointCount = props.NumberOfPoints,
    minElevation = props.MinimumElevation,
    maxElevation = props.MaximumElevation
};
```

## Usage Notes
- The surface stays dynamically linked to the point group — new points added to the group later will flow into the surface on rebuild.
- Add breaklines/boundaries afterward with `add_breaklines_from_polylines` / `add_boundary_from_polyline` for a survey-grade TIN.
