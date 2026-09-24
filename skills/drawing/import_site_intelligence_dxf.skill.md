---
name: import_site_intelligence_dxf
category: drawing
description: Insert a Gradon Site Intelligence DXF export into the current drawing (block-insert-and-explode, or as an xref), creating the NCS layers with a monochrome scheme if missing
requires_write: true
parameters:
  - name: dxfPath
    type: string
    required: true
    description: Absolute path to the Site Intelligence export.dxf
  - name: asXref
    type: boolean
    required: false
    description: If true, stage the DXF as a DWG next to it and attach that as an xref instead of inserting/exploding (default false)
  - name: insertX
    type: double
    required: false
  - name: insertY
    type: double
    required: false
---

## Code Template

```csharp
string dxfPath = @"C:\Projects\NEW_PROJECT_NAME\GradonSiteIntelligence\export.dxf";
bool asXref = false;
double insertX = 0.0, insertY = 0.0;

if (!System.IO.File.Exists(dxfPath))
    return new { error = $"DXF file not found: {dxfPath}" };

// Monochrome NCS layer palette for every layer the Site Intelligence service can export.
// Grayscale ACI indices only (8, 9, 250-254) -- no color-coding across categories.
var ncsLayerColors = new Dictionary<string, short>(StringComparer.OrdinalIgnoreCase)
{
    ["C-BLDG"] = 8,
    ["C-ROAD-CNTR"] = 9,
    ["C-ROAD-SURF"] = 250,
    ["C-SWLK"] = 251,
    ["C-XWLK"] = 252,
    ["C-BIKE"] = 253,
    ["C-PRKG"] = 254,
    ["C-DRWY"] = 8,
    ["C-WATR"] = 9,
    ["C-VEG-CNPY"] = 250,
    ["C-VEG-TURF"] = 251,
    ["C-IMPV"] = 252,
    ["C-LAND-NLCD"] = 253,
    ["C-ADDR"] = 254
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

if (asXref)
{
    // AutoCAD cannot xref a DXF directly -- read it into a side database, save that out as a
    // DWG next to the source file, then attach the staged DWG the normal way (see attach_xref).
    var sideDb0 = new Database(false, true);
    try { sideDb0.DxfIn(dxfPath, null); }
    catch (System.Exception ex) { sideDb0.Dispose(); return new { error = $"DXF read failed: {ex.Message}" }; }

    string stagedDwgPath = System.IO.Path.ChangeExtension(dxfPath, ".dwg");
    try { sideDb0.SaveAs(stagedDwgPath, DwgVersion.Current); }
    catch (System.Exception ex) { sideDb0.Dispose(); return new { error = $"Staging DWG save failed: {ex.Message}" }; }
    sideDb0.Dispose();

    var xrefId = Database.AttachXref(stagedDwgPath, System.IO.Path.GetFileNameWithoutExtension(stagedDwgPath));

    var bt0 = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForWrite);
    var btr0 = (BlockTableRecord)Transaction.GetObject(bt0[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
    var xrefRef = new BlockReference(new Point3d(insertX, insertY, 0), xrefId);
    btr0.AppendEntity(xrefRef);
    Transaction.AddNewlyCreatedDBObject(xrefRef, true);

    return new
    {
        success = true,
        mode = "xref",
        dxfPath,
        stagedDwgPath,
        xrefHandle = xrefRef.Handle.ToString(),
        layersCreated
    };
}

// ---- Insert-as-block, explode into model space, then purge the temp block definition ----
var sideDb = new Database(false, true);
try { sideDb.DxfIn(dxfPath, null); }
catch (System.Exception ex) { sideDb.Dispose(); return new { error = $"DXF read failed: {ex.Message}" }; }

string blockName = "GRADON_SI_" + DateTime.Now.Ticks;
ObjectId btrId;
try { btrId = Database.Insert(blockName, sideDb, false); }
catch (System.Exception ex) { sideDb.Dispose(); return new { error = $"Block insert from DXF failed: {ex.Message}" }; }
finally { sideDb.Dispose(); }

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

var blockRef = new BlockReference(new Point3d(insertX, insertY, 0), btrId);
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
    else
    {
        obj.Dispose();
    }
}

blockRef.Erase();

string purgeError = null;
try
{
    var blockDef = Transaction.GetObject(btrId, OpenMode.ForWrite) as BlockTableRecord;
    blockDef.Erase();
}
catch (System.Exception ex) { purgeError = ex.Message; }

return new
{
    success = true,
    mode = "insert-explode",
    dxfPath,
    layersCreated,
    entityCountByLayer,
    purgeError
};
```

## Usage Notes
- `Database.DxfIn` (not `ReadDxfFile` -- that member does not exist on the real API) reads a DXF straight into a side `Database`; the log-filename argument can be `null`.
- The insert/explode path uses `Database.Insert(blockName, sideDb, false)` to clone the whole side database in as one block definition (mirrors `create_layout_from_template`'s `WblockCloneObjects` side-database pattern but for a full-drawing DXF rather than a single layout), places one `BlockReference`, explodes it with `Entity.Explode(DBObjectCollection)`, appends each resulting entity to model space itself (`Explode` only produces detached copies -- it does not add them to the database), then erases the reference and the now-unused block definition. `purgeError` is non-fatal; the imported geometry is already in the drawing either way.
- The xref path cannot attach the DXF directly (AutoCAD xrefs require DWG) -- it stages a same-named `.dwg` next to the source file via `Database.SaveAs(path, DwgVersion.Current)` and attaches that, same as `attach_xref`. Re-running with `asXref: true` after the source DXF changes will silently reuse a stale staged DWG unless it is deleted first.
- Layer colors are grayscale ACI indices only (8, 9, 250-254) per the monochrome requirement; unmapped layers present in the DXF (e.g. a future NCS code not in this table) still import correctly, they just keep whatever layer/color the DXF itself defines.
- Run `qa/drawing_health_check` afterward if the source DXF might carry duplicate/foreign styles; the block-insert path can pull in nested block definitions and text/dimension styles from the export the same way any DWG/DXF insert would.
