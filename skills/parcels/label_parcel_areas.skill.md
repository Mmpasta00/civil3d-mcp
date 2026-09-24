---
name: label_parcel_areas
category: parcels
description: Add or reset the area label for every parcel in a site
requires_write: true
parameters:
  - name: siteName
    type: string
    required: true
---

## Code Template

```csharp
string siteName = "SITE_NAME";

Site site = null;
foreach (ObjectId id in CivilDoc.GetSiteIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Site;
    if (s != null && s.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase)) { site = s; break; }
}
if (site == null) return new { error = "Site not found" };

int labeled = 0;
foreach (ObjectId parcelId in site.GetParcelIds())
{
    var parcel = Transaction.GetObject(parcelId, OpenMode.ForWrite) as Parcel;
    if (parcel == null) continue;
    try
    {
        parcel.ResetAreaSelectionLabel();
        labeled++;
    }
    catch { /* parcel may not support an area label with its current style — skip */ }
}

return new { siteName = site.Name, parcelsLabeled = labeled };
```

## Usage Notes
- `ResetAreaSelectionLabel()` regenerates the parcel's area label at its default location using the parcel style's configured area label style — it does not create a NEW label style, only re-applies the existing one.
- If a parcel's style has no area label style configured, this is a no-op for that parcel (caught and skipped).
- For a text-based area schedule instead of in-drawing labels, use `parcel_area_table`.
