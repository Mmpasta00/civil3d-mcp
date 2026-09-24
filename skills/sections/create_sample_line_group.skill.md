---
name: create_sample_line_group
category: sections
description: Create a sample line group on an alignment — the container that section-cut sample lines belong to
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: groupName
    type: string
    required: false
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";
string groupName = "Sample Line Group - 1";

ObjectId alignId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignId = id; break; }
}
if (alignId.IsNull) return new { error = "Alignment not found" };

var groupId = SampleLineGroup.Create(groupName, alignId);
var group = Transaction.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;

return new { success = true, groupName = group.Name, alignmentName = alignName };
```

## Usage Notes
- An alignment can have multiple sample line groups (e.g. one for storm design, one for grading) — name them distinctly.
- Follow with `create_sample_lines_by_station_range` to actually populate it with cuts.
