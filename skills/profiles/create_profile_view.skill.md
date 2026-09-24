---
name: create_profile_view
category: profiles
description: Create a profile view (plotted profile grid) for an alignment at a given insertion point
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: insertX
    type: double
    required: true
  - name: insertY
    type: double
    required: true
  - name: profileViewName
    type: string
    required: false
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";
double insertX = 0.0; // Replace — pick an empty area of the drawing
double insertY = 0.0; // Replace

ObjectId alignId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignId = id; break; }
}
if (alignId.IsNull) return new { error = "Alignment not found" };

var insertPoint = new Point3d(insertX, insertY, 0);
var pvId = ProfileView.Create(alignId, insertPoint);
var pv = Transaction.GetObject(pvId, OpenMode.ForRead) as ProfileView;

return new
{
    success = true,
    alignmentName = alignName,
    stationStart = pv.StationStart,
    stationEnd = pv.StationEnd,
    elevationMin = pv.ElevationMin,
    elevationMax = pv.ElevationMax
};
```

## Usage Notes
- Uses drawing-default profile view style, station range, and datum — for a full-featured setup use the interactive "Create Profile View" wizard for the first one per project, then script the rest by copying its style.
- For a long alignment needing multiple sheets, use `Profile View.CreateMultiple`/`CreateStacked` overloads (not templated here) and pass `SplitProfileViewCreationOptions`.
- Add bands afterward with `add_profile_view_bands`.
