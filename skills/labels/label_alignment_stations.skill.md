---
name: label_alignment_stations
category: labels
description: Add major station labels along an alignment at a fixed interval
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: interval
    type: number
    required: true
    description: Station labeling interval (e.g. 100 for every 100 ft)
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";
double interval = 100.0;

ObjectId alignId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignId = id; break; }
}
if (alignId.IsNull) return new { error = "Alignment not found" };

var labelGroupId = AlignmentStationLabelGroup.CreateMajor(alignId, ObjectId.Null, interval);
var labelGroup = Transaction.GetObject(labelGroupId, OpenMode.ForRead) as AlignmentStationLabelGroup;

return new
{
    success = true,
    alignmentName = alignName,
    interval,
    rangeStart = labelGroup.RangeStart,
    rangeEnd = labelGroup.RangeEnd
};
```

## Usage Notes
- `ObjectId.Null` for the style uses the alignment's default major-station label style — pass a real style ObjectId if the firm has a station-labeling standard.
- Use `AlignmentStationLabelGroup.Create` (non-"Major") for minor/intermediate station ticks at a finer interval.
