---
name: create_parcels_from_closed_polylines
category: parcels
description: Create parcels (lots) from one or more closed polylines in a site
requires_write: true
parameters:
  - name: siteName
    type: string
    required: true
    description: Existing site name to create the parcels in
  - name: polylineHandles
    type: string
    required: true
    description: Comma-separated handles of closed polylines defining lot boundaries
---

## Code Template

```csharp
string siteName = "SITE_NAME";
string handlesCsv = "HANDLE1,HANDLE2";

ObjectId siteId = ObjectId.Null;
foreach (ObjectId id in CivilDoc.GetSiteIds())
{
    var s = Transaction.GetObject(id, OpenMode.ForRead) as Site;
    if (s != null && s.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase)) { siteId = id; break; }
}
if (siteId.IsNull) return new { error = "Site not found" };

int before = (Transaction.GetObject(siteId, OpenMode.ForRead) as Site).GetParcelIds().Count;

// Parcel creation-from-objects has no direct managed-API factory in this package version —
// it runs through the command line, matching Civil 3D's own "Create Parcel from Objects" tool.
var handleList = handlesCsv.Split(',').Select(s => s.Trim());
foreach (var h in handleList)
{
    long handleValue = Convert.ToInt64(h, 16);
    if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId plineId)) continue;
    Editor.Command("_AeccParcelCreateFromObjects", plineId, "", "_Y");
}

var siteAfter = Transaction.GetObject(siteId, OpenMode.ForRead) as Site;
int after = siteAfter.GetParcelIds().Count;

return new { success = true, siteName, parcelsBefore = before, parcelsAfter = after, parcelsCreated = after - before };
```

## Usage Notes
- **Command-based**: `Parcel` has no public static `Create` in the referenced managed API — this uses the `_AeccParcelCreateFromObjects` command (Civil 3D's "Create Parcel from Objects" tool) inside the script's command context.
- The command's exact prompt sequence (style selection, area label placement, automatic numbering) can vary by version/site defaults — test interactively once and adjust the argument list if it doesn't complete cleanly.
- Polylines should be closed and non-self-intersecting.
