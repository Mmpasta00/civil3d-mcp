---
name: create_volume_surface
category: surfaces
description: Create a TIN volume surface (cut/fill surface) between two surfaces — useful for grid/contour volume visualization
requires_write: true
parameters:
  - name: volumeSurfaceName
    type: string
    required: true
  - name: baseSurfaceName
    type: string
    required: true
  - name: comparisonSurfaceName
    type: string
    required: true
---

## Code Template

```csharp
string volumeSurfaceName = "VOLUME_SURFACE_NAME";
string baseSurfaceName = "BASE_SURFACE_NAME";
string comparisonSurfaceName = "COMPARISON_SURFACE_NAME";

ObjectId baseId = ObjectId.Null, compId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
    if (s == null) continue;
    if (s.Name.Equals(baseSurfaceName, StringComparison.OrdinalIgnoreCase)) baseId = id;
    if (s.Name.Equals(comparisonSurfaceName, StringComparison.OrdinalIgnoreCase)) compId = id;
}
if (baseId.IsNull) return new { error = "Base surface not found" };
if (compId.IsNull) return new { error = "Comparison surface not found" };

var volSurfId = TinVolumeSurface.Create(volumeSurfaceName, baseId, compId);
var volSurf = Transaction.GetObject(volSurfId, OpenMode.ForRead) as TinVolumeSurface;
var volProps = volSurf.GetVolumeProperties();

return new
{
    success = true,
    name = volSurf.Name,
    cutVolume = volProps.UnadjustedCutVolume,
    fillVolume = volProps.UnadjustedFillVolume,
    netVolume = volProps.UnadjustedCutVolume - volProps.UnadjustedFillVolume
};
```

## Usage Notes
- Unlike `surface_volume` (a one-time calculation), a TIN volume surface stays live in the drawing — extract elevation-banded contours from it to color-map cut/fill areas.
- Assign a "Cut and Fill" analysis style (`surface.Analysis`) after creation for a color-banded plot.
