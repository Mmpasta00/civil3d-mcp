---
name: create_design_profile_from_pvis
category: profiles
description: Create a new layout (design) profile on an alignment and add PVIs to define the vertical geometry
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: profileName
    type: string
    required: true
  - name: pvis
    type: array
    required: true
    description: Ordered array of {station, elevation, curveLength} — curveLength 0 = no vertical curve at that PVI
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";
string profileName = "PROFILE_NAME_HERE";

ObjectId alignId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignId = id; break; }
}
if (alignId.IsNull) return new { error = "Alignment not found" };

var profileId = Profile.CreateByLayout(profileName, alignId, Database.Clayer, ObjectId.Null, ObjectId.Null);
var profile = Transaction.GetObject(profileId, OpenMode.ForWrite) as Profile;

// PVI list: (station, elevation, curveLength). curveLength 0 = simple grade break.
var pviData = new (double station, double elevation, double curveLength)[]
{
    (0.0, 100.0, 0.0),
    (500.0, 105.0, 150.0),
    (1000.0, 98.0, 0.0)
};

foreach (var p in pviData)
{
    if (p.curveLength > 0)
        profile.PVIs.AddPVISymParabola(p.station, p.elevation, p.curveLength);
    else
        profile.PVIs.AddPVI(p.station, p.elevation);
}

return new
{
    success = true,
    name = profile.Name,
    pviCount = profile.PVIs.Count,
    minElevation = profile.ElevationMin,
    maxElevation = profile.ElevationMax
};
```

## Usage Notes
- `AddPVISymParabola` creates a symmetric vertical curve; use `AddPVIArc` for a curve defined by radius or `AddPVIAsymParabola` for unequal tangent lengths.
- PVI stations must be added in increasing order and stay within the alignment's station range.
- Follow with `create_profile_view` to visualize, and `edit_profile_pvi` to fine-tune individual points.
