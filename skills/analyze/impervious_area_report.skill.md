---
name: impervious_area_report
category: analyze
description: Sum closed-polyline area per NCS impervious-surface layer (C-BLDG, C-ROAD-SURF, C-PRKG, C-DRWY, C-SWLK, C-IMPV) and report impervious percent of a boundary
requires_write: false
parameters:
  - name: boundaryHandle
    type: string
    required: false
    description: Hex handle of the boundary polyline to compute percent-impervious against; if omitted, the first closed curve found on layer C-PROP is used
---

## Code Template

```csharp
string boundaryHandle = "";

var imperviousLayers = new[] { "C-BLDG", "C-ROAD-SURF", "C-PRKG", "C-DRWY", "C-SWLK", "C-IMPV" };

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

double SumClosedAreaOnLayer(string layerName, out int closedCount, out int skippedOpenCount)
{
    double total = 0.0;
    closedCount = 0;
    skippedOpenCount = 0;
    foreach (ObjectId id in btr)
    {
        var ent = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
        if (ent == null || !ent.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase)) continue;
        if (ent is Curve curve)
        {
            if (!curve.Closed) { skippedOpenCount++; continue; }
            try { total += curve.Area; closedCount++; }
            catch (System.Exception) { /* non-planar or otherwise unmeasurable curve */ }
        }
    }
    return total;
}

var table = new List<object>();
double totalImperviousAreaSf = 0.0;
foreach (var layerName in imperviousLayers)
{
    double areaSf = SumClosedAreaOnLayer(layerName, out int closedCount, out int skippedOpenCount);
    totalImperviousAreaSf += areaSf;
    table.Add(new { layer = layerName, areaSf, closedEntityCount = closedCount, skippedOpenEntityCount = skippedOpenCount });
}

// ---- Resolve the boundary ----
Curve boundaryCurve = null;
string boundarySource;

if (!string.IsNullOrWhiteSpace(boundaryHandle))
{
    long handleValue;
    try { handleValue = Convert.ToInt64(boundaryHandle, 16); }
    catch { return new { error = $"'{boundaryHandle}' is not a valid hex handle" }; }

    if (!Database.TryGetObjectId(new Handle(handleValue), out ObjectId boundaryId))
        return new { error = $"No object found with handle {boundaryHandle}" };

    boundaryCurve = Transaction.GetObject(boundaryId, OpenMode.ForRead) as Curve;
    if (boundaryCurve == null)
        return new { error = $"Object at handle {boundaryHandle} is not a curve" };
    if (!boundaryCurve.Closed)
        return new { error = $"Object at handle {boundaryHandle} is not a closed curve" };

    boundarySource = $"handle {boundaryHandle}";
}
else
{
    foreach (ObjectId id in btr)
    {
        var ent = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
        if (ent == null || !ent.Layer.Equals("C-PROP", StringComparison.OrdinalIgnoreCase)) continue;
        if (ent is Curve curve && curve.Closed) { boundaryCurve = curve; break; }
    }
    if (boundaryCurve == null)
        return new { error = "No boundaryHandle given and no closed curve found on layer C-PROP", table, totalImperviousAreaSf };

    boundarySource = "layer C-PROP";
}

double boundaryAreaSf = boundaryCurve.Area;
double imperviousPercent = boundaryAreaSf > 0 ? (totalImperviousAreaSf / boundaryAreaSf) * 100.0 : 0.0;

return new
{
    success = true,
    table,
    totalImperviousAreaSf,
    boundarySource,
    boundaryHandle = boundaryCurve.Handle.ToString(),
    boundaryAreaSf,
    imperviousPercent
};
```

## Usage Notes
- Only model space is scanned (same scope limit as `select_by_layer`); an impervious layer's geometry inside paper space or a nested block is not counted.
- `Curve.Area` (the base `Curve` property, not something `Polyline`-specific) is used so any closed curve type on these layers — polyline, circle, ellipse — contributes correctly, not just `Polyline`.
- An open curve on one of the impervious layers is skipped and counted in `skippedOpenEntityCount` rather than erroring — e.g. a centerline mistakenly left on `C-ROAD-SURF` — so check that field if `areaSf` looks low for a layer.
- `totalImperviousAreaSf` simply sums the six layers; overlapping geometry (e.g. a driveway polyline drawn on top of `C-IMPV`) is not deduplicated, so avoid double-digitizing the same footprint across two of these layers.
- With no `boundaryHandle`, the first closed curve found on `C-PROP` wins — if a site has multiple closed curves on that layer (parcel plus an easement, say), pass an explicit `boundaryHandle` from `select_by_layer("C-PROP")` instead of relying on scan order.
