---
name: list_profiles
category: profiles
description: List every profile attached to an alignment (existing ground, design, offset) with station/elevation ranges
requires_write: false
parameters:
  - name: alignmentName
    type: string
    required: true
---

## Code Template

```csharp
string alignName = "ALIGNMENT_NAME";

Alignment alignment = null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals(alignName, StringComparison.OrdinalIgnoreCase)) { alignment = a; break; }
}
if (alignment == null) return new { error = "Alignment not found" };

var profiles = new List<object>();
foreach (ObjectId profId in alignment.GetProfileIds())
{
    var p = Transaction.GetObject(profId, OpenMode.ForRead) as Profile;
    if (p == null) continue;
    profiles.Add(new
    {
        name = p.Name,
        handle = p.Handle.ToString(),
        type = p.ProfileType.ToString(),
        startStation = p.StartingStation,
        endStation = p.EndingStation,
        minElevation = p.ElevationMin,
        maxElevation = p.ElevationMax,
        pviCount = p.PVIs.Count
    });
}

return new { alignmentName = alignment.Name, count = profiles.Count, profiles };
```

## Usage Notes
- `ProfileType` distinguishes surface-sampled EG profiles from layout (design) profiles.
- Use `pviCount` to tell layout profiles (which have PVIs) apart from surface profiles (which typically don't expose editable PVIs).
