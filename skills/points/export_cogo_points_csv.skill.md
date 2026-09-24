---
name: export_cogo_points_csv
category: points
description: Export COGO points (optionally filtered to a point group) as CSV text in the response — no file write needed
requires_write: false
parameters:
  - name: pointGroupName
    type: string
    required: false
    description: Limit export to this point group; omit for all points
---

## Code Template

```csharp
string pointGroupName = ""; // optional filter

HashSet<uint> allowedNumbers = null;
if (!string.IsNullOrWhiteSpace(pointGroupName))
{
    if (!CivilDoc.PointGroups.Contains(pointGroupName))
        return new { error = $"Point group '{pointGroupName}' not found" };
    var groupId = CivilDoc.PointGroups[pointGroupName];
    var group = Transaction.GetObject(groupId, OpenMode.ForRead) as PointGroup;
    allowedNumbers = new HashSet<uint>(group.GetPointNumbers());
}

var sb = new StringBuilder();
sb.AppendLine("PointNumber,Northing,Easting,Elevation,Description");
int count = 0;

foreach (ObjectId id in CivilDoc.CogoPoints)
{
    var pt = Transaction.GetObject(id, OpenMode.ForRead) as CogoPoint;
    if (pt == null) continue;
    if (allowedNumbers != null && !allowedNumbers.Contains(pt.PointNumber)) continue;

    sb.AppendLine($"{pt.PointNumber},{pt.Northing:F3},{pt.Easting:F3},{pt.Elevation:F3},{pt.RawDescription}");
    count++;
}

return new { pointGroupName = string.IsNullOrWhiteSpace(pointGroupName) ? "(all points)" : pointGroupName, count, csv = sb.ToString() };
```

## Usage Notes
- Returns CSV as a string in the JSON response rather than writing a file — the calling side can save it or hand it to another tool directly.
- Column order (PN, Northing, Easting, Elevation, Description) matches the common Civil 3D point-file format for easy round-tripping with `import_points_from_csv`.
