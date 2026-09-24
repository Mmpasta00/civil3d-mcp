---
name: import_points_from_csv
category: points
description: Import COGO points from CSV text (point#,northing,easting,elevation,description) passed inline — no file path required
requires_write: true
parameters:
  - name: csvText
    type: string
    required: true
    description: "Raw CSV rows, one per point: PN,Northing,Easting,Elevation,Description"
  - name: pointGroupName
    type: string
    required: false
    description: Optional point group to create/add these points into
---

## Code Template

```csharp
string csvText = "1,2000.00,1000.00,100.0,TOPO\n2,2010.00,1015.00,101.5,TOPO";
string targetGroup = ""; // optional

var created = new List<object>();
var lines = csvText.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

foreach (var line in lines)
{
    var parts = line.Split(',');
    if (parts.Length < 4) continue;

    double northing = double.Parse(parts[1]);
    double easting = double.Parse(parts[2]);
    double elevation = double.Parse(parts[3]);
    string description = parts.Length > 4 ? parts[4] : "";

    var ptId = CivilDoc.CogoPoints.Add(new Point3d(easting, northing, elevation), true);
    var point = Transaction.GetObject(ptId, OpenMode.ForWrite) as CogoPoint;
    point.RawDescription = description;

    created.Add(new { pointNumber = point.PointNumber, northing, easting, elevation, description });
}

if (!string.IsNullOrWhiteSpace(targetGroup) && created.Count > 0)
{
    ObjectId groupId = CivilDoc.PointGroups.Contains(targetGroup)
        ? CivilDoc.PointGroups[targetGroup]
        : CivilDoc.PointGroups.Add(targetGroup);
    var group = Transaction.GetObject(groupId, OpenMode.ForWrite) as PointGroup;

    // Precise membership: match exactly the point numbers just imported (not a wildcard).
    var numberList = string.Join(",", created.Select(c => c.GetType().GetProperty("pointNumber").GetValue(c)));
    var query = new StandardPointGroupQuery { IncludeNumbers = numberList };
    group.SetQuery(query);
    group.Update();
}

return new { success = true, importedCount = created.Count, points = created };
```

## Usage Notes
- CSV is passed as inline text (not a file path) because the plugin runs on the engineer's workstation but the AI may be assembling data elsewhere — paste rows directly.
- Point numbering: `Add(..., true)` auto-assigns the next available number; switch to `false` and pass an explicit number if you need to preserve survey point numbers exactly (requires a different `Add` overload with a number argument).
- For very large CSVs (thousands of rows), consider `Editor.Command("_-ImportPoints", ...)` against a real file path instead — it is far faster than a per-point managed API loop.
