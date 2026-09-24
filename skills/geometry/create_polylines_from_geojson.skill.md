---
name: create_polylines_from_geojson
category: geometry
description: Read a GeoJSON file in project-foot coordinates and create closed polylines for Polygon/MultiPolygon rings and open polylines for LineString/MultiLineString geometry, tagged with source XData
requires_write: true
parameters:
  - name: geojsonPath
    type: string
    required: true
    description: Absolute path to a GeoJSON file already in project-ft coordinates (e.g. a Site Intelligence per-layer export fetched with ?crs=project)
  - name: layerName
    type: string
    required: true
    description: Layer to place every created polyline on
  - name: xdataSource
    type: string
    required: false
    description: "Value stored in the GRADON_SI XData record's source field (e.g. \"NAIP 2024\")"
  - name: xdataVintage
    type: string
    required: false
    description: "Value stored in the GRADON_SI XData record's vintage field (e.g. \"2024\")"
---

## Code Template

```csharp
using System.Text.Json;

string geojsonPath = @"C:\Projects\NEW_PROJECT_NAME\GradonSiteIntelligence\C-BLDG.geojson";
string layerName = "C-BLDG";
string xdataSource = "";
string xdataVintage = "";
const string AppName = "GRADON_SI";

if (!System.IO.File.Exists(geojsonPath))
    return new { error = $"GeoJSON file not found: {geojsonPath}" };

JsonDocument doc;
try { doc = JsonDocument.Parse(System.IO.File.ReadAllText(geojsonPath)); }
catch (System.Exception ex) { return new { error = $"Could not parse GeoJSON: {ex.Message}" }; }

// Ensure the target layer exists.
var layerTable = Transaction.GetObject(Database.LayerTableId, OpenMode.ForWrite) as LayerTable;
if (!layerTable.Has(layerName))
{
    var newLayer = new LayerTableRecord { Name = layerName };
    layerTable.Add(newLayer);
    Transaction.AddNewlyCreatedDBObject(newLayer, true);
}

// Register the GRADON_SI application name so XData can be attached.
var regAppTable = Transaction.GetObject(Database.RegAppTableId, OpenMode.ForWrite) as RegAppTable;
if (!regAppTable.Has(AppName))
{
    var app = new RegAppTableRecord { Name = AppName };
    regAppTable.Add(app);
    Transaction.AddNewlyCreatedDBObject(app, true);
}
var xdata = new ResultBuffer(
    new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
    new TypedValue((int)DxfCode.ExtendedDataAsciiString, xdataSource ?? ""),
    new TypedValue((int)DxfCode.ExtendedDataAsciiString, xdataVintage ?? ""));

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

int polygonRingsCreated = 0;
int linesCreated = 0;
var errors = new List<object>();

Polyline AppendPolyline(IEnumerable<Point2d> pts, bool closed)
{
    var pl = new Polyline();
    int i = 0;
    foreach (var pt in pts) { pl.AddVertexAt(i, pt, 0, 0, 0); i++; }
    pl.Closed = closed;
    pl.Elevation = 0.0;
    pl.Layer = layerName;
    btr.AppendEntity(pl);
    Transaction.AddNewlyCreatedDBObject(pl, true);
    pl.XData = xdata;
    return pl;
}

List<Point2d> ReadRing(JsonElement ring)
{
    var pts = new List<Point2d>();
    foreach (var coord in ring.EnumerateArray())
    {
        var arr = coord.EnumerateArray().ToList();
        pts.Add(new Point2d(arr[0].GetDouble(), arr[1].GetDouble()));
    }
    return pts;
}

void ProcessGeometry(JsonElement geom)
{
    if (geom.ValueKind != JsonValueKind.Object || !geom.TryGetProperty("type", out var typeEl))
        return;

    string geomType = typeEl.GetString();
    var coords = geom.TryGetProperty("coordinates", out var coordsEl) ? coordsEl : default;

    switch (geomType)
    {
        case "Polygon":
            foreach (var ring in coords.EnumerateArray())
            {
                AppendPolyline(ReadRing(ring), true);
                polygonRingsCreated++;
            }
            break;

        case "MultiPolygon":
            foreach (var polygon in coords.EnumerateArray())
                foreach (var ring in polygon.EnumerateArray())
                {
                    AppendPolyline(ReadRing(ring), true);
                    polygonRingsCreated++;
                }
            break;

        case "LineString":
            AppendPolyline(ReadRing(coords), false);
            linesCreated++;
            break;

        case "MultiLineString":
            foreach (var line in coords.EnumerateArray())
            {
                AppendPolyline(ReadRing(line), false);
                linesCreated++;
            }
            break;

        case "GeometryCollection":
            if (geom.TryGetProperty("geometries", out var geomsEl))
                foreach (var g in geomsEl.EnumerateArray()) ProcessGeometry(g);
            break;

        default:
            errors.Add(new { error = $"Unsupported geometry type: {geomType}" });
            break;
    }
}

var root = doc.RootElement;
string rootType = root.TryGetProperty("type", out var rootTypeEl) ? rootTypeEl.GetString() : null;

if (rootType == "FeatureCollection" && root.TryGetProperty("features", out var featuresEl))
{
    int featureIndex = 0;
    foreach (var feature in featuresEl.EnumerateArray())
    {
        try
        {
            if (feature.TryGetProperty("geometry", out var geomEl) && geomEl.ValueKind != JsonValueKind.Null)
                ProcessGeometry(geomEl);
        }
        catch (System.Exception ex) { errors.Add(new { featureIndex, error = ex.Message }); }
        featureIndex++;
    }
}
else if (rootType == "Feature" && root.TryGetProperty("geometry", out var singleGeomEl))
{
    ProcessGeometry(singleGeomEl);
}
else
{
    ProcessGeometry(root);
}

return new
{
    success = true,
    geojsonPath,
    layerName,
    polygonRingsCreated,
    linesCreated,
    errors
};
```

## Usage Notes
- Accepts a `FeatureCollection`, a single `Feature`, or a bare geometry object as the file root -- covers both a whole-layer GeoJSON export and a single-feature clip.
- Every ring of a `Polygon`/`MultiPolygon` (exterior and any interior/hole rings) becomes its own closed polyline on `layerName` -- holes are not boolean-subtracted here, just represented as their own closed loop, so a hole-aware area or solid needs `Region.CreateFromCurves` with all rings passed together (see `extrude_buildings_from_geojson`, which only uses the first/exterior ring).
- `pl.Elevation = 0.0` is explicit even though it is `Polyline`'s default, per this skill's spec — geometry stays 2D; use `set_feature_line_elevations` or a 3D polyline skill if Z values are needed later.
- XData is attached under the `GRADON_SI` registered application as two `1000` (ASCII string) records — `source` then `vintage` — after `1001` (the app name); read them back with `DBObject.GetXDataForApplication("GRADON_SI")`.
- Coordinates are read as plain `[x, y, ...]` pairs and only the first two values are used — a GeoJSON `[x, y, z]` triple's z is ignored, consistent with "sets Elevation 0" in this skill's scope.
