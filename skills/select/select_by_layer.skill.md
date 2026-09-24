---
name: select_by_layer
category: select
description: Get every entity's handle and type on a given layer — the standard way to find object handles for other skills that need one
requires_write: false
parameters:
  - name: layerName
    type: string
    required: true
---

## Code Template

```csharp
string layerName = "LAYER_NAME";

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

var results = new List<object>();
foreach (ObjectId id in btr)
{
    var ent = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
    if (ent == null || !ent.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase)) continue;

    results.Add(new
    {
        handle = ent.Handle.ToString(),
        type = ent.GetType().Name,
        color = ent.Color.ToString()
    });
}

return new { layerName, count = results.Count, entities = results };
```

## Usage Notes
- Handles returned here (e.g. "2A4") are the `polylineHandle`/`polylineHandles` inputs several other skills expect (`create_alignment_from_polyline`, `add_breaklines_from_polylines`, etc.).
- Only scans model space — objects on the layer but inside paper space or nested blocks aren't listed.
