---
name: rebuild_all_corridors
category: corridors
description: Rebuild every out-of-date corridor in the drawing
requires_write: true
parameters: []
---

## Code Template

```csharp
var results = new List<object>();

foreach (ObjectId corrId in CivilDoc.CorridorCollection)
{
    var corridor = Transaction.GetObject(corrId, OpenMode.ForWrite) as Corridor;
    if (corridor == null) continue;

    bool wasOutOfDate = corridor.IsOutOfDate;
    if (wasOutOfDate)
    {
        corridor.Rebuild();
    }

    results.Add(new
    {
        name = corridor.Name,
        wasOutOfDate,
        isOutOfDateNow = corridor.IsOutOfDate
    });
}

return new
{
    corridorCount = results.Count,
    rebuiltCount = results.Count(r => (bool)r.GetType().GetProperty("wasOutOfDate").GetValue(r)),
    results
};
```

## Usage Notes
- `Corridor.Rebuild()` can be slow on large corridors with dense frequencies — expect this to take longer than most other skills.
- `CivilDoc.CorridorCollection.RebuildAll()` is a one-line alternative that rebuilds unconditionally; this skill only rebuilds what's flagged out of date.
