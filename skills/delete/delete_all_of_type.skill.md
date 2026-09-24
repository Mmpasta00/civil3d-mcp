---
name: delete_all_of_type
category: delete
description: Erase every entity of a given type from model space (e.g. all point-block references, all leaders) — a targeted cleanup tool
requires_write: true
parameters:
  - name: typeName
    type: string
    required: true
    description: "Class name to match, e.g. Leader, MText, Circle (matches Entity.GetType().Name exactly)"
---

## Code Template

```csharp
string typeName = "TYPE_NAME_HERE"; // e.g. "Leader", "MText", "Circle"

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

var toErase = new List<ObjectId>();
foreach (ObjectId id in btr)
{
    var ent = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
    if (ent != null && ent.GetType().Name.Equals(typeName, StringComparison.OrdinalIgnoreCase))
        toErase.Add(id);
}

int erased = 0;
foreach (var id in toErase)
{
    var ent = Transaction.GetObject(id, OpenMode.ForWrite) as Autodesk.AutoCAD.DatabaseServices.Entity;
    ent.Erase();
    erased++;
}

return new { success = true, typeName, erasedCount = erased };
```

## Usage Notes
- Matches the EXACT runtime type name (e.g. "Polyline" will not also catch "Polyline3d") — use `count_entities_by_type` first to see the exact type names present in the drawing.
- For Civil 3D object types (e.g. "Alignment", "TinSurface"), prefer the dedicated `delete_alignment` or a similar object-specific skill — those clean up dependent objects (profiles, labels) that a raw `Erase()` here would leave dangling references to.
- `requires_write: true` — this is a blunt tool; confirm scope with the engineer first.
