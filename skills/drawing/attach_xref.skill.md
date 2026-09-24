---
name: attach_xref
category: drawing
description: Attach an external DWG as an xref at a given insertion point, scale, and layer
requires_write: true
parameters:
  - name: xrefPath
    type: string
    required: true
    description: Absolute path to the DWG to attach
  - name: layerName
    type: string
    required: false
    description: Layer to place the xref block reference on (default current layer)
  - name: insertX
    type: double
    required: false
  - name: insertY
    type: double
    required: false
---

## Code Template

```csharp
string xrefPath = @"C:\Projects\NEW_PROJECT_NAME\01-Drawings\Base.dwg";
string layerName = "";
double insertX = 0.0, insertY = 0.0;

if (!System.IO.File.Exists(xrefPath))
    return new { error = $"Xref file not found: {xrefPath}" };

var xrefId = Database.AttachXref(xrefPath, System.IO.Path.GetFileNameWithoutExtension(xrefPath));

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForWrite);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

var xrefRef = new BlockReference(new Point3d(insertX, insertY, 0), xrefId);
if (!string.IsNullOrWhiteSpace(layerName))
{
    var layerTable = Transaction.GetObject(Database.LayerTableId, OpenMode.ForRead) as LayerTable;
    if (!layerTable.Has(layerName))
    {
        layerTable.UpgradeOpen();
        var newLayer = new LayerTableRecord { Name = layerName };
        layerTable.Add(newLayer);
        Transaction.AddNewlyCreatedDBObject(newLayer, true);
    }
    xrefRef.Layer = layerName;
}

btr.AppendEntity(xrefRef);
Transaction.AddNewlyCreatedDBObject(xrefRef, true);

return new
{
    success = true,
    xrefPath,
    blockName = System.IO.Path.GetFileNameWithoutExtension(xrefPath),
    handle = xrefRef.Handle.ToString(),
    insertPoint = new { x = insertX, y = insertY }
};
```

## Usage Notes
- `Database.AttachXref` returns the block table record ObjectId for the xref definition; a `BlockReference` is still needed to actually place it in model/paper space.
- If the same file is already attached, `AttachXref` reuses the existing xref definition rather than creating a duplicate.
- Reload with `Database.ReloadXrefs` (not templated here) if the source file changes after attachment; check status with `drawing_health_check`.
