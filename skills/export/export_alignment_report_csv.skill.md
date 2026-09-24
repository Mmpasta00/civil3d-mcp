---
name: export_alignment_report_csv
category: export
description: Export an alignment's full station/geometry breakdown as CSV text (no file write) for handoff to a spreadsheet or plan-check response
requires_write: false
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: stationInterval
    type: number
    required: false
    description: Sample interval for coordinate rows (default 100)
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";
double interval = 100.0;

Alignment alignment = null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignment = a; break; }
}
if (alignment == null) return new { error = "Alignment not found" };

var sb = new StringBuilder();
sb.AppendLine("Station,Easting,Northing");

for (double station = alignment.StartingStation; station <= alignment.EndingStation + 1e-6; station += interval)
{
    double clamped = Math.Min(station, alignment.EndingStation);
    double x = 0, y = 0;
    alignment.PointLocation(clamped, 0.0, ref x, ref y);
    sb.AppendLine($"{clamped:F2},{x:F3},{y:F3}");
    if (clamped >= alignment.EndingStation) break;
}

return new
{
    alignmentName = alignment.Name,
    startStation = alignment.StartingStation,
    endStation = alignment.EndingStation,
    interval,
    csv = sb.ToString()
};
```

## Usage Notes
- Returns CSV inline in the JSON response — hand it directly to the AI's response or ask the calling side to save it, no drawing file write involved.
- Offset column defaults to centerline (0.0) — extend the loop to also sample at ROW/easement offsets for a wider report.
