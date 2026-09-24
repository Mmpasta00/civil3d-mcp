---
name: delete_by_layer
category: delete
description: Erase every entity on a given layer
requires_write: true
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

var toErase = new List<ObjectId>();
foreach (ObjectId id in btr)
{
    var ent = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
    if (ent != null && ent.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
        toErase.Add(id);
}

int erased = 0;
foreach (var id in toErase)
{
    var ent = Transaction.GetObject(id, OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Entity;
    ent.Erase();
    erased++;
}

return new { success = true, layerName, erasedCount = erased };
```

## Usage Notes
- Erases entities only — the layer itself remains defined (now empty). Use `purge_and_audit` afterward if the empty layer should also be removed.
- Civil 3D objects (surfaces, alignments, pipes) can sit on a layer too — this will erase them the same as plain AutoCAD entities, so confirm the layer isn't a shared Civil 3D object layer before running.
- `requires_write: true` — always confirm scope with the engineer before a bulk layer erase.
