---
name: list_corridors_and_baselines
category: corridors
description: List every corridor with its baselines, regions, and assigned assembly
requires_write: false
parameters: []
---

## Code Template

```csharp
var corridors = new List<object>();

foreach (ObjectId corrId in CivilDoc.CorridorCollection)
{
    var corridor = Transaction.GetObject(corrId, OpenMode.ForRead) as Corridor;
    if (corridor == null) continue;

    var baselines = new List<object>();
    foreach (Baseline bl in corridor.Baselines)
    {
        var regions = new List<object>();
        foreach (BaselineRegion region in bl.BaselineRegions)
        {
            regions.Add(new
            {
                name = region.Name,
                startStation = region.StartStation,
                endStation = region.EndStation
            });
        }
        baselines.Add(new
        {
            name = bl.Name,
            type = bl.BaselineType.ToString(),
            isFeatureLineBased = bl.IsFeatureLineBased(),
            regionCount = regions.Count,
            regions
        });
    }

    corridors.Add(new
    {
        name = corridor.Name,
        handle = corridor.Handle.ToString(),
        isOutOfDate = corridor.IsOutOfDate,
        rebuildAutomatic = corridor.RebuildAutomatic,
        baselineCount = baselines.Count,
        baselines
    });
}

return new { count = corridors.Count, corridors };
```

## Usage Notes
- `isOutOfDate = true` means the corridor needs `rebuild_all_corridors` before its solids/feature lines are trustworthy.
- Baseline `type` distinguishes alignment-and-profile-based baselines from feature-line-based ones.
