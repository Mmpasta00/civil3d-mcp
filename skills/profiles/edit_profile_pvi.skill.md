---
name: edit_profile_pvi
category: profiles
description: Move or delete a single PVI on an existing layout profile, or insert a new one
requires_write: true
parameters:
  - name: alignmentName
    type: string
    required: true
  - name: profileName
    type: string
    required: true
  - name: action
    type: string
    required: true
    description: "add | remove | move"
  - name: station
    type: double
    required: true
  - name: elevation
    type: double
    required: false
    description: Required for add/move
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";
string profileName = "PROFILE_NAME";
string action = "move"; // add | remove | move
double station = 500.0;   // Replace
double elevation = 106.0; // Replace (add/move)

Profile profile = null;
ObjectId alignId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignId = id; break; }
}
if (alignId.IsNull) return new { error = "Alignment not found" };

var align = Transaction.GetObject(alignId, OpenMode.ForRead) as Alignment;
foreach (ObjectId profId in align.GetProfileIds())
{
    var p = Transaction.GetObject(profId, OpenMode.ForWrite) as Profile;
    if (p != null && p.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase)) { profile = p; break; }
}
if (profile == null) return new { error = "Profile not found" };

switch (action.ToLowerInvariant())
{
    case "add":
        profile.PVIs.AddPVI(station, elevation);
        break;
    case "remove":
        profile.PVIs.RemoveAt(station, elevation);
        break;
    case "move":
        // Civil 3D PVIs are located by their current station/elevation, so remove + re-add
        var existing = profile.PVIs.GetPVIAt(station, elevation);
        if (existing == null) return new { error = "No PVI found at that exact station/elevation — pass the current values" };
        profile.PVIs.Remove(existing);
        profile.PVIs.AddPVI(station, elevation);
        break;
    default:
        return new { error = "action must be add, remove, or move" };
}

return new { success = true, profileName = profile.Name, pviCount = profile.PVIs.Count, action };
```

## Usage Notes
- `move` in this API is really remove-then-add at a new station/elevation since PVIs are keyed by position, not index.
- `GetPVIAt(station, elevation)` requires an exact match — use `alignment_geometry_report`/profile inspection to get current values first.
- Vertical curve data (radius/length) is lost on remove — re-add with `AddPVISymParabola`/`AddPVIArc` if the point needs a curve.
