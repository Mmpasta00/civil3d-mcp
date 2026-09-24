---
name: create_surface_profile
category: profiles
description: Create a profile by sampling an existing ground surface along an alignment
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: surfaceName
    type: string
    required: true
  - name: profileName
    type: string
    required: false
    description: Name for the new profile (default "SurfaceName - AlignmentName")
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";
string surfName = "SURFACE_NAME";
string profileName = $"{surfName} - {alignName}";

ObjectId alignId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignId = id; break; }
}
if (alignId.IsNull) return new { error = "Alignment not found" };

ObjectId surfId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetSurfaceIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
    if (s != null && s.Name.Equals(surfName, StringComparison.OrdinalIgnoreCase)) { surfId = id; break; }
}
if (surfId.IsNull) return new { error = "Surface not found" };

var profileId = Profile.CreateFromSurface(profileName, alignId, surfId, Database.Clayer, ObjectId.Null, ObjectId.Null);
var profile = Transaction.GetObject(profileId, OpenMode.ForRead) as Profile;

return new
{
    success = true,
    name = profile.Name,
    startStation = profile.StartingStation,
    endStation = profile.EndingStation,
    minElevation = profile.ElevationMin,
    maxElevation = profile.ElevationMax
};
```

## Usage Notes
- This is the "EG profile" step — run before `create_design_profile_from_pvis` for the proposed (FG) profile.
- Style/label-set are passed as ObjectId.Null (drawing defaults) — reassign per firm standard afterward.
