---
name: parcel_area_table
category: parcels
description: Report area, perimeter, and number for every parcel in a site
requires_write: false
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

var rows = new List<object>();
double totalArea = 0;

foreach (ObjectId parcelId in site.GetParcelIds())
{
    var parcel = Transaction.GetObject(parcelId, OpenMode.ForRead) as Parcel;
    if (parcel == null) continue;

    double areaSqFt = parcel.Area; // inherited from the underlying AutoCAD Curve
    double areaAcres = areaSqFt / 43560.0;
    totalArea += areaSqFt;

    rows.Add(new
    {
        number = parcel.Number,
        name = parcel.Name,
        area_sqft = Math.Round(areaSqFt, 1),
        area_acres = Math.Round(areaAcres, 4),
        perimeter_ft = Math.Round(parcel.GetDistanceAtParameter(parcel.EndParam), 1)
    });
}

return new
{
    siteName = site.Name,
    parcelCount = rows.Count,
    totalArea_acres = Math.Round(totalArea / 43560.0, 3),
    parcels = rows
};
```

## Usage Notes
- `Parcel.Area` and perimeter come from Civil 3D's `Parcel` inheriting the AutoCAD `Curve` object model (parcels are closed boundary curves) — units follow the drawing's linear unit setting (assumed feet here).
- `GetDistanceAtParameter(EndParam)` returns total curve length for a closed curve, i.e. perimeter — verify this reads correctly for parcels with curved (non-polyline) segments.
