---
name: delete_parcel_segments
category: parcels
description: Delete a parcel (removing its boundary line/lot from the site) by parcel number
requires_write: true
parameters:
  - name: siteName
    type: string
    required: true
  - name: parcelNumber
    type: int
    required: true
---

## Code Template

```csharp
string siteName = "SITE_NAME";
int parcelNumber = 1;

Site site = null;
foreach (ObjectId id in CivilDoc.GetSiteIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Site;
    if (s != null && s.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase)) { site = s; break; }
}
if (site == null) return new { error = "Site not found" };

foreach (ObjectId parcelId in site.GetParcelIds())
{
    var parcel = Transaction.GetObject(parcelId, OpenMode.ForWrite) as Parcel;
    if (parcel != null && parcel.Number == parcelNumber)
    {
        parcel.Erase();
        return new { success = true, siteName = site.Name, deletedParcelNumber = parcelNumber };
    }
}

return new { error = $"Parcel #{parcelNumber} not found in site '{siteName}'" };
```

## Usage Notes
- Deleting a parcel removes its boundary segments from the parcel topology — shared lot lines with adjacent parcels may need to be redrawn/recreated depending on how the parcel fabric was built.
- Parcel numbers are only unique within a site — always pass both `siteName` and `parcelNumber`.
