---
name: paste_surfaces
category: surfaces
description: Paste (merge) one surface's data into another, e.g. combining an on-site TIN with an adjacent offsite TIN
requires_write: true
parameters:
  - name: targetSurfaceName
    type: string
    required: true
  - name: sourceSurfaceName
    type: string
    required: true
---

## Code Template

```csharp
string targetName = "TARGET_SURFACE_NAME";
string sourceName = "SOURCE_SURFACE_NAME";

TinSurface target = null, source = null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForWrite) as TinSurface;
    if (s == null) continue;
    if (s.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase)) target = s;
    if (s.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase)) source = s;
}
if (target == null) return new { error = "Target surface not found" };
if (source == null) return new { error = "Source surface not found" };

target.PasteSurface(source.ObjectId);
target.Rebuild();

var props = target.GetGeneralProperties();

return new
{
    success = true,
    targetSurface = target.Name,
    pastedFrom = source.Name,
    pointCountAfter = props.NumberOfPoints
};
```

## Usage Notes
- Pasting is a one-time merge — the target does not stay dynamically linked to the source after this. Re-run if the source changes.
- Overlapping areas are resolved by paste order; the most recently pasted surface's data wins in the overlap.
- Common use: merging a project TIN with a neighboring offsite TIN survey before running boundary-spanning earthwork.
