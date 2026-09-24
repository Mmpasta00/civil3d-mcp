---
name: grading_group_list
category: grading
description: List every grading object (daylight/pad grading entities) in the drawing with style and footprint feature line
requires_write: false
parameters: []
---

## Code Template

```csharp
var gradings = new List<object>();

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

foreach (ObjectId id in btr)
{
    var obj = Transaction.GetObject(id, OpenMode.ForRead);
    if (obj is Grading grading)
    {
        gradings.Add(new
        {
            name = grading.Name,
            handle = grading.Handle.ToString(),
            layer = grading.Layer,
            styleName = grading.StyleName
        });
    }
}

return new { count = gradings.Count, gradings };
```

## Usage Notes
- Iterates model space directly and filters by type since this API version exposes no `CivilDocument.GetGradingIds()`-style shortcut for individual `Grading` entities.
- For grading GROUPS (the surface-producing container that gathers multiple grading objects), use the Civil 3D UI's Grading Group Properties, or extend this by grouping results by each grading's `FolderId`/site if your version's `Grading` exposes a group reference.
