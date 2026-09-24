---
name: select_objects_in_window
category: select
description: Get every entity whose extents fall inside (or cross) a rectangular window — useful for "everything near this pad" style queries
requires_write: false
parameters:
  - name: minX
    type: double
    required: true
  - name: minY
    type: double
    required: true
  - name: maxX
    type: double
    required: true
  - name: maxY
    type: double
    required: true
  - name: crossingOk
    type: boolean
    required: false
    description: true = include entities that only partially overlap the window (default true)
---

## Code Template

```csharp
double minX = 0, minY = 0, maxX = 500, maxY = 500;
bool crossingOk = true;

var windowMin = new Point3d(minX, minY, 0);
var windowMax = new Point3d(maxX, maxY, 0);

var bt = (BlockTable)Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead);
var btr = (BlockTableRecord)Transaction.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

var results = new List<object>();
foreach (ObjectId id in btr)
{
    var ent = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.Entity;
    if (ent == null) continue;

    Extents3d extents;
    try { extents = ent.GeometricExtents; }
    catch { continue; } // some entities (e.g. empty text) can throw on GeometricExtents

    bool fullyInside = extents.MinPoint.X >= minX && extents.MinPoint.Y >= minY &&
                        extents.MaxPoint.X <= maxX && extents.MaxPoint.Y <= maxY;
    bool overlaps = extents.MinPoint.X <= maxX && extents.MaxPoint.X >= minX &&
                    extents.MinPoint.Y <= maxY && extents.MaxPoint.Y >= minY;

    bool include = crossingOk ? overlaps : fullyInside;
    if (!include) continue;

    results.Add(new
    {
        handle = ent.Handle.ToString(),
        type = ent.GetType().Name,
        layer = ent.Layer,
        fullyInside
    });
}

return new { window = new { minX, minY, maxX, maxY }, count = results.Count, entities = results };
```

## Usage Notes
- Uses each entity's bounding-box extents, not exact geometry — a diagonal line whose bounding box overlaps the window but whose actual geometry doesn't will still be included when `crossingOk = true`.
- For a true AutoCAD-style crossing/window select (exact geometry test), use `Editor.SelectCrossingWindow`/`SelectWindow` instead — this skill is a pure-geometry alternative that works without an active graphical selection context.
