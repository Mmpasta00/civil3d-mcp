---
name: create_section_views
category: sections
description: Create a section view at each sample line in a group, laid out in a grid for plan-check plotting
requires_write: true
parameters:
  - name: groupName
    type: string
    required: true
  - name: alignmentName
    type: string
    required: true
  - name: originX
    type: double
    required: true
  - name: originY
    type: double
    required: true
  - name: columnSpacing
    type: double
    required: false
    description: Horizontal spacing between section views in drawing units (default 100)
  - name: rowSpacing
    type: double
    required: false
    description: Vertical spacing between rows (default 80)
  - name: columns
    type: int
    required: false
    description: Section views per row before wrapping (default 5)
---

## Code Template

```csharp
string groupName = "SAMPLE_LINE_GROUP_NAME";
string alignName = "ALIGNMENT_NAME";
double originX = 0.0, originY = 0.0;
double columnSpacing = 100.0, rowSpacing = 80.0;
int columnsPerRow = 5;

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
if (groupId.IsNull) return new { error = "Sample line group not found" };

var group = Transaction.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
var sampleLineIds = group.GetSampleLineIds();

var created = new List<object>();
int i = 0;
foreach (ObjectId slId in sampleLineIds)
{
    var sampleLine = Transaction.GetObject(slId, OpenMode.ForRead) as SampleLine;
    int col = i % columnsPerRow;
    int row = i / columnsPerRow;
    var insertPt = new Point3d(originX + col * columnSpacing, originY - row * rowSpacing, 0);

    var svId = SectionView.Create($"SV-{sampleLine.Station:F0}", slId, insertPt);
    created.Add(new { station = sampleLine.Station, x = insertPt.X, y = insertPt.Y });
    i++;
}

return new { success = true, groupName, sectionViewsCreated = created.Count, layout = created };
```

## Usage Notes
- Grid layout keeps a plan-check reviewer's section sheet organized without manual placement — adjust spacing to fit the widest expected section (culvert crossings, deep cuts) so views don't overlap.
- Uses drawing-default section view style/grid — for firm-standard styling, set it on the sample line group's `DefaultSamplineStyleId`/section view style before running, or restyle after.
