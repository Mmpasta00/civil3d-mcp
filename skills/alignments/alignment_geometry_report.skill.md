---
name: alignment_geometry_report
category: alignments
description: Report every geometry entity (lines, curves, spirals) in an alignment with bearings, radii, and lengths
requires_write: false
parameters:
  - name: alignmentName
    type: string
    required: true
---

## Code Template

```csharp
Alignment alignment = null;
foreach (ObjectId id in CivilDoc.GetAlignmentIds())
{
    var a = Transaction.GetObject(id, OpenMode.ForRead) as Alignment;
    if (a != null && a.Name.Equals("ALIGNMENT_NAME", StringComparison.OrdinalIgnoreCase))
    { alignment = a; break; }
}
if (alignment == null) return new { error = "Alignment not found" };

var entities = new List<object>();
foreach (AlignmentEntity ent in alignment.Entities)
{
    // AlignmentEntity itself holds no station/length — it groups 1+ AlignmentSubEntity
    // segments (e.g. a spiral-curve-spiral entity has 3 sub-entities). Aggregate across them.
    double? startStation = null, endStation = null;
    double totalLength = 0;
    for (int i = 0; i < ent.SubEntityCount; i++)
    {
        var sub = ent[i];
        if (startStation == null) startStation = sub.StartStation;
        endStation = sub.EndStation;
        totalLength += sub.Length;
    }

    entities.Add(new
    {
        type = ent.EntityType.ToString(),
        subEntityCount = ent.SubEntityCount,
        startStation,
        endStation,
        length = totalLength
    });
}

return new
{
    alignmentName = alignment.Name,
    totalLength = alignment.Length,
    startStation = alignment.StartingStation,
    endStation = alignment.EndingStation,
    stationEquationCount = alignment.StationEquations.Count,
    entityCount = entities.Count,
    entities
};
```

## Usage Notes
- `AlignmentEntity.EntityType` distinguishes Line, Arc, Spiral, and compound types (e.g. spiral-curve-spiral); each entity's own station/length come from its `AlignmentSubEntity` children, not the entity itself — this template sums across them.
- For curve-specific data (radius, delta angle) cast the entity to `AlignmentSCS`/`AlignmentArc` per the Civil 3D entity object model — left generic here since entity subtypes vary by geometry.
- Combine with `station_offset` to get X,Y at any entity boundary.
