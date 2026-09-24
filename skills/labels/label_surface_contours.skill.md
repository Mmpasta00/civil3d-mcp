---
name: label_surface_contours
category: labels
description: Add contour elevation labels along a line drawn across a surface, at a fixed spacing
requires_write: true
parameters:
  - name: surfaceName
    type: string
    required: true
  - name: startX
    type: double
    required: true
  - name: startY
    type: double
    required: true
  - name: endX
    type: double
    required: true
  - name: endY
    type: double
    required: true
  - name: interval
    type: number
    required: false
    description: Spacing between labels along the line (drawing units, default 50)
---

## Code Template

```csharp
string surfaceName = "SURFACE_NAME";
double startX = 0, startY = 0, endX = 500, endY = 0;
double interval = 50.0;

ObjectId surfId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
    if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surfId = id; break; }
}
if (surfId.IsNull) return new { error = "Surface not found" };

var startPt = new Point2d(startX, startY);
var endPt = new Point2d(endX, endY);

SurfaceContourLabelGroup.CreateMultipleAtInterval(surfId, startPt, endPt, interval);

return new
{
    success = true,
    surfaceName,
    line = new { startX, startY, endX, endY },
    interval
};
```

## Usage Notes
- The "label line" is a construction line the AI defines (not a permanent drawing entity) — every contour it crosses along that line gets a label at roughly the given spacing.
- Run across the steepest part of the site for the most useful set of labels, or run several lines (grid pattern) for full coverage.
- For a single specific contour instead of a spaced set, use `SurfaceContourLabelGroup.Create` with one label-line point pair.
