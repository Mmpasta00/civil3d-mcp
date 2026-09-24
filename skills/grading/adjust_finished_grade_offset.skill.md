---
name: adjust_finished_grade_offset
category: grading
description: Shift every elevation on a finished-grade feature line up or down by a constant offset (e.g. raising a pad 0.5 ft after a design change)
requires_write: true
parameters:
  - name: featureLineHandle
    type: string
    required: true
  - name: offsetFeet
    type: number
    required: true
    description: Positive raises the grade, negative lowers it
---

## Code Template

```csharp
string handleStr = "FEATURE_LINE_HANDLE";
double offsetFeet = 0.5; // Replace — positive = raise, negative = lower

long handleValue = Convert.ToInt64(handleStr, 16);
if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId flId))
    return new { error = "Feature line handle not found" };

var featureLine = Transaction.GetObject(flId, OpenMode.ForWrite) as FeatureLine;
if (featureLine == null) return new { error = "Object is not a feature line" };

double oldMin = featureLine.MinElevation;
double oldMax = featureLine.MaxElevation;

var points = featureLine.GetPoints(FeatureLinePointType.AllPoints);
for (int i = 0; i < points.Count; i++)
{
    var pt = points[i];
    featureLine.SetPointElevation(i, pt.Z + offsetFeet);
}

return new
{
    success = true,
    featureLineName = featureLine.Name,
    offsetFeet,
    oldElevationRange = new { min = oldMin, max = oldMax },
    newElevationRange = new { min = featureLine.MinElevation, max = featureLine.MaxElevation }
};
```

## Usage Notes
- Shifts the ENTIRE feature line by a flat offset — for a sloped/tapered adjustment, edit individual PI elevations with `set_feature_line_elevations` (byIndex mode) instead.
- If this feature line is a corridor baseline or grading footprint, rebuild the dependent corridor/grading after adjusting (`rebuild_all_corridors`).
- `FeatureLinePointType.AllPoints` includes both PI (grade-break) and elevation points — confirm the index order matches what you expect before applying to a line with intermediate elevation points.
