---
name: purge_and_audit
category: drawing
description: Purge unused named objects (blocks, layers, styles) and run an audit/fix pass on the drawing
requires_write: true
parameters:
  - name: purgeUnused
    type: boolean
    required: false
    description: Purge unused items after auditing (default true)
---

## Code Template

```csharp
bool purgeUnused = true;

// Audit: check and fix drawing errors
Database.Audit(true, false); // fixErrors=true, printResultsToScreen=false

int purgedCount = 0;
if (purgeUnused)
{
    // Repeated passes since purging one item type can free up others (e.g. a layer only
    // used by a now-purged block)
    for (int pass = 0; pass < 3; pass++)
    {
        var toPurge = new ObjectIdCollection();

        var layerTable = Transaction.GetObject(Database.LayerTableId, OpenMode.ForRead) as LayerTable;
        foreach (ObjectId id in layerTable) toPurge.Add(id);

        var blockTable = Transaction.GetObject(Database.BlockTableId, OpenMode.ForRead) as BlockTable;
        foreach (ObjectId id in blockTable) toPurge.Add(id);

        Database.Purge(toPurge);
        if (toPurge.Count == 0) break;

        foreach (ObjectId id in toPurge)
        {
            var obj = Transaction.GetObject(id, OpenMode.ForWrite);
            obj.Erase();
            purgedCount++;
        }
    }
}

return new { success = true, purgedCount, note = "Audit ran with fixErrors=true — any repaired issues were corrected silently; review drawing_health_check afterward to confirm a clean bill of health." };
```

## Usage Notes
- `Database.Purge(ObjectIdCollection)` filters the input collection down to only the items that ARE actually purgeable — the remaining ids in the (mutated) collection are what gets erased here.
- Three passes catch cascading purges (e.g. purging a block frees up the layer it was the only user of) — increase if the drawing has deeply nested unused block references.
- Run `drawing_health_check` before and after to confirm the purge didn't remove something still referenced by an xref or data shortcut.
