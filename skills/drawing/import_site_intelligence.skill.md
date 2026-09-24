---
name: import_site_intelligence
category: drawing
description: One-message import of a Gradon Site Intelligence export folder -- reads layers.json, inserts export.dxf, extrudes buildings if their GeoJSON is present, then zooms to extents
requires_write: true
parameters:
  - name: folder
    type: string
    required: true
    description: Absolute path to the Site Intelligence export folder (contains layers.json, export.dxf, and per-layer *.geojson files)
  - name: buildingsGeojsonFileName
    type: string
    required: false
    description: File name of the buildings GeoJSON inside the folder (default "C-BLDG.geojson")
---

## Code Template

```csharp
using System.Text.Json;

string folder = @"C:\Projects\NEW_PROJECT_NAME\GradonSiteIntelligence";
string buildingsGeojsonFileName = "C-BLDG.geojson";

if (!System.IO.Directory.Exists(folder))
    return new { error = $"Folder not found: {folder}" };

string manifestPath = System.IO.Path.Combine(folder, "layers.json");
if (!System.IO.File.Exists(manifestPath))
    return new { error = $"layers.json not found in {folder}" };

JsonDocument manifestDoc;
try { manifestDoc = JsonDocument.Parse(System.IO.File.ReadAllText(manifestPath)); }
catch (System.Exception ex) { return new { error = $"Could not parse layers.json: {ex.Message}" }; }

var manifestRoot = manifestDoc.RootElement;
string crsName = null;
if (manifestRoot.TryGetProperty("aoi", out var aoiEl) && aoiEl.TryGetProperty("crs_name", out var crsEl))
    crsName = crsEl.GetString();

var layerSummary = new List<object>();
if (manifestRoot.TryGetProperty("layers", out var layersEl))
{
    foreach (var layerEl in layersEl.EnumerateArray())
    {
        layerSummary.Add(new
        {
            name = layerEl.TryGetProperty("name", out var n) ? n.GetString() : null,
            dxfLayer = layerEl.TryGetProperty("dxf_layer", out var dl) ? dl.GetString() : null,
            source = layerEl.TryGetProperty("source", out var s) ? s.GetString() : null,
            vintage = layerEl.TryGetProperty("vintage", out var v) ? v.GetString() : null,
            featureCount = layerEl.TryGetProperty("feature_count", out var fc) ? (int?)fc.GetInt32() : null,
            areaSf = layerEl.TryGetProperty("area_sf", out var a) ? (double?)a.GetDouble() : null
        });
    }
}

// ---- Step 1: DXF insert-and-explode (inlined from import_site_intelligence_dxf; skills cannot call skills) ----
object dxfResult;
string dxfPath = System.IO.Path.Combine(folder, "export.dxf");
if (!System.IO.File.Exists(dxfPath))
{
    dxfResult = new { error = $"export.dxf not found in {folder}" };
}
else
{
    var ncsLayerColors = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
    {
        ["C-BLDG"] = 8, ["C-ROAD-CNTR"] = 9, ["C-ROAD-SURF"] = 250, ["C-SWLK"] = 251,
        ["C-XWLK"] = 252, ["C-BIKE"] = 253, ["C-PRKG"] = 254, ["C-DRWY"] = 8,
        ["C-WATR"] = 9, ["C-VEG-CNPY"] = 250, ["C-VEG-TURF"] = 251, ["C-IMPV"] = 252,
        ["C-LAND-NLCD"] = 253, ["C-ADDR"] = 254
    };

    var layerTable = Transaction.GetObject(Database.LayerTableId, OpenMode.ForWrite) as LayerTable;
    var layersCreated = new List<string>();
    foreach (var kv in ncsLayerColors)
    {
        if (layerTable.Has(kv.Key)) continue;
        var newLayer = new LayerTableRecord { Name = kv.Key };
        newLayer.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, kv.Value);
        layerTable.Add(newLayer);
        Transaction.AddNewlyCreatedDBObject(newLayer, true);
        layersCreated.Add(kv.Key);
    }

    var sideDb = new Database(false, true);
    try
    {
        sideDb.DxfIn(dxfPath, null);

        string blockName = "GRADON_SI_" + DateTime.Now.Ticks;
        var btrId = Database.Insert(blockName, sideDb, false);

        var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
        var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        var blockRef = new BlockReference(new Point3d(0, 0, 0), btrId);
        btr.AppendEntity(blockRef);
        Transaction.AddNewlyCreatedDBObject(blockRef, true);

        var exploded = new DBObjectCollection();
        blockRef.Explode(exploded);

        var entityCountByLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj in exploded)
        {
            if (obj is Autodesk.AutoCAD.DatabaseServices.Entity ent)
            {
                btr.AppendEntity(ent);
                Transaction.AddNewlyCreatedDBObject(ent, true);
                entityCountByLayer.TryGetValue(ent.Layer, out int c);
                entityCountByLayer[ent.Layer] = c + 1;
            }
            else { obj.Dispose(); }
        }

        blockRef.Erase();
        string purgeError = null;
        try { (Transaction.GetObject(btrId, OpenMode.ForWrite) as BlockTableRecord).Erase(); }
        catch (System.Exception ex) { purgeError = ex.Message; }

        dxfResult = new { success = true, dxfPath, layersCreated, entityCountByLayer, purgeError };
    }
    catch (System.Exception ex)
    {
        dxfResult = new { error = $"DXF import failed: {ex.Message}" };
    }
    finally { sideDb.Dispose(); }
}

// ---- Step 2: extrude buildings if the GeoJSON is present (inlined from extrude_buildings_from_geojson) ----
object buildingsResult = null;
string buildingsPath = System.IO.Path.Combine(folder, buildingsGeojsonFileName);
if (System.IO.File.Exists(buildingsPath))
{
    try
    {
        string bLayerName = "C-BLDG-3D";
        JsonDocument bDoc = JsonDocument.Parse(System.IO.File.ReadAllText(buildingsPath));

        var layerTable2 = Transaction.GetObject(Database.LayerTableId, OpenMode.ForWrite) as LayerTable;
        if (!layerTable2.Has(bLayerName))
        {
            var newLayer2 = new LayerTableRecord { Name = bLayerName };
            newLayer2.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, (short)8);
            layerTable2.Add(newLayer2);
            Transaction.AddNewlyCreatedDBObject(newLayer2, true);
        }

        var bt2 = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
        var btr2 = (BlockTableRecord)Transaction.GetObject(bt2[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

        int extrudedCount = 0;
        var skipped = new List<object>();
        var extrudeErrors = new List<object>();

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
            catch (System.Exception ex) { poly.Dispose(); extrudeErrors.Add(new { featureId, error = ex.Message }); return; }

            if (regions.Count == 0) { poly.Dispose(); extrudeErrors.Add(new { featureId, error = "No region produced" }); return; }

            var region = regions[0] as Region;
            for (int i = 1; i < regions.Count; i++) regions[i]?.Dispose();

            var solid = new Solid3d();
            try { solid.Extrude(region, heightFt, 0.0); }
            catch (System.Exception ex)
            {
                solid.Dispose(); region.Dispose(); poly.Dispose();
                extrudeErrors.Add(new { featureId, error = ex.Message });
                return;
            }

            solid.Layer = bLayerName;
            btr2.AppendEntity(solid);
            Transaction.AddNewlyCreatedDBObject(solid, true);
            region.Dispose();
            poly.Dispose();
            extrudedCount++;
        }

        var bRoot = bDoc.RootElement;
        string bRootType = bRoot.TryGetProperty("type", out var bRootTypeEl) ? bRootTypeEl.GetString() : null;
        var features = bRootType == "FeatureCollection" && bRoot.TryGetProperty("features", out var fsEl)
            ? fsEl.EnumerateArray().ToList()
            : (bRootType == "Feature" ? new List<JsonElement> { bRoot } : new List<JsonElement>());

        int featureIndex = 0;
        foreach (var feature in features)
        {
            string featureId = feature.TryGetProperty("id", out var idEl) ? idEl.ToString() : featureIndex.ToString();
            featureIndex++;
            try
            {
                if (!feature.TryGetProperty("geometry", out var geom) || geom.ValueKind == JsonValueKind.Null)
                { skipped.Add(new { featureId, reason = "No geometry" }); continue; }
                if (!feature.TryGetProperty("properties", out var props) ||
                    !props.TryGetProperty("height_ft", out var heightEl) || heightEl.ValueKind == JsonValueKind.Null)
                { skipped.Add(new { featureId, reason = "Missing height_ft property" }); continue; }

                double heightFt = heightEl.GetDouble();
                if (heightFt <= 0) { skipped.Add(new { featureId, reason = $"Non-positive height_ft ({heightFt})" }); continue; }

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
                else { skipped.Add(new { featureId, reason = $"Unsupported geometry type: {geomType}" }); }
            }
            catch (System.Exception ex) { extrudeErrors.Add(new { featureId, error = ex.Message }); }
        }

        buildingsResult = new { success = true, buildingsPath, extrudedCount, skipped, errors = extrudeErrors };
    }
    catch (System.Exception ex)
    {
        buildingsResult = new { error = $"Buildings extrusion failed: {ex.Message}" };
    }
}

// ---- Step 3: zoom to extents ----
string zoomError = null;
try { Editor.Command("_.ZOOM", "_Extents"); }
catch (System.Exception ex) { zoomError = ex.Message; }

return new
{
    folder,
    crsName,
    layers = layerSummary,
    dxf = dxfResult,
    buildings = buildingsResult,
    zoomError
};
```

