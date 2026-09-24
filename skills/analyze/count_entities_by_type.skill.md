---
name: count_entities_by_type
category: analyze
description: Count every entity in model space grouped by its AutoCAD/Civil 3D type name — a quick drawing-content census
requires_write: false
parameters: []
---

## Code Template

```csharp
var counts = new Dictionary<string, int>();

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

foreach (ObjectId id in btr)
{
    var obj = Transaction.GetObject(id, OpenMode.ForRead);
    string typeName = obj.GetType().Name;
    counts[typeName] = counts.TryGetValue(typeName, out int c) ? c + 1 : 1;
}

return new
{
    totalEntities = counts.Values.Sum(),
    byType = counts.OrderByDescending(kv => kv.Value).Select(kv => new { type = kv.Key, count = kv.Value })
};
```

## Usage Notes
- Only counts model space top-level entities — nested block-reference contents aren't expanded.
- Civil 3D object types show their real class name (e.g. `TinSurface`, `Alignment`, `Pipe`) alongside plain AutoCAD entities (`Polyline`, `BlockReference`) — useful as a fast "what's actually in this drawing" sanity check before a QA pass.
