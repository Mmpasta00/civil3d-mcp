---
name: create_feature_line_from_polyline
category: grading
description: Convert a polyline into a Civil 3D feature line (a 3D-aware grading/design line — top of wall, ridge, gutter flow line)
requires_write: true
parameters:
  - name: polylineHandle
    type: string
    required: true
  - name: featureLineName
    type: string
    required: true
  - name: siteName
    type: string
    required: false
    description: Optional site to assign the feature line to
---

## Code Template

```csharp
string handleStr = "POLYLINE_HANDLE";
string featureLineName = "FEATURE_LINE_NAME_HERE";
string siteName = "";

long handleValue = Convert.ToInt64(handleStr, 16);
if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId plineId))
    return new { error = $"No object found with handle {handleStr}" };

ObjectId featureLineId;
if (!string.IsNullOrWhiteSpace(siteName))
{
    ObjectId siteId = ObjectId.Null;
    foreach (ObjectId id in CivilDoc.GetSiteIds())
    {
        var s = Transaction.GetObject(id, OpenMode.ForRead) as Site;
        if (s != null && s.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase)) { siteId = id; break; }
    }
    if (siteId.IsNull) return new { error = "Site not found" };
    featureLineId = FeatureLine.Create(featureLineName, plineId, siteId);
}
else
{
    featureLineId = FeatureLine.Create(featureLineName, plineId);
}

var featureLine = Transaction.GetObject(featureLineId, OpenMode.ForRead) as FeatureLine;

return new
{
    success = true,
    name = featureLine.Name,
    length2D = featureLine.Length2D,
    length3D = featureLine.Length3D,
    pointCount = featureLine.PointsCount
};
```

## Usage Notes
- The source polyline is consumed into the feature line (it becomes the feature line's geometry, not left as a separate entity).
- Feature lines carry per-vertex elevations — a flat 2D source polyline creates a flat feature line; assign real elevations after with `set_feature_line_elevations` or `assign_elevations_from_surface`-style calls.
- Feature lines are the building block for `create_grading` daylight/offset operations.
