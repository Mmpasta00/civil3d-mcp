---
name: create_sample_lines_by_station_range
category: sections
description: Populate a sample line group with sample lines at a fixed station interval over a range
requires_write: true
parameters:
  - name: groupName
    type: string
    required: true
  - name: alignmentName
    type: string
    required: true
  - name: startStation
    type: double
    required: true
  - name: endStation
    type: double
    required: true
  - name: interval
    type: double
    required: true
---

## Code Template

```csharp
string groupName = "SAMPLE_LINE_GROUP_NAME";
string alignName = "ALIGNMENT_NAME";
double startStation = 0.0;
double endStation = 1000.0;
double interval = 50.0;

Alignment alignment = null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignment = a; break; }
}
if (alignment == null) return new { error = "Alignment not found" };

ObjectId groupId = ObjectId.Null;
foreach (ObjectId id in alignment.GetSampleLineGroupIds())
{
    var g = Transaction.GetObject(id, OpenMode.ForRead) as SampleLineGroup;
    if (g != null && g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase)) { groupId = id; break; }
}
if (groupId.IsNull) return new { error = "Sample line group not found on that alignment" };

var created = new List<double>();
for (double station = startStation; station <= endStation + 1e-6; station += interval)
{
    double clamped = Math.Min(station, endStation);
    SampleLine.Create($"SL-{clamped:F0}", groupId, clamped);
    created.Add(clamped);
    if (clamped >= endStation) break;
}

return new { success = true, groupName, sampleLinesCreated = created.Count, stations = created };
```

## Usage Notes
- Sample lines are perpendicular cuts through every surface/corridor/pipe crossing the alignment at that station — this is the setup step before `create_section_views` and `section_volumes`.
- For irregular (non-uniform) spacing, call `SampleLine.Create` per station individually instead of looping a fixed interval.