## Usage Notes
- Composes `import_site_intelligence_dxf` (insert-explode mode only, not the xref option) and `extrude_buildings_from_geojson` by inlining their logic per this library's rule that a skill script cannot call another skill script -- same approach `import_gradon_design` uses.
- Assumes the per-layer GeoJSON files are named `<dxf_layer>.geojson` (e.g. `C-BLDG.geojson`) inside `folder`; this filename convention is inferred from the DXF layer names in `layers.json`, not independently confirmed against the Site Intelligence service -- adjust `buildingsGeojsonFileName` if the service names files differently.
- `buildings` stays `null` when no buildings GeoJSON file is found at `buildingsGeojsonFileName` -- that is not treated as an error, since a folder may only contain the DXF.
- `layers` in the return value is the manifest's own metadata (source/vintage/feature_count/area_sf per layer) passed through as-is for a per-layer provenance summary; it is not re-derived from what actually got imported into the drawing -- cross-check `dxf.entityCountByLayer` against `layers[].featureCount` if a mismatch needs investigating.
- `zoomError` is non-fatal, matching `import_gradon_design`'s `Editor.Command("_.ZOOM", "_Extents")` fallback note.
- Runs inside the plugin's single existing `Transaction`, so a mid-way failure (e.g. the DXF imports but the buildings GeoJSON is malformed) still leaves the DXF import committed when the host closes the transaction -- check `dxf.error`/`buildings.error` rather than assuming all-or-nothing.
