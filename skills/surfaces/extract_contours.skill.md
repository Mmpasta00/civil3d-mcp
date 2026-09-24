---
name: extract_contours
category: surfaces
description: Extract contour polylines from a TIN surface at a fixed interval (or a single elevation) as drawing entities
requires_write: true
parameters:
  - name: surfaceName
    type: string
    required: true
  - name: interval
    type: number
    required: false
    description: Contour interval (e.g. 1.0 ft); omit and use singleElevation instead for one contour
  - name: singleElevation
    type: number
    required: false
    description: Extract just one contour at this elevation
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME";
double interval = 2.0;       // Replace, or use singleElevation below
double? singleElevation = null; // Replace with a value to extract only one contour

TinSurface surface = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForWrite) as TinSurface;
    if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surface = s; break; }
}
if (surface == null) return new { error = "Surface not found" };

ObjectIdCollection contourIds = singleElevation.HasValue
    ? surface.ExtractContoursAt(singleElevation.Value)
    : surface.ExtractContours(interval);

return new
{
    success = true,
    surfaceName = surface.Name,
    mode = singleElevation.HasValue ? "single" : "interval",
    interval = singleElevation.HasValue ? (double?)null : interval,
    elevation = singleElevation,
    contoursCreated = contourIds.Count
};
```

## Usage Notes
- Extracted contours are plain AutoCAD polylines dropped into the current layer/space — they are NOT dynamically linked to the surface (unlike the surface's built-in contour display).
- Use `ExtractMajorContours`/`ExtractMinorContours` (not templated here) if you need major/minor styling split automatically.
- For a labeled major/minor contour display instead of exported geometry, use `label_surface_contours`.
