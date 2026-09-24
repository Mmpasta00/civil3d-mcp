---
name: list_parts_lists
category: pipe_networks
description: List every pipe/structure parts list available in the drawing, with part family counts
requires_write: false
parameters: []
---

## Code Template

```csharp
var lists = new List<object>();

foreach (ObjectId id in CivilDoc.Styles.PartsListSet)
{
    var partsList = Transaction.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.PartsList;
    if (partsList == null) continue;

    int pipeFamilies = partsList.GetPartFamilyIdsByDomain(DomainType.Pipe).Count;
    int structFamilies = partsList.GetPartFamilyIdsByDomain(DomainType.Structure).Count;

    lists.Add(new
    {
        name = partsList.Name,
        handle = partsList.Handle.ToString(),
        pipePartFamilies = pipeFamilies,
        structurePartFamilies = structFamilies
    });
}

return new { count = lists.Count, partsLists = lists };
```

## Usage Notes
- Use this before `add_structure_and_pipe` / `resize_pipe` to confirm which part family/size names are actually available in the target network's assigned parts list.
- `DomainType.Pipe` / `DomainType.Structure` separate gravity pipe parts from structure (manhole/inlet) parts — pressure pipe parts use a different domain not covered here.
