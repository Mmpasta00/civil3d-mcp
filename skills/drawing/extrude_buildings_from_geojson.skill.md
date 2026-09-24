---
name: extrude_buildings_from_geojson
category: drawing
description: Create 3D solids by extruding each building footprint in a GeoJSON file by its height_ft property, on layer C-BLDG-3D
requires_write: true
parameters:
  - name: geojsonPath
    type: string
    required: true
    description: Absolute path to the buildings GeoJSON (Polygon/MultiPolygon features with a height_ft property, project-ft coordinates)
  - name: layerName
    type: string
    required: false
    description: Layer for the extruded solids (default "C-BLDG-3D")
---

## Code Template

```csharp
using System.Text.Json;

string geojsonPath = @"C:\Projects\NEW_PROJECT_NAME\GradonSiteIntelligence\C-BLDG.geojson";
string layerName = "C-BLDG-3D";

if (!System.IO.File.Exists(geojsonPath))
    return new { error = $"GeoJSON file not found: {geojsonPath}" };

JsonDocument doc;
try { doc = JsonDocument.Parse(System.IO.File.ReadAllText(geojsonPath)); }
catch (System.Exception ex) { return new { error = $"Could not parse GeoJSON: {ex.Message}" }; }

var layerTable = Transaction.GetObject(Database.LayerTableId, OpenMode.ForWrite) as LayerTable;
if (!layerTable.Has(layerName))
{
    var newLayer = new LayerTableRecord { Name = layerName };
    newLayer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)8);
    layerTable.Add(newLayer);
    Transaction.AddNewlyCreatedDBObject(newLayer, true);
}

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

int extrudedCount = 0;
var skipped = new List<object>();
var errors = new List<object>();

List<Point2d> ReadExteriorRing(JsonElement polygon)
{
    var exteriorRing = polygon.EnumerateArray().First();
    var pts = new List<Point2d>();
    foreach (var coord in exteriorRing.EnumerateArray())
    {
        var arr = coord.EnumerateArray().ToList();
        pts.Add(new Point2d(arr[0].GetDouble(), arr[1].GetDouble()));
    }
    return pts;
}

void ExtrudeFootprint(List<Point2d> ring, double heightFt, string featureId)
{
    var poly = new Polyline();
    for (int i = 0; i < ring.Count; i++) poly.AddVertexAt(i, ring[i], 0, 0, 0);
    poly.Closed = true;

    var curves = new DBObjectCollection { poly };
    DBObjectCollection regions;
    try { regions = Region.CreateFromCurves(curves); }
    catch (System.Exception ex) { poly.Dispose(); errors.Add(new { featureId, error = $"Region.CreateFromCurves failed: {ex.Message}" }); return; }

    if (regions.Count == 0)
    {
        poly.Dispose();
        errors.Add(new { featureId, error = "No region produced from footprint (self-intersecting or degenerate ring?)" });
        return;
    }

    var region = regions[0] as Region;
    for (int i = 1; i < regions.Count; i++) regions[i]?.Dispose();

    var solid = new Solid3d();
    try { solid.Extrude(region, heightFt, 0.0); }
    catch (System.Exception ex)
    {
        solid.Dispose(); region.Dispose(); poly.Dispose();
        errors.Add(new { featureId, error = $"Extrude failed: {ex.Message}" });
        return;
    }

    solid.Layer = layerName;
    btr.AppendEntity(solid);
    Transaction.AddNewlyCreatedDBObject(solid, true);

    region.Dispose();
    poly.Dispose();
    extrudedCount++;
}

void ProcessFeature(JsonElement feature, string featureId)
{
    if (!feature.TryGetProperty("geometry", out var geom) || geom.ValueKind == JsonValueKind.Null)
    {
        skipped.Add(new { featureId, reason = "No geometry" });
        return;
    }
    if (!feature.TryGetProperty("properties", out var props) ||
        !props.TryGetProperty("height_ft", out var heightEl) ||
        heightEl.ValueKind == JsonValueKind.Null)
    {
        skipped.Add(new { featureId, reason = "Missing height_ft property" });
        return;
    }
    double heightFt = heightEl.GetDouble();
    if (heightFt <= 0)
    {
        skipped.Add(new { featureId, reason = $"Non-positive height_ft ({heightFt})" });
        return;
    }

    string geomType = geom.GetProperty("type").GetString();
    var coords = geom.GetProperty("coordinates");

    if (geomType == "Polygon")
    {
        ExtrudeFootprint(ReadExteriorRing(coords), heightFt, featureId);
    }
    else if (geomType == "MultiPolygon")
    {
        int partIndex = 0;
        foreach (var polygon in coords.EnumerateArray())
        {
            ExtrudeFootprint(ReadExteriorRing(polygon), heightFt, $"{featureId}[{partIndex}]");
            partIndex++;
        }
    }
    else
    {
        skipped.Add(new { featureId, reason = $"Unsupported geometry type for extrusion: {geomType}" });
    }
}

var root = doc.RootElement;
string rootType = root.TryGetProperty("type", out var rootTypeEl) ? rootTypeEl.GetString() : null;

if (rootType == "FeatureCollection" && root.TryGetProperty("features", out var featuresEl))
{
    int featureIndex = 0;
    foreach (var feature in featuresEl.EnumerateArray())
    {
        string featureId = feature.TryGetProperty("id", out var idEl) ? idEl.ToString() : featureIndex.ToString();
        try { ProcessFeature(feature, featureId); }
        catch (System.Exception ex) { errors.Add(new { featureId, error = ex.Message }); }
        featureIndex++;
    }
}
else if (rootType == "Feature")
{
    try { ProcessFeature(root, "0"); }
    catch (System.Exception ex) { errors.Add(new { featureId = "0", error = ex.Message }); }
}
else
{
    return new { error = $"Expected a FeatureCollection or Feature root with height_ft properties, got '{rootType}'" };
}

return new
{
    success = true,
    geojsonPath,
    layerName,
    extrudedCount,
    skipped,
    errors
};
```

## Usage Notes
- Only the exterior ring (`coordinates[0]`) of each `Polygon`/`MultiPolygon` part is used -- interior rings (courtyards/light wells) are not subtracted, so a donut-shaped footprint extrudes as a solid block. Use `create_polylines_from_geojson` first if the hole geometry itself needs to be preserved on paper.
- `Region.CreateFromCurves` and `Solid3d.Extrude(Region, height, taperAngle)` both take/produce transient (non-database-resident) objects here -- the working `Polyline` and `Region` are built in memory, never appended to model space, and explicitly `Dispose()`d once the solid exists; only the final `Solid3d` is added to the drawing.
- A feature with no `height_ft`, a null one, or `height_ft <= 0` is recorded in `skipped` with a reason rather than causing an error; `errors` is reserved for footprints that fail to region/extrude (self-intersecting rings, coincident points, etc.).
- `MultiPolygon` features are extruded per-part, each reported as `"<featureId>[<partIndex>]"` in `skipped`/`errors` so one bad part doesn't obscure which one of several footprints failed.
- Layer defaults to `C-BLDG-3D` (not the flat `C-BLDG` used by `import_site_intelligence_dxf`) so the 3D massing and the 2D footprint polylines can be frozen/thawed independently.
