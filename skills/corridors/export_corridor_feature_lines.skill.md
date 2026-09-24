---
name: export_corridor_feature_lines
category: corridors
description: Export a corridor's feature lines (top of curb, edge of pavement, etc.) as COGO points for a point group
requires_write: true
parameters:
  - name: corridorName
    type: string
    required: true
  - name: pointGroupName
    type: string
    required: true
---

## Code Template

```csharp
string corridorName = "CORRIDOR_NAME";
string pointGroupName = "POINT_GROUP_NAME";

Corridor corridor = null;
foreach (ObjectId id in CivilDoc.CorridorCollection)
{
    var c = Transaction.GetObject(id, OpenMode.ForWrite) as Corridor;
    if (c != null && c.Name.Equals(corridorName, StringComparison.OrdinalIgnoreCase)) { corridor = c; break; }
}
if (corridor == null) return new { error = "Corridor not found" };

var codeSelector = new CorridorPointCodeSelector(corridor);
codeSelector.SelectAll();
var pointGroupId = corridor.ExportFeatureLinesAsCogoPoints(pointGroupName, codeSelector);
var pointGroup = Transaction.GetObject(pointGroupId, OpenMode.ForRead) as PointGroup;

return new
{
    success = true,
    corridorName = corridor.Name,
    pointGroupName = pointGroup?.Name ?? pointGroupName,
    pointCount = pointGroup?.PointsCount ?? 0
};
```

## Usage Notes
- `CorridorPointCodeSelector` filters which point codes get exported — construct it from the corridor, then either `SelectAll()` for every coded point or select individual codes via its indexer/`GetAllCodes()` for a targeted export (e.g. only "Top_Curb").
- Large corridors can generate thousands of points — filter by code for a targeted export (e.g. curb returns for a curb-ramp compliance check).
- Combine with `export_cogo_points_csv` to hand the result to a survey crew or a QA script.
