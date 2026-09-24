---
name: set_feature_line_elevations
category: grading
description: Set or adjust elevations at specific points along a feature line, or drape the whole line onto a reference surface
requires_write: true
parameters:
  - name: featureLineHandle
    type: string
    required: true
  - name: mode
    type: string
    required: true
    description: "fromSurface | byIndex"
  - name: surfaceName
    type: string
    required: false
    description: Required if mode = fromSurface
  - name: pointElevations
    type: array
    required: false
    description: "Array of {index, elevation} — required if mode = byIndex"
---

## Code Template

```csharp
string handleStr = "FEATURE_LINE_HANDLE";
string mode = "fromSurface"; // fromSurface | byIndex
string surfaceName = "SURFACE_NAME";

long handleValue = Convert.ToInt64(handleStr, 16);
if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId flId))
    return new { error = "Feature line handle not found" };

var featureLine = Transaction.GetObject(flId, OpenMode.ForWrite) as FeatureLine;
if (featureLine == null) return new { error = "Object is not a feature line" };

if (mode == "fromSurface")
{
    ObjectId surfId = ObjectId.Null;
    foreach (ObjectId id in CivilDoc.GetSurfaceIds())
    {
        var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
        if (s != null && s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase)) { surfId = id; break; }
    }
    if (surfId.IsNull) return new { error = "Surface not found" };

    featureLine.AssignElevationsFromSurface(surfId, true);
    return new { success = true, mode, surfaceName, minElevation = featureLine.MinElevation, maxElevation = featureLine.MaxElevation };
}
else if (mode == "byIndex")
{
    var edits = new (int index, double elevation)[] { (0, 100.0), (1, 101.5) }; // Replace with caller's list
    foreach (var e in edits)
    {
        featureLine.SetPointElevation(e.index, e.elevation);
    }
    return new { success = true, mode, pointsEdited = edits.Length, minElevation = featureLine.MinElevation, maxElevation = featureLine.MaxElevation };
}

return new { error = "mode must be 'fromSurface' or 'byIndex'" };
```

## Usage Notes
- `fromSurface` is the common case: drape a new feature line onto existing/finished grade (`bIncIntermediate: true` also elevates intermediate PI-free vertices, not just endpoints).
- `byIndex` lets you hand-edit specific vertices for a design tweak (e.g. raising one corner of a pad) without redraping the whole line.
